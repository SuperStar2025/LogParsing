using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议中命令类应用服务数据单元（ASDU）的解析结果基类。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类用于封装来自主站下发的控制或系统命令请求（如遥控、遥调、总召唤、对时等），
    /// 其派生类应代表具有明确业务意图的操作（例如“分闸命令”、“同步时间请求”）。
    /// </para>
    /// <para>
    /// 所有属性均基于协议解析后的工程语义，不包含底层编码细节（如 SCO、QOC 等原始字段）。
    /// </para>
    /// </remarks>
    public abstract class Iec104CommandResult : Iec104ParsedResult
    {
        /// <summary>
        /// 初始化 <see cref="Iec104CommandResult"/> 类的新实例。
        /// </summary>
        /// <param name="typeId">
        /// 命令对应的 IEC 60870-5-104 类型标识符（Type Identification），
        /// 例如 <see cref="IEC104TypeId.C_SC_NA_1"/>（单点遥控）或 <see cref="IEC104TypeId.C_IC_NA_1"/>（总召唤）。
        /// </param>
        /// <param name="commonAddress">
        /// 公共地址（Common Address of ASDU），标识目标站或设备组。
        /// </param>
        /// <param name="informationObjectAddress">
        /// 信息对象地址（Information Object Address），标识被控或被操作的具体点号。
        /// </param>
        /// <param name="causeOfTransmission">
        /// 传输原因（Cause of Transmission, COT），表示该命令的触发上下文，
        /// 例如激活（通常为 6）或激活确认（通常为 7）。
        /// </param>
        /// <param name="timestamp">
        /// 可选时间戳。对于带时标的命令（如 C_SC_TB_1），此值为命令指定的执行时间；
        /// 否则为 <see langword="null"/>。
        /// </param>
        protected Iec104CommandResult(
            IEC104TypeId typeId,
            ushort commonAddress,
            int informationObjectAddress,
            ushort causeOfTransmission,
            DateTimeOffset? timestamp)
            : base(typeId, commonAddress, informationObjectAddress, causeOfTransmission, timestamp)
        {
        }
    }
}

