using LogParsing.Core.Models;

namespace LogParsing.Core.Abstractions
{
    /// <summary>
    /// 定义日志缓冲区的抽象接口，用于在日志解析过程中临时存储和管理 <see cref="LogEntry"/> 实例。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本接口支持多种底层存储策略（如纯内存缓存、基于临时文件的持久化、或混合模式），
    /// 同时为日志帧组装、上下文关联和资源清理提供统一契约。
    /// </para>
    /// <para>
    /// 核心设计目标包括：
    /// <list type="bullet">
    ///   <item><description>提供线程安全或单线程友好的日志暂存机制，作为解析流水线的中间状态容器；</description></item>
    ///   <item><description>支持按自定义谓词（<see cref="Func{T,TResult}"/>，此处为 <c>Func&lt;LogEntry, bool&gt;</c>）查询条目，便于向后查找匹配帧片段；</description></item>
    ///   <item><description>允许显式移除已处理条目，减少内存占用或清理临时文件；</description></item>
    ///   <item><description>通过实现 <see cref="IDisposable"/>，确保文件型缓冲区能正确释放系统资源（如删除临时文件）。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 实现此接口的类型应保证：
    /// <list type="number">
    ///   <item><description><c>Add</c> 操作应高效，适用于高频日志摄入场景；</description></item>
    ///   <item><description><c>Find</c> 返回的集合应反映调用时刻的缓冲区快照，避免并发修改异常；</description></item>
    ///   <item><description>多次调用 <c>Dispose</c> 应是安全的（幂等性）。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface ILogBuffer : IDisposable
    {
        /// <summary>
        /// 将指定的日志条目添加到缓冲区中。
        /// </summary>
        /// <param name="entry">
        /// 要添加的 <see cref="LogEntry"/> 实例。不得为 <see langword="null"/>。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="entry"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        void Add(LogEntry entry);

        /// <summary>
        /// 根据指定的筛选条件，从缓冲区中检索符合条件的日志条目。
        /// </summary>
        /// <param name="predicate">
        /// 用于筛选日志条目的谓词函数。不得为 <see langword="null"/>。
        /// </param>
        /// <returns>
        /// 一个包含所有匹配 <see cref="LogEntry"/> 实例的只读序列。
        /// 若无匹配项，则返回空序列（而非 <see langword="null"/>）。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="predicate"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <remarks>
        /// 此方法常用于帧组装阶段，例如查找与当前条目具有相同协议、通道和方向的相邻条目。
        /// 返回的序列不应被修改，且其内容在调用后可能随缓冲区变更而失效。
        /// </remarks>
        IEnumerable<LogEntry> Find(Func<LogEntry, bool> predicate);

        /// <summary>
        /// 从缓冲区中移除指定的日志条目。
        /// </summary>
        /// <param name="entry">
        /// 要移除的 <see cref="LogEntry"/> 实例。不得为 <see langword="null"/>。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="entry"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <remarks>
        /// 若缓冲区中不存在该条目，实现可选择静默忽略或抛出异常，
        /// 但应保持行为一致性。移除操作有助于控制缓冲区大小并释放资源。
        /// </remarks>
        void Remove(LogEntry entry);

        /// <summary>
        /// 获取当前缓冲区中存储的日志条目数量。
        /// </summary>
        /// <value>
        /// 非负整数，表示缓冲区内有效 <see cref="LogEntry"/> 实例的总数。
        /// </value>
        /// <remarks>
        /// 该属性可用于实现容量控制、老化策略或性能监控。
        /// </remarks>
        int Count { get; }
    }
}