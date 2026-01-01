using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Results;

namespace LogParsing.Protocols.IEC104.Parsing
{
    /// <summary>
    /// 提供对 IEC 60870-5-104 系统召唤命令（Interrogation Command）的解析功能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 支持的标准及扩展类型包括：
    /// <see cref="IEC104TypeId.C_IC_NA_1"/>（标准总召唤命令）和
    /// <see cref="IEC104TypeId.C_IC_TOTAL_ACTIVE_POWER"/>（工程自定义的总有功功率召唤等扩展类型）。
    /// </para>
    /// <para>
    /// 解析逻辑严格遵循 IEC 60870-5-104 标准中关于可变结构限定词（VSQ）和序列标志（SQ）的规定。
    /// 具体行为由 <see cref="Parse"/> 方法的 <c>isSequence</c> 参数决定：
    /// 当其为 <see langword="true"/> 时，首对象包含完整 IOA，
    /// 后续对象地址按序递增；否则每个对象均携带独立 IOA。
    /// </para>
    /// <para>
    /// 每个召唤命令对象包含一个字节的召唤限定词（QOI, Qualifier of Interrogation），
    /// 用于指示召唤范围或类型（如 20 表示总召唤）。
    /// 所有解析结果封装为 <see cref="InterrogationCommandResult"/>，仅表达“召唤意图”，
    /// 不包含从站响应或执行状态。
    /// </para>
    /// </remarks>
    internal sealed class InterrogationCommandParser
    {
        /// <summary>
        /// 获取当前解析器支持的 IEC 104 召唤命令类型标识符集合。
        /// </summary>
        /// <remarks>
        /// 该集合包含标准类型 <see cref="IEC104TypeId.C_IC_NA_1"/> 及工程扩展类型（如总有功功率召唤）。
        /// 若新增支持类型，必须同步更新此集合以确保协议一致性。
        /// </remarks>
        private static readonly HashSet<IEC104TypeId> SupportedTypeIds =
            new() { IEC104TypeId.C_IC_NA_1, IEC104TypeId.C_IC_TOTAL_ACTIVE_POWER };

        /// <summary>
        /// 解析召唤命令 ASDU 载荷，并返回一组标准化的召唤命令结果。
        /// </summary>
        /// <param name="typeId">
        /// ASDU 的类型标识符。必须是 <see cref="SupportedTypeIds"/> 中的成员，
        /// 否则将抛出 <see cref="NotSupportedException"/>。
        /// </param>
        /// <param name="commonAddress">公共地址（Common Address, CA），标识目标站点或设备组。</param>
        /// <param name="payload">
        /// ASDU 载荷字节序列（不含类型标识、VSQ、CA 等头部字段）。
        /// 调用方需确保其长度足以容纳指定数量的对象。
        /// 每个对象包含 3 字节信息对象地址（IOA） + 1 字节召唤限定词（QOI）。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳。由于标准召唤命令（如 C_IC_NA_1）不包含内嵌 CP56Time2a 时标，
        /// 此值通常表示帧接收时间；若未记录，则为 <see langword="null"/>。
        /// </param>
        /// <param name="numberOfObjects">
        /// 信息对象数量（来自 VSQ 的低 7 位）。必须大于 0。
        /// 注意：尽管召唤命令通常只含一个对象，协议仍允许批量形式。
        /// </param>
        /// <param name="isSequence">
        /// 序列标志（SQ 位）。若为 <see langword="true"/>，表示对象地址连续（仅首对象含完整 IOA）；
        /// 若为 <see langword="false"/>，每个对象均包含独立 IOA。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（Cause of Transmission, COT），如激活（6）或激活确认（7）。</param>
        /// <returns>
        /// 一个只读列表，包含解析生成的 <see cref="InterrogationCommandResult"/> 实例。
        /// 列表顺序与载荷中对象顺序一致。
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// 当 <paramref name="typeId"/> 不在 <see cref="SupportedTypeIds"/> 中时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="payload"/> 长度不足以解析指定数量的对象时，
        /// 可能因索引越界而引发异常（由 <see cref="ReadOnlySpan{T}"/> 访问触发）。
        /// </exception>
        public IReadOnlyList<InterrogationCommandResult> Parse(
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
                    $"TypeId '{typeId}' is not supported by {nameof(InterrogationCommandParser)}.");

            var results = new List<InterrogationCommandResult>();
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

                var qoi = payload[index++];
                results.Add(new InterrogationCommandResult(
                    typeId,
                    commonAddress,
                    ioa,
                    qoi,
                    causeOfTransmission,
                    timestamp));
            }

            return results;
        }
    }
}
