using LogParsing.Core.Models;

namespace LogParsing.Core.Internal.Parsers
{
    /// <summary>
    /// 提供高性能、零分配的日志头部解析功能，用于从制表符分隔的原始日志行中提取通用元数据。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本解析器专为结构化日志格式设计，假设字段按固定顺序出现，并以制表符（<c>\t</c>）分隔。
    /// 它直接操作 <see cref="ReadOnlySpan{Char}"/>，避免中间字符串分配，
    /// 并将消息体部分以 <see cref="ReadOnlySpan{Char}"/> 形式返回，供后续协议解析器复用。
    /// </para>
    /// <para>
    /// 支持的字段顺序（共10个必需字段 + 可选行号）：
    /// <list type="number">
    ///   <item><description>时间戳（ISO 8601 格式）</description></item>
    ///   <item><description>时区标识</description></item>
    ///   <item><description>日志级别（如 <c>[INFO]</c>）</description></item>
    ///   <item><description>模块名（如 <c>[Network]</c>）</description></item>
    ///   <item><description>线程 ID（如 <c>[12345]</c>）</description></item>
    ///   <item><description>来源模块（如 <c>[ProtocolEngine]</c>）</description></item>
    ///   <item><description>函数名（如 <c>[OnReceive]</c>）</description></item>
    ///   <item><description>文件名（如 <c>[Parser.cs]</c>）</description></item>
    ///   <item><description>文件路径（如 <c>[Core/Parsers/...]</c>）</description></item>
    ///   <item><description>行号（可选，如 <c>[42]</c>）</description></item>
    /// </list>
    /// 消息体位于最后一个字段之后，可选地以冒号（<c>:</c>）与头部分隔。
    /// </para>
    /// <para>
    /// ⚠️ 此解析器为“非宽容型”实现：
    /// <list type="bullet">
    ///   <item><description>若缺少任一必需制表符，<see cref="TryParse"/> 返回 <see langword="false"/>；</description></item>
    ///   <item><description>不处理字段缺失、乱序或额外字段；</description></item>
    ///   <item><description>仅适用于格式稳定、由受控系统生成的日志源。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 本类为内部静态类（<see langword="internal static"/>），仅供日志解析管道使用。
    /// </para>
    /// </remarks>
    internal static class LogHeaderParser
    {
        /// <summary>
        /// 尝试从给定的日志行中解析标准头部字段，并输出消息体的字符范围。
        /// </summary>
        /// <param name="span">
        /// 表示完整日志行的只读字符范围。不得为 <see langword="null"/>。
        /// </param>
        /// <param name="entry">
        /// 用于接收解析结果的 <see cref="LogEntry"/> 实例。
        /// 方法将直接修改其属性，**不会创建新对象**，以最小化 GC 压力。
        /// 不得为 <see langword="null"/>。
        /// </param>
        /// <param name="messageSpan">
        /// 输出参数：指向日志消息体部分的 <see cref="ReadOnlySpan{Char}"/>。
        /// 若日志包含冒号（<c>:</c>），则从冒号后开始；
        /// 否则为剩余全部内容。内容已去除首尾空白。
        /// </param>
        /// <returns>
        /// <see langword="true"/> 表示成功解析出所有必需头部字段；
        /// <see langword="false"/> 表示日志格式不符合预期（如制表符数量不足）。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="entry"/> 为 <see langword="null"/> 时抛出。
        /// （注：由于 <paramref name="span"/> 为值类型，不可能为 null）
        /// </exception>
        /// <exception cref="FormatException">
        /// 当时间戳字段无法被 <see cref="DateTimeOffset.Parse(string)"/> 解析时抛出。
        /// </exception>
        /// <remarks>
        /// <para>
        /// 时间戳使用 <see cref="DateTimeOffset"/> 而非 <see cref="DateTime"/>，
        /// 以保留原始时区偏移信息，避免跨时区系统解析歧义。
        /// </para>
        /// <para>
        /// 所有带方括号的字段（如 <c>[INFO]</c>）会自动去除首尾 <c>[</c> 和 <c>]</c>。
        /// </para>
        /// <para>
        /// 行号字段为可选；若存在且为有效整数，则赋值给 <see cref="LogEntry.Line"/>；
        /// 否则保持默认值（通常为 0）。
        /// </para>
        /// </remarks>
        public static bool TryParse(
            ReadOnlySpan<char> span,
            LogEntry entry,
            out ReadOnlySpan<char> messageSpan)
        {
            messageSpan = default;

            // 1️⃣ 时间戳（假设位于首字段）
            int tab1 = span.IndexOf('\t');
            if (tab1 < 0) return false;

            // 使用 DateTimeOffset 而非 DateTime，
            // 以保留时区语义，避免跨系统解析歧义
            entry.Timestamp = DateTimeOffset.Parse(span[..tab1].ToString());

            // 2️⃣ 时区
            int tab2 = span[(tab1 + 1)..].IndexOf('\t');
            entry.TimeZone = span.Slice(tab1 + 1, tab2).ToString();

            // 3️⃣ 日志级别
            int tab3 = span[(tab1 + 1 + tab2 + 1)..].IndexOf('\t');
            var levelSpan = span
                .Slice(tab1 + 1 + tab2 + 1, tab3)
                .Trim();

            entry.Level = levelSpan
                .TrimStart('[')
                .TrimEnd(']')
                .ToString();

            int idx = tab1 + 1 + tab2 + 1 + tab3 + 1;
            int nextTab;

            // 4️⃣ 模块名
            nextTab = span[idx..].IndexOf('\t');
            var moduleSpan = span.Slice(idx, nextTab).Trim();
            entry.Module = moduleSpan
                .TrimStart('[')
                .TrimEnd(']')
                .Trim()
                .ToString();

            idx += nextTab + 1;

            // 5️⃣ 线程 ID
            nextTab = span[idx..].IndexOf('\t');
            var threadIdSpan = span.Slice(idx, nextTab).Trim();
            entry.ThreadId = threadIdSpan
                .TrimStart('[')
                .TrimEnd(']')
                .ToString();

            idx += nextTab + 1;

            // 6️⃣ 来源模块
            nextTab = span[idx..].IndexOf('\t');
            var sourceSpan = span.Slice(idx, nextTab).Trim();
            entry.Source = sourceSpan
                .TrimStart('[')
                .TrimEnd(']')
                .Trim()
                .ToString();

            idx += nextTab + 1;

            // 7️⃣ 函数名
            nextTab = span[idx..].IndexOf('\t');
            var functionSpan = span.Slice(idx, nextTab).Trim();
            entry.Function = functionSpan
                .TrimStart('[')
                .TrimEnd(']')
                .Trim()
                .ToString();

            idx += nextTab + 1;

            // 8️⃣ 文件名
            nextTab = span[idx..].IndexOf('\t');
            var fileSpan = span.Slice(idx, nextTab).Trim();
            entry.File = fileSpan
                .TrimStart('[')
                .TrimEnd(']')
                .Trim()
                .ToString();

            idx += nextTab + 1;

            // 9️⃣ 文件路径
            nextTab = span[idx..].IndexOf('\t');
            var filePathSpan = span.Slice(idx, nextTab).Trim();
            entry.FilePath = filePathSpan
                .TrimStart('[')
                .TrimEnd(']')
                .Trim()
                .ToString();

            idx += nextTab + 1;

            // 🔟 行号（部分日志可能缺失）
            nextTab = span[idx..].IndexOf('\t');
            var lineSpan = (nextTab >= 0
                    ? span.Slice(idx, nextTab)
                    : span.Slice(idx))
                .TrimStart('[')
                .TrimEnd(']')
                .Trim();

            if (int.TryParse(lineSpan.ToString(), out int line))
                entry.Line = line;

            idx += nextTab >= 0
                ? nextTab + 1
                : span.Length - idx;

            // 1️⃣1️⃣ 消息体
            // 优先按 ':' 分隔，兼容“头:消息”与“纯数据行”两种格式
            int msgIdx = span.Slice(idx).IndexOf(':');
            if (msgIdx >= 0)
            {
                messageSpan = span.Slice(idx + msgIdx + 1).Trim();
                entry.Message = messageSpan.ToString();
            }
            else
            {
                // 没有冒号时，整段视为消息（常见于数据行）
                messageSpan = span.Slice(idx).Trim();
                entry.Message = messageSpan.ToString();
            }

            return true;
        }

        /// <summary>
        /// 从 IEC 60870-5（电力系统通信规约）日志消息中提取连续的十六进制字节序列。
        /// </summary>
        /// <param name="msg">
        /// 日志消息体的只读字符范围，通常包含空格分隔的十六进制对（如 <c>00 11 aa ff</c>）。
        /// </param>
        /// <returns>
        /// 成功提取至少一个有效字节时，返回对应的 <see cref="byte"/> 数组；
        /// 否则返回空数组（长度为 0）。永不返回 <see langword="null"/>。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 本方法采用“贪婪扫描”策略：
        /// 遍历整个输入，识别所有连续的两个十六进制字符组合，
        /// 忽略非十六进制字符和多余空格。
        /// </para>
        /// <para>
        /// 适用于“纯数据行”场景（如调试输出中的原始报文），
        /// 不依赖任何长度前缀或结构化字段。
        /// </para>
        /// <para>
        /// 性能提示：由于使用 <see cref="List{T}"/> 中间存储，会产生一次数组分配。
        /// 在高频解析路径中应谨慎使用。
        /// </para>
        /// </remarks>
        public static byte[]? ParsePowerHexData(ReadOnlySpan<char> msg)
        {
            // 查找 16进制字符串：00 11 aa ff ...
            var hexList = new List<byte>();
            int i = 0;
            while (i < msg.Length)
            {
                // 跳过空格
                while (i < msg.Length && msg[i] == ' ') i++;
                if (i + 2 > msg.Length) break;

                if (IsHex(msg[i]) && IsHex(msg[i + 1]))
                {
                    string hex = new string(new char[] { msg[i], msg[i + 1] });
                    hexList.Add(Convert.ToByte(hex, 16));
                    i += 2;
                }
                else
                {
                    i++;
                }
            }

            if (hexList.Count > 0)
            {
                return hexList.ToArray();
            }
            else
            {
                return [];
            }
        }

        /// <summary>
        /// 从 Modbus 协议日志消息中提取指定长度的十六进制数据负载。
        /// </summary>
        /// <param name="msg">
        /// 包含 "Length" 字段和后续十六进制数据的日志消息体。
        /// 例如：<c>Raw Receive Length 6\t01 03 00 00 00 01</c>。
        /// </param>
        /// <returns>
        /// 若成功解析出长度声明并提取到至少一个字节，则返回对应 <see cref="byte"/> 数组；
        /// 否则返回空数组（长度为 0）。永不返回 <see langword="null"/>。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 本方法强依赖日志中存在格式为 <c>" Length &lt;decimal&gt;\t"</c> 的子串，
        /// 用于确定预期数据长度。若该模式缺失或长度无效，则立即返回空数组。
        /// </para>
        /// <para>
        /// 数据提取从第一个制表符之后开始，并严格限制最多读取 <c>expectedLength</c> 个字节，
        /// 以防止因日志截断导致的越界解析。
        /// </para>
        /// <para>
        /// ⚠️ 此实现对日志格式高度敏感；若上游日志格式变更，需同步更新此解析逻辑。
        /// </para>
        /// </remarks>
        public static byte[]? ParseModbusHexData(ReadOnlySpan<char> msg)
        {
            // 查找 "Raw Receive Length <num>"
            var msgStr = msg.ToString();
            int lenIdx = msgStr.IndexOf(" Length");
            if (lenIdx < 0) return [];

            // 提取十进制长度
            int tabIdx = msgStr.IndexOf('\t', lenIdx);
            if (tabIdx < 0) return [];

            string lenStr = msgStr.Substring(lenIdx + 7, tabIdx - (lenIdx + 7)).Trim();
            if (!int.TryParse(lenStr, out int expectedLength)) return [];

            // Tab 之后开始是 Hex 数据
            var hexSpan = msg.Slice(tabIdx + 1);

            var hexList = new List<byte>();
            int i = 0;
            while (i < hexSpan.Length && hexList.Count < expectedLength)
            {
                // 跳过空格
                while (i < hexSpan.Length && hexSpan[i] == ' ') i++;

                // 两个字符组成一个字节
                if (i + 2 <= hexSpan.Length && IsHex(hexSpan[i]) && IsHex(hexSpan[i + 1]))
                {
                    string hex = new string(new char[] { hexSpan[i], hexSpan[i + 1] });
                    hexList.Add(Convert.ToByte(hex, 16));
                    i += 2;
                }
                else
                {
                    // 非 Hex 字符，跳过
                    i++;
                }
            }

            if (hexList.Count > 0)
            {
                return hexList.ToArray();
            }
            else
            {
                return [];
            }
        }

        /// <summary>
        /// 判断指定字符是否为有效的十六进制数字字符。
        /// </summary>
        /// <param name="c">待判断的 Unicode 字符。</param>
        /// <returns>
        /// 如果 <paramref name="c"/> 属于以下任一范围，则返回 <see langword="true"/>：
        /// <list type="bullet">
        ///   <item><description><c>'0'</c> 到 <c>'9'</c></description></item>
        ///   <item><description><c>'a'</c> 到 <c>'f'</c></description></item>
        ///   <item><description><c>'A'</c> 到 <c>'F'</c></description></item>
        /// </list>
        /// 否则返回 <see langword="false"/>。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 本方法为内联友好、无分支预测惩罚的紧凑实现，
        /// 专为高频调用的底层解析循环优化。
        /// </para>
        /// <para>
        /// 相较于 <see cref="char.IsAsciiHexDigit(char)"/>（.NET 7+），
        /// 此实现兼容更早的 .NET 版本，并明确限定为 ASCII 范围。
        /// </para>
        /// </remarks>
        private static bool IsHex(char c)
        {
            return c >= '0' && c <= '9' ||
                   c >= 'a' && c <= 'f' ||
                   c >= 'A' && c <= 'F';
        }
    }
}