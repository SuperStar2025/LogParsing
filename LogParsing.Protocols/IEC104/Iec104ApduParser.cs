using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Results;

namespace LogParsing.Protocols.IEC104
{
    /// <summary>
    /// 提供对 IEC 60870-5-104 应用协议数据单元（APDU）的完整解析功能。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本解析器支持完整的 APDU 结构，包括：
    /// <list type="bullet">
    ///   <item><description>应用规约控制信息（APCI）：起始符 0x68、长度字段、控制域（I/S/U 帧）</description></item>
    ///   <item><description>应用服务数据单元（ASDU）：类型标识（TypeID）、可变结构限定词（VSQ）、传输原因（COT，2 字节）、公共地址（CA，2 字节）、信息对象地址（IOA，3 字节）</description></item>
    ///   <item><description>多对象 VSQ 支持（最多 127 个对象）及序列地址模式（SQ=1）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 解析结果按帧类型分类：
    /// - <see cref="Iec104FrameType.I"/>：调用内部调度器分发至具体业务解析器（如遥测、遥信、命令等）
    /// - <see cref="Iec104FrameType.S"/>：返回确认帧（S 帧）结果
    /// - <see cref="Iec104FrameType.U"/>：返回无编号控制帧（U 帧）结果（如 StartDT、StopDT、TestDT）
    /// </para>
    /// <para>
    /// 所有时间戳均以 UTC 语义处理。输入字节数组必须为完整且有效的 APDU，不含链路层（如 APCI 前导或校验和）。
    /// </para>
    /// </remarks>
    public sealed class Iec104ApduParser
    {
        /// <summary>
        /// 内部负载分发器，用于将 ASDU 载荷路由到对应的业务解析器。
        /// </summary>
        private readonly Iec104PayloadDispatcher _dispatcher;

        /// <summary>
        /// 初始化 <see cref="Iec104ApduParser"/> 的新实例。
        /// </summary>
        /// <remarks>
        /// 构造时创建内部 <see cref="Iec104PayloadDispatcher"/> 实例，
        /// 该分发器负责根据 TypeID 选择正确的解析策略。
        /// </remarks>
        public Iec104ApduParser()
        {
            _dispatcher = new Iec104PayloadDispatcher();
        }

        /// <summary>
        /// 解析完整的 IEC 104 APDU 字节数组，并返回一组标准化的解析结果。
        /// </summary>
        /// <param name="apdu">
        /// 完整的 APDU 字节数组（从 0x68 开始，包含 APCI + ASDU 或 S/U 控制帧）。
        /// 必须符合 IEC 60870-5-104 标准格式。
        /// 若为 <see langword="null"/> 或长度小于 6，则视为无效输入。
        /// </param>
        /// <param name="timestamp">
        /// 可选接收时间戳（通常为帧到达本地的时间）。此值会透传至所有生成的结果中，
        /// 用于日志追踪、性能分析或无时标 ASDU 的上下文补充。
        /// </param>
        /// <returns>
        /// 一个只读列表，包含零个或多个 <see cref="Iec104ParsedResult"/> 派生对象。
        /// 可能的结果类型包括：
        /// <list type="bullet">
        ///   <item><description><see cref="SFrameResult"/>：S 帧确认</description></item>
        ///   <item><description><see cref="UFrameResult"/>：U 帧控制命令</description></item>
        ///   <item><description>业务结果（如 <see cref="MeasurementResult"/>、<see cref="StatusResult"/> 等）：来自 ASDU 解析</description></item>
        /// </list>
        /// 若输入无效或无法识别，返回空列表（永不返回 <see langword="null"/>）。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="apdu"/> 为 <see langword="null"/> 时，由长度检查隐式处理，实际返回空列表；
        /// 因此本方法不抛出异常，符合“容错解析”设计原则。
        /// </exception>
        public IReadOnlyList<Iec104ParsedResult> Parse(byte[] apdu, DateTimeOffset? timestamp = null)
        {
            static IReadOnlyList<Iec104ParsedResult> Empty()
                => Array.Empty<Iec104ParsedResult>();

            if (apdu == null || apdu.Length < 6)
                return Empty();

            int index = 0;

            // -------------------
            // APCI
            // -------------------
            if (apdu[index++] != 0x68)
                return Empty();

            byte length = apdu[index++];
            if (length != apdu.Length - 2)
                return Empty();

            // Control Field
            byte c0 = apdu[index++];
            byte c1 = apdu[index++];
            byte c2 = apdu[index++];
            byte c3 = apdu[index++];

            Iec104FrameType isFrame = ParseFrameType(c0);

            // -------------------
            // S 帧
            // -------------------
            if (isFrame == Iec104FrameType.S)
            {
                ushort receiveSeq = (ushort)((c2 | (c3 << 8)) >> 1);

                return new Iec104ParsedResult[]
                {
                    new SFrameResult(receiveSeq, timestamp)
                };
            }

            // -------------------
            // U 帧
            // -------------------
            if (isFrame == Iec104FrameType.U)
            {
                IEC104TypeId typeId;
                Iec104UFrameAction action;

                switch (c0)
                {
                    // StartDT
                    case 0x07:
                        typeId = IEC104TypeId.StartDT;
                        action = Iec104UFrameAction.Activate;
                        break;
                    case 0x0B:
                        typeId = IEC104TypeId.StartDT;
                        action = Iec104UFrameAction.Confirm;
                        break;

                    // StopDT
                    case 0x13:
                        typeId = IEC104TypeId.StopDT;
                        action = Iec104UFrameAction.Activate;
                        break;
                    case 0x23:
                        typeId = IEC104TypeId.StopDT;
                        action = Iec104UFrameAction.Confirm;
                        break;

                    // TestDT
                    case 0x43:
                        typeId = IEC104TypeId.TestDT;
                        action = Iec104UFrameAction.Activate;
                        break;
                    case 0x83:
                        typeId = IEC104TypeId.TestDT;
                        action = Iec104UFrameAction.Confirm;
                        break;

                    default:
                        return Empty();
                }

                return new Iec104ParsedResult[]
                {
                    new UFrameResult(typeId, action, timestamp)
                };
            }

            // -------------------
            // ASDU（仅 I 帧）
            // -------------------
            if (isFrame == Iec104FrameType.I)
            {
                if (index >= apdu.Length)
                    return Empty();

                byte typeIdByte = apdu[index++];
                if (!Enum.IsDefined(typeof(IEC104TypeId), typeIdByte))
                    return Empty();

                var typeId = (IEC104TypeId)typeIdByte;

                byte vsq = apdu[index++];
                bool isSequence = (vsq & 0x80) != 0;
                int numberOfObjects = vsq & 0x7F;

                // COT = 2 bytes
                if (index + 2 > apdu.Length)
                    return Empty();

                ushort causeOfTransmission = BitConverter.ToUInt16(apdu, index);
                index += 2;

                // CA = 2 bytes
                if (index + 2 > apdu.Length)
                    return Empty();

                ushort commonAddress = BitConverter.ToUInt16(apdu, index);
                index += 2;

                // Payload
                if (index > apdu.Length)
                    return Empty();

                var payload = new ReadOnlySpan<byte>(apdu, index, apdu.Length - index);

                return _dispatcher.Dispatch(
                    typeId,
                    commonAddress,
                    payload,
                    timestamp,
                    numberOfObjects,
                    isSequence,
                    causeOfTransmission);
            }
            return Empty();
        }

        /// <summary>
        /// 根据控制域首字节（C0）的低两位判断帧类型。
        /// </summary>
        /// <param name="c0">APCI 控制域的第一个字节。</param>
        /// <returns>
        /// 对应的 <see cref="Iec104FrameType"/> 枚举值：
        /// <list type="table">
        ///   <listheader><term>低 2 位</term><description>帧类型</description></listheader>
        ///   <item><term>00 / 10</term><description>I 帧（信息传输）</description></item>
        ///   <item><term>01</term><description>S 帧（确认）</description></item>
        ///   <item><term>11</term><description>U 帧（无编号控制）</description></item>
        /// </list>
        /// 若无法识别，返回 <see cref="Iec104FrameType.Invalid"/>。
        /// </returns>
        /// <remarks>
        /// 依据 IEC 60870-5-104 标准第 5.3 节定义：
        /// - I 帧：bit 0 = 0（无论 bit 1）
        /// - S 帧：bit 0 = 1, bit 1 = 0
        /// - U 帧：bit 0 = 1, bit 1 = 1
        /// </remarks>
        private static Iec104FrameType ParseFrameType(byte c0)
        {
            switch (c0 & 0x03)
            {
                case 0x02 or 0x00:
                    return Iec104FrameType.I;

                case 0x01:
                    return Iec104FrameType.S;

                case 0x03:
                    return Iec104FrameType.U;

                default:
                    // 兜底
                    return Iec104FrameType.Invalid;
            }
        }
    }
}