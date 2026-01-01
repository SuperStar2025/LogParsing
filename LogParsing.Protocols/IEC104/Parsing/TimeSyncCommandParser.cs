using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Results;

namespace LogParsing.Protocols.IEC104.Parsing
{
    /// <summary>
    /// 提供对 IEC 60870-5-104 对时命令（Clock Synchronization Command, C_RTC_SYNC）的解析功能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 仅支持标准类型标识符 <see cref="IEC104TypeId.C_RTC_SYNC"/>。
    /// 该命令用于主站向子站下发 UTC 时间，子站据此校准本地时钟。
    /// </para>
    /// <para>
    /// 解析逻辑严格遵循 IEC 60870-5-104 标准中关于可变结构限定词（VSQ）和序列标志（SQ）的规定。
    /// 具体行为由 <see cref="Parse"/> 方法的 <c>isSequence</c> 参数决定：
    /// 当其为 <see langword="true"/> 时，首对象包含完整 IOA，
    /// 后续对象地址按序递增；否则每个对象均携带独立 IOA。
    /// </para>
    /// <para>
    /// 每个命令对象由 3 字节 IOA + 7 字节 CP56Time2a 时间戳组成。
    /// CP56Time2a 编码格式符合 IEC 60870-5-7 标准，表示精确到毫秒的 UTC 时间。
    /// </para>
    /// </remarks>
    internal sealed class TimeSyncCommandParser
    {
        /// <summary>
        /// 获取当前解析器支持的 IEC 104 类型标识符集合。
        /// </summary>
        /// <remarks>
        /// 本解析器仅支持 <see cref="IEC104TypeId.C_RTC_SYNC"/> 类型。
        /// 若未来扩展支持其他时间同步相关命令，需同步更新此集合及解析逻辑。
        /// </remarks>
        private static readonly HashSet<IEC104TypeId> SupportedTypeIds =
            new() { IEC104TypeId.C_RTC_SYNC };

        /// <summary>
        /// 解析对时命令 ASDU 载荷，并返回一组标准化的时间同步命令结果。
        /// </summary>
        /// <param name="typeId">
        /// ASDU 的类型标识符。必须是 <see cref="SupportedTypeIds"/> 中的成员（即 <see cref="IEC104TypeId.C_RTC_SYNC"/>），
        /// 否则将抛出 <see cref="NotSupportedException"/>。
        /// </param>
        /// <param name="commonAddress">公共地址（Common Address, CA），标识目标子站。</param>
        /// <param name="payload">
        /// ASDU 信息体原始字节序列（不含类型标识、VSQ、CA、COT 等头部字段）。
        /// 调用方需确保其长度足以容纳指定数量的对象。
        /// 每个对象包含：3 字节信息对象地址（IOA）+ 7 字节 CP56Time2a 时间戳。
        /// </param>
        /// <param name="timestamp">
        /// 可选接收时间戳（通常为主站发送帧的本地时间）。此值不参与命令解析，
        /// 但会作为元数据保留在结果中，用于日志追踪或性能分析。
        /// </param>
        /// <param name="numberOfObjects">
        /// 信息对象数量（来自 VSQ 的低 7 位）。必须大于 0。
        /// 注意：实际应用中，C_RTC_SYNC 通常只包含一个对象，但协议允许多个。
        /// </param>
        /// <param name="isSequence">
        /// 序列标志（SQ 位）。若为 <see langword="true"/>，表示对象地址连续（仅首对象含完整 IOA）；
        /// 若为 <see langword="false"/>，每个对象均包含独立 IOA。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（Cause of Transmission, COT），如激活（6）、激活确认（7）等。</param>
        /// <returns>
        /// 一个只读列表，包含解析生成的 <see cref="TimeSyncCommandResult"/> 实例。
        /// 列表顺序与载荷中对象顺序一致。
        /// 每个结果包含目标 IOA 和主站下发的同步时间（UTC）。
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// 当 <paramref name="typeId"/> 不等于 <see cref="IEC104TypeId.C_RTC_SYNC"/> 时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="payload"/> 长度不足以解析指定数量的对象时，
        /// 可能因索引越界而引发异常（由 <see cref="ReadOnlySpan{T}"/> 访问触发）。
        /// </exception>
        public IReadOnlyList<TimeSyncCommandResult> Parse(
            IEC104TypeId typeId,
            ushort commonAddress,
            ReadOnlySpan<byte> payload,
            DateTimeOffset? timestamp,
            int numberOfObjects,
            bool isSequence,
            ushort causeOfTransmission)
        {
            if (!SupportedTypeIds.Contains(typeId))
                throw new NotSupportedException(
                    $"TypeId '{typeId}' is not supported by {nameof(TimeSyncCommandParser)}.");

            var results = new List<TimeSyncCommandResult>();
            int index = 0;
            int ioaBase = 0;

            for (int i = 0; i < numberOfObjects; i++)
            {
                int ioa;
                if (isSequence)
                {
                    if (i == 0)
                    {
                        ioaBase = payload[index] | (payload[index + 1] << 8) | (payload[index + 2] << 16);
                        index += 3;
                    }
                    ioa = ioaBase + i;
                }
                else
                {
                    ioa = payload[index] | (payload[index + 1] << 8) | (payload[index + 2] << 16);
                    index += 3;
                }

                var syncTime = ParseCp56Time2a(payload.Slice(index, 7));
                index += 7;

                results.Add(new TimeSyncCommandResult(
                    typeId,
                    commonAddress,
                    ioa,
                    syncTime,
                    causeOfTransmission,
                    timestamp));
            }

            return results;
        }

        /// <summary>
        /// 将 CP56Time2a 格式的 7 字节时间编码解析为 <see cref="DateTime"/>（UTC）。
        /// </summary>
        /// <param name="buffer">
        /// 长度为 7 的字节序列，符合 IEC 60870-5-7 CP56Time2a 编码规范。
        /// 字节布局如下：
        /// <list type="table">
        ///   <listheader><term>字节索引</term><description>内容（低位在前）</description></listheader>
        ///   <item><term>0–1</term><description>毫秒（0–59999）</description></item>
        ///   <item><term>2</term><description>分钟（bit 0–5），bit 6–7 保留</description></item>
        ///   <item><term>3</term><description>小时（bit 0–4），bit 5–7 保留</description></item>
        ///   <item><term>4</term><description>日（bit 0–4），bit 5–7 保留</description></item>
        ///   <item><term>5</term><description>月（bit 0–3），bit 4–7 保留</description></item>
        ///   <item><term>6</term><description>年（0–99，表示 2000–2099），bit 7 保留</description></item>
        /// </list>
        /// </param>
        /// <returns>
        /// 表示 UTC 时间的 <see cref="DateTime"/> 实例，精度为毫秒。
        /// </returns>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="buffer"/> 长度不为 7 时，由 <c>buffer.Slice(…)</c> 或位操作隐式引发。
        /// </exception>
        /// <remarks>
        /// 此方法假设输入数据已通过协议层校验，不进行额外范围检查（如月份是否在 1–12）。
        /// 在生产环境中，若需防御性编程，建议增加有效性验证。
        /// </remarks>
        private static DateTime ParseCp56Time2a(ReadOnlySpan<byte> buffer)
        {
            int milliseconds = buffer[0] | (buffer[1] << 8);
            int second = milliseconds / 1000;
            int millisecond = milliseconds % 1000;

            int minute = buffer[2] & 0x3F;
            int hour = buffer[3] & 0x1F;
            int day = buffer[4] & 0x1F;
            int month = buffer[5] & 0x0F;
            int year = 2000 + (buffer[6] & 0x7F);

            return new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);
        }
    }
}
