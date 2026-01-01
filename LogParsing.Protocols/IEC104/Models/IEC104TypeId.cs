namespace LogParsing.Protocols.IEC104.Models
{
    /// <summary>
    /// 表示 IEC 60870-5-104 协议中的应用服务数据单元（ASDU）类型标识符（Type Identification）。
    /// 该枚举定义了协议中各类控制命令、遥测、遥信和遥控操作的类型代码。
    /// </summary>
    /// <remarks>
    /// 根据 IEC 60870-5-104 标准，Type ID 决定了 ASDU 的结构和用途。
    /// 值范围通常为 1–255，此处仅列出项目中实际使用的类型。
    /// </remarks>
    public enum IEC104TypeId : byte
    {
        // -------------------
        // U 帧 / 链路控制（Unnumbered Control Functions）
        // -------------------

        /// <summary>
        /// 启动数据传输（StartDT）——用于建立或激活链路的数据传输。
        /// 对应 Type ID = 7。
        /// </summary>
        StartDT = 7,

        /// <summary>
        /// 停止数据传输（StopDT）——用于停止链路的数据传输。
        /// 对应 Type ID = 19。
        /// </summary>
        StopDT = 19,

        /// <summary>
        /// 测试链路（TestDT）——用于链路保活测试。
        /// 对应 Type ID = 67。
        /// </summary>
        TestDT = 67,

        /// <summary>
        /// 总召唤命令（Interrogation Command）——请求站端上送全部遥测与遥信数据。
        /// 对应 Type ID = 100 (C_IC_NA_1)。
        /// </summary>
        C_IC_NA_1 = 100,

        /// <summary>
        /// 电能量总召唤命令——请求站端上送全部累计电度量数据。
        /// 对应 Type ID = 101。
        /// </summary>
        C_IC_TOTAL_ACTIVE_POWER = 101,

        /// <summary>
        /// 系统对时命令（Clock Synchronization）——主站向子站下发时间同步信息。
        /// 对应 Type ID = 103 (C_CS_NA_1，此处简写为 C_RTC_SYNC)。
        /// </summary>
        C_RTC_SYNC = 103,


        // -------------------
        // 遥测（Measured Values / Telemetry）
        // -------------------

        /// <summary>
        /// 带品质描述的归一化测量值（Normalized Value with Quality）。
        /// 对应 Type ID = 9 (M_ME_NA_1)。
        /// </summary>
        M_ME_NA_1 = 9,

        /// <summary>
        /// 带 3 字节短时标的归一化测量值（Normalized Value with Short Timestamp）。
        /// 对应 Type ID = 10 (M_ME_TB_1)。
        /// </summary>
        M_ME_TB_1 = 10,

        /// <summary>
        /// 不带时标的标度化测量值（Scaled Value without Timestamp）。
        /// 对应 Type ID = 11 (M_ME_NB_1)。
        /// </summary>
        M_ME_NB_1 = 11,

        /// <summary>
        /// 带时标的标度化测量值（Scaled Value with Timestamp）。
        /// 对应 Type ID = 12 (M_ME_TD_1)。
        /// </summary>
        M_ME_TD_1 = 12,

        /// <summary>
        /// 带品质描述的浮点型测量值（Short Floating Point Value with Quality）。
        /// 对应 Type ID = 13 (M_ME_NC_1)。
        /// </summary>
        M_ME_NC_1 = 13,

        /// <summary>
        /// 带时标的浮点型测量值（Short Floating Point Value with Timestamp）。
        /// 对应 Type ID = 14 (M_ME_TF_1)。
        /// </summary>
        M_ME_TF_1 = 14,

        /// <summary>
        /// 不带品质描述的遥测值（Measured Value, Normalized, No Quality）。
        /// 通常用于高频率上传且无需品质判断的场景。
        /// 对应 Type ID = 21 (M_ME_ND_1)。
        /// </summary>
        M_ME_ND_1 = 21,


        // -------------------
        // 遥信（Status Information / Teleindication）
        // -------------------

        /// <summary>
        /// 不带时标的单点遥信（Single-point Information without Timestamp）。
        /// 表示一个二进制状态（如开关分/合）。
        /// 对应 Type ID = 1 (M_SP_NA_1)。
        /// </summary>
        M_SP_NA_1 = 1,

        /// <summary>
        /// 不带时标的双点遥信（Double-point Information without Timestamp）。
        /// 使用两位表示状态（如：00=中间态，01=分，10=合，11=无效）。
        /// 对应 Type ID = 3 (M_DP_NA_1)。
        /// </summary>
        M_DP_NA_1 = 3,

        /// <summary>
        /// 成组单点遥信（Packed Single-point Information）。
        /// 多个遥信状态打包在一个字节中传输。
        /// 对应 Type ID = 20 (M_SP_GB_1)。
        /// </summary>
        M_SP_GB_1 = 20,

        /// <summary>
        /// 带短时标的单点遥信（Single-point Information with Short Timestamp）。
        /// 对应 Type ID = 2 (M_SP_TB_1)。
        /// </summary>
        M_SP_TB_1 = 2,

        /// <summary>
        /// 带短时标的双点遥信（Double-point Information with Short Timestamp）。
        /// 对应 Type ID = 4 (M_DP_TB_1)。
        /// </summary>
        M_DP_TB_1 = 4,

        /// <summary>
        /// 带 7 字节 CP56Time2a 时标的单点遥信（Single-point with Long Timestamp）。
        /// 时标精度达毫秒级，符合 IEC 60870-5-4 标准。
        /// 对应 Type ID = 30。
        /// </summary>
        M_SP_TB_7 = 30,

        /// <summary>
        /// 带 7 字节 CP56Time2a 时标的双点遥信（Double-point with Long Timestamp）。
        /// 对应 Type ID = 31。
        /// </summary>
        M_DP_TB_7 = 31,


        // -------------------
        // 遥控（Control Commands / Telecontrol）
        // -------------------

        /// <summary>
        /// 单点遥控命令（Single Command）——用于控制二进制设备（如断路器分/合）。
        /// 包含选择/执行标志和品质信息。
        /// 对应 Type ID = 45 (C_SC_NA_1)。
        /// </summary>
        C_SC_NA_1 = 45,

        /// <summary>
        /// 双点遥控命令（Double Command）——使用两位编码表示控制命令。
        /// 对应 Type ID = 46 (C_DC_NA_1)。
        /// </summary>
        C_DC_NA_1 = 46,

        /// <summary>
        /// 带长时标的单点遥控命令（Single Command with Long Timestamp）。
        /// 用于需要精确记录操作时间的遥控场景。
        /// 对应 Type ID = 58 (C_SC_TB_1)。
        /// </summary>
        C_SC_TB_1 = 58,

        /// <summary>
        /// 带长时标的双点遥控命令（Double Command with Long Timestamp）。
        /// 对应 Type ID = 59 (C_DC_TB_1)。
        /// </summary>
        C_DC_TB_1 = 59,

        /// <summary>
        /// 双点遥调命令（Regulating Step Command）——用于调节设备档位或输出。
        /// 例如变压器分接头调节。
        /// 对应 Type ID = 47 (C_RC_NA_1，此处命名为 C_DC_ADJUST)。
        /// </summary>
        C_DC_ADJUST = 47
    }
}
