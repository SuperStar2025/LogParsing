using LogParsing.Core.Models;

namespace LogParsing.Core.Abstractions
{
    /// <summary>
    /// 定义日志解析器的统一抽象接口，用于将原始日志文本行转换为结构化的 <see cref="LogEntry"/> 对象。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本接口通过多态机制屏蔽不同日志格式（如 Modbus、IEC 60870-5-104、DL/T 等电力规约）的解析差异，
    /// 使上层组件（如日志摄入管道、缓冲管理器、帧组装器）无需感知具体协议细节。
    /// </para>
    /// <para>
    /// 设计原则：
    /// <list type="bullet">
    ///   <item><description><strong>单一职责</strong>：每个实现类仅处理一种日志格式，避免在单个解析器中嵌入多协议分支逻辑；</description></item>
    ///   <item><description><strong>可路由性</strong>：通过 <see cref="CanParse"/> 方法支持高效的解析器选择机制；</description></item>
    ///   <item><description><strong>空安全</strong>：即使解析失败，<see cref="Parse"/> 也返回有效对象（非 <see langword="null"/>），简化调用方错误处理。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 典型使用流程：
    /// <code>
    /// foreach (var parser in parsers)
    /// {
    ///     if (parser.CanParse(line))
    ///     {
    ///         var entry = parser.Parse(line);
    ///         // 处理 entry
    ///         break;
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface ILogParser
    {
        /// <summary>
        /// 判断当前解析器是否能够识别并处理指定的日志文本行。
        /// </summary>
        /// <param name="logLine">
        /// 待检测的原始日志文本行。不得为 <see langword="null"/> 或空字符串（由调用方保证）。
        /// </param>
        /// <returns>
        /// 如果该解析器专用于此日志格式且能成功解析其结构，则返回 <see langword="true"/>；
        /// 否则返回 <see langword="false"/>，表示应尝试其他解析器。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 此方法用于解析器路由阶段，应在 <see cref="Parse"/> 之前调用，
        /// 以避免无效解析带来的性能开销或日志污染。
        /// </para>
        /// <para>
        /// 实现应基于日志前缀、关键字、时间格式、字段模式等特征进行快速匹配，
        /// 通常不执行完整解析，仅做“指纹”识别。
        /// </para>
        /// </remarks>
        bool CanParse(string logLine);

        /// <summary>
        /// 将单行原始日志文本解析为结构化的 <see cref="LogEntry"/> 实例。
        /// </summary>
        /// <param name="logLine">
        /// 要解析的日志文本行。不得为 <see langword="null"/>。
        /// 调用方应确保已通过 <see cref="CanParse"/> 验证兼容性。
        /// </param>
        /// <returns>
        /// 一个有效的 <see cref="LogEntry"/> 对象：
        /// <list type="bullet">
        ///   <item><description>若解析成功，包含完整的元数据（时间、级别、模块等）和业务字段；</description></item>
        ///   <item><description>若解析部分失败（如时间格式错误），仍填充可识别字段，并记录原始消息；</description></item>
        ///   <item><description>绝不返回 <see langword="null"/>，以保证调用方无需空值检查。</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>
        /// 即使日志格式异常，实现也应尽力构造一个“降级”但有效的日志条目，
        /// 例如保留 <see cref="LogEntry.Message"/>、<see cref="LogEntry.Timestamp"/>（设为当前时间）等基础信息。
        /// </para>
        /// <para>
        /// 此方法不抛出业务异常（如 <see cref="FormatException"/>），
        /// 所有解析错误应被内部消化并反映在输出对象的状态中。
        /// </para>
        /// </remarks>
        LogEntry Parse(string logLine);
    }
}