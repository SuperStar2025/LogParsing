using LogParsing.Core.Models;

namespace LogParsing.Core.Processing
{
    /// <summary>
    /// 将按时间顺序排列的 <see cref="PowerLogEntry"/> 实例聚合成完整的电力通信帧。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本组装器基于实际电力系统调试日志的结构特征进行帧边界推断，适用于 IEC 60870-5-104、DL/T645 等常见规约。
    /// 其核心假设是：一个逻辑通信帧由一条带有明确动作（<c>"Sending"</c> 或 <c>"Received"</c>）的起始日志行，
    /// 后跟若干条无动作但包含十六进制数据的“延续行”组成。
    /// </para>
    /// <para>
    /// 帧识别依赖以下隐式规则（按优先级）：
    /// <list type="number">
    ///   <item><description>起始行必须包含非空 <see cref="PowerLogEntry.Action"/>（值为 <c>"Sending"</c> 或 <c>"Received"</c>）且 <see cref="PowerLogEntry.ExpectedLength"/> &gt; 0；</description></item>
    ///   <item><description>后续数据行必须无 <c>Action</c>（即 <see langword="null"/> 或空字符串）；</description></item>
    ///   <item><description>数据行的时间戳分钟部分必须与起始行一致（因日志时间精度通常为秒级，故仅比对分钟）；</description></item>
    ///   <item><description>所有数据行必须源自同一原始日志行号（<see cref="LogEntry.Line"/>），防止跨块拼接。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// ⚠️ 注意：本实现**不校验协议完整性**（如 CRC、帧头尾标志），仅做日志层聚合。
    /// 数据是否有效应由上层协议解析器判断。
    /// </para>
    /// <para>
    /// 该类为 <see langword="sealed"/>，不可继承，符合 .NET 设计指南中对工具类的推荐。
    /// </para>
    /// </remarks>
    public sealed class PowerFrameAssembler
    {
        /// <summary>
        /// 将按日志原始顺序排列的 <see cref="PowerLogEntry"/> 序列组装为 <see cref="PowerFrame"/> 流。
        /// </summary>
        /// <param name="entries">
        /// 已按日志出现顺序（即时间顺序）排列的 <see cref="PowerLogEntry"/> 集合。
        /// 本方法**不会对输入进行排序**，调用方必须确保顺序正确。
        /// 不得为 <see langword="null"/>。
        /// </param>
        /// <returns>
        /// 按输入顺序生成的 <see cref="PowerFrame"/> 实例序列。
        /// 即使帧数据不完整（如日志被截断或未达 <see cref="PowerLogEntry.ExpectedLength"/>），
        /// 仍会输出当前已收集的数据，以支持部分恢复场景。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="entries"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <remarks>
        /// <para>
        /// 本方法采用单次线性扫描（时间复杂度 O(n)，空间复杂度 O(帧大小)），
        /// 无回溯、无缓存全量数据，适合大文件或流式处理。
        /// </para>
        /// <para>
        /// 组装流程如下：
        /// <list type="number">
        ///   <item>遇到有效起始行（含 Action 和 ExpectedLength）时，若存在未完成帧，则立即输出；</item>
        ///   <item>后续无 Action、时间戳分钟匹配、Line 号一致的数据行被追加到当前缓冲区；</item>
        ///   <item>一旦缓冲区字节数 ≥ ExpectedLength，立即结束当前帧并输出；</item>
        ///   <item>遍历结束后，若仍有未完成帧，也予以输出（容忍日志末尾截断）。</item>
        /// </list>
        /// </para>
        /// <para>
        /// 时间戳比较仅使用 <see cref="DateTime.Minute"/>，这是对典型电力日志时间精度（秒级）的合理妥协，
        /// 避免因毫秒缺失导致误判。若需更高精度，应改进上游日志记录格式。
        /// </para>
        /// </remarks>
        public IEnumerable<PowerFrame> Assemble(IEnumerable<PowerLogEntry> entries)
        {
            PowerLogEntry? currentStart = null;
            List<byte> buffer = new();
            int? dataLine = null;

            foreach (var entry in entries)
            {
                // 1️⃣ 新帧起点：明确的发送 / 接收行为，并声明期望长度
                if (!string.IsNullOrEmpty(entry.Action) &&
                    (entry.Action == "Sending" || entry.Action == "Received") &&
                    entry.ExpectedLength > 0)
                {
                    // 若已有未完成帧，先输出（容忍不完整帧）
                    if (currentStart != null)
                        yield return BuildFrame(currentStart, buffer);

                    currentStart = entry;
                    buffer = new List<byte>();
                    dataLine = null;
                    continue;
                }

                // 2️⃣ 无帧上下文时，忽略数据行
                if (currentStart == null)
                    continue;

                // 3️⃣ 数据行匹配规则：
                // - 无 Action（避免误把控制行当数据）
                // - 时间戳与起始行一致（你已知：时间精度有限）
                // - 必须包含网络数据
                if (string.IsNullOrEmpty(entry.Action) &&
                    entry.Timestamp.DateTime.Minute == currentStart.Timestamp.DateTime.Minute &&
                    entry.NetworkData?.Length > 0)
                {
                    // 首个数据行，记录其 Line 号
                    if (dataLine == null)
                        dataLine = entry.Line;

                    // 后续数据行必须来自同一行号
                    if (entry.Line == dataLine)
                        buffer.AddRange(entry.NetworkData);
                }

                // 4️⃣ 数据已满足期望长度，提前结束帧
                if (buffer.Count >= currentStart.ExpectedLength)
                {
                    yield return BuildFrame(currentStart, buffer);
                    currentStart = null;
                    buffer = new List<byte>();
                    dataLine = null;
                }
            }

            // 5️⃣ 文件结束时，仍存在未输出帧
            if (currentStart != null)
                yield return BuildFrame(currentStart, buffer);
        }

        /// <summary>
        /// 根据帧起始日志条目和已收集的字节缓冲区创建 <see cref="PowerFrame"/> 实例。
        /// </summary>
        /// <param name="start">
        /// 表示帧起始的 <see cref="PowerLogEntry"/>，其 <see cref="PowerLogEntry.Action"/> 字段
        /// 用于确定通信方向（<c>"Sending"</c> 或 <c>"Received"</c>）。
        /// 不得为 <see langword="null"/>。
        /// </param>
        /// <param name="buffer">
        /// 包含按日志顺序拼接的原始网络数据字节的列表。
        /// 可能为空或长度小于 <see cref="PowerLogEntry.ExpectedLength"/>（表示不完整帧）。
        /// </param>
        /// <returns>
        /// 一个初始化完成的 <see cref="PowerFrame"/> 实例，包含方向、时间戳、期望长度、实际数据及原始起始条目引用。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 本方法**不执行任何数据验证或完整性检查**。调用方应通过比较
        /// <see cref="PowerFrame.Data"/>.<see cref="Array.Length"/> 与
        /// <see cref="PowerFrame.ExpectedLength"/> 来判断帧是否完整。
        /// </para>
        /// <para>
        /// 此设计将“组装”与“校验”职责分离，符合单一职责原则，并允许上层灵活处理部分数据（如重传分析、截断诊断）。
        /// </para>
        /// <para>
        /// 返回的 <see cref="PowerFrame.StartEntry"/> 引用可用于溯源或调试。
        /// </para>
        /// </remarks>
        private static PowerFrame BuildFrame(PowerLogEntry start, List<byte> buffer)
        {
            return new PowerFrame
            {
                Direction = start.Action!,
                Timestamp = start.Timestamp,
                ExpectedLength = start.ExpectedLength,
                Data = buffer.ToArray(),
                StartEntry = start
            };
        }
    }
}