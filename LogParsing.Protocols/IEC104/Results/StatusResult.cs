using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议中状态类信息（遥信）的解析结果。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类统一抽象了单点信息（如 M_SP_NA_1）、双点信息（如 M_DP_NA_1）等遥信数据，
    /// 不区分具体的 <see cref="IEC104TypeId"/> 变体，仅暴露标准化的工程语义。
    /// </para>
    /// <para>
    /// 所有属性均为协议解析后的高层语义值，已屏蔽底层编码细节（如 SPI、DPI、品质位 QDS 等）。
    /// </para>
    /// </remarks>
    public sealed class StatusResult : Iec104ParsedResult
    {
        /// <summary>
        /// 获取状态的工程值。
        /// </summary>
        /// <value>
        /// 状态值的取值范围和含义取决于原始信息类型：
        /// <list type="bullet">
        ///   <item>
        ///     <description>对于单点遥信（如 <see cref="IEC104TypeId.M_SP_NA_1"/>）：0 表示分闸/断开，1 表示合闸/闭合。</description>
        ///   </item>
        ///   <item>
        ///     <description>对于双点遥信（如 <see cref="IEC104TypeId.M_DP_NA_1"/>）：0 和 3 通常表示无效或不确定状态，1 表示分闸，2 表示合闸。</description>
        ///   </item>
        /// </list>
        /// 调用方应结合业务上下文解释具体含义。
        /// </value>
        public int State { get; }

        /// <summary>
        /// 获取一个值，指示该状态是否有效。
        /// </summary>
        /// <value>
        /// <see langword="true"/> 表示状态有效（例如品质位中 OV = 0，未溢出且未被取代）；
        /// <see langword="false"/> 表示状态无效（如传感器故障、通信异常、被人工置数等）。
        /// 此值已根据协议中的品质描述词（QDS）解析得出，代表工程意义上的可用性。
        /// </value>
        public bool IsValid { get; }

        /// <summary>
        /// 初始化 <see cref="StatusResult"/> 类的新实例。
        /// </summary>
        /// <param name="typeId">
        /// 原始 ASDU 的类型标识符（如 <see cref="IEC104TypeId.M_SP_NA_1"/> 或 <see cref="IEC104TypeId.M_DP_NA_1"/>）。
        /// 虽然本类不区分具体类型，但仍保留此信息用于调试或日志溯源。
        /// </param>
        /// <param name="commonAddress">公共地址（站地址），标识数据来源站点。</param>
        /// <param name="informationObjectAddress">信息对象地址（点号），标识具体遥信点。</param>
        /// <param name="state">
        /// 解析后的状态值。取值范围通常为 0–3，具体语义由原始 Type ID 决定。
        /// </param>
        /// <param name="isValid">
        /// 指示状态是否有效的工程标志。该值已从协议品质位（Quality Descriptor）中提取并转换。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（Cause of Transmission, COT），如周期上送、突发变位等。</param>
        /// <param name="timestamp">
        /// 可选时间戳。若原始 ASDU 包含 CP56Time2a 时标，则为此时间；否则为 <see langword="null"/>。
        /// </param>
        public StatusResult(
            IEC104TypeId typeId,
            ushort commonAddress,
            int informationObjectAddress,
            int state,
            bool isValid,
            ushort causeOfTransmission,
            DateTimeOffset? timestamp)
            : base(typeId, commonAddress, informationObjectAddress, causeOfTransmission, timestamp)
        {
            State = state;
            IsValid = isValid;
        }
    }
}
