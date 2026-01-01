using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议解析后具备工程语义的数据结果的抽象基类。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类型封装了已完成协议解析的业务数据，**不包含任何底层协议字段**（如 VSQ、QDS、SCO 等），
    /// 仅暴露对上层应用有意义的属性（如时间戳、地址、传输原因等）。
    /// </para>
    /// <para>
    /// 派生类应代表具体业务语义，例如遥信变位、遥测更新、遥控确认等。
    /// </para>
    /// </remarks>
    public abstract class Iec104ParsedResult
    {
        /// <summary>
        /// 获取此结果对应的 IEC 60870-5-104 应用服务数据单元（ASDU）类型标识符。
        /// </summary>
        /// <value>
        /// 一个 <see cref="IEC104TypeId"/> 枚举值，标识数据来源的 ASDU 类型（如遥测、遥信、总召等）。
        /// </value>
        public IEC104TypeId TypeId { get; }

        /// <summary>
        /// 获取公共地址（Common Address of ASDU），用于标识站地址或设备组。
        /// </summary>
        /// <value>一个 16 位无符号整数，范围通常为 1–65535。</value>
        public ushort CommonAddress { get; }

        /// <summary>
        /// 获取信息对象地址（Information Object Address），用于唯一标识一个监测或控制点。
        /// </summary>
        /// <value>一个 24 位有符号整数（实际使用中通常为正），范围一般为 1–16777215。</value>
        public int InformationObjectAddress { get; }

        /// <summary>
        /// 获取结果对应的时间戳。
        /// </summary>
        /// <value>
        /// 如果源 ASDU 包含有效时标（如 CP56Time2a），则返回解析后的 <see cref="DateTimeOffset"/>；
        /// 否则返回 <see langword="null"/>。
        /// </value>
        public DateTimeOffset? Timestamp { get; }

        /// <summary>
        /// 获取传输原因（Cause of Transmission, COT）。
        /// </summary>
        /// <value>
        /// 表示该 ASDU 的触发上下文，例如：
        /// <list type="bullet">
        ///   <item><description>3：自发上传</description></item>
        ///   <item><description>6：激活确认</description></item>
        ///   <item><description>20：总召唤响应</description></item>
        /// </list>
        /// 具体含义需结合 IEC 60870-5-104 标准及系统配置解释。
        /// </value>
        public ushort CauseOfTransmission { get; }

        /// <summary>
        /// 初始化 <see cref="Iec104ParsedResult"/> 类的新实例。
        /// </summary>
        /// <param name="typeId">IEC 60870-5-104 ASDU 类型标识符。</param>
        /// <param name="commonAddress">公共地址（站地址）。</param>
        /// <param name="informationObjectAddress">信息对象地址（点号）。</param>
        /// <param name="causeOfTransmission">传输原因（COT）。</param>
        /// <param name="timestamp">可选的时间戳；若 ASDU 未携带时标，则传入 <see langword="null"/>。</param>
        protected Iec104ParsedResult(
            IEC104TypeId typeId,
            ushort commonAddress,
            int informationObjectAddress,
            ushort causeOfTransmission,
            DateTimeOffset? timestamp)
        {
            TypeId = typeId;
            CommonAddress = commonAddress;
            InformationObjectAddress = informationObjectAddress;
            CauseOfTransmission = causeOfTransmission;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// 表示 IEC 60870-5-104 帧类型（Frame Format）。
    /// </summary>
    /// <remarks>
    /// IEC 104 使用三种帧格式进行通信：
    /// <list type="table">
    ///   <listheader>
    ///     <term>帧类型</term>
    ///     <description>用途</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="I"/></term>
    ///     <description>信息帧（I-frame）：携带应用数据（ASDU）。</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="S"/></term>
    ///     <description>监视帧（S-frame）：用于确认接收到的 I 帧（仅含序列号）。</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="U"/></term>
    ///     <description>未编号控制帧（U-frame）：用于链路控制（如启动、停止、测试）。</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Invalid"/></term>
    ///     <description>无法识别或格式错误的帧。</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public enum Iec104FrameType
    {
        /// <summary>
        /// 信息帧（I-frame）——包含应用层数据（ASDU）。
        /// </summary>
        I,

        /// <summary>
        /// 监视帧（S-frame）——用于接收确认（Acknowledgement）。
        /// </summary>
        S,

        /// <summary>
        /// 未编号控制帧（U-frame）——用于链路管理（如 StartDT、TestDT）。
        /// </summary>
        U,

        /// <summary>
        /// 无效帧——解析失败或不符合 IEC 104 帧结构。
        /// </summary>
        Invalid
    }
}
