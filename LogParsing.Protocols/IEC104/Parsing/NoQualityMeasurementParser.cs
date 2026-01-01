using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Results;

namespace LogParsing.Protocols.IEC104.Parsing
{
    /// <summary>
    /// 提供对 IEC 60870-5-104 无品质描述遥测值（Normalized Value without Quality）的解析功能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 仅支持类型标识符 <see cref="IEC104TypeId.M_ME_ND_1"/>，
    /// 该类型用于传输归一化遥测值（16 位有符号整数），且**不包含品质描述字节（Quality Descriptor）**。
    /// 因此所有解析结果均视为有效（<see cref="MeasurementResult.IsValid"/> 恒为 <see langword="true"/>）。
    /// </para>
    /// <para>
    /// 解析逻辑严格遵循 IEC 60870-5-104 标准中关于可变结构限定词（VSQ）和序列标志（SQ）的规定。
    /// 具体行为由 <see cref="Parse"/> 方法的 <c>isSequence</c> 参数决定：
    /// 当其为 <see langword="true"/> 时，首对象包含完整 IOA，
    /// 后续对象地址按序递增；否则每个对象均携带独立 IOA。
    /// </para>
    /// <para>
    /// 每个遥测对象由 3 字节 IOA + 2 字节归一化值（小端序）组成。
    /// 值域范围为 [-32768, 32767]，需由上层应用根据缩放因子转换为工程单位。
    /// </para>
    /// </remarks>
    internal sealed class NoQualityMeasurementParser
    {
        /// <summary>
        /// 获取当前解析器支持的 IEC 104 类型标识符集合。
        /// </summary>
        /// <remarks>
        /// 目前仅包含 <see cref="IEC104TypeId.M_ME_ND_1"/>。
        /// 若未来扩展支持其他无品质遥测类型，需同步更新此集合及解析逻辑。
        /// </remarks>
        private static readonly HashSet<IEC104TypeId> SupportedTypeIds =
            new() { IEC104TypeId.M_ME_ND_1 };

        /// <summary>
        /// 解析无品质遥测 ASDU 载荷，并返回一组标准化的测量结果。
        /// </summary>
        /// <param name="typeId">
        /// ASDU 的类型标识符。必须等于 <see cref="IEC104TypeId.M_ME_ND_1"/>，
        /// 否则将抛出 <see cref="NotSupportedException"/>。
        /// </param>
        /// <param name="commonAddress">公共地址（Common Address, CA），标识数据来源站点。</param>
        /// <param name="payload">
        /// ASDU 载荷字节序列（不含类型标识、VSQ、CA 等头部字段）。
        /// 调用方需确保其长度足以容纳指定数量的对象。
        /// 每个对象包含 3 字节信息对象地址（IOA） + 2 字节归一化遥测值（小端序）。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳。由于 <see cref="IEC104TypeId.M_ME_ND_1"/> 不包含内嵌时标，
        /// 此值通常表示帧接收时间；若未记录，则为 <see langword="null"/>。
        /// </param>
        /// <param name="numberOfObjects">
        /// 信息对象数量（来自 VSQ 的低 7 位）。必须大于 0。
        /// </param>
        /// <param name="isSequence">
        /// 序列标志（SQ 位）。若为 <see langword="true"/>，表示对象地址连续（仅首对象含完整 IOA）；
        /// 若为 <see langword="false"/>，每个对象均包含独立 IOA。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（Cause of Transmission, COT），如周期上送（3）或突发变化（11）等。</param>
        /// <returns>
        /// 一个只读列表，包含解析生成的 <see cref="MeasurementResult"/> 实例。
        /// 列表顺序与载荷中对象顺序一致。
        /// 所有结果的 <see cref="MeasurementResult.IsValid"/> 属性均为 <see langword="true"/>。
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// 当 <paramref name="typeId"/> 不等于 <see cref="IEC104TypeId.M_ME_ND_1"/> 时抛出。
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
                    $"TypeId '{typeId}' is not supported by {nameof(NoQualityMeasurementParser)}.");

            var results = new List<MeasurementResult>();
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

                short value = (short)(payload[index] | (payload[index + 1] << 8));
                index += 2;

                results.Add(new MeasurementResult(
                    typeId,
                    commonAddress,
                    ioa,
                    value,
                    isValid: true,
                    causeOfTransmission,
                    timestamp));
            }

            return results;
        }
    }
}