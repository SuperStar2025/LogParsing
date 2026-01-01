using LogParsing.Core.Abstractions;
using LogParsing.Core.Models;

namespace LogParsing.Core.Internal.Parsers
{
    /// <summary>
    /// 实现 <see cref="ILogParser"/> 接口，用于解析 Modbus 通信调试日志。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本解析器从 IOServer 或 DCB 相关日志中提取以下关键信息：
    /// <list type="bullet">
    ///   <item><description>通信动作（<c>Action</c>），如 <c>"Send"</c>、<c>"Receive"</c> 或 <c>"Reply()"</c>；</description></item>
    ///   <item><description>设备通道标识（<c>DCB</c>）；</description></item>
    ///   <item><description>请求/会话唯一标识（<c>ID</c>）；</description></item>
    ///   <item><description>协议声明的数据长度（<c>ExpectedLength</c>）；</description></item>
    ///   <item><description>原始十六进制网络负载（<c>NetworkData</c>）。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 解析流程分为两个阶段：
    /// <list type="number">
    ///   <item>通用日志头（时间戳、线程、模块名等）由 <see cref="LogHeaderParser.TryParse"/> 统一处理；</item>
    ///   <item>Modbus 特有字段由 <see cref="ParseModbusMessage"/> 从消息体中提取。</item>
    /// </list>
    /// </para>
    /// <para>
    /// ⚠️ 注意：本解析器依赖日志中的关键词（如 <c>"IOServer"</c>、<c>"DCB="</c>）进行识别，
    /// 不执行协议格式校验。若上游日志格式变更，可能导致字段缺失。
    /// </para>
    /// <para>
    /// 所有解析方法均保证返回非 <see langword="null"/> 的 <see cref="ModbusLogEntry"/> 实例，
    /// 便于调用方免空检查，符合“防御性返回”设计原则。
    /// </para>
    /// </remarks>
    public class ModbusLogParser : ILogParser
    {
        /// <summary>
        /// 判断指定日志行是否可能包含 Modbus 通信信息。
        /// </summary>
        /// <param name="logLine">
        /// 待检测的日志行字符串。不得为 <see langword="null"/>。
        /// </param>
        /// <returns>
        /// 若日志行包含子串 <c>"IOServer"</c> 或 <c>"DCB="</c>，则返回 <see langword="true"/>；
        /// 否则返回 <see langword="false"/>。
        /// </returns>
        /// <remarks>
        /// 此方法仅基于关键词存在性进行快速筛选，**不验证日志格式合法性**，
        /// 以降低预过滤开销。误报（false positive）是可接受的，后续 <see cref="Parse"/> 会处理无效内容。
        /// </remarks>
        public bool CanParse(string logLine)
        {
            // Modbus 日志常见特征：IOServer 模块或 DCB 字段
            return logLine.Contains("IOServer") || logLine.Contains("DCB=");
        }

        /// <summary>
        /// 解析单行 Modbus 日志，并返回结构化的日志条目。
        /// </summary>
        /// <param name="logLine">
        /// 要解析的日志行字符串。不得为 <see langword="null"/>。
        /// </param>
        /// <returns>
        /// 一个已填充通用头字段（如时间戳）和 Modbus 特有字段的 <see cref="ModbusLogEntry"/> 实例。
        /// 即使解析失败（如头部无效或无 Modbus 数据），仍返回有效对象（字段可能为空或默认值），
        /// 以避免调用方进行 <see langword="null"/> 检查。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="logLine"/> 为 <see langword="null"/> 时抛出。
        /// </exception>
        /// <exception cref="FormatException">
        /// 当通用日志头中的时间戳格式无效时，由 <see cref="LogHeaderParser.TryParse"/> 抛出。
        /// </exception>
        /// <remarks>
        /// 本方法首先尝试解析标准日志头。若失败，则跳过业务字段解析，但仍返回部分初始化的条目。
        /// 成功解析头部后，将委托 <see cref="ParseModbusMessage"/> 提取协议相关数据。
        /// </remarks>
        public LogEntry Parse(string logLine)
        {
            ReadOnlySpan<char> span = logLine.AsSpan();
            var entry = new ModbusLogEntry();

            // 统一解析日志头（时间、线程、模块等），
            // 若头部解析失败，则认为该行无法继续解析业务数据
            if (!LogHeaderParser.TryParse(span, entry, out var msgSpan))
                return entry;

            ParseModbusMessage(entry, msgSpan);
            return entry;
        }

        /// <summary>
        /// 从日志消息体中提取 Modbus 协议相关的字段。
        /// </summary>
        /// <param name="entry">
        /// 已填充通用日志头的 <see cref="ModbusLogEntry"/> 实例。
        /// 本方法将直接修改其属性以填充 Modbus 特有数据。
        /// 不得为 <see langword="null"/>。
        /// </param>
        /// <param name="msg">
        /// 去除通用日志头后的消息体部分，表示为只读字符范围。
        /// </param>
        /// <remarks>
        /// <para>
        /// 本方法按以下顺序提取字段（任一字段解析失败不影响其他字段）：
        /// <list type="number">
        ///   <item><description>通过 <see cref="ParseAction"/> 提取 <see cref="ModbusLogEntry.Action"/>；</description></item>
        ///   <item><description>查找 <c>"DCB=xxx"</c> 提取通道标识；</description></item>
        ///   <item><description>查找 <c>"ID=yyy"</c> 提取会话标识；</description></item>
        ///   <item><description>解析 <c>"Length N"</c> 获取期望字节数；</description></item>
        ///   <item><description>调用 <see cref="LogHeaderParser.ParseModbusHexData"/> 提取十六进制负载。</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// 所有字符串提取操作均使用 <see cref="ReadOnlySpan{Char}"/> 以减少分配，
        /// 仅在必要时（如赋值给字符串属性）调用 <c>ToString</c>。
        /// </para>
        /// <para>
        /// 长度解析采用手动空格/制表符扫描，避免正则表达式开销，符合高性能日志处理场景要求。
        /// </para>
        /// </remarks>
        private void ParseModbusMessage(ModbusLogEntry entry, ReadOnlySpan<char> msg)
        {
            // 1️⃣ Action：位于消息起始位置，通常在逗号或制表符前
            entry.Action = ParseAction(msg);

            // 2️⃣ DCB：用于标识通信通道，格式固定为 "DCB=xxx"
            int dcbIdx = msg.IndexOf("DCB=");
            if (dcbIdx >= 0)
            {
                int endIdx = msg[dcbIdx..].IndexOf(' ');
                if (endIdx < 0)
                    endIdx = msg.Length - dcbIdx;

                // +4 跳过 "DCB="，-5 排除后续空格
                entry.DCB = msg.Slice(dcbIdx + 4, endIdx - 5)
                    .ToString()
                    .Trim();
            }

            // 3️⃣ ID：设备或请求标识，用于区分不同通信实例
            int idIdx = msg.IndexOf("ID=");
            if (idIdx >= 0)
            {
                int endIdx = msg[idIdx..].IndexOf(' ');
                if (endIdx < 0)
                    endIdx = msg.Length - idIdx;

                entry.ID = msg.Slice(idIdx + 3, endIdx - 3)
                    .ToString()
                    .Trim();
            }

            // 4️⃣ Length：协议声明的期望数据长度，用于后续完整性校验
            int lenIdx = msg.IndexOf("Length");
            if (lenIdx >= 0)
            {
                // 跳过 "Length" 本身，解析后续数字
                var afterLength = msg[(lenIdx + 6)..].TrimStart();

                int endIdx = -1;
                for (int i = 0; i < afterLength.Length; i++)
                {
                    if (afterLength[i] == ' ' || afterLength[i] == '\t')
                    {
                        endIdx = i;
                        break;
                    }
                }

                ReadOnlySpan<char> lenSpan =
                    endIdx >= 0 ? afterLength[..endIdx] : afterLength;

                if (int.TryParse(lenSpan, out int len))
                    entry.ExpectedLength = len;
            }

            // 5️⃣ 网络数据：十六进制负载，用于后续帧重组
            entry.NetworkData = LogHeaderParser.ParseModbusHexData(msg);
        }

        /// <summary>
        /// 从消息体开头提取通信动作（Action）关键字。
        /// </summary>
        /// <param name="msg">
        /// 日志消息体的只读字符范围。
        /// </param>
        /// <returns>
        /// 提取到的动作字符串，例如 <c>"Send"</c>、<c>"Receive"</c> 或 <c>"Reply()"</c>；
        /// 若无法识别或输入为空，则返回空字符串（<see cref="string.Empty"/>）。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 动作关键字位于消息最前端，通常在第一个逗号（<c>,</c>）或制表符（<c>\t</c>）之前。
        /// 本方法优先截断至最早出现的分隔符，以避免后续描述文本干扰。
        /// </para>
        /// <para>
        /// 特殊处理 <c>"Reply()"</c> 格式——因其包含括号，视为完整动词直接返回。
        /// 其他情况取首个空白分隔的单词作为动作名称。
        /// </para>
        /// <para>
        /// 返回空字符串而非 <see langword="null"/>，确保调用方无需空值检查。
        /// </para>
        /// </remarks>
        private static string ParseAction(ReadOnlySpan<char> msg)
        {
            if (msg.IsEmpty)
                return string.Empty;

            // 优先截断到逗号或制表符，避免后续描述干扰
            int cutIdx = msg.IndexOf(',');
            int tabIdx = msg.IndexOf('\t');

            if (cutIdx < 0 || (tabIdx >= 0 && tabIdx < cutIdx))
                cutIdx = tabIdx;

            ReadOnlySpan<char> head =
                cutIdx >= 0 ? msg.Slice(0, cutIdx) : msg;

            head = head.Trim();
            if (head.IsEmpty)
                return string.Empty;

            // 特殊格式：Reply() 直接作为 Action 返回
            if (head.StartsWith("Reply()", StringComparison.Ordinal))
                return "Reply()";

            // 默认取首个单词作为 Action
            int spaceIdx = head.IndexOf(' ');
            return spaceIdx > 0
                ? head.Slice(0, spaceIdx).ToString()
                : head.ToString();
        }
    }
}