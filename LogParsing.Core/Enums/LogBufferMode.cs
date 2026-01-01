namespace LogParsing.Core.Enums
{
    /// <summary>
    /// 定义日志缓冲区的存储模式，用于控制 <see cref="LogParsing.Core.Abstractions.ILogBuffer"/> 实现的底层存储策略。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 此枚举支持在内存效率与系统资源占用之间进行权衡：
    /// <list type="bullet">
    ///   <item><description>在内存充足且日志量较小时，使用内存缓冲可获得最佳性能；</description></item>
    ///   <item><description>在处理大规模日志流时，文件缓冲可避免内存溢出；</description></item>
    ///   <item><description>自动模式根据运行时环境动态选择最优策略。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 该枚举通常由日志解析管道的配置组件读取，并传递给缓冲区工厂以创建适当的 <see cref="LogParsing.Core.Abstractions.ILogBuffer"/> 实例。
    /// </para>
    /// </remarks>
    public enum LogBufferMode
    {
        /// <summary>
        /// 根据当前系统可用内存和日志负载自动选择使用内存缓冲区或文件缓冲区。
        /// </summary>
        /// <remarks>
        /// 此为推荐的默认模式，适用于大多数场景。
        /// 自动决策逻辑通常由缓冲区工厂实现，可能基于阈值（如条目数量、总字节数）或 GC 压力。
        /// </remarks>
        Auto,

        /// <summary>
        /// 强制使用纯内存存储日志条目。
        /// </summary>
        /// <remarks>
        /// 提供最低延迟和最高吞吐量，但可能在高负载下导致 <see cref="OutOfMemoryException"/>。
        /// 适用于短时、小规模日志分析任务。
        /// </remarks>
        InMemory,

        /// <summary>
        /// 强制使用临时文件存储日志条目。
        /// </summary>
        /// <remarks>
        /// 内存占用低，可处理任意规模的日志流，但 I/O 开销较高。
        /// 临时文件应在缓冲区释放时（<see cref="IDisposable.Dispose"/>）被自动清理。
        /// </remarks>
        File
    }
}