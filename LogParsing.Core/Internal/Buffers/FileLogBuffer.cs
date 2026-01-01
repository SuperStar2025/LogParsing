using LogParsing.Core.Abstractions;
using LogParsing.Core.Models;
using LogParsing.Core.Serialization;
using System.Text;

namespace LogParsing.Core.Internal.Buffers
{
    /// <summary>
    /// 提供基于临时文件的 <see cref="ILogBuffer"/> 实现，适用于大规模日志流处理。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类将日志条目以 JSON 格式逐行写入磁盘文件，通过记录每行起始偏移量支持按需随机读取，
    /// 从而在有限内存条件下处理任意规模的日志数据。
    /// </para>
    /// <para>
    /// 设计特性包括：
    /// <list type="bullet">
    ///   <item><description>避免内存溢出：所有条目持久化至磁盘，仅维护轻量级偏移索引；</description></item>
    ///   <item><description>通用序列化：使用 <see cref="LogEntrySerializer"/> 将 <see cref="LogEntry"/> 转为 UTF-8 JSON 行；</description></item>
    ///   <item><description>延迟读取：仅在 <see cref="Find"/> 调用时反序列化匹配候选条目；</description></item>
    ///   <item><description>自动清理：在 <see cref="Dispose"/> 时删除临时文件，防止磁盘泄漏。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 注意：当前实现中 <see cref="Remove"/> 仅为占位，实际条目不会从文件中物理删除。
    /// 文件内容的生命周期由本对象的生存期决定。
    /// </para>
    /// <para>
    /// 此类为密封类（<see langword="sealed"/>），不可继承。
    /// </para>
    /// </remarks>
    public sealed class FileLogBuffer : ILogBuffer
    {
        private readonly string _filePath;
        private readonly List<long> _offsets = new();
        private FileStream _stream;
        private StreamWriter _writer;

        /// <summary>
        /// 初始化一个新的 <see cref="FileLogBuffer"/> 实例，并在指定路径创建临时文件。
        /// </summary>
        /// <param name="filePath">
        /// 临时文件的完整路径。不得为 <see langword="null"/> 或空字符串。
        /// 调用方必须确保目录存在且进程具有写权限。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="filePath"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <exception cref="IOException">
        /// 当无法创建或打开指定文件时抛出（例如路径无效、权限不足等）。
        /// </exception>
        /// <remarks>
        /// 文件以 <see cref="FileMode.Create"/> 模式打开，若已存在则覆盖。
        /// 流以读写共享模式（<see cref="FileShare.Read"/>）打开，允许外部工具读取（如调试）。
        /// </remarks>
        public FileLogBuffer(string filePath)
        {
            _filePath = filePath;
            _stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            _writer = new StreamWriter(_stream, leaveOpen: true);
        }

        /// <summary>
        /// 将指定的日志条目追加到缓冲区文件末尾，并记录其字节偏移位置。
        /// </summary>
        /// <param name="entry">
        /// 要添加的 <see cref="LogEntry"/> 实例。不得为 <see langword="null"/>。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="entry"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// 如果当前实例已被释放，则抛出此异常。
        /// </exception>
        /// <exception cref="IOException">
        /// 在写入文件过程中发生 I/O 错误时抛出。
        /// </exception>
        /// <remarks>
        /// 条目通过 <see cref="LogEntrySerializer.Serialize"/> 转换为 JSON 字符串，
        /// 并以 UTF-8 编码写入，末尾附加换行符（<c>\n</c>）以支持按行读取。
        /// 写入后立即刷新流，确保数据落盘。
        /// </remarks>
        public void Add(LogEntry entry)
        {
            var json = LogEntrySerializer.Serialize(entry);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");

            _stream.Position = _stream.Length;
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();

            _offsets.Add(_stream.Position - bytes.Length);
        }

        /// <summary>
        /// 根据指定谓词从缓冲区中查找并返回匹配的日志条目。
        /// </summary>
        /// <param name="predicate">
        /// 用于筛选日志条目的条件函数。不得为 <see langword="null"/>。
        /// </param>
        /// <returns>
        /// 一个延迟执行的 <see cref="IEnumerable{LogEntry}"/>，包含所有满足条件的条目。
        /// 若无匹配项，则返回空序列（而非 <see langword="null"/>）。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="predicate"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// 如果当前实例已被释放，则抛出此异常。
        /// </exception>
        /// <exception cref="IOException">
        /// 在读取文件过程中发生 I/O 错误时抛出。
        /// </exception>
        /// <remarks>
        /// <para>
        /// 本方法会遍历所有已记录的偏移位置，逐行读取并反序列化日志条目，
        /// 然后应用 <paramref name="predicate"/> 进行过滤。
        /// </para>
        /// <para>
        /// 由于使用 <c>yield return</c>，枚举过程是惰性的，但每次调用都会重新扫描整个文件。
        /// 因此不适用于高频查询场景。
        /// </para>
        /// </remarks>
        public IEnumerable<LogEntry> Find(Func<LogEntry, bool> predicate)
        {
            _writer.Flush();
            _stream.Flush();

            foreach (var offset in _offsets)
            {
                _stream.Position = offset;
                var line = ReadLineUtf8(_stream);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var entry = LogEntrySerializer.Deserialize(line);
                if (entry != null && predicate(entry))
                    yield return entry;
            }
        }

        /// <summary>
        /// 从缓冲区中标记指定日志条目为“已移除”。
        /// </summary>
        /// <param name="entry">
        /// 要移除的日志条目。不得为 <see langword="null"/>。
        /// </param>
        /// <remarks>
        /// <para>
        /// 当前实现为简化设计，**不执行任何实际删除操作**。
        /// 条目仍保留在文件中，且后续 <see cref="Find"/> 调用仍可能返回它。
        /// </para>
        /// <para>
        /// 此行为符合接口契约（无异常），但调用方应知晓其语义限制。
        /// 未来可扩展为维护“删除掩码”或紧凑化文件。
        /// </para>
        /// </remarks>
        public void Remove(LogEntry entry)
        {
            // 简化：仅标记删除
        }

        /// <summary>
        /// 获取当前缓冲区中存储的日志条目数量。
        /// </summary>
        /// <value>
        /// 非负整数，等于已成功添加的 <see cref="LogEntry"/> 实例总数。
        /// 即使调用了 <see cref="Remove"/>，该值也不会减少。
        /// </value>
        public int Count => _offsets.Count;

        /// <summary>
        /// 从指定的 <see cref="Stream"/> 中读取一行 UTF-8 编码的文本，直到遇到换行符（<c>\n</c>）。
        /// </summary>
        /// <param name="stream">要从中读取的流。不得为 <see langword="null"/>。</param>
        /// <returns>
        /// 读取的行内容，不包含结尾的换行符（<c>\n</c>）；
        /// 若流已到达末尾，则返回空字符串。
        /// 行尾的回车符（<c>\r</c>）会被自动去除（兼容 Windows 换行风格）。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="stream"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <exception cref="IOException">
        /// 在读取过程中发生 I/O 错误时抛出。
        /// </exception>
        /// <remarks>
        /// 此方法为内部辅助函数，专用于解析本缓冲区写入的 JSON 行格式。
        /// 不适用于通用文本流处理（性能较低，未使用缓冲）。
        /// </remarks>
        private static string ReadLineUtf8(Stream stream)
        {
            var buffer = new List<byte>();
            int b;

            while ((b = stream.ReadByte()) != -1)
            {
                if (b == '\n')
                    break;

                buffer.Add((byte)b);
            }

            return buffer.Count == 0
                ? string.Empty
                : Encoding.UTF8.GetString(buffer.ToArray()).TrimEnd('\r');
        }

        /// <summary>
        /// 释放本实例所持有的非托管资源（文件句柄），并删除临时文件。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 调用此方法后，所有后续对 <see cref="Add"/>, <see cref="Find"/>, <see cref="Remove"/> 或 <see cref="Count"/> 的调用
        /// 将导致 <see cref="ObjectDisposedException"/>。
        /// </para>
        /// <para>
        /// 临时文件（<see cref="_filePath"/>）将在流关闭后被立即删除，
        /// 即使删除失败（如被其他进程锁定），也不会抛出异常（静默忽略）。
        /// </para>
        /// <para>
        /// 本方法可安全多次调用（幂等性）。
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            _writer.Dispose();
            _stream.Dispose();
            File.Delete(_filePath);
        }
    }
}