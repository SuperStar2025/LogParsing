using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议中召唤命令（Interrogation Command）的解析结果。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类封装主站下发的系统级数据召唤请求，例如总召唤（General Interrogation）或电度召唤。
    /// 其核心作用是表达“召唤意图”，不包含从站的执行状态、响应进度或数据内容。
    /// </para>
    /// <para>
    /// 召唤类型由 <see cref="QualifierOfInterrogation"/>（QOI）字段定义，符合 IEC 60870-5-104 标准。
    /// 所有属性均为协议解析后的工程语义值，已屏蔽底层 ASDU 编码细节。
    /// </para>
    /// </remarks>
    public sealed class InterrogationCommandResult : Iec104CommandResult
    {
        /// <summary>
        /// 获取召唤限定词（Qualifier of Interrogation, QOI）。
        /// </summary>
        /// <value>
        /// 一个字节值，用于指定召唤的范围或类型。常见取值包括：
        /// <list type="bullet">
        ///   <item><description>20：标准总召唤（General Interrogation），要求从站上送全部遥信、遥测等数据。</description></item>
        ///   <item><description>1–19、21–255：保留或厂站/工程自定义用途（如分组召唤、电度召唤等）。</description></item>
        /// </list>
        /// 该值直接对应 ASDU 中的 QOI 字段，未经修改。
        /// </value>
        public byte QualifierOfInterrogation { get; }

        /// <summary>
        /// 初始化 <see cref="InterrogationCommandResult"/> 类的新实例。
        /// </summary>
        /// <param name="typeId">
        /// 召唤命令的类型标识符。通常为 <see cref="IEC104TypeId.C_IC_NA_1"/>（总召唤命令），
        /// 也可能为工程扩展类型（如电度召唤）。保留此参数以支持协议溯源和日志分析。
        /// </param>
        /// <param name="commonAddress">
        /// 公共地址（Common Address, CA），标识目标站点或设备组。
        /// </param>
        /// <param name="informationObjectAddress">
        /// 信息对象地址（Information Object Address, IOA）。
        /// 对于标准召唤命令（如 C_IC_NA_1），此字段通常为 0，但协议未强制限制其取值。
        /// </param>
        /// <param name="qualifierOfInterrogation">
        /// 召唤限定词（QOI），用于区分召唤类型或范围（如 20 表示总召唤）。
        /// </param>
        /// <param name="causeOfTransmission">
        /// 传输原因（Cause of Transmission, COT），通常为激活（6）或激活确认（7）。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳。由于标准召唤命令（如 C_IC_NA_1）不携带 CP56Time2a 时标，
        /// 此值通常表示帧接收时间；若未记录，则为 <see langword="null"/>。
        /// </param>
        public InterrogationCommandResult(
            IEC104TypeId typeId,
            ushort commonAddress,
            int informationObjectAddress,
            byte qualifierOfInterrogation,
            ushort causeOfTransmission,
            DateTimeOffset? timestamp)
            : base(typeId, commonAddress, informationObjectAddress, causeOfTransmission, timestamp)
        {
            QualifierOfInterrogation = qualifierOfInterrogation;
        }
    }
}
