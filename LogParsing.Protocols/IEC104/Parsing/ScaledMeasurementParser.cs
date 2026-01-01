using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Results;

namespace LogParsing.Protocols.IEC104.Parsing
{
    /// <summary>
    /// 提供对 IEC 60870-5-104 标度化遥测值（Scaled Measurement Value）的解析功能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 支持的标准类型包括：
    /// <see cref="IEC104TypeId.M_ME_NB_1"/>（无时标标度化遥测）和
    /// <see cref="IEC104TypeId.M_ME_TD_1"/>（带 CP56Time2a 时标的标度化遥测）。
    /// </para>
    /// <para>
    /// 解析逻辑严格遵循 IEC 60870-5-104 标准中关于可变结构限定词（VSQ）和序列标志（SQ）的规定。
    /// 具体行为由 <see cref="Parse"/> 方法的 <c>isSequence</c> 参数决定：
    /// 当其为 <see langword="true"/> 时，首对象包含完整 IOA，
    /// 后续对象地址按序递增；否则每个对象均携带独立 IOA。
    /// </para>
    /// <para>
    /// 每个遥测对象由 3 字节 IOA + 2 字节标度化值（小端序）+ 1 字节品质描述（QDS）组成。
    /// 标度化值为 16 位有符号整数，表示已通过缩放因子转换后的工程单位近似值（如 123 表示 123 A、V 等）。
    /// 品质描述字节中的 OV（Overflow）位（bit 7）用于判断数据有效性：
    /// 若 OV = 0（即 <c>qds &amp; 0x80 == 0</c>），则视为有效。
    /// </para>
    /// </remarks>
    internal sealed class ScaledMeasurementParser
    {
        /// <summary>
        /// 获取当前解析器支持的 IEC 104 类型标识符集合。
        /// </summary>
        /// <remarks>
        /// 该集合包含标准标度化遥测类型 <see cref="IEC104TypeId.M_ME_NB_1"/> 和
        /// 带时标扩展类型 <see cref="IEC104TypeId.M_ME_TD_1"/>。
        /// 若未来扩展支持其他标度化遥测类型，需同步更新此集合及解析逻辑。
        /// </remarks>
        private static readonly HashSet<IEC104TypeId> SupportedTypeIds =
            new()
            {
                IEC104TypeId.M_ME_NB_1,
                IEC104TypeId.M_ME_TD_1
            };

        /// <summary>
        /// 解析标度化遥测 ASDU 载荷，并返回一组标准化的测量结果。
        /// </summary>
        /// <param name="typeId">
        /// ASDU 的类型标识符。必须是 <see cref="SupportedTypeIds"/> 中的成员，
        /// 否则将抛出 <see cref="NotSupportedException"/>。
        /// </param>
        /// <param name="commonAddress">公共地址（Common Address, CA），标识数据来源站点。</param>
        /// <param name="payload">
        /// ASDU 载荷字节序列（不含类型标识、VSQ、CA 等头部字段）。
        /// 调用方需确保其长度足以容纳指定数量的对象。
        /// 每个对象包含：3 字节信息对象地址（IOA）+ 2 字节标度化值（小端序）+ 1 字节品质描述（QDS）。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳。对于 <see cref="IEC104TypeId.M_ME_TD_1"/>，
        /// 此值应来自已解析的 CP56Time2a 字段；
        /// 对于 <see cref="IEC104TypeId.M_ME_NB_1"/>，通常为帧接收时间或 <see langword="null"/>。
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
        /// 一个只读列表，包含解析生成的 <see cref="MeasurementResult"/> 实例。
        /// 列表顺序与载荷中对象顺序一致。
        /// 每个结果的 <see cref="MeasurementResult.IsValid"/> 属性基于 QDS 的 OV 位（bit 7）确定。
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// 当 <paramref name="typeId"/> 不在 <see cref="SupportedTypeIds"/> 中时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="payload"/> 长度不足以解析指定数量的对象时，
        /// 可能因索引越界而引发异常（由 <see cref="ReadOnlySpan{T}"/> 访问触发）。
        /// </exception>
        public IReadOnlyList<MeasurementResult> Parse(
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
                    $"TypeId '{typeId}' is not supported by {nameof(ScaledMeasurementParser)}.");

            var results = new List<MeasurementResult>();
            int index = 0;
            int ioaBase = 0;

            for (int i = 0; i < numberOfObjects; i++)
            {
                int informationObjectAddress;

                if (isSequence)
                {
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
                    informationObjectAddress =
                        payload[index]
                        | (payload[index + 1] << 8)
                        | (payload[index + 2] << 16);
                    index += 3;
                }

                short scaledValue = (short)(payload[index] | (payload[index + 1] << 8));
                index += 2;

                var qds = payload[index++];
                var isValid = (qds & 0x80) == 0;

                results.Add(new MeasurementResult(
                    typeId,
                    commonAddress,
                    informationObjectAddress,
                    scaledValue,
                    isValid,
                    causeOfTransmission,
                    timestamp));
            }

            return results;
        }
    }
}
