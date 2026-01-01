using LogParsing.Core.Abstractions;
using LogParsing.Core.Models;
using System.Text.RegularExpressions;

namespace LogParsing.Core.Internal.Parsers
{
    /// <summary>
    /// 实现 <see cref="ILogParser"/> 接口，用于解析 IEC 60870-5-104（电力系统通信规约）调试日志。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本解析器从结构化日志行中提取以下关键信息：
    /// <list type="bullet">
    ///   <item><description>通信方向（<c>Sending</c> / <c>Received</c>）；</description></item>
    ///   <item><description>通道编号（<c>Channel</c>）；</description></item>
    ///   <item><description>报文序号（<c>SequenceNumber</c>）；</description></item>
    ///   <item><description>延迟确认标志（<c>DelayACK</c>）；</description></item>
    ///   <item><description>原始十六进制网络数据负载；</description></item>
    ///   <item><description>预期或实际传输字节数（<c>ExpectedLength</c>）。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 解析流程分为两阶段：
    /// <list type="number">
    ///   <item>通用日志头由 <see cref="LogHeaderParser.TryParse"/> 处理；</item>
    ///   <item>IEC 870 特有字段由 <see cref="ParsePowerSpecific"/> 提取。</item>
    /// </list>
    /// </para>
    /// <para>
    /// ⚠️ 注意：当前实现对日志格式高度敏感，依赖特定关键词（如 <c>"[iec870ip"</c>、<c>"Channel ("</c> 等）。
    /// 若上游日志格式变更，可能导致字段缺失或解析失败。
    /// </para>
    /// <para>
    /// 内部保留一个未使用的 <see cref="_hexBuffer"/> 字段，为未来支持跨行十六进制数据拼接预留扩展点。
    /// </para>
    /// </remarks>
    public class PowerLogParser : ILogParser
    {
        // 预留缓冲区：用于未来支持跨行 Hex 数据拼接
        // 当前版本未使用，保留是为了避免后续频繁调整字段结构
        private readonly List<byte> _hexBuffer = new List<byte>();

        /// <summary>
        /// 判断指定日志行是否可由本解析器处理。
        /// </summary>
        /// <param name="logLine">
        /// 待检测的日志行字符串。不得为 <see langword="null"/>。
        /// </param>
        /// <returns>
        /// 若日志行包含特征子串 <c>"[iec870ip"</c>，则返回 <see langword="true"/>；
        /// 否则返回 <see langword="false"/>。
        /// </returns>
        /// <remarks>
        /// 此方法基于子字符串匹配，是 IEC 870 日志的稳定识别特征。
        /// 不进行完整解析，仅作快速筛选。
        /// </remarks>
        public bool CanParse(string logLine)
        {
            // IEC 870 日志的稳定特征字符串
            return logLine.Contains("[iec870ip");
        }

        /// <summary>
        /// 解析符合 IEC 60870-5-104 格式的日志行，并返回结构化的日志条目。
        /// </summary>
        /// <param name="logLine">
        /// 要解析的日志行字符串。不得为 <see langword="null"/>。
        /// </param>
        /// <returns>
        /// 一个填充了通用头字段和 IEC 870 特有字段的 <see cref="PowerLogEntry"/> 实例。
        /// 即使解析部分失败，仍返回有效对象（字段可能为空或默认值）。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="logLine"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <exception cref="FormatException">
        /// 当通用日志头中的时间戳格式无效时，由 <see cref="LogHeaderParser.TryParse"/> 抛出。
        /// </exception>
        /// <remarks>
        /// 本方法首先尝试解析通用日志头（时间、线程、模块等），
        /// 若成功，则进一步解析 IEC 870 特有字段；
        /// 若头部解析失败，仍返回空 <see cref="PowerLogEntry"/>（不含特有字段）。
        /// </remarks>
        public LogEntry Parse(string logLine)
        {
            ReadOnlySpan<char> span = logLine.AsSpan();
            var entry = new PowerLogEntry();

            if (!LogHeaderParser.TryParse(span, entry, out var msgSpan))
                return entry;

            ParsePowerSpecific(entry, msgSpan);
            return entry;
        }

        /// <summary>
        /// 从消息体中提取 IEC 60870-5-104 规约相关的特定字段。
        /// </summary>
        /// <param name="entry">
        /// 已填充通用头字段的 <see cref="PowerLogEntry"/> 实例。
        /// 方法将直接修改其属性以填充规约特有数据。
        /// 不得为 <see langword="null"/>。
        /// </param>
        /// <param name="msg">
        /// 日志消息体部分的只读字符范围（已去除通用头）。
        /// </param>
        /// <remarks>
        /// <para>
        /// 本方法按以下顺序提取字段（失败时跳过，不影响后续解析）：
        /// <list type="number">
        ///   <item><description><c>Channel (N)</c> → <see cref="PowerLogEntry.Channel"/></description></item>
        ///   <item><description><c>SequenceNumber: N,</c> → <see cref="PowerLogEntry.SequenceNumber"/></description></item>
        ///   <item><description><c>DelayACK: 1</c> → <see cref="PowerLogEntry.DelayACK"/></description></item>
        ///   <item><description>冒号前的动词（如 <c>Sending</c>）→ <see cref="PowerLogEntry.Action"/></description></item>
        ///   <item><description>连续十六进制对 → <see cref="LogEntry.NetworkData"/></description></item>
        ///   <item><description>通过正则或关键词提取字节长度 → <see cref="PowerLogEntry.ExpectedLength"/></description></item>
        /// </list>
        /// </para>
        /// <para>
        /// ⚠️ 性能注意：部分逻辑（如 <c>Action</c> 和长度提取）调用了 <see cref="ReadOnlySpan{Char}.ToString()"/>，
        /// 在高频路径中会产生临时字符串分配。此为当前版本权衡，未来可通过 Span 模式匹配优化。
        /// </para>
        /// <para>
        /// 长度字段优先级：先尝试正则匹配 <c>"\d+ bytes"</c>，再尝试 <c>"X bytes of data"</c>。
        /// </para>
        /// </remarks>
        private void ParsePowerSpecific(PowerLogEntry entry, ReadOnlySpan<char> msg)
        {
            // 1. 解析 Channel
            int chIdx = msg.IndexOf("Channel (");
            if (chIdx >= 0)
            {
                int chEnd = msg[chIdx..].IndexOf(')');
                if (chEnd > 0)
                {
                    var chStr = msg.Slice(chIdx + 9, chEnd - 9).ToString();
                    entry.Channel = int.Parse(chStr);
                }
            }

            // 2. 解析 SequenceNumber
            int seqIdx = msg.IndexOf("SequenceNumber:");
            if (seqIdx >= 0)
            {
                int seqStart = seqIdx + "SequenceNumber:".Length;
                int commaIdx = msg[seqStart..].IndexOf(',');
                string seqStr;
                if (commaIdx >= 0)
                {
                    seqStr = msg.Slice(seqStart, commaIdx).ToString().Trim();
                }
                else
                {
                    seqStr = msg[seqStart..].ToString().Trim();
                }

                if (int.TryParse(seqStr, out int seq))
                    entry.SequenceNumber = seq;
            }


            // 3. 解析 DelayACK
            int delayIdx = msg.IndexOf("DelayACK:");
            if (delayIdx >= 0)
            {
                int delayEnd = msg[delayIdx..].IndexOf(' ');
                if (delayEnd < 0) delayEnd = msg.Length - delayIdx;
                var delayStr = msg.Slice(delayIdx + 9, delayEnd - 9).ToString();
                entry.DelayACK = delayStr == "1";
            }

            // 4. 解析 Action
            var colonIdx = msg.IndexOf(':');
            if (colonIdx >= 0)
            {
                entry.Action = msg.Slice(0, colonIdx).ToString().Trim();
            }
            else
            {
                // 避免多次 ToString()，只转一次（或者更高效：直接在 Span 上搜索）
                // 方法 1：使用 Span 搜索（推荐，零分配）
                if (msg.ToString().Contains("Sending"))
                {
                    entry.Action = "Sending";
                }
                else if (msg.ToString().Contains("Received"))
                {
                    entry.Action = "Received";
                }
                else
                {
                    // 可选：兜底逻辑，比如设为空或保留原逻辑
                    entry.Action = string.Empty; // 或其他默认值
                }
            }

            // 5. 解析 Hex 网络数据
            entry.NetworkData = LogHeaderParser.ParsePowerHexData(msg);

            // 解析发送/接收长度
            if (msg.ToString().Contains("Sending") || msg.ToString().Contains("Received"))
            {
                var m = Regex.Match(msg.ToString(), @"\b(\d+)\s+bytes");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int len))
                    entry.ExpectedLength = len;
            }

            //查找 "bytes of data"
            int dataLenIdx = msg.IndexOf("bytes of data"); // 用 ReadOnlySpan<char> 的重载
            if (dataLenIdx >= 0)
            {
                // 找到最后一个空格，获取数字
                int spaceIdx = msg.Slice(0, dataLenIdx).LastIndexOf(' ');
                if (spaceIdx >= 0)
                {
                    var lenSpan = msg.Slice(spaceIdx + 1, dataLenIdx - spaceIdx - 1);
                    if (int.TryParse(lenSpan, out int len))
                    {
                        entry.ExpectedLength = len; // 新增属性
                    }
                }
            }
        }
    }
}