using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Results;

namespace LogParsing.Protocols.IEC104.Parsing
{
    /// <summary>
    /// 提供对 IEC 60870-5-104 单点遥信状态（Single Point Information）的解析功能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 支持的标准类型包括：
    /// <see cref="IEC104TypeId.M_SP_NA_1"/>（无时标单点遥信）、
    /// <see cref="IEC104TypeId.M_SP_TB_1"/>（带 CP56Time2a 时标的单点遥信）、
    /// <see cref="IEC104TypeId.M_SP_TB_7"/>（带 CP56Time2a 且含附加品质的单点遥信），
    /// 以及 <see cref="IEC104TypeId.M_SP_GB_1"/>（通用单点遥信，常用于测试或扩展场景）。
    /// </para>
    /// <para>
    /// 解析逻辑严格遵循 IEC 60870-5-104 标准中关于可变结构限定词（VSQ）和序列标志（SQ）的规定。
    /// 具体行为由 <see cref="Parse"/> 方法的 <c>isSequence</c> 参数决定：
    /// 当其为 <see langword="true"/> 时，首对象包含完整 IOA，
    /// 后续对象地址按序递增；否则每个对象均携带独立 IOA。
    /// </para>
    /// <para>
    /// 每个信息对象由 3 字节 IOA + 1 字节状态与品质描述（SIQ）组成。
    /// SIQ 字节定义如下：
    /// - Bit 0 (LSB): 状态值（0 = OFF / 分闸，1 = ON / 合闸）
    /// - Bit 7: OV（Overflow/Invalid）位，0 表示有效，1 表示无效
    /// 其余位（1–6）在标准单点遥信中保留，解析时忽略。
    /// </para>
    /// </remarks>
    internal sealed class SinglePointStatusParser
    {
        /// <summary>
        /// 获取当前解析器支持的 IEC 104 类型标识符集合。
        /// </summary>
        /// <remarks>
        /// 包含所有标准及部分扩展的单点遥信类型。若未来新增支持类型，
        /// 需同步更新此集合及协议兼容性说明。
        /// </remarks>
        private static readonly HashSet<IEC104TypeId> SupportedTypeIds =
            new()
            {
                IEC104TypeId.M_SP_NA_1,
                IEC104TypeId.M_SP_TB_1,
                IEC104TypeId.M_SP_TB_7,
                IEC104TypeId.M_SP_GB_1
            };

        /// <summary>
        /// 解析单点遥信 ASDU 载荷，并返回一组标准化的状态结果。
        /// </summary>
        /// <param name="typeId">
        /// ASDU 的类型标识符。必须是 <see cref="SupportedTypeIds"/> 中的成员，
        /// 否则将抛出 <see cref="NotSupportedException"/>。
        /// </param>
        /// <param name="commonAddress">公共地址（Common Address, CA），标识数据来源站点。</param>
        /// <param name="payload">
        /// ASDU 信息体原始字节序列（不含类型标识、VSQ、CA、COT 等头部字段）。
        /// 调用方需确保其长度足以容纳指定数量的对象。
        /// 每个对象包含：3 字节信息对象地址（IOA）+ 1 字节 SIQ（状态与品质）。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳。对于带时标类型（如 <see cref="IEC104TypeId.M_SP_TB_1"/> 或 <see cref="IEC104TypeId.M_SP_TB_7"/>），
        /// 此值应来自已解析的 CP56Time2a 字段；
        /// 对于无时标类型（如 <see cref="IEC104TypeId.M_SP_NA_1"/>），通常为帧接收时间或 <see langword="null"/>。
        /// </param>
        /// <param name="numberOfObjects">
        /// 信息对象数量（来自 VSQ 的低 7 位）。必须大于 0。
        /// </param>
        /// <param name="isSequence">
        /// 序列标志（SQ 位）。若为 <see langword="true"/>，表示对象地址连续（仅首对象含完整 IOA）；
        /// 若为 <see langword="false"/>，每个对象均包含独立 IOA。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（Cause of Transmission, COT），如周期上送（3）、突发变化（11）等。</param>
        /// <returns>
        /// 一个只读列表，包含解析生成的 <see cref="StatusResult"/> 实例。
        /// 列表顺序与载荷中对象顺序一致。
        /// 每个结果的 <see cref="StatusResult.State"/> 为 0 或 1，
        /// <see cref="StatusResult.IsValid"/> 基于 SIQ 的 OV 位（bit 7）确定。
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// 当 <paramref name="typeId"/> 不在 <see cref="SupportedTypeIds"/> 中时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="payload"/> 长度不足以解析指定数量的对象时，
        /// 可能因索引越界而引发异常（由 <see cref="ReadOnlySpan{T}"/> 访问触发）。
        /// </exception>
        public IReadOnlyList<StatusResult> Parse(
            IEC104TypeId typeId,
            ushort commonAddress,
            ReadOnlySpan<byte> payload,
            DateTimeOffset? timestamp,
            int numberOfObjects,
            bool isSequence,
            ushort causeOfTransmission)
        {
            if (!SupportedTypeIds.Contains(typeId))
            {
                throw new NotSupportedException(
                    $"TypeId '{typeId}' is not supported by {nameof(SinglePointStatusParser)}.");
            }

            var results = new List<StatusResult>();
            int index = 0;
            int ioaBase = 0;

            for (int i = 0; i < numberOfObjects; i++)
            {
                int informationObjectAddress;

                if (isSequence)
                {
                    // 连续 IOA：第一个 IOA 从 payload 读 3 字节，其余递增
                    if (i == 0)
                    {
                        ioaBase =
                            payload[index]
                            | (payload[index + 1] << 8)
                            | (payload[index + 2] << 16);
                        index += 3;
                    }
                    informationObjectAddress = ioaBase + i;
                }
                else
                {
                    // 非连续 IOA，每个信息对象 3 字节
                    informationObjectAddress =
                        payload[index]
                        | (payload[index + 1] << 8)
                        | (payload[index + 2] << 16);
                    index += 3;
                }

                var siq = payload[index++];
                var state = siq & 0x01;
                var isValid = (siq & 0x80) == 0;

                results.Add(new StatusResult(
                    typeId,
                    commonAddress,
                    informationObjectAddress,
                    state,
                    isValid,
                    causeOfTransmission,
                    timestamp));
            }

            return results;
        }
    }
}
