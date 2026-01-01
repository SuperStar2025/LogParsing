namespace LogParsing.Core.Models
{
    /// <summary>
    /// 表示 Modbus 协议专用的日志条目，扩展自通用日志基类 <see cref="LogEntry"/>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本类在通用日志结构基础上，增加 Modbus 通信特有的元数据字段，
    /// 用于支持协议解析、请求-响应匹配、完整性校验及调试分析。
    /// </para>
    /// <para>
    /// 主要设计目标包括：
    /// <list type="bullet">
    ///   <item><description>通过 <see cref="DCB"/> 标识设备控制块，区分不同从站或会话上下文；</description></item>
    ///   <item><description>利用 <see cref="ID"/> 实现请求与响应的关联（如事务 ID）；</description></item>
    ///   <item><description>通过 <see cref="Action"/> 明确消息角色（请求/响应/处理中），辅助状态机建模；</description></item>
    ///   <item><description>借助 <see cref="ExpectedLength"/> 快速判断接收数据是否完整，避免无效解析。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 所有新增属性均为可空引用类型（<c>string?</c> 或 <c>int?</c>），
    /// 以适应部分日志源可能缺失某些字段的实际情况。
    /// </para>
    /// </remarks>
    public class ModbusLogEntry : LogEntry
    {
        /// <summary>
        /// 获取或设置设备控制块（Device Control Block, DCB）标识符。
        /// </summary>
        /// <value>
        /// 通常为字符串形式的设备地址、会话 ID 或通道标识（如 "COM1", "RTU_01"），
        /// 用于在多设备环境中区分 Modbus 通信上下文。
        /// 若日志未提供 DCB 信息，则为 <see langword="null"/>。
        /// </value>
        /// <remarks>
        /// DCB 并非 Modbus 标准术语，但在工业日志系统中常用于表示物理或逻辑通信端点。
        /// </remarks>
        public string? DCB { get; set; }

        /// <summary>
        /// 获取或设置 Modbus 消息的唯一标识符（ID）。
        /// </summary>
        /// <value>
        /// 通常以十六进制字符串表示（如 "0x00dd"、"1234"），对应 Modbus TCP 的事务 ID
        /// 或 RTU/ASCII 模式下的隐式序号。
        /// 用于匹配请求与响应帧。若不可用，则为 <see langword="null"/>。
        /// </value>
        public string? ID { get; set; }

        /// <summary>
        /// 获取或设置当前日志条目的动作类型。
        /// </summary>
        /// <value>
        /// 常见值包括：
        /// <list type="bullet">
        ///   <item><description><c>"Request"</c>：主站发出的请求帧；</description></item>
        ///   <item><description><c>"Reply"</c> 或 <c>"Response"</c>：从站返回的响应帧；</description></item>
        ///   <item><description><c>"Processing"</c>：中间处理状态（如超时重试、队列等待）。</description></item>
        /// </list>
        /// 具体取值取决于日志生成系统的约定。若未指定，则为 <see langword="null"/>。
        /// </value>
        /// <remarks>
        /// 此字段对构建 Modbus 通信状态机和异常检测至关重要。
        /// </remarks>
        public string? Action { get; set; }

        /// <summary>
        /// 获取或设置期望接收到的原始数据长度（以字节为单位）。
        /// </summary>
        /// <value>
        /// 通常来源于日志中的 "Raw Receive Length"、"Expected Len" 等字段，
        /// 用于快速判断当前 <see cref="LogEntry.NetworkData"/> 是否已完整接收。
        /// 若该信息不可用或不适用，则为 <see langword="null"/>。
        /// </value>
        /// <remarks>
        /// 在流式解析场景中，此值可用于决定是否继续等待更多数据，
        /// 避免对截断帧进行无效解析。
        /// </remarks>
        public int? ExpectedLength { get; set; }
    }
}