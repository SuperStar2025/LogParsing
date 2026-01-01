using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Results;
using System;

namespace LogParsing.Protocols.IEC104.Parsing
{
    /// <summary>
    /// 提供 IEC 60870-5-104 成组单点遥信（Grouped Single Point Information, M_SP_GB_1）的解析功能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该解析器专用于解析 ASDU 类型标识符 <see cref="IEC104TypeId.M_SP_GB_1"/>。
    /// 每个字节包含 8 个遥信点，每比特表示一个点的开关状态（0 或 1）。
    /// </para>
    /// <para>
    /// 解析逻辑严格遵循 VSQ（可变结构限定词）中的对象数量和 SQ（序列标志）：
    /// <list type="bullet">
    ///   <item>
    ///     <c>isSequence = true</c>：仅载荷开头包含第一个信息对象地址（IOA），后续点地址按顺序递增。
    ///   </item>
    ///   <item>
    ///     <c>isSequence = false</c>：每个状态字节前均有独立 3 字节 IOA，表示该字节对应的起始点地址。
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// 解析结果统一为 <see cref="StatusResult"/>：
    /// 每个比特展开为独立的遥信点，并将 <see cref="StatusResult.IsValid"/> 强制设置为 <see langword="true"/>。
    /// </para>
    /// </remarks>
    internal sealed class GroupSinglePointStatusParser
    {
        /// <summary>
        /// 当前解析器支持的 IEC 104 类型标识符集合。
        /// </summary>
        /// <remarks>
        /// 目前仅包含 <see cref="IEC104TypeId.M_SP_GB_1"/>。
        /// 将来若支持更多类型，需要同步更新该集合及解析逻辑。
        /// </remarks>
        private static readonly HashSet<IEC104TypeId> SupportedTypeIds =
            new() { IEC104TypeId.M_SP_GB_1 };

        /// <summary>
        /// 解析 M_SP_GB_1 ASDU 载荷，返回展开后的遥信点列表。
        /// </summary>
        /// <param name="typeId">ASDU 类型标识符，必须为 <see cref="IEC104TypeId.M_SP_GB_1"/>。</param>
        /// <param name="commonAddress">公共地址（CA），标识数据来源站点。</param>
        /// <param name="payload">
        /// ASDU 载荷字节序列（不含类型标识、VSQ、CA 等头部字段）。
        /// 调用方必须保证长度足够：至少包含一个 3 字节 IOA 和若干状态字节。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳，通常表示帧接收时间。M_SP_GB_1 不包含内嵌时标。
        /// </param>
        /// <param name="numberOfObjects">
        /// 遥信点总数（VSQ 低 7 位）。必须大于 0。
        /// 表示点数量，而非字节数。
        /// </param>
        /// <param name="isSequence">
        /// 序列标志（SQ 位）：
        /// <see langword="true"/> 表示所有点地址连续（仅首字节前有 IOA），
        /// <see langword="false"/> 表示每个状态字节前均有独立 IOA。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（COT）。</param>
        /// <returns>
        /// 返回只读列表，每个 <see cref="StatusResult"/> 对应一个遥信点，
        /// 按地址升序排列。
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// 当 <paramref name="typeId"/> 不等于 <see cref="IEC104TypeId.M_SP_GB_1"/> 时抛出。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="payload"/> 长度不足或 <paramref name="numberOfObjects"/> 与载荷内容不匹配时抛出。
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
                throw new NotSupportedException(
                    $"TypeId '{typeId}' is not supported by {nameof(GroupSinglePointStatusParser)}.");

            if (payload.Length < 3)
                throw new ArgumentException("Payload too short to contain initial IOA.");

            var results = new List<StatusResult>();
            int index = 0;
            int pointsParsed = 0;

            if (isSequence)
            {
                // -----------------------
                // 序列模式
                // -----------------------
                int ioaBase = payload[index] | (payload[index + 1] << 8) | (payload[index + 2] << 16);
                index += 3;

                while (pointsParsed < numberOfObjects)
                {
                    if (index >= payload.Length)
                        throw new ArgumentException("Payload too short for sequence state byte.");

                    byte valueByte = payload[index++];
                    int pointsInByte = Math.Min(8, numberOfObjects - pointsParsed);

                    for (int bit = 0; bit < pointsInByte; bit++)
                    {
                        results.Add(new StatusResult(
                            typeId,
                            commonAddress,
                            ioaBase + pointsParsed,
                            (valueByte >> bit) & 0x01,
                            isValid: true,
                            causeOfTransmission,
                            timestamp));
                        pointsParsed++;
                    }
                }
            }
            else
            {
                // -----------------------
                // 非序列模式
                // -----------------------
                while (pointsParsed < numberOfObjects)
                {
                    // 每组必须至少 4 字节: 3 字节 IOA + 1 字节状态
                    if (index + 3 >= payload.Length)
                        throw new ArgumentException("Payload too short for non-sequence IOA + state byte.");

                    int ioa = payload[index] | (payload[index + 1] << 8) | (payload[index + 2] << 16);
                    index += 3;

                    if (index >= payload.Length)
                        throw new ArgumentException("Payload too short for non-sequence state byte.");

                    byte valueByte = payload[index++];
                    int pointsInByte = Math.Min(8, numberOfObjects - pointsParsed);

                    for (int bit = 0; bit < pointsInByte; bit++)
                    {
                        results.Add(new StatusResult(
                            typeId,
                            commonAddress,
                            ioa + bit,
                            (valueByte >> bit) & 0x01,
                            isValid: true,
                            causeOfTransmission,
                            timestamp));
                        pointsParsed++;
                    }
                }
            }

            return results;
        }
    }
}