using LogParsing.Protocols.IEC104;
using LogParsing.Protocols.IEC104.Models;
using LogParsing.Protocols.IEC104.Results;

namespace LogParsing.Core.Tests
{
    /// <summary>
    /// IEC104 Payload Dispatcher 单元测试。
    /// <para>
    /// 使用示例 byte[] 电力数据进行测试，覆盖遥测、遥信和命令解析。
    /// </para>
    /// </summary>
    public class Iec104DispatcherTests
    {
        /// <summary>
        /// 用于解析 IEC 60870-5-104 应用协议数据单元（APDU）的解析器实例。
        /// </summary>
        private readonly Iec104ApduParser _parser;
        /// <summary>
        /// 用于分发 IEC 104 负载并生成对应结果对象的调度器实例。
        /// </summary>
        private readonly Iec104PayloadDispatcher _dispatcher;
        /// <summary>
        /// 初始化 <see cref="Iec104DispatcherTests"/> 类的新实例。
        /// </summary>
        public Iec104DispatcherTests()
        {
            _parser = new Iec104ApduParser();
            _dispatcher = new Iec104PayloadDispatcher();
        }

        /// <summary>
        /// 测试单点遥信（M_SP_NA_1）负载的解析是否返回有效的状态结果。
        /// </summary>
        [Fact]
        public void SinglePointStatusParsing_ShouldReturnValidStatus()
        {
            // 模拟电力数据 byte[]
            // IOA=0x000001, 状态=1
            byte[] payload = new byte[]
            {
                0x01, 0x00, 0x00, // IOA
                0x01              // 状态
            };

            var results = _dispatcher.Dispatch(
                IEC104TypeId.M_SP_NA_1,
                commonAddress: 1,
                payload,
                timestamp: DateTime.Now,
                numberOfObjects: 1,
                isSequence: false);

            var statusResult = Assert.Single(results) as StatusResult;

            Assert.NotNull(statusResult);
            Assert.Equal(1, statusResult.InformationObjectAddress);
            Assert.Equal(1, statusResult.State);
            Assert.True(statusResult.IsValid);
        }

        /// <summary>
        /// 测试双点遥信（M_DP_NA_1）负载的解析是否返回有效的状态结果。
        /// </summary>
        [Fact]
        public void DoublePointStatusParsing_ShouldReturnValidStatus()
        {
            // Arrange
            // 模拟 IEC 104 双点遥信负载：
            // IOA = 0x000001
            // DIQ = 0x02（状态 = 2，OV = 0 → 有效）
            byte[] payload = new byte[]
            {
                0x01, 0x00, 0x00, // IOA
                0x02              // DIQ
            };

            // Act
            var results = _dispatcher.Dispatch(
                IEC104TypeId.M_DP_NA_1,
                commonAddress: 1,
                payload,
                timestamp: DateTimeOffset.Now,
                numberOfObjects: 1,
                isSequence: false);

            // Assert
            var statusResult = Assert.Single(results) as StatusResult;

            Assert.NotNull(statusResult);
            Assert.Equal(IEC104TypeId.M_DP_NA_1, statusResult.TypeId);
            Assert.Equal(1, statusResult.CommonAddress);
            Assert.Equal(1, statusResult.InformationObjectAddress);
            Assert.Equal(2, statusResult.State);
            Assert.True(statusResult.IsValid);
        }

        /// <summary>
        /// 测试成组单点遥信（M_SP_GB_1）负载解析，包含序列模式和非序列模式。
        /// </summary>
        [Fact]
        public void GroupSinglePointStatusParsing_FullCoverage_ShouldReturnValidStatuses()
        {
            // ========================
            // 序列模式测试 (isSequence = true)
            // ========================
            byte[] sequencePayload = new byte[]
            {
                0x10, 0x00, 0x00, // 起始 IOA = 0x10
                0xAA,             // 状态字节1 = 10101010b
                0x55              // 状态字节2 = 01010101b
            };

            int sequenceNumberOfObjects = 16; // 总点数
            bool isSequence = true;

            var sequenceResults = _dispatcher.Dispatch(
                IEC104TypeId.M_SP_GB_1,
                commonAddress: 1,
                sequencePayload,
                timestamp: DateTime.Now,
                numberOfObjects: sequenceNumberOfObjects,
                isSequence);

            Assert.Equal(16, sequenceResults.Count);

            var expectedSequenceStates = new int[]
            {
                0,1,0,1,0,1,0,1, // 0xAA LSB first
                1,0,1,0,1,0,1,0  // 0x55 LSB first
            };

            for (int i = 0; i < sequenceResults.Count; i++)
            {
                var statusResult = Assert.IsType<StatusResult>(sequenceResults[i]);
                Assert.Equal(0x10 + i, statusResult.InformationObjectAddress);
                Assert.Equal(expectedSequenceStates[i], statusResult.State);
                Assert.True(statusResult.IsValid);
            }

            // ========================
            // 非序列模式测试 (isSequence = false)
            // ========================
            byte[] nonSequencePayload = new byte[]
            {
                0x20, 0x00, 0x00, 0xF0, // IOA=0x20 状态=0xF0
                0x30, 0x00, 0x00, 0x0F  // IOA=0x30 状态=0x0F
            };

            int nonSequenceNumberOfObjects = 16;
            bool isNonSequence = false;

            var nonSequenceResults = _dispatcher.Dispatch(
                IEC104TypeId.M_SP_GB_1,
                commonAddress: 1,
                nonSequencePayload,
                timestamp: DateTime.Now,
                numberOfObjects: nonSequenceNumberOfObjects,
                isSequence: isNonSequence);

            Assert.Equal(nonSequenceNumberOfObjects, nonSequenceResults.Count);

            var expectedNonSequenceStates = new int[]
            {
                0,0,0,0,1,1,1,1, // 0xF0 LSB first
                1,1,1,1,0,0,0,0  // 0x0F LSB first
            };

            var expectedIOAs = new int[]
            {
                0x20,0x21,0x22,0x23,0x24,0x25,0x26,0x27,
                0x30,0x31,0x32,0x33,0x34,0x35,0x36,0x37
            };

            for (int i = 0; i < nonSequenceResults.Count; i++)
            {
                var statusResult = Assert.IsType<StatusResult>(nonSequenceResults[i]);
                Assert.Equal(expectedIOAs[i], statusResult.InformationObjectAddress);
                Assert.Equal(expectedNonSequenceStates[i], statusResult.State);
                Assert.True(statusResult.IsValid);
            }
        }

        /// <summary>
        /// 测试标度化遥测（M_ME_NB_1）负载的解析是否返回有效的测量结果。
        /// </summary>
        [Fact]
        public void ScaledMeasurementParsing_ShouldReturnValidMeasurement()
        {
            // 模拟电力数据 byte[]
            // IOA=0x000001, 值=1000 (short)
            byte[] payload = new byte[]
            {
                0x01, 0x00, 0x00, // IOA
                0xE8, 0x03,       // Value = 1000
                0x00              // QDS
            };

            var results = _dispatcher.Dispatch(
                IEC104TypeId.M_ME_NB_1,
                commonAddress: 1,
                payload,
                timestamp: DateTime.Now,
                numberOfObjects: 1,
                isSequence: false);

            var measurement = Assert.Single(results) as MeasurementResult;

            Assert.NotNull(measurement);
            Assert.Equal(1, measurement.InformationObjectAddress);
            Assert.Equal(1000, measurement.Value);
            Assert.True(measurement.IsValid);
        }

        /// <summary>
        /// 测试无品质描述遥测（M_ME_ND_1）负载解析，
        /// 包含序列模式和非序列模式，确保返回有效的测量结果。
        /// </summary>
        [Fact]
        public void NoQualityMeasurementParsing_FullCoverage_ShouldReturnValidMeasurements()
        {
            // ========================
            // 序列模式测试 (isSequence = true)
            // ========================
            byte[] sequencePayload = new byte[]
            {
                0x10, 0x00, 0x00,       // 起始 IOA = 0x10
                0x34, 0x12,             // 遥测值1 = 0x1234
                0x78, 0x56              // 遥测值2 = 0x5678
            };

            int sequenceNumberOfObjects = 2;
            bool isSequence = true;

            var sequenceResults = _dispatcher.Dispatch(
                IEC104TypeId.M_ME_ND_1,
                commonAddress: 1,
                sequencePayload,
                timestamp: DateTime.Now,
                numberOfObjects: sequenceNumberOfObjects,
                isSequence);

            Assert.Equal(sequenceNumberOfObjects, sequenceResults.Count);

            var expectedIOAsSeq = new int[] { 0x10, 0x11 };
            var expectedValuesSeq = new short[] { 0x1234, 0x5678 };

            for (int i = 0; i < sequenceResults.Count; i++)
            {
                var measurement = Assert.IsType<MeasurementResult>(sequenceResults[i]);
                Assert.Equal(expectedIOAsSeq[i], measurement.InformationObjectAddress);
                Assert.Equal(expectedValuesSeq[i], measurement.Value);
                Assert.True(measurement.IsValid);
            }

            // ========================
            // 非序列模式测试 (isSequence = false)
            // ========================
            byte[] nonSequencePayload = new byte[]
            {
                0x20, 0x00, 0x00, 0x01, 0x00, // IOA=0x20, 值=0x0001
                0x30, 0x00, 0x00, 0xFF, 0x7F  // IOA=0x30, 值=0x7FFF
            };

            int nonSequenceNumberOfObjects = 2;
            bool isNonSequence = false;

            var nonSequenceResults = _dispatcher.Dispatch(
                IEC104TypeId.M_ME_ND_1,
                commonAddress: 1,
                nonSequencePayload,
                timestamp: DateTime.Now,
                numberOfObjects: nonSequenceNumberOfObjects,
                isSequence: isNonSequence);

            Assert.Equal(nonSequenceNumberOfObjects, nonSequenceResults.Count);

            var expectedIOAsNonSeq = new int[] { 0x20, 0x30 };
            var expectedValuesNonSeq = new short[] { 0x0001, 0x7FFF };

            for (int i = 0; i < nonSequenceResults.Count; i++)
            {
                var measurement = Assert.IsType<MeasurementResult>(nonSequenceResults[i]);
                Assert.Equal(expectedIOAsNonSeq[i], measurement.InformationObjectAddress);
                Assert.Equal(expectedValuesNonSeq[i], measurement.Value);
                Assert.True(measurement.IsValid);
            }
        }

        /// <summary>
        /// 测试归一化遥测值（M_ME_NA_1 / M_ME_TB_1）负载解析，
        /// 包含序列模式和非序列模式，验证 OV 位对 IsValid 的影响。
        /// </summary>
        [Fact]
        public void NormalizedMeasurementParsing_FullCoverage_ShouldReturnValidMeasurements()
        {
            // ========================
            // 序列模式测试 (isSequence = true)
            // ========================
            byte[] sequencePayload = new byte[]
            {
                0x10, 0x00, 0x00,       // 起始 IOA = 0x10
                0x34, 0x12,             // 遥测值1 = 0x1234
                0x00,                   // QDS OV = 0 -> 有效
                0x78, 0x56,             // 遥测值2 = 0x5678
                0x80                    // QDS OV = 1 -> 无效
            };

            int sequenceNumberOfObjects = 2;
            bool isSequence = true;

            var sequenceResults = _dispatcher.Dispatch(
                IEC104TypeId.M_ME_NA_1,
                commonAddress: 1,
                sequencePayload,
                timestamp: DateTime.Now,
                numberOfObjects: sequenceNumberOfObjects,
                isSequence);

            Assert.Equal(sequenceNumberOfObjects, sequenceResults.Count);

            var expectedIOAsSeq = new int[] { 0x10, 0x11 };
            var expectedValuesSeq = new short[] { 0x1234, 0x5678 };
            var expectedValiditySeq = new bool[] { true, false };

            for (int i = 0; i < sequenceResults.Count; i++)
            {
                var measurement = Assert.IsType<MeasurementResult>(sequenceResults[i]);
                Assert.Equal(expectedIOAsSeq[i], measurement.InformationObjectAddress);
                Assert.Equal(expectedValuesSeq[i], measurement.Value);
                Assert.Equal(expectedValiditySeq[i], measurement.IsValid);
            }

            // ========================
            // 非序列模式测试 (isSequence = false)
            // ========================
            byte[] nonSequencePayload = new byte[]
            {
                0x20, 0x00, 0x00, 0x01, 0x00, 0x00, // IOA=0x20, 值=0x0001, QDS OV=0
                0x30, 0x00, 0x00, 0xFF, 0x7F, 0x80  // IOA=0x30, 值=0x7FFF, QDS OV=1
            };

            int nonSequenceNumberOfObjects = 2;
            bool isNonSequence = false;

            var nonSequenceResults = _dispatcher.Dispatch(
                IEC104TypeId.M_ME_NA_1,
                commonAddress: 1,
                nonSequencePayload,
                timestamp: DateTime.Now,
                numberOfObjects: nonSequenceNumberOfObjects,
                isSequence: isNonSequence);

            Assert.Equal(nonSequenceNumberOfObjects, nonSequenceResults.Count);

            var expectedIOAsNonSeq = new int[] { 0x20, 0x30 };
            var expectedValuesNonSeq = new short[] { 0x0001, 0x7FFF };
            var expectedValidityNonSeq = new bool[] { true, false };

            for (int i = 0; i < nonSequenceResults.Count; i++)
            {
                var measurement = Assert.IsType<MeasurementResult>(nonSequenceResults[i]);
                Assert.Equal(expectedIOAsNonSeq[i], measurement.InformationObjectAddress);
                Assert.Equal(expectedValuesNonSeq[i], measurement.Value);
                Assert.Equal(expectedValidityNonSeq[i], measurement.IsValid);
            }
        }

        /// <summary>
        /// 测试单点遥控命令（C_SC_NA_1）负载的解析是否返回有效的控制命令结果。
        /// </summary>
        [Fact]
        public void ControlCommandParsing_ShouldReturnValidCommand()
        {
            // 模拟电力数据 byte[]
            // IOA=0x000001, 命令=1, 选择位=0
            byte[] payload = new byte[]
            {
                0x01, 0x00, 0x00, // IOA
                0x01              // 控制字节
            };

            var results = _dispatcher.Dispatch(
                IEC104TypeId.C_SC_NA_1,
                commonAddress: 1,
                payload,
                timestamp: DateTime.Now,
                numberOfObjects: 1,
                isSequence: false);

            var command = Assert.Single(results) as ControlCommandResult;

            Assert.NotNull(command);
            Assert.Equal(1, command.InformationObjectAddress);
            Assert.Equal(1, command.CommandValue);
            Assert.False(command.IsSelect);
        }

        /// <summary>
        /// 测试总召唤命令（C_IC_NA_1）负载的解析是否返回有效的总召唤命令结果。
        /// </summary>
        [Fact]
        public void InterrogationCommandParsing_ShouldReturnValidInterrogation()
        {
            // 模拟电力数据 byte[]
            // IOA=0x000000, QOI=20
            byte[] payload = new byte[]
            {
                0x00, 0x00, 0x00, // IOA
                0x14              // QOI = 20
            };

            var results = _dispatcher.Dispatch(
                IEC104TypeId.C_IC_NA_1,
                commonAddress: 1,
                payload,
                timestamp: DateTime.Now,
                numberOfObjects: 1,
                isSequence: false);

            var interrogation = Assert.Single(results) as InterrogationCommandResult;

            Assert.NotNull(interrogation);
            Assert.Equal(0, interrogation.InformationObjectAddress);
            Assert.Equal(20, interrogation.QualifierOfInterrogation);
        }

        /// <summary>
        /// 测试对时命令（C_RTC_SYNC）负载的解析是否返回有效的时间同步结果。
        /// </summary>
        [Fact]
        public void TimeSyncCommandParsing_ShouldReturnValidTime()
        {
            // 模拟电力数据 byte[]
            // IOA=0x000001, 时间=2025-12-31 23:59:59.500
            byte[] payload = new byte[]
            {
                0x01, 0x00, 0x00,       // IOA
                0x6C, 0xE8,             // Milliseconds = 59500 (59秒500毫秒) //0x8C, 0xE8,  = 59532(59秒532毫秒)
                0x3B,                   // Minute = 59
                0x17,                   // Hour = 23
                0x1F,                   // Day = 31
                0x0C,                   // Month = 12
                0x19                    // Year = 25 -> 2000+25=2025
            };

            var results = _dispatcher.Dispatch(
                IEC104TypeId.C_RTC_SYNC,
                commonAddress: 1,
                payload,
                timestamp: DateTime.Now,
                numberOfObjects: 1,
                isSequence: false);

            var timeSync = Assert.Single(results) as TimeSyncCommandResult;

            Assert.NotNull(timeSync);
            Assert.Equal(1, timeSync.InformationObjectAddress);
            Assert.Equal(new DateTime(2025, 12, 31, 23, 59, 59, 500, DateTimeKind.Utc), timeSync.SyncTime);
        }

        /// <summary>
        /// 测试包含单点遥信（M_SP_NA_1）的完整 APDU 报文解析是否返回有效的状态结果。
        /// </summary>
        [Fact]
        public void SinglePointStatusApduParsing_ShouldReturnValidStatus()
        {
            // 构造 APDU: APCI(6字节) + ASDU(TypeId=1, VSQ=1, CA=1) + Payload(IOA + 状态)
            byte[] apdu = new byte[]
            {
                0x68, 0x0E,             // 起始字节 + 长度
                0x00, 0x00, 0x00, 0x00, // 控制域
                0x01,                   // TypeId = M_SP_NA_1
                0x01,                   // VSQ
                0x01, 0x00,
                0x01, 0x00,
                0x01, 0x00, 0x00,       // IOA = 1
                0x01                    // 状态 = 1
            };

            var results = _parser.Parse(apdu, DateTime.Now);
            var statusResult = Assert.Single(results) as StatusResult;

            Assert.NotNull(statusResult);
            Assert.Equal(1, statusResult.InformationObjectAddress);
            Assert.Equal(1, statusResult.State);
            Assert.True(statusResult.IsValid);
        }

        /// <summary>
        /// 测试包含标度化遥测（M_ME_NB_1）的完整 APDU 报文解析是否返回有效的测量结果。
        /// </summary>
        [Fact]
        public void ScaledMeasurementApduParsing_ShouldReturnValidMeasurement()
        {
            byte[] apdu = new byte[]
            {
                0x68, 0x10,
                0x00, 0x00, 0x00, 0x00,
                0x0B,       // TypeId = M_ME_NB_1 (示例)
                0x01,       // VSQ
                0x14, 0x00,       // 
                0x01, 0x00,       // 
                0x01, 0x00, 0x00, // IOA
                0xE8, 0x03, 0x00         // Value = 1000
            };

            var results = _parser.Parse(apdu, DateTime.Now);
            var measurement = Assert.Single(results) as MeasurementResult;

            Assert.NotNull(measurement);
            Assert.Equal(1, measurement.InformationObjectAddress);
            Assert.Equal(1000, measurement.Value);
            Assert.True(measurement.IsValid);
        }

        /// <summary>
        /// 测试包含遥控命令（C_SC_NA_1）的完整 APDU 报文解析是否返回有效的控制命令结果。
        /// </summary>
        [Fact]
        public void ControlCommandApduParsing_ShouldReturnValidCommand()
        {
            byte[] apdu = new byte[]
            {
                0x68, 0x0E,
                0x00, 0x00, 0x00, 0x00,
                0x2D,       // TypeId = C_SC_NA_1
                0x01,       // VSQ
                0x06, 0x00,       // CA
                0x01, 0x00,
                0x01, 0x00, 0x00, // IOA
                0x01        // 控制字节
            };

            var results = _parser.Parse(apdu, DateTime.Now);
            var command = Assert.Single(results) as ControlCommandResult;

            Assert.NotNull(command);
            Assert.Equal(1, command.InformationObjectAddress);
            Assert.Equal(1, command.CommandValue);
        }

        /// <summary>
        /// 测试包含总召唤命令（C_IC_NA_1）的完整 APDU 报文解析是否返回有效的总召唤命令结果。
        /// </summary>
        [Fact]
        public void InterrogationCommandApduParsing_ShouldReturnValidInterrogation()
        {
            byte[] apdu = new byte[]
            {
                0x68, 0x0E,
                0x00, 0x00, 0x00, 0x00,
                0x64,       // TypeId = C_IC_NA_1
                0x01,
                0x06, 0x00,
                0x01, 0x00,
                0x00, 0x00, 0x00, // IOA = 0
                0x14              // QOI = 20
            };

            var results = _parser.Parse(apdu, DateTime.Now);
            var interrogation = Assert.Single(results) as InterrogationCommandResult;

            Assert.NotNull(interrogation);
            Assert.Equal(0, interrogation.InformationObjectAddress);
            Assert.Equal(20, interrogation.QualifierOfInterrogation);
        }

        /// <summary>
        /// 测试包含对时命令（C_RTC_SYNC）的完整 APDU 报文解析是否返回有效的时间同步结果。
        /// </summary>
        [Fact]
        public void TimeSyncCommandApduParsing_ShouldReturnValidTime()
        {
            byte[] apdu = new byte[]
            {
                0x68, 0x14,
                0x00, 0x00, 0x00, 0x00,
                0x67,       // TypeId = C_RTC_SYNC
                0x01,
                0x01, 0x00,
                0x01, 0x00,
                0x01, 0x00, 0x00, // IOA
                0xF4, 0x01,       // Milliseconds = 500
                0x3B,             // Minute = 59
                0x17,             // Hour = 23
                0x1F,             // Day = 31
                0x0C,             // Month = 12
                0x19              // Year = 25 -> 2025
            };

            var results = _parser.Parse(apdu, DateTime.Now);
            var timeSync = Assert.Single(results) as TimeSyncCommandResult;

            Assert.NotNull(timeSync);
            Assert.Equal(1, timeSync.InformationObjectAddress);
            Assert.Equal(new DateTime(2025, 12, 31, 23, 59, 00, 500), timeSync.SyncTime.DateTime);
        }
    }
}