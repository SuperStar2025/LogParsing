using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议中 S 帧（监视帧）的解析结果。
    /// </summary>
    /// <remarks>
    /// <para>
    /// S 帧（Supervisory Frame）用于链路层流量控制，仅包含接收序号 N(R)，不携带应用服务数据单元（ASDU）。
    /// 其作用是向对端确认已成功接收至指定序号的所有 I 帧。
    /// </para>
    /// <para>
    /// 由于 S 帧无 ASDU，基类中的 <see cref="Iec104ParsedResult.TypeId"/>、
    /// <see cref="Iec104ParsedResult.CauseOfTransmission"/> 等字段无实际协议意义，
    /// 仅通过占位值（如 <see cref="IEC104TypeId.TestDT"/>）满足基类构造要求。
    /// 实际有效信息仅为 <see cref="ReceiveSequenceNumber"/>。
    /// </para>
    /// </remarks>
    public sealed class SFrameResult : Iec104LinkFrameResult
    {
        /// <summary>
        /// 获取接收序号 N(R)（Next Expected Receive Sequence Number）。
        /// </summary>
        /// <value>
        /// 表示本端期望接收的下一个 I 帧的发送序号（N(S)）。
        /// 该值用于确认所有小于 N(R) 的 I 帧已被成功接收。
        /// 序号范围为 0 到 32767（15 位），按模 32768 循环。
        /// </value>
        public ushort ReceiveSequenceNumber { get; }

        /// <summary>
        /// 初始化 <see cref="SFrameResult"/> 类的新实例。
        /// </summary>
        /// <param name="receiveSequenceNumber">
        /// 接收序号 N(R)，表示已确认接收到的所有 I 帧的下一个期望序号。
        /// 有效取值范围为偶数（因 IEC 104 序号最低位恒为 0），但本属性保留原始 15 位值（左移前）。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳。S 帧不包含协议内嵌时标，此值通常表示帧接收时间；
        /// 若未记录，则为 <see langword="null"/>。
        /// </param>
        public SFrameResult(
            ushort receiveSequenceNumber,
            DateTimeOffset? timestamp
            )
            : base(
                Iec104FrameType.S,
                IEC104TypeId.TestDT, // 占位，不用于 ASDU
                causeOfTransmission: 0,
                timestamp)
        {
            ReceiveSequenceNumber = receiveSequenceNumber;
        }
    }
}
