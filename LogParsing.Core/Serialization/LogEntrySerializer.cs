using LogParsing.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace LogParsing.Core.Serialization
{
    /// <summary>
    /// 提供对 <see cref="LogEntry"/> 及其已知派生类型的安全 JSON 序列化与反序列化支持。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 由于 <see cref="System.Text.Json"/> 默认不支持运行时多态反序列化，
    /// 本类通过配置 <see cref="JsonPolymorphismOptions"/> 显式注册支持的派生类型，
    /// 并在序列化结果中嵌入类型判别字段（<c>"$type"</c>），以确保反序列化时能正确还原具体类型。
    /// </para>
    /// <para>
    /// 当前支持的派生类型包括：
    /// <list type="bullet">
    ///   <item><see cref="ModbusLogEntry"/></item>
    ///   <item><see cref="PowerLogEntry"/></item>
    /// </list>
    /// </para>
    /// <para>
    /// 若遇到未知的 <c>"$type"</c> 值，将根据
    /// <see cref="JsonUnknownDerivedTypeHandling.FallBackToBaseType"/>
    /// 回退为基类 <see cref="LogEntry"/>，避免反序列化失败。
    /// </para>
    /// <para>
    /// 本类为静态工具类，线程安全，适用于日志缓冲区（如 <see cref="Internal.Buffers.FileLogBuffer"/>）
    /// 的持久化与恢复场景。
    /// </para>
    /// </remarks>
    public static class LogEntrySerializer
    {
        /// <summary>
        /// 共享的 <see cref="JsonSerializerOptions"/> 实例，配置了多态支持和紧凑输出。
        /// </summary>
        /// <remarks>
        /// 此选项在静态构造函数中完成初始化，确保线程安全且仅初始化一次。
        /// 禁用缩进（<see cref="JsonSerializerOptions.WriteIndented"/> = <see langword="false"/>）
        /// 以减小持久化体积，适合日志存储场景。
        /// </remarks>
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        /// <summary>
        /// 静态构造函数，用于配置 <see cref="_options"/> 的多态类型解析规则。
        /// </summary>
        /// <remarks>
        /// 通过向 <see cref="DefaultJsonTypeInfoResolver.Modifiers"/> 添加自定义修饰器，
        /// 为 <see cref="LogEntry"/> 类型注入多态元数据。
        /// 此方法仅在类型首次使用时由 .NET 运行时调用一次，保证初始化原子性。
        /// </remarks>
        static LogEntrySerializer()
        {
            // 获取 DefaultJsonTypeInfoResolver 实例
            var resolver = (DefaultJsonTypeInfoResolver)_options.TypeInfoResolver!;

            // 为 Log Assistant 类型创建多态
            resolver.Modifiers.Add(jsonTypeInfo =>
            {
                // 只对对象类型且为 LogEntry 生效
                if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
                    return;

                if (jsonTypeInfo.Type == typeof(LogEntry))
                {
                    jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                    {
                        TypeDiscriminatorPropertyName = "$type",
                        DerivedTypes =
                        {
                            new JsonDerivedType(typeof(ModbusLogEntry), nameof(ModbusLogEntry)),
                            new JsonDerivedType(typeof(PowerLogEntry), nameof(PowerLogEntry))
                        },
                        UnknownDerivedTypeHandling =
                            JsonUnknownDerivedTypeHandling.FallBackToBaseType
                    };
                }
            });
        }

        /// <summary>
        /// 将指定的日志实体序列化为包含类型信息的 JSON 字符串。
        /// </summary>
        /// <param name="entry">
        /// 要序列化的日志实体实例。不得为 <see langword="null"/>。
        /// 支持 <see cref="LogEntry"/> 及其已注册的派生类型。
        /// </param>
        /// <returns>
        /// 一个紧凑格式（无缩进）的 JSON 字符串，其中包含一个名为 <c>"$type"</c> 的判别字段，
        /// 其值为派生类型的名称（如 <c>"ModbusLogEntry"</c>）。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="entry"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// 当传入未注册的派生类型时，<see cref="System.Text.Json"/> 可能抛出此异常。
        /// （当前仅注册了 <see cref="ModbusLogEntry"/> 和 <see cref="PowerLogEntry"/>）
        /// </exception>
        /// <remarks>
        /// 序列化结果可被 <see cref="Deserialize"/> 方法正确还原。
        /// 生成的 JSON 不包含额外空格或换行，以优化存储效率。
        /// </remarks>
        public static string Serialize(LogEntry entry)
        {
            return JsonSerializer.Serialize(entry, _options);
        }

        /// <summary>
        /// 从 JSON 字符串反序列化出日志实体。
        /// </summary>
        /// <param name="json">
        /// 由 <see cref="Serialize"/> 生成的 JSON 字符串。
        /// 必须包含有效的 <c>"$type"</c> 字段以指示具体类型。
        /// 不得为 <see langword="null"/> 或空字符串。
        /// </param>
        /// <returns>
        /// 反序列化得到的 <see cref="LogEntry"/> 实例。
        /// 若 <c>"$type"</c> 值匹配已知派生类型，则返回对应的具体实例；
        /// 若类型未知或缺失，则回退为基类 <see cref="LogEntry"/> 实例（字段仍可正常还原）。
        /// 返回 <see langword="null"/> 仅当输入 JSON 表示 null 值。
        /// </returns>
        /// <exception cref="JsonException">
        /// 当 JSON 格式无效或无法映射到 <see cref="LogEntry"/> 结构时抛出。
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="json"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <remarks>
        /// 本方法依赖序列化时嵌入的 <c>"$type"</c> 字段进行类型路由。
        /// 未知类型不会导致失败，而是安全降级为基类，便于向前兼容新日志类型。
        /// </remarks>
        public static LogEntry? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<LogEntry>(@json, _options);
        }
    }
}