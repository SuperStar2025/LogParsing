namespace LogParsing.Core.Models
{
    /// <summary>
    /// 表示通用日志条目的基类，封装所有日志类型共有的元数据和内容字段。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类作为日志解析系统的统一入口模型，旨在：
    /// <list type="bullet">
    ///   <item><description>提供标准化的日志结构，便于跨协议（如 Modbus、IEC104、Power 系统等）日志的统一处理；</description></item>
    ///   <item><description>支持面向对象继承机制，允许特定协议或业务日志派生子类以扩展专用字段；</description></item>
    ///   <item><description>保留完整的原始上下文信息（时间、位置、调用栈、线程等），用于调试、审计、性能分析及故障回溯。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 所有属性均为可读写（<see langword="public get; set;"/>），以支持反序列化、日志增强或后处理场景。
    /// 默认字符串属性初始化为空字符串（<see cref="string.Empty"/>），避免 <see langword="null"/> 值导致的空引用异常。
    /// </para>
    /// <para>
    /// 设计为非密封（<see langword="non-sealed"/>）类，鼓励通过继承实现多态日志模型。
    /// </para>
    /// </remarks>
    public class LogEntry
    {
        /// <summary>
        /// 获取或设置日志记录的时间戳（UTC 或本地时间，含偏移量）。
        /// </summary>
        /// <value>
        /// 使用 <see cref="DateTimeOffset"/> 类型确保时区感知性，适用于跨时区日志聚合与排序。
        /// 精度通常为毫秒级，具体取决于日志源系统。
        /// </value>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// 获取或设置日志生成时的时区标识符（如 "+08:00" 或 "UTC"）。
        /// </summary>
        /// <value>
        /// 以字符串形式表示时区偏移，便于在无法使用 <see cref="TimeZoneInfo"/> 的场景下进行时间对齐。
        /// 默认值为 <see cref="string.Empty"/>。
        /// </value>
        /// <remarks>
        /// 注意：此字段为冗余信息，因 <see cref="Timestamp"/> 已包含偏移量；
        /// 保留此字段是为了兼容某些仅输出时区字符串的日志格式（如 syslog、自定义文本日志）。
        /// </remarks>
        public string TimeZone { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置日志严重性级别（如 "TRACE", "DEBUG", "INFO", "WARN", "ERROR", "FATAL"）。
        /// </summary>
        /// <value>
        /// 字符串形式的日志级别，用于过滤、着色显示、告警触发或存储分级。
        /// 默认值为 <see cref="string.Empty"/>。
        /// </value>
        /// <remarks>
        /// 虽然 .NET 提供 <c>LogLevel</c> 枚举，但此处采用字符串以兼容异构日志源的自由格式。
        /// </remarks>
        public string Level { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置日志所属的功能模块名称（如 "ProtocolParser", "DataCollector"）。
        /// </summary>
        /// <value>
        /// 用于按模块维度聚合或筛选日志，默认值为 <see cref="string.Empty"/>。
        /// </value>
        public string Module { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置生成日志的线程标识符。
        /// </summary>
        /// <value>
        /// 通常为数字字符串（如 "12345"），用于在多线程或异步环境中追踪执行上下文。
        /// 默认值为 <see cref="string.Empty"/>。
        /// </value>
        public string ThreadId { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置日志来源的逻辑组件名称（如类名、服务名或设备 ID）。
        /// </summary>
        /// <value>
        /// 用于快速定位日志产生源头，默认值为 <see cref="string.Empty"/>。
        /// </value>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置记录日志的函数或方法名称。
        /// </summary>
        /// <value>
        /// 用于重构调用链路或分析执行路径，默认值为 <see cref="string.Empty"/>。
        /// </value>
        public string Function { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置日志所在的源代码文件名（不含路径）。
        /// </summary>
        /// <value>
        /// 通常由编译器通过 <c>__FILE__</c> 或日志框架自动填充，
        /// 用于快速跳转到日志语句位置，默认值为 <see cref="string.Empty"/>。
        /// </value>
        public string File { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置日志所在文件的完整绝对路径。
        /// </summary>
        /// <value>
        /// 可选字段，主要用于调试、归档或日志溯源场景。
        /// 若不可用，则保留为空字符串，默认值为 <see cref="string.Empty"/>。
        /// </value>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置日志在源文件中的行号。
        /// </summary>
        /// <value>
        /// 通常由编译器通过 <c>__LINE__</c> 自动注入，用于精确定位日志语句。
        /// 默认值为 <c>0</c>，表示未提供或不可用。
        /// </value>
        public int Line { get; set; }

        /// <summary>
        /// 获取或设置日志的主体消息内容。
        /// </summary>
        /// <value>
        /// 包含具体的业务描述、错误详情、状态信息或协议数据摘要。
        /// 默认值为 <see cref="string.Empty"/>。
        /// </value>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置与日志关联的原始网络帧数据（以字节数组形式）。
        /// </summary>
        /// <value>
        /// 通常为十六进制字节序列（如从串口、TCP 流捕获的原始报文），
        /// 用于后续协议解析、重放或取证分析。
        /// 若无网络数据，则为 <see langword="null"/>。
        /// </value>
        /// <remarks>
        /// 此字段是日志解析系统的核心输入之一，尤其在通信协议分析场景中至关重要。
        /// </remarks>
        public byte[]? NetworkData { get; set; }
    }
}