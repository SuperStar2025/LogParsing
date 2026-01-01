using LogParsing.Core.Abstractions;
using LogParsing.Core.Models;

namespace LogParsing.Core.Internal.Buffers
{
    /// <summary>
    /// 提供基于内存的 <see cref="ILogBuffer"/> 实现，适用于日志量较小且内存资源充足的场景。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类使用 <see cref="List{T}"/> 内部存储所有 <see cref="LogEntry"/> 实例，
    /// 支持高效的追加、查询和删除操作，无磁盘 I/O 开销。
    /// </para>
    /// <para>
    /// 典型适用场景包括：
    /// <list type="bullet">
    ///   <item><description>短时日志分析任务（如单元测试、调试会话）；</description></item>
    ///   <item><description>低吞吐量日志流（如配置变更、状态事件）；</description></item>
    ///   <item><description>需要频繁随机访问或过滤日志条目的处理阶段。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 注意：由于所有数据驻留内存，大规模日志可能导致 <see cref="OutOfMemoryException"/>。
    /// 若日志规模不可控，应优先使用 <see cref="FileLogBuffer"/>。
    /// </para>
    /// <para>
    /// 此类为密封类（<see langword="sealed"/>），不可继承。
    /// </para>
    /// </remarks>
    public sealed class InMemoryLogBuffer : ILogBuffer
    {
        // 内部存储日志条目的集合
        private readonly List<LogEntry> _entries = new();

        /// <summary>
        /// 将指定的日志条目添加到内存缓冲区末尾。
        /// </summary>
        /// <param name="entry">
        /// 要添加的 <see cref="LogEntry"/> 实例。不得为 <see langword="null"/>。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="entry"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <remarks>
        /// 添加操作的时间复杂度为 O(1)（均摊），由底层 <see cref="List{T}.Add"/> 保证。
        /// </remarks>
        public void Add(LogEntry entry) => _entries.Add(entry);

        /// <summary>
        /// 根据指定谓词从内存缓冲区中筛选并返回匹配的日志条目。
        /// </summary>
        /// <param name="predicate">
        /// 用于过滤日志条目的条件函数。不得为 <see langword="null"/>。
        /// </param>
        /// <returns>
        /// 一个延迟执行的 <see cref="IEnumerable{LogEntry}"/>，包含所有满足条件的条目。
        /// 若无匹配项，则返回空序列（而非 <see langword="null"/>）。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="predicate"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <remarks>
        /// 查询在内存中完成，无 I/O 开销，适合用于帧合并、状态回溯等需要快速访问历史条目的场景。
        /// 返回结果基于当前缓冲区快照；后续对缓冲区的修改不会影响已返回的枚举器。
        /// </remarks>
        public IEnumerable<LogEntry> Find(Func<LogEntry, bool> predicate)
            => _entries.Where(predicate);

        /// <summary>
        /// 从内存缓冲区中移除指定的日志条目（若存在）。
        /// </summary>
        /// <param name="entry">
        /// 要移除的 <see cref="LogEntry"/> 实例。不得为 <see langword="null"/>。
        /// </param>
        /// <remarks>
        /// <para>
        /// 移除操作通过值相等性（引用相等）进行匹配，调用 <see cref="List{T}.Remove"/> 实现。
        /// 若缓冲区中存在多个相同引用的条目，仅移除第一个匹配项。
        /// </para>
        /// <para>
        /// 此操作适用于已完成协议帧组装或不再需要保留的条目清理。
        /// 时间复杂度为 O(n)，其中 n 为缓冲区大小。
        /// </para>
        /// </remarks>
        public void Remove(LogEntry entry)
            => _entries.Remove(entry);

        /// <summary>
        /// 获取当前内存缓冲区中存储的日志条目数量。
        /// </summary>
        /// <value>
        /// 非负整数，表示已成功添加且未被移除的 <see cref="LogEntry"/> 实例总数。
        /// </value>
        public int Count => _entries.Count;

        /// <summary>
        /// 释放本实例所持有的资源。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 由于本类不持有非托管资源或外部句柄，<see cref="Dispose"/> 仅清空内部集合，
        /// 以帮助垃圾回收器尽早释放日志条目引用。
        /// </para>
        /// <para>
        /// 调用后，<see cref="Count"/> 返回 0，但可继续调用 <see cref="Add"/> 等方法（对象仍有效）。
        /// 因此，本实现不设置“disposed”标志，也不抛出 <see cref="ObjectDisposedException"/>。
        /// </para>
        /// <para>
        /// 本方法可安全多次调用（幂等性）。
        /// </para>
        /// </remarks>
        public void Dispose() => _entries.Clear();
    }
}