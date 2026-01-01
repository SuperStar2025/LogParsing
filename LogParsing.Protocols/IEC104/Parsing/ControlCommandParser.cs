using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Results;

namespace LogParsing.Protocols.IEC104.Parsing
{
    /// <summary>
    /// 提供对 IEC 60870-5-104 遥控命令（单点/双点控制）的解析功能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 支持的标准类型包括：
    /// <see cref="IEC104TypeId.C_SC_NA_1"/>（单点遥控）、
    /// <see cref="IEC104TypeId.C_DC_NA_1"/>（双点遥控）、
    /// <see cref="IEC104TypeId.C_SC_TB_1"/>（带时标的单点遥控）和
    /// <see cref="IEC104TypeId.C_DC_TB_1"/>（带时标的双点遥控）。
    /// </para>
    /// <para>
    /// 解析逻辑严格遵循 IEC 60870-5-104 标准中关于可变结构限定词（VSQ）和序列标志（SQ）的规定。
    /// 具体行为由 <see cref="Parse"/> 方法的 <c>isSequence</c> 参数决定：
    /// 当其为 <see langword="true"/> 时，首对象包含完整 IOA，
    /// 后续对象地址按序递增；否则每个对象均携带独立 IOA。
    /// </para>
    /// </remarks>
    internal sealed class ControlCommandParser
    {
        /// <summary>
        /// 获取当前解析器支持的 IEC 104 类型标识符集合。
        /// </summary>
        /// <remarks>
        /// 该集合包含所有可被本解析器处理的遥控命令 Type ID。
        /// 修改此集合需同步更新解析逻辑以确保协议一致性。
        /// </remarks>
        private static readonly HashSet<IEC104TypeId> SupportedTypeIds =
            new()
            {
                IEC104TypeId.C_SC_NA_1,
                IEC104TypeId.C_DC_NA_1,
                IEC104TypeId.C_SC_TB_1,
                IEC104TypeId.C_DC_TB_1
            };

        /// <summary>
        /// 解析遥控命令 ASDU 载荷，并返回一组标准化的控制命令结果。
        /// </summary>
        /// <param name="typeId">
        /// ASDU 的类型标识符。必须是 <see cref="SupportedTypeIds"/> 中的成员，
        /// 否则将抛出 <see cref="NotSupportedException"/>。
        /// </param>
        /// <param name="commonAddress">公共地址（Common Address, CA），标识目标站点。</param>
        /// <param name="payload">
        /// ASDU 载荷字节序列（不含类型标识、VSQ、CA 等头部字段）。
        /// 调用方需确保其长度足以容纳指定数量的对象。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳。对于带时标类型（如 C_SC_TB_1），此值应来自 CP56Time2a 字段；
        /// 对于无时标类型，通常为帧接收时间或 <see langword="null"/>。
        /// </param>
        /// <param name="numberOfObjects">
        /// 信息对象数量（来自 VSQ 的低 7 位）。必须大于 0。
        /// </param>
        /// <param name="isSequence">
        /// 序列标志（SQ 位）。若为 <see langword="true"/>，表示对象地址连续（仅首对象含完整 IOA）；
        /// 若为 <see langword="false"/>，每个对象均包含独立 IOA。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（Cause of Transmission, COT）。</param>
        /// <returns>
        /// 一个只读列表，包含解析生成的 <see cref="ControlCommandResult"/> 实例。
        /// 列表顺序与载荷中对象顺序一致。
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// 当 <paramref name="typeId"/> 不在支持列表中时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="payload"/> 长度不足以解析指定数量的对象时可能抛出（由底层索引越界触发）。
        /// </exception>
        public IReadOnlyList<ControlCommandResult> Parse(
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
                    $"TypeId '{typeId}' is not supported by {nameof(ControlCommandParser)}.");

            var results = new List<ControlCommandResult>();
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

                var controlByte = payload[index++];
                var isSelect = (controlByte & 0x80) != 0;
                int commandValue = typeId == IEC104TypeId.C_SC_NA_1
                                   || typeId == IEC104TypeId.C_SC_TB_1
                    ? controlByte & 0x01
                    : controlByte & 0x03;

                results.Add(new ControlCommandResult(
                    typeId,
                    commonAddress,
                    ioa,
                    commandValue,
                    isSelect,
                    causeOfTransmission,
                    timestamp));
            }

            return results;
        }
    }
}