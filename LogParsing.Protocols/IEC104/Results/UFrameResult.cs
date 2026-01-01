using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议中 U 帧（未编号控制帧）的解析结果。
    /// </summary>
    /// <remarks>
    /// <para>
    /// U 帧用于链路层控制（如启动数据传输、停止数据传输、链路测试），不包含应用服务数据单元（ASDU）。
    /// 本类通过 <see cref="Action"/> 属性提供 U 帧的操作语义（激活或确认），并通过只读属性
    /// <see cref="IsStartDT"/>, <see cref="IsStopDT"/>, <see cref="IsTestDT"/> 提供对具体 U 帧类型的便捷判断。
    /// </para>
    /// <para>
    /// 注意：尽管基类 <see cref="Iec104ParsedResult.TypeId"/> 被用于区分 U 帧类型，
    /// 但其在协议中并非标准 Type ID（U 帧无 ASDU），此处仅为内部标识用途。
    /// 实际控制意图应优先通过 <see cref="Action"/> 判断。
    /// </para>
    /// </remarks>
    public sealed class UFrameResult : Iec104LinkFrameResult
    {
        /// <summary>
        /// 获取 U 帧的操作语义（激活请求或确认应答）。
        /// </summary>
        /// <value>
        /// 一个 <see cref="Iec104UFrameAction"/> 枚举值，表示该 U 帧是由主站发起的控制请求，
        /// 还是由从站返回的确认响应。
        /// </value>
        public Iec104UFrameAction Action { get; }

        /// <summary>
        /// 获取一个值，指示该 U 帧是否为 StartDT（启动数据传输）帧。
        /// </summary>
        /// <value>
        /// 如果 <see cref="Iec104ParsedResult.TypeId"/> 等于 <see cref="IEC104TypeId.StartDT"/>，则为 <see langword="true"/>；否则为 <see langword="false"/>。
        /// </value>
        public bool IsStartDT => TypeId == IEC104TypeId.StartDT;

        /// <summary>
        /// 获取一个值，指示该 U 帧是否为 StopDT（停止数据传输）帧。
        /// </summary>
        /// <value>
        /// 如果 <see cref="Iec104ParsedResult.TypeId"/> 等于 <see cref="IEC104TypeId.StopDT"/>，则为 <see langword="true"/>；否则为 <see langword="false"/>。
        /// </value>
        public bool IsStopDT => TypeId == IEC104TypeId.StopDT;

        /// <summary>
        /// 获取一个值，指示该 U 帧是否为 TestDT（测试数据传输）帧。
        /// </summary>
        /// <value>
        /// 如果 <see cref="Iec104ParsedResult.TypeId"/> 等于 <see cref="IEC104TypeId.TestDT"/>，则为 <see langword="true"/>；否则为 <see langword="false"/>。
        /// </value>
        public bool IsTestDT => TypeId == IEC104TypeId.TestDT;

        /// <summary>
        /// 初始化 <see cref="UFrameResult"/> 类的新实例。
        /// </summary>
        /// <param name="typeId">
        /// 用于标识 U 帧类型的伪 Type ID（如 <see cref="IEC104TypeId.StartDT"/>）。
        /// 此值将传递给基类以支持 <see cref="IsStartDT"/> 等属性的判断，但不代表标准 ASDU 类型。
        /// </param>
        /// <param name="action">
        /// U 帧的操作语义，指示是主站发起的激活请求还是从站返回的确认应答。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳。由于 U 帧不携带协议内嵌时标，此值通常表示帧接收时间；
        /// 若未记录，则为 <see langword="null"/>。
        /// </param>
        public UFrameResult(
            IEC104TypeId typeId,
            Iec104UFrameAction action,
            DateTimeOffset? timestamp
            )
            : base(Iec104FrameType.U, typeId, 0, timestamp)
        {
            Action = action;
        }
    }

    /// <summary>
    /// 定义 IEC 60870-5-104 协议中 U 帧（未编号控制帧）的操作语义。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该枚举用于区分 U 帧的通信方向和意图：
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description><see cref="Activate"/>：由控制站（主站）发送，用于发起链路操作（如 StartDT、TestDT）。</description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="Confirm"/>：由被控站（从站）发送，用于确认已收到并处理了对应的激活帧。</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public enum Iec104UFrameAction
    {
        /// <summary>
        /// 表示该 U 帧是由主站发送的激活请求（例如 StartDT、TestDT）。
        /// </summary>
        Activate,

        /// <summary>
        /// 表示该 U 帧是由从站发送的确认应答（例如对 StartDT 的确认）。
        /// </summary>
        Confirm
    }
}