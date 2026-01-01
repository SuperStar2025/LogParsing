using LogParsing.Protocols.IEC104.Models;

namespace LogParsing.Protocols.IEC104.Results
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议中对时命令（时间同步请求）的解析结果。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类封装了主站下发的时间同步命令（如类型标识符 <see cref="IEC104TypeId.C_RTC_SYNC"/>），
    /// 其核心语义是要求从站将本地时钟调整为指定的同步时间。
    /// </para>
    /// <para>
    /// 所有属性均为解析后的高层工程值，已屏蔽底层 CP56Time2a 编码等协议细节。
    /// </para>
    /// </remarks>
    public sealed class TimeSyncCommandResult : Iec104CommandResult
    {
        /// <summary>
        /// 获取命令中指定的同步时间值。
        /// </summary>
        /// <value>
        /// 主站要求从站设置的目标时间，以 <see cref="DateTimeOffset"/> 形式表示，
        /// 包含日期、时间及 UTC 偏移量。该值已从 ASDU 中的 CP56Time2a 字段解析得出。
        /// </value>
        public DateTimeOffset SyncTime { get; }

        /// <summary>
        /// 初始化 <see cref="TimeSyncCommandResult"/> 类的新实例。
        /// </summary>
        /// <param name="typeId">
        /// 对时命令的类型标识符，通常为 <see cref="IEC104TypeId.C_RTC_SYNC"/>。
        /// 虽然本类专用于时间同步，但仍保留此参数以支持基类兼容性和日志溯源。
        /// </param>
        /// <param name="commonAddress">公共地址（站地址），标识目标从站或设备组。</param>
        /// <param name="informationObjectAddress">
        /// 信息对象地址。对于标准对时命令（C_CS_NA_1），此字段通常为 0 或保留值。
        /// </param>
        /// <param name="syncTime">
        /// 主站指定的同步时间。该值已从协议中的 CP56Time2a 时标解析为 <see cref="DateTimeOffset"/>。
        /// </param>
        /// <param name="causeOfTransmission">传输原因（Cause of Transmission, COT），通常为激活（6）。</param>
        /// <param name="timestamp">
        /// 可选接收时间戳。由于对时命令本身已包含 <see cref="SyncTime"/>，
        /// 此参数通常表示帧到达解析器的时间，可用于性能分析或日志记录；
        /// 若未记录，则为 <see langword="null"/>。
        /// </param>
        public TimeSyncCommandResult(
            IEC104TypeId typeId,
            ushort commonAddress,
            int informationObjectAddress,
            DateTimeOffset syncTime,
            ushort causeOfTransmission,
            DateTimeOffset? timestamp)
            : base(typeId, commonAddress, informationObjectAddress, causeOfTransmission, timestamp)
        {
            SyncTime = syncTime;
        }
    }
}
