using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议中测量值类信息（遥测）的解析结果。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类统一抽象了多种遥测数据类型，包括标度化值（如 M_ME_NA_1）、短浮点数（如 M_ME_NB_1）
    /// 和 IEEE 标准浮点数（如 M_ME_NC_1），将原始协议编码透明地转换为统一的工程数值。
    /// </para>
    /// <para>
    /// 所有属性均为高层业务语义值，已屏蔽底层编码格式（如 SVA、R32、IEEE 754）、品质位（QDS）
    /// 及单位换算等协议细节。调用方无需关心原始 Type ID 差异。
    /// </para>
    /// </remarks>
    public sealed class MeasurementResult : Iec104ParsedResult
    {
        /// <summary>
        /// 获取解析后的工程测量值。
        /// </summary>
        /// <value>
        /// 测量值以 <see cref="double"/> 类型表示，已根据原始 ASDU 类型完成以下处理：
        /// <list type="bullet">
        ///   <item><description>标度化值（M_ME_NA_1）：应用系数和偏移量转换为物理量（如 kV、A）。</description></item>
        ///   <item><description>短浮点（M_ME_NB_1）：按 IEC 60870-5-104 规范解码为 IEEE 754 等效值。</description></item>
        ///   <item><description>IEEE 浮点（M_ME_NC_1）：直接解析为标准 <see cref="float"/> 并提升为 <see cref="double"/>。</description></item>
        /// </list>
        /// 若原始值无效（如溢出、替换），<see cref="IsValid"/> 将为 <see langword="false"/>，但 <see cref="Value"/> 仍保留原始数值供诊断使用。
        /// </value>
        public double Value { get; }

        /// <summary>
        /// 获取一个值，指示该测量值是否有效。
        /// </summary>
        /// <value>
        /// <see langword="true"/> 表示测量值有效（例如品质位中 OV = 0，未溢出且未被人工置数）；
        /// <see langword="false"/> 表示测量值无效（如传感器故障、通信异常、被取代等）。
        /// 此标志已从协议中的品质描述词（Quality Descriptor, QDS）解析得出，代表工程意义上的可用性。
        /// </value>
        public bool IsValid { get; }

        /// <summary>
        /// 初始化 <see cref="MeasurementResult"/> 类的新实例。
        /// </summary>
        /// <param name="typeId">
        /// 原始 ASDU 的类型标识符（如 <see cref="IEC104TypeId.M_ME_NA_1"/>、<see cref="IEC104TypeId.M_ME_NB_1"/> 等）。
        /// 虽然本类统一处理所有遥测类型，但仍保留此信息用于调试、日志或高级分析。
        /// </param>
        /// <param name="commonAddress">公共地址（站地址），标识数据来源站点。</param>
        /// <param name="informationObjectAddress">信息对象地址（点号），标识具体遥测点。</param>
        /// <param name="value">
        /// 已转换为工程单位的测量值。无论原始编码格式如何，均以 <see cref="double"/> 统一表示。
        /// </param>
        /// <param name="isValid">
        /// 指示测量值是否有效的工程标志。该值已从协议品质位（QDS）中提取并转换。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（Cause of Transmission, COT），如周期上送、突发越限等。</param>
        /// <param name="timestamp">
        /// 可选时间戳。若原始 ASDU 包含 CP56Time2a 时标，则为此时间；否则为 <see langword="null"/>。
        /// </param>
        public MeasurementResult(
            IEC104TypeId typeId,
            ushort commonAddress,
            int informationObjectAddress,
            double value,
            bool isValid,
            ushort causeOfTransmission,
            DateTimeOffset? timestamp)
            : base(typeId, commonAddress, informationObjectAddress, causeOfTransmission, timestamp)
        {
            Value = value;
            IsValid = isValid;
        }
    }
}
