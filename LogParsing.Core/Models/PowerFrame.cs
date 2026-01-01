namespace LogParsing.Core.Models
{
    /// <summary>
    /// 表示从电力系统日志中解析出的一个完整通信帧。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类封装了电力协议（如 DL/T645、IEC 61850、私有规约等）通信帧的核心元数据与有效载荷，
    /// 用于支持协议解析、完整性校验、性能分析及原始日志回溯。
    /// </para>
    /// <para>
    /// 关键设计特性包括：
    /// <list type="bullet">
    ///   <item><description><see cref="Direction"/> 明确通信方向，区分主站发送与从站接收；</description></item>
    ///   <item><description><see cref="Timestamp"/> 提供帧起始时间，用于时序排序与延迟计算；</description></item>
    ///   <item><description><see cref="ExpectedLength"/> 取自日志声明的预期长度，作为完整性判断依据；</description></item>
    ///   <item><description><see cref="Data"/> 存储聚合后的完整字节序列，可直接交付业务层解析；</description></item>
    ///   <item><description><see cref="IsComplete"/> 提供快速完整性检查，避免对截断帧进行无效处理；</description></item>
    ///   <item><description><see cref="ActualLength"/> 暴露实际长度，便于统计、调试或异常诊断；</description></item>
    ///   <item><description><see cref="StartEntry"/> 保留对原始日志条目的引用，实现端到端可追溯性。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 本类为密封类（<see langword="sealed"/>），不可继承，确保模型语义封闭且性能优化。
    /// 所有必需成员通过 <c>required</c> 修饰，强制在对象初始化时提供有效值。
    /// </para>
    /// </remarks>
    public sealed class PowerFrame
    {
        /// <summary>
        /// 获取帧的通信方向。
        /// </summary>
        /// <value>
        /// 必须为非空字符串，典型值包括：
        /// <list type="bullet">
        ///   <item><description><c>"Sending"</c>：表示本端主动发出的帧；</description></item>
        ///   <item><description><c>"Received"</c>：表示从对端接收到的帧。</description></item>
        /// </list>
        /// 该字段由日志解析器根据上下文填充，不可为 <see langword="null"/> 或空。
        /// </value>
        /// <remarks>
        /// 使用字符串而非枚举以兼容不同日志源的自由格式，但系统内部应遵循统一约定。
        /// </remarks>
        public required string Direction { get; init; }

        /// <summary>
        /// 获取帧的起始时间戳。
        /// </summary>
        /// <value>
        /// 通常取自构成该帧的第一个日志条目的时间，
        /// 用于帧排序、会话重建或端到端延迟分析。
        /// 该值在对象创建时必须提供。
        /// </value>
        public required DateTimeOffset Timestamp { get; init; }

        /// <summary>
        /// 获取日志中声明的期望帧长度（以字节为单位）。
        /// </summary>
        /// <value>
        /// 若日志明确指定了预期长度（如 "Expected Length: 20"），则为此值；
        /// 若未声明或不可知，则为 <see langword="null"/>。
        /// 该字段用于完整性校验（见 <see cref="IsComplete"/>）。
        /// </value>
        public required int? ExpectedLength { get; init; }

        /// <summary>
        /// 获取帧的实际字节数据。
        /// </summary>
        /// <value>
        /// 包含完整的、已聚合的原始网络数据（如一个完整的报文或 APDU），
        /// 可直接用于协议解析器输入。
        /// 此数组不可为 <see langword="null"/>，但可为空数组（<c>Length == 0</c>）。
        /// </value>
        public required byte[] Data { get; init; }

        /// <summary>
        /// 获取一个值，指示当前帧是否已完整接收。
        /// </summary>
        /// <value>
        /// 如果 <see cref="ExpectedLength"/> 为 <see langword="null"/>，则返回 <see langword="true"/>（视为无需校验）；
        /// 否则，当 <see cref="Data"/> 的长度大于或等于 <see cref="ExpectedLength"/> 时返回 <see langword="true"/>。
        /// </value>
        /// <remarks>
        /// 此属性为只读计算属性，无副作用，适合在过滤、告警或流程控制中使用。
        /// 注意：部分协议允许变长帧，此时 <see cref="ExpectedLength"/> 可能为 <see langword="null"/>。
        /// </remarks>
        public bool IsComplete => Data.Length >= ExpectedLength;

        /// <summary>
        /// 获取帧的实际数据长度（以字节为单位）。
        /// </summary>
        /// <value>
        /// 等价于 <c>Data.Length</c>，提供语义清晰的访问方式。
        /// 始终为非负整数。
        /// </value>
        public int ActualLength => Data.Length;

        /// <summary>
        /// 获取指向该帧起始位置的原始电力日志条目。
        /// </summary>
        /// <value>
        /// 引用一个 <see cref="PowerLogEntry"/> 实例，包含原始日志的时间、文件、行号、消息等上下文信息。
        /// 该引用不可为 <see langword="null"/>（通过 <c>null!</c> 断言保证），
        /// 用于调试、审计或日志-帧双向追溯。
        /// </value>
        /// <remarks>
        /// 尽管字段初始化为 <c>null!</c>，但构造时必须通过对象初始化器显式赋值，
        /// 以满足 <c>required</c> 约束。运行时该字段始终有效。
        /// </remarks>
        public PowerLogEntry StartEntry { get; init; } = null!;
    }
}