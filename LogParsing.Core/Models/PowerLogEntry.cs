namespace LogParsing.Core.Models
{
    /// <summary>
    /// 表示电力系统专用的日志条目，扩展自通用日志基类 <see cref="LogEntry"/>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类在通用日志结构基础上，增加电力通信协议（如 IEC 60870-5-104、DL/T634.5104 等）
    /// 特有的控制与元数据字段，用于支持高级协议解析、帧重组、重传检测及确认机制处理。
    /// </para>
    /// <para>
    /// 主要设计目标包括：
    /// <list type="bullet">
    ///   <item><description>通过 <see cref="Channel"/> 支持多物理/逻辑通道下的日志隔离；</description></item>
    ///   <item><description>利用 <see cref="SequenceNumber"/> 实现帧顺序校验与重发识别；</description></item>
    ///   <item><description>借助 <see cref="DelayACK"/> 标识延迟确认状态，辅助 ACK/S 帧调度逻辑；</description></item>
    ///   <item><description>通过 <see cref="Action"/> 描述内部协议引擎行为，便于调试状态机；</description></item>
    ///   <item><description>使用 <see cref="ExpectedLength"/> 指导帧组装过程，确保完整性。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 所有属性均为可读写（<c>public get; set;</c>），以支持反序列化、日志增强或运行时动态填充。
    /// </para>
    /// </remarks>
    public class PowerLogEntry : LogEntry
    {
        /// <summary>
        /// 获取或设置通信通道编号。
        /// </summary>
        /// <value>
        /// 表示当前日志所属的物理或逻辑通信通道（如串口 COM1、TCP 连接 ID、设备地址等）。
        /// 在多通道并发通信场景中，用于区分不同会话流。
        /// 默认值为 <c>0</c>，但实际使用中应由解析器赋予有效通道标识。
        /// </value>
        public int Channel { get; set; }

        /// <summary>
        /// 获取或设置消息的序列号。
        /// </summary>
        /// <value>
        /// 通常对应 IEC 104 中的发送/接收序号（Send Sequence Number / Receive Sequence Number），
        /// 用于检测丢帧、乱序或重传。序列号一般按协议规范单调递增。
        /// </value>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// 获取或设置一个值，指示当前消息是否触发延迟确认（Delayed ACK）机制。
        /// </summary>
        /// <value>
        /// 若为 <see langword="true"/>，表示协议栈暂不立即发送确认帧（S-frame），
        /// 而是等待后续数据帧合并 ACK，以提升带宽效率；
        /// 若为 <see langword="false"/>，则通常需立即响应。
        /// </value>
        /// <remarks>
        /// 此字段反映协议栈内部状态，对分析通信效率和 ACK 行为至关重要。
        /// </remarks>
        public bool DelayACK { get; set; }

        /// <summary>
        /// 获取或设置当前日志条目所对应的协议引擎动作类型。
        /// </summary>
        /// <value>
        /// 描述日志生成时协议栈执行的操作，典型值包括：
        /// <list type="bullet">
        ///   <item><description><c>"ACKDelayedMessage"</c>：处理被延迟确认的消息；</description></item>
        ///   <item><description><c>"ProcessPendingACKs"</c>：批量处理待确认帧；</description></item>
        ///   <item><description><c>"SendSFrame"</c>：发送监督帧（S-frame）；</description></item>
        ///   <item><description><c>"IECDecode"</c>：正在解码 IEC 应用层数据。</description></item>
        /// </list>
        /// 若动作类型未知或未记录，则为 <see langword="null"/>。
        /// </value>
        /// <remarks>
        /// 该字段主要用于诊断协议状态机行为，不参与核心数据传输逻辑。
        /// </remarks>
        public string? Action { get; set; }

        /// <summary>
        /// 获取或设置期望的完整帧长度（以字节为单位）。
        /// </summary>
        /// <value>
        /// 通常来源于日志中声明的“预期接收长度”或协议头中的长度字段，
        /// 用于指导帧组装器判断是否已收集足够数据以构成完整帧。
        /// 若长度信息不可用或不适用，则为 <see langword="null"/>。
        /// </value>
        /// <remarks>
        /// 在流式解析中，此值与实际聚合数据长度对比，可决定是否继续等待更多日志条目。
        /// </remarks>
        public int? ExpectedLength { get; set; }
    }
}