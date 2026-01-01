using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Parsing;
using LogParsing.Protocols.IEC104.Results;

namespace LogParsing.Protocols.IEC104
{
    /// <summary>
    /// 提供 IEC 60870-5-104 应用服务数据单元（ASDU）信息体载荷（Payload）的解析调度功能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本调度器根据 ASDU 中的类型标识符（TypeID）自动选择对应的专用解析器，
    /// 将原始字节载荷转换为强类型的业务对象集合（如遥信、遥测、命令等）。
    /// </para>
    /// <para>
    /// 支持标准 IEC 104 信息对象模型，包括：
    /// <list type="bullet">
    ///   <item><description>单点/双点状态（带或不带时标）</description></item>
    ///   <item><description>归一化值、标度化值、短浮点数遥测（带品质描述）</description></item>
    ///   <item><description>无品质遥测（M_ME_ND_1）</description></item>
    ///   <item><description>单点/双点遥控命令、总召命令、对时命令等</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 所有解析器均支持可变结构限定词（VSQ）定义的多对象模式及序列地址（SQ=1）。
    /// 调度过程不修改输入参数，且线程安全（因内部解析器为无状态瞬态实例）。
    /// </para>
    /// </remarks>
    public sealed class Iec104PayloadDispatcher
    {
        /// <summary>
        /// 根据指定的类型标识符（TypeID）调度对应的解析器，将 ASDU 信息体载荷解析为业务结果集合。
        /// </summary>
        /// <param name="typeId">
        /// IEC 60870-5-104 类型标识符（TypeID），决定信息对象的语义和编码格式。
        /// 必须是当前调度器支持的枚举值之一；否则抛出 <see cref="NotSupportedException"/>。
        /// </param>
        /// <param name="commonAddress">
        /// 公共地址（Common Address, CA），通常标识子站或设备组。
        /// 该值会透传至所有生成的解析结果中。
        /// </param>
        /// <param name="payload">
        /// ASDU 信息体部分的原始字节序列（不含 TypeID、VSQ、COT、CA 等头部字段）。
        /// 调用方需确保其长度与 <paramref name="numberOfObjects"/> 及具体 TypeID 的编码规则一致。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳（通常来自主站接收帧的时间）。若 ASDU 本身不包含时标（如 M_SP_NA_1），
        /// 此值可用于提供上下文时间；若 ASDU 包含 CP56Time2a 时标，则优先使用内嵌时间。
        /// 该值会作为元数据附加到每个结果对象中。
        /// </param>
        /// <param name="numberOfObjects">
        /// 信息对象数量，取自 VSQ 的低 7 位。默认值为 1。
        /// 必须大于 0 且与 <paramref name="payload"/> 长度匹配。
        /// </param>
        /// <param name="isSequence">
        /// 序列标志（SQ 位），取自 VSQ 的最高位。若为 <see langword="true"/>，
        /// 表示仅第一个对象包含完整信息对象地址（IOA），后续地址按序递增。
        /// 默认值为 <see langword="false"/>（每个对象独立 IOA）。
        /// </param>
        /// <param name="causeOfTransmission">
        /// 传输原因（Cause of Transmission, COT），通常为 2 字节（此处以 <see cref="ushort"/> 表示）。
        /// 常见值包括：周期上送（3）、突发（4）、总召（20）、激活（6）、激活确认（7）等。
        /// 该值会透传至解析结果中，用于业务逻辑判断。
        /// </param>
        /// <returns>
        /// 一个只读列表，包含零个或多个 <see cref="Iec104ParsedResult"/> 派生对象。
        /// 具体类型取决于 <paramref name="typeId"/>，例如：
        /// <list type="bullet">
        ///   <item><description><see cref="SinglePointStatusParser"/>（M_SP_NA_1 等）</description></item>
        ///   <item><description><see cref="ScaledMeasurementParser"/>（M_ME_NB_1 等）</description></item>
        ///   <item><description><see cref="ControlCommandResult"/>（C_SC_NA_1 等）</description></item>
        ///   <item><description><see cref="InterrogationCommandResult"/>（C_IC_NA_1）</description></item>
        ///   <item><description><see cref="TimeSyncCommandResult"/>（C_RTC_SYNC）</description></item>
        /// </list>
        /// 列表顺序与载荷中对象顺序一致。
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// 当 <paramref name="typeId"/> 不在当前支持的 TypeID 列表中时抛出。
        /// 异常消息包含未识别的 TypeID 值，便于调试。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 若 <paramref name="payload"/> 长度不足以解析指定数量的对象，
        /// 由底层解析器在访问 <see cref="ReadOnlySpan{T}"/> 时可能引发索引越界异常。
        /// （此异常非本方法直接抛出，但属于契约的一部分）
        /// </exception>
        /// <remarks>
        /// <para>
        /// 本方法采用“策略模式”实现：每个 TypeID 组映射到一个专用解析器实例。
        /// 解析器在每次调用时临时创建（无状态），避免线程安全问题。
        /// </para>
        /// <para>
        /// 支持的 TypeID 分组说明：
        /// <list type="table">
        ///   <listheader><term>TypeID 范围</term><description>解析器</description></listheader>
        ///   <item><term>M_SP_* / M_DP_*</term><description>状态类（单点/双点）</description></item>
        ///   <item><term>M_ME_NA/NB/NC/ND</term><description>遥测类（归一化/标度化/浮点/无品质）</description></item>
        ///   <item><term>C_SC / C_DC / C_IC / C_RTC</term><description>命令类（遥控/总召/对时）</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        public IReadOnlyList<Iec104ParsedResult> Dispatch(
            IEC104TypeId typeId,
            ushort commonAddress,
            ReadOnlySpan<byte> payload,
            DateTimeOffset? timestamp = null,
            int numberOfObjects = 1,
            bool isSequence = false,
            ushort causeOfTransmission = 0)
        {
            switch (typeId)
            {
                // -------------------
                // 遥信
                // -------------------
                case IEC104TypeId.M_SP_NA_1:
                case IEC104TypeId.M_SP_TB_1:
                case IEC104TypeId.M_SP_TB_7:
                    return new SinglePointStatusParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                case IEC104TypeId.M_DP_NA_1:
                case IEC104TypeId.M_DP_TB_1:
                case IEC104TypeId.M_DP_TB_7:
                    return new DoublePointStatusParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                case IEC104TypeId.M_SP_GB_1:
                    return new GroupSinglePointStatusParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                // -------------------
                // 遥测
                // -------------------
                case IEC104TypeId.M_ME_NB_1:
                case IEC104TypeId.M_ME_TD_1:
                    return new ScaledMeasurementParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                case IEC104TypeId.M_ME_NC_1:
                case IEC104TypeId.M_ME_TF_1:
                    return new ShortFloatMeasurementParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                case IEC104TypeId.M_ME_NA_1:
                case IEC104TypeId.M_ME_TB_1:
                    return new NormalizedMeasurementParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                case IEC104TypeId.M_ME_ND_1:
                    return new NoQualityMeasurementParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                // -------------------
                // 遥控 / 命令
                // -------------------
                case IEC104TypeId.C_SC_NA_1:
                case IEC104TypeId.C_DC_NA_1:
                case IEC104TypeId.C_SC_TB_1:
                case IEC104TypeId.C_DC_TB_1:
                case IEC104TypeId.C_DC_ADJUST:
                    return new ControlCommandParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                case IEC104TypeId.C_IC_NA_1:
                case IEC104TypeId.C_IC_TOTAL_ACTIVE_POWER:
                    return new InterrogationCommandParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                case IEC104TypeId.C_RTC_SYNC:
                    return new TimeSyncCommandParser().Parse(typeId, commonAddress, payload, timestamp, numberOfObjects, isSequence, causeOfTransmission);

                default:
                    throw new NotSupportedException($"TypeId '{typeId}' is not supported by IEC104 dispatcher.");
            }
        }
    }
}