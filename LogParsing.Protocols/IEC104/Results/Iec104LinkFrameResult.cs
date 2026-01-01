using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 链路层（APCI）控制帧（S 帧或 U 帧）的解析结果基类。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类用于封装不包含应用服务数据单元（ASDU）的链路控制帧（如确认帧 S、控制帧 U）的解析结果。
    /// 由于 S/U 帧不含 ASDU，其公共地址、信息对象地址等应用层字段在基类中被初始化为默认值（0）。
    /// </para>
    /// <para>
    /// 注意：尽管构造函数接受 <c>typeId</c> 和 <c>causeOfTransmission</c> 参数，
    /// 但基类中的 <see cref="Iec104ParsedResult.CauseOfTransmission"/> 实际被硬编码为 0，
    /// 且 <see cref="Iec104ParsedResult.TypeId"/> 虽被赋值，
    /// 但在 S/U 帧上下文中不具备标准 ASDU 类型语义。
    /// 调用方应优先通过 <see cref="FrameType"/> 判断帧类别，
    /// 并通过具体派生类获取 U 帧的功能与控制语义。
    /// </para>
    /// </remarks>
    public abstract class Iec104LinkFrameResult : Iec104ParsedResult
    {
        /// <summary>
        /// 获取当前帧的类型（S 帧、U 帧等）。
        /// </summary>
        /// <value>
        /// 一个 <see cref="Iec104FrameType"/> 枚举值，标识该结果来源于何种链路层帧。
        /// 对于本类实例，该值通常为 <see cref="Iec104FrameType.S"/> 或 <see cref="Iec104FrameType.U"/>。
        /// </value>
        public Iec104FrameType FrameType { get; }

        /// <summary>
        /// 初始化 <see cref="Iec104LinkFrameResult"/> 类的新实例。
        /// </summary>
        /// <param name="frameType">帧类型，标识是 S 帧还是 U 帧。</param>
        /// <param name="typeId">
        /// IEC104 类型标识。注意：对于 S/U 帧，此值不对应标准 ASDU Type ID，
        /// 仅作为基类初始化所需参数传入，实际语义由派生类定义。
        /// </param>
        /// <param name="causeOfTransmission">
        /// 传输原因（COT）。注意：此参数在当前实现中**未被使用**，
        /// 基类中的 <see cref="Iec104ParsedResult.CauseOfTransmission"/> 始终为 0。
        /// </param>
        /// <param name="timestamp">可选时间戳，通常为帧接收时间。</param>
        protected Iec104LinkFrameResult(
            Iec104FrameType frameType,
            IEC104TypeId typeId,
            ushort causeOfTransmission,
            DateTimeOffset? timestamp)
            : base(
                typeId,
                commonAddress: 0,
                informationObjectAddress: 0,
                causeOfTransmission: 0,
                timestamp)
        {
            FrameType = frameType;
        }
    }
}