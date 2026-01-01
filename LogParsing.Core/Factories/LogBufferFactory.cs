using LogParsing.Core.Abstractions;
using LogParsing.Core.Enums;
using LogParsing.Core.Internal.Buffers;

namespace LogParsing.Core.Factories
{
    /// <summary>
    /// 提供创建 <see cref="ILogBuffer"/> 实例的静态工厂方法。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本工厂根据指定的缓冲区模式和预估数据规模，智能选择内存或文件作为底层存储，
    /// 以在性能与资源占用之间取得平衡。
    /// </para>
    /// <para>
    /// 设计目标包括：
    /// <list type="bullet">
    ///   <item><description>封装具体的缓冲区实现（<see cref="InMemoryLogBuffer"/> / <see cref="FileLogBuffer"/>），解耦调用方与实现细节；</description></item>
    ///   <item><description>在 <see cref="LogBufferMode.Auto"/> 模式下，基于可用内存动态决策，避免内存溢出；</description></item>
    ///   <item><description>通过参数 <c>estimatedSizeBytes</c> 支持容量预判，提升自动模式的准确性。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 所有返回的 <see cref="ILogBuffer"/> 实例均需由调用方负责释放（通过 <see cref="IDisposable.Dispose"/>），
    /// 尤其是文件缓冲区，以确保临时文件被正确清理。
    /// </para>
    /// </remarks>
    public static class LogBufferFactory
    {
        /// <summary>
        /// 根据指定的缓冲区模式、预估数据大小和临时目录，创建并返回一个 <see cref="ILogBuffer"/> 实例。
        /// </summary>
        /// <param name="mode">
        /// 缓冲区存储模式。参见 <see cref="LogBufferMode"/> 枚举定义。
        /// </param>
        /// <param name="estimatedSizeBytes">
        /// 预估的日志数据总大小（以字节为单位）。该值用于 <see cref="LogBufferMode.Auto"/> 模式下的决策。
        /// 若无法预估，建议传入保守估计值（如 0 或典型批次大小）。
        /// 必须为非负数。
        /// </param>
        /// <param name="tempDir">
        /// 用于创建临时文件的目录路径。仅在使用文件缓冲区时生效。
        /// 路径必须有效且当前进程具有写权限。
        /// 不得为 <see langword="null"/> 或空字符串。
        /// </param>
        /// <returns>
        /// 一个已初始化的 <see cref="ILogBuffer"/> 实例，其生命周期由调用方管理。
        /// 返回的实例永不为 <see langword="null"/>。
        /// </returns>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="estimatedSizeBytes"/> 为负数时抛出。
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="tempDir"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="tempDir"/> 为空字符串或仅包含空白字符时抛出。
        /// </exception>
        /// <exception cref="IOException">
        /// 当 <paramref name="tempDir"/> 指定的路径无效或不可访问时，可能在后续文件操作中抛出（延迟至实际使用）。
        /// </exception>
        /// <remarks>
        /// <para>
        /// 在 <see cref="LogBufferMode.Auto"/> 模式下，工厂使用以下启发式规则：
        /// 如果 <paramref name="estimatedSizeBytes"/> 小于当前 GC 可用内存的 30%，
        /// 则选择内存缓冲区；否则回退到文件缓冲区。
        /// </para>
        /// <para>
        /// 文件缓冲区的临时文件命名格式为 <c>logbuffer_{GUID}.tmp</c>，位于 <paramref name="tempDir"/> 目录下，
        /// 并将在缓冲区被 <see cref="IDisposable.Dispose"/> 时自动删除。
        /// </para>
        /// </remarks>
        public static ILogBuffer Create(
            LogBufferMode mode,
            long estimatedSizeBytes,
            string tempDir)
        {
            if (mode == LogBufferMode.InMemory)
                return new InMemoryLogBuffer();

            if (mode == LogBufferMode.File)
                return new FileLogBuffer(
                    Path.Combine(tempDir, $"logbuffer_{Guid.NewGuid()}.tmp"));

            // Auto
            long availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

            if (estimatedSizeBytes < availableMemory * 0.3)
            {
                return new InMemoryLogBuffer();
            }

            return new FileLogBuffer(
                Path.Combine(tempDir, $"logbuffer_{Guid.NewGuid()}.tmp"));
        }
    }
}