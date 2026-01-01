using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议中遥控命令（Telecontrol Command）的解析结果。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类封装了主站下发的单点或双点遥控指令，适用于类型标识符如
    /// <see cref="IEC104TypeId.C_SC_NA_1"/>（单命令）、<see cref="IEC104TypeId.C_DC_NA_1"/>（双命令）等。
    /// </para>
    /// <para>
    /// 所有属性均为协议解析后的工程语义值，不暴露底层编码细节（如 SCO 中的 S/E 位、QU 等）。
    /// </para>
    /// </remarks>
    public sealed class ControlCommandResult : Iec104CommandResult
    {
        /// <summary>
        /// 获取遥控命令的操作值。
        /// </summary>
        /// <value>
        /// 对于单点命令（C_SC_NA_1），有效值通常为：
        /// <list type="bullet">
        ///   <item><description>0：分闸（OFF）</description></item>
        ///   <item><description>1：合闸（ON）</description></item>
        /// </list>
        /// 对于双点命令（C_DC_NA_1），有效值通常为：
        /// <list type="bullet">
        ///   <item><description>1：分闸</description></item>
        ///   <item><description>2：合闸</description></item>
        ///   <item><description>0 或 3：无效或中间状态</description></item>
        /// </list>
        /// 具体含义需结合设备定义和 Type ID 解释。
        /// </value>
        public int CommandValue { get; }

        /// <summary>
        /// 获取一个值，指示该命令是否为“选择”阶段（Select）而非“执行”阶段（Execute）。
        /// </summary>
        /// <value>
        /// <see langword="true"/> 表示此为预选命令（用于确认操作可行性）；
        /// <see langword="false"/> 表示此为执行命令（实际触发控制动作）。
        /// </value>
        public bool IsSelect { get; }

        /// <summary>
        /// 初始化 <see cref="ControlCommandResult"/> 类的新实例。
        /// </summary>
        /// <param name="typeId">
        /// 遥控命令对应的 IEC 60870-5-104 类型标识符，
        /// 例如 <see cref="IEC104TypeId.C_SC_NA_1"/> 或 <see cref="IEC104TypeId.C_DC_NA_1"/>。
        /// </param>
        /// <param name="commonAddress">公共地址（站地址）。</param>
        /// <param name="informationObjectAddress">被控对象的信息对象地址（点号）。</param>
        /// <param name="commandValue">
        /// 命令操作值，取值范围通常为 0–3，具体语义由命令类型决定。
        /// </param>
        /// <param name="isSelect">
        /// 指示是否为选择命令（Select）。若为 <see langword="true"/>，表示此命令用于预选；
        /// 若为 <see langword="false"/>，表示直接执行。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（Cause of Transmission, COT）。</param>
        /// <param name="timestamp">
        /// 可选时间戳。若命令 ASDU 包含 CP56Time2a 时标，则为指定执行时间；否则为 <see langword="null"/>。
        /// </param>
        public ControlCommandResult(
            IEC104TypeId typeId,
            ushort commonAddress,
            int informationObjectAddress,
            int commandValue,
            bool isSelect,
            ushort causeOfTransmission,
            DateTimeOffset? timestamp)
            : base(typeId, commonAddress, informationObjectAddress, causeOfTransmission, timestamp)
        {
            CommandValue = commandValue;
            IsSelect = isSelect;
        }
    }
}

