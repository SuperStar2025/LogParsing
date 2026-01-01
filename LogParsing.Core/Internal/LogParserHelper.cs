using LogParsing.Core.Abstractions;
using LogParsing.Core.Models;

namespace LogParsing.Core.Internal
{
    /// <summary>
    /// 提供日志解析的辅助方法，用于将原始日志行批量转换为结构化日志条目并写入缓冲区。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类封装了通用的日志解析与缓冲流程，适用于所有实现 <see cref="ILogParser"/> 和 <see cref="ILogBuffer"/>
    /// 接口的日志系统（如 IEC 60870-5-104 或 Modbus 协议日志）。
    /// </para>
    /// <para>
    /// 所有方法均为静态且线程安全（前提是传入的 <c>parser</c> 和 <c>buffer</c>
    /// 实例自身具备线程安全性）。
    /// </para>
    /// </remarks>
    public static class LogParserHelper
    {
        /// <summary>
        /// 将指定的日志行集合使用给定解析器进行解析，并将匹配指定类型的日志条目添加到目标缓冲区中。
        /// </summary>
        /// <param name="lines">
        /// 待解析的日志行集合。不得为 <see langword="null"/>。
        /// 每一行应为完整的日志文本（通常来自文件或流）。
        /// </param>
        /// <param name="parser">
        /// 用于解析单行日志的解析器实例（例如 <see cref="Parsers.PowerLogParser"/> 或 <see cref="Parsers.ModbusLogParser"/>）。
        /// 不得为 <see langword="null"/>。
        /// </param>
        /// <param name="buffer">
        /// 用于接收有效日志条目的缓冲区（例如 <see cref="Buffers.InMemoryLogBuffer"/> 或 <see cref="Buffers.FileLogBuffer"/>）。
        /// 不得为 <see langword="null"/>。
        /// </param>
        /// <typeparam name="TEntry">
        /// 期望的日志条目类型（必须派生自 <see cref="LogEntry"/>）。
        /// 仅当解析结果在运行时是该类型时，才会被添加到缓冲区和返回列表中。
        /// </typeparam>
        /// <returns>
        /// 一个包含所有成功解析且类型匹配的 <typeparamref name="TEntry"/> 实例的列表。
        /// 若无匹配项，则返回空列表（永不返回 <see langword="null"/>）。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="lines"/>、<paramref name="parser"/> 或 <paramref name="buffer"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <remarks>
        /// <para>
        /// 本方法执行以下操作：
        /// <list type="number">
        ///   <item>遍历 <paramref name="lines"/> 中的每一行；</item>
        ///   <item>调用 <paramref name="parser"/>.<see cref="ILogParser.Parse"/> 解析该行；</item>
        ///   <item>若返回的 <see cref="LogEntry"/> 在运行时是 <typeparamref name="TEntry"/> 类型，
        ///       则将其同时添加到 <paramref name="buffer"/> 和返回列表中。</item>
        /// </list>
        /// </para>
        /// <para>
        /// ⚠️ 注意：解析器可能返回基类 <see cref="LogEntry"/> 或其他子类型（如混合日志源）。
        /// 本方法通过 <c>is TEntry</c> 进行类型筛选，确保类型安全。
        /// </para>
        /// <para>
        /// 性能提示：本方法对输入集合进行单次遍历，无中间分配（除返回列表外），
        /// 适合处理大型日志流。但请注意，<see cref="ILogParser.Parse"/> 的性能取决于具体实现。
        /// </para>
        /// <para>
        /// 典型用法示例：
        /// <code>
        /// var powerEntries = LogParserHelper.ParseLinesToBuffer&lt;PowerLogEntry&gt;(lines, new PowerLogParser(), buffer);
        /// </code>
        /// </para>
        /// </remarks>
        public static List<TEntry> ParseLinesToBuffer<TEntry>(
            IEnumerable<string> lines,
            ILogParser parser,
            ILogBuffer buffer)
            where TEntry : LogEntry
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (parser == null) throw new ArgumentNullException(nameof(parser));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            var entries = new List<TEntry>();

            foreach (var line in lines)
            {
                // 尝试解析日志
                var entry = parser.Parse(line);

                // 只添加指定类型的日志条目
                if (entry is TEntry typedEntry)
                {
                    buffer.Add(typedEntry);
                    entries.Add(typedEntry);
                }
            }

            return entries;
        }
    }
}