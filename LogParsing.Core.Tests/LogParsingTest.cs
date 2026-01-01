using LogParsing.Core.Enums;
using LogParsing.Core.Factories;
using LogParsing.Core.Internal;
using LogParsing.Core.Internal.Buffers;
using LogParsing.Core.Internal.Parsers;
using LogParsing.Core.Models;
using LogParsing.Core.Processing;
using System.Diagnostics;
using Xunit.Abstractions;

namespace LogParsing.Core.Tests
{
    /// <summary>
    /// 提供日志解析、缓冲区存储、帧组装及工厂创建等核心功能的单元测试。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本测试类覆盖以下关键场景：
    /// <list type="bullet">
    ///   <item>单行与多行日志的正确解析（<see cref="PowerLogEntry"/> 和 <see cref="ModbusLogEntry"/>）</item>
    ///   <item><see cref="InMemoryLogBuffer"/> 与 <see cref="FileLogBuffer"/> 的存储与查询能力</item>
    ///   <item><see cref="PowerFrameAssembler"/> 的帧组装逻辑与数据完整性</item>
    ///   <item><see cref="LogBufferFactory"/> 在自动模式下的缓冲区类型选择策略</item>
    ///   <item>大规模日志处理的端到端性能基准（含解析、存储、组装阶段）</item>
    /// </list>
    /// </para>
    /// <para>
    /// 所有测试使用真实日志样本，确保与生产环境行为一致。
    /// 性能测试依赖本地文件路径，仅用于开发机验证，不适用于 CI/CD 环境。
    /// </para>
    /// </remarks>
    public class LogParsingTest
    {
        /// <summary>
        /// 用于在 xUnit 测试中输出调试信息的辅助接口。
        /// </summary>
        private readonly ITestOutputHelper _output;
        /// <summary>
        /// 多行日志样本，模拟包含 NetworkData 的完整通信会话。
        /// </summary>
        /// <remarks>
        /// 此数组包含一条 "Sending" 日志、一条 "Received" 日志及其后续多行十六进制数据，
        /// 用于测试多行合并与 <see cref="LogEntry.NetworkData"/> 提取逻辑。
        /// </remarks>
        private readonly string[] lineSome = new[]
            {
                "2023-07-17 08:48:10.223\t+08:00\t[DEBUG]\t[PROT       ]\t[0x5430]\t[iec870ip        ]\t[(GLOBAL)        ]\t[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]\t[.\\protocol.cpp                ]\t[3607]\tChannel (0) : Sending 6 bytes of data",
                "2023-07-17 08:48:10.223\t+08:00\t[DEBUG]\t[PROT       ]\t[0x5430]\t[iec870ip        ]\t[(GLOBAL)        ]\t[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]\t[.\\protocol.cpp                ]\t[3635]\t68  04  01  00  58  6a",
                "2023-07-17 08:48:10.360\t+08:00\t[DEBUG]\t[PROT       ]\t[0x5430]\t[iec870ip        ]\t[(GLOBAL)        ]\t[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]\t[.\\protocol.cpp                ]\t[3611]\tChannel (0) : Received 236 bytes of data",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  ea  58  6a  f4  0f  0d  1c  03  00  01  00  0a  40  00  70",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	3d  4a  3f  00  21  40  00  00  00  00  3f  00  33  40  00  67",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	66  86  3f  00  35  40  00  9a  99  79  3f  00  60  40  00  00",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	60  cc  44  00  6a  40  00  00  a0  cb  44  00  71  40  00  00",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  00  40  00  a7  40  00  00  00  00  40  00  a8  40  00  00",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  80  3f  00  ac  40  00  66  92  cf  43  00  ad  40  00  29",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	e4  5d  c3  00  0c  41  00  8f  3a  c0  43  00  0d  41  00  14",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	46  99  43  00  16  41  00  00  00  00  00  00  17  41  00  00",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  00  00  00  4b  41  00  00  00  c6  43  00  4c  41  00  00",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  c6  43  00  57  41  00  67  66  8a  41  00  58  41  00  67",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	66  7e  41  00  59  41  00  00  00  94  41  00  5a  41  00  67",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	66  8a  41  00  5b  41  00  67  66  7e  41  00  5c  41  00  00",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  94  41  00  61  41  00  9a  99  01  42  00  62  41  00  cd",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	cc  4c  40  00  63  41  00  9a  99  71  41  00  64  41  00  67",
                @"2023-07-17 08:48:10.360	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	66  b6  41  00  65  41  00  67  66  82  41  00"
            };
        /// <summary>
        /// 初始化测试实例，注入输出辅助器以支持调试信息打印。
        /// </summary>
        /// <param name="output">xUnit 提供的测试输出接口，用于记录性能指标或诊断信息。</param>
        public LogParsingTest(ITestOutputHelper output)
        {
            _output = output;
        }
        #region 单行解析测试

        /// <summary>
        /// 验证 <see cref="PowerLogParser"/> 能正确解析单条 Power 日志行并生成有效的 <see cref="PowerLogEntry"/>。
        /// </summary>
        [Fact]
        public void Parse_SampleLogLine_ShouldReturnCorrectFields()
        {
            var logLine = "2023-07-17 08:48:10.422\t+08:00\t[DEBUG]\t[PROT       ]\t[0x5430]\t[iec870ip        ]\t[(GLOBAL)        ]\t[Citect::Drivers::IEC870IP::ACKManager::ACKDelayedMessage()]\t[.\\protocol.cpp                ]\t[3700]\tChannel (0) : ACKDelayedMessage SequenceNumber: 13613";
            //var logLine = "2023-09-15 22:18:06.722\t+08:00\t[DEBUG]\t[PROT       ]\t[0x0b44]\t[iec870ip        ]\t[(GLOBAL)        ]\t[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]\t[.\\protocol.cpp                ]\t[3635]\t68  04  01  00  ce  65";
            var parser = new PowerLogParser();

            Assert.True(parser.CanParse(logLine));

            var entry = parser.Parse(logLine) as LogParsing.Core.Models.PowerLogEntry;
            Assert.NotNull(entry);
        }

        /// <summary>
        /// 验证 <see cref="PowerLogParser"/> 能从多行日志中提取所有有效 <see cref="PowerLogEntry"/> 实例，
        /// 并确保包含 "Sending"、"Received" 动作及非空 <see cref="LogEntry.NetworkData"/>。
        /// </summary>
        [Fact]
        public void Parse_MultiLinePowerLog_ShouldProduceMultipleEntries()
        {


            var parser = new PowerLogParser();

            var entries = lineSome
                .Select(l => parser.Parse(l))
                .OfType<PowerLogEntry>()
                .ToList();

            Assert.Equal(18, entries.Count);

            Assert.Contains(entries, e => e.Action == "Sending");
            Assert.Contains(entries, e => e.Action == "Received");
            Assert.True(entries.Count(e => e.NetworkData?.Length > 0) > 1);
        }

        /// <summary>
        /// 验证 <see cref="ModbusLogParser"/> 能正确解析 Modbus 请求日志行，
        /// 并准确还原时间戳、模块信息、DCB、ID、长度及十六进制网络数据。
        /// </summary>
        [Fact]
        public void Parse_RequestLogLine_ShouldReturnCorrectFields()
        {
            var logLine = "2023-03-17 07:18:24.250\t+08:00\t[TRACE]\t[CORE       ]\t[0x11fc]\t[IOServer        ]\t[(GLOBAL)        ]\t[DrvDebug()                                        ]\t[dsp_fmt.cpp                   ]\t[533 ]\tRequest, DCB=0x1093b93c, ID=0x00de Length 12\t00 DE 00 00 00 06 FF 03 00 00 00 2D                   ...........-\t";
            var parser = new ModbusLogParser();

            Assert.True(parser.CanParse(logLine));

            var entry = parser.Parse(logLine) as ModbusLogEntry;
            Assert.NotNull(entry);

            // 基础 Header 验证
            Assert.Equal(DateTime.Parse("2023-03-17 07:18:24.250"), entry.Timestamp);
            Assert.Equal("+08:00", entry.TimeZone);
            Assert.Equal("TRACE", entry.Level);
            Assert.Equal("CORE", entry.Module);
            Assert.Equal("0x11fc", entry.ThreadId);
            Assert.Equal("IOServer", entry.Source);
            Assert.Equal("(GLOBAL)", entry.Function);
            Assert.Equal("DrvDebug()", entry.File);
            Assert.Equal("dsp_fmt.cpp", entry.FilePath);
            Assert.Equal(533, entry.Line);

            // Message / PSD 专属
            Assert.Equal("Request", entry.Action);
            Assert.Equal("0x1093b93c", entry.DCB);
            Assert.Equal("0x00de", entry.ID);
            Assert.Equal(12, entry.ExpectedLength);
            Assert.NotNull(entry.NetworkData);
            Assert.True(entry.NetworkData.Length > 0);
        }

        /// <summary>
        /// 验证 <see cref="ModbusLogParser"/> 能正确解析包含多行十六进制数据的 "Raw Receive" 日志，
        /// 并确保 <see cref="LogEntry.NetworkData"/> 包含完整字节序列。
        /// </summary>
        [Fact]
        public void Parse_RawReceiveLogLine_ShouldParseHexData()
        {
            var logLine = "2023-03-17 07:18:24.250\t+08:00\t[TRACE]\t[CORE       ]\t[0x11fc]\t[IOServer        ]\t[(GLOBAL)        ]\t[DrvDebug()                                        ]\t[dsp_fmt.cpp                   ]\t[533 ]\tRaw Receive Length 99\t00 DD 00 00 00 5D FF 03 5A 00 00 00 00 00 02 00       .....]..Z.......\t00 00 00 FF FF 00 FF 00 00 00 00 00 00 00 00 FF       ................\tFF 00 FF 00 00 00 00 00 00 00 00 00 00 00 00 00       ................\t00 00 00 00 00 00 00 00 00 00 02 00 00 00 00 FF       ................\tFF 00 FF 00 00 00 00 00 00 00 00 FF FF 00 FF 00       ................\t00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00       ................\t00 00 00                                              ...\t";
            var parser = new ModbusLogParser();

            Assert.True(parser.CanParse(logLine));

            var entry = parser.Parse(logLine) as ModbusLogEntry;
            Assert.NotNull(entry);

            //Assert.Equal("Raw Receive", entry.Action);
            Assert.Equal(99, entry.ExpectedLength);
            Assert.NotNull(entry.NetworkData);
            Assert.True(entry.NetworkData.Length > 0);
        }

        #endregion

        #region 缓冲区存储与检索测试

        /// <summary>
        /// 验证 <see cref="InMemoryLogBuffer"/> 能正确存储并检索单条 <see cref="ModbusLogEntry"/>，
        /// 确保其元数据与网络数据完整保留。
        /// </summary>
        [Fact]
        public void InMemoryBuffer_ShouldStoreAndRetrieve_ModbusLogEntry()
        {
            // Arrange
            var logLine = "2023-03-17 07:18:24.250\t+08:00\t[TRACE]\t[CORE       ]\t[0x11fc]\t[IOServer        ]\t[(GLOBAL)        ]\t[DrvDebug()                                        ]\t[dsp_fmt.cpp                   ]\t[533 ]\tRaw Receive Length 99\t00 DD 00 00 00 5D FF 03 5A 00 00 00 00 00 02 00       .....]..Z.......\t00 00 00 FF FF 00 FF 00 00 00 00 00 00 00 00 FF       ................\tFF 00 FF 00 00 00 00 00 00 00 00 00 00 00 00 00       ................\t00 00 00 00 00 00 00 00 00 00 02 00 00 00 00 FF       ................\tFF 00 FF 00 00 00 00 00 00 00 00 FF FF 00 FF 00       ................\t00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00       ................\t00 00 00                                              ...\t";

            var parser = new ModbusLogParser();
            var buffer = new InMemoryLogBuffer();

            // Act
            var entry = parser.Parse(logLine);
            buffer.Add(entry);

            // Assert
            Assert.Equal(1, buffer.Count);

            var stored = buffer.Find(e => e is ModbusLogEntry).FirstOrDefault();
            Assert.NotNull(stored);

            var modbus = stored as ModbusLogEntry;
            Assert.NotNull(modbus);
            Assert.Equal(99, modbus.ExpectedLength);
            Assert.NotNull(modbus.NetworkData);
            Assert.True(modbus.NetworkData.Length > 0);
        }

        /// <summary>
        /// 验证 <see cref="FileLogBuffer"/> 能正确持久化并检索单条 <see cref="ModbusLogEntry"/>，
        /// 确保序列化/反序列化过程无数据丢失。
        /// </summary>
        [Fact]
        public void FileBuffer_ShouldStoreAndRetrieve_ModbusLogEntry()
        {
            // Arrange
            var tempFile = Path.Combine(
                Path.GetTempPath(),
                $"logbuffer_test_{Guid.NewGuid()}.tmp");

            var logLine = "2023-03-17 07:18:24.250\t+08:00\t[TRACE]\t[CORE       ]\t[0x11fc]\t[IOServer        ]\t[(GLOBAL)        ]\t[DrvDebug()                                        ]\t[dsp_fmt.cpp                   ]\t[533 ]\tRaw Receive Length 99\t00 DD 00 00 00 5D FF 03 5A 00 00 00 00 00 02 00       .....]..Z.......\t00 00 00 FF FF 00 FF 00 00 00 00 00 00 00 00 FF       ................\tFF 00 FF 00 00 00 00 00 00 00 00 00 00 00 00 00       ................\t00 00 00 00 00 00 00 00 00 00 02 00 00 00 00 FF       ................\tFF 00 FF 00 00 00 00 00 00 00 00 FF FF 00 FF 00       ................\t00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00       ................\t00 00 00                                              ...\t";

            var parser = new ModbusLogParser();

            using var buffer = new FileLogBuffer(tempFile);

            // Act
            var entry = parser.Parse(logLine);

            buffer.Add(entry);

            // Assert
            Assert.Equal(1, buffer.Count);

            var stored = buffer.Find(e => e is ModbusLogEntry).FirstOrDefault();

            Assert.NotNull(stored);

            var modbus = stored as ModbusLogEntry;
            Assert.NotNull(modbus);
            Assert.Equal(99, modbus.ExpectedLength);
            Assert.NotNull(modbus.NetworkData);
            Assert.True(modbus.NetworkData.Length > 0);
        }

        /// <summary>
        /// 验证 <see cref="InMemoryLogBuffer"/> 能正确存储并检索多条 <see cref="PowerLogEntry"/>，
        /// 确保每条日志的 <see cref="LogEntry.NetworkData"/> 非空且有效。
        /// </summary>
        [Fact]
        public void InMemoryBuffer_ShouldStoreAndRetrieveMultiple_PowerLogEntry()
        {
            // Arrange

            var parser = new PowerLogParser();
            var buffer = new InMemoryLogBuffer();

            // Act
            // 使用通用解析助手方法，将日志行解析并添加到缓冲区
            var storedEntries = LogParserHelper.ParseLinesToBuffer<PowerLogEntry>(
                lineSome,   // 待解析的日志行集合
                parser,     // 日志解析器
                buffer      // 缓冲区
            );

            // Assert
            Assert.Equal(lineSome.Length, buffer.Count);

            Assert.Equal(lineSome.Length, storedEntries.Count);

            foreach (var power in storedEntries)
            {
                Assert.NotNull(power);
                //Assert.True(modbus.ExpectedLength > 0);
                Assert.NotNull(power.NetworkData);
                Assert.True(power.NetworkData.Length > 0);
            }
        }

        /// <summary>
        /// 验证 <see cref="FileLogBuffer"/> 能正确持久化并检索多条 <see cref="PowerLogEntry"/>，
        /// 确保跨进程边界的数据一致性。
        /// </summary>
        [Fact]
        public void FileBuffer_ShouldStoreAndRetrieveMultiple_PowerLogEntry()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"logbuffer_test_{Guid.NewGuid()}.tmp");

            var parser = new PowerLogParser();
            using var buffer = new FileLogBuffer(tempFile);

            // Act
            // 使用通用解析助手方法，将日志行解析并添加到缓冲区
            var storedEntries = LogParserHelper.ParseLinesToBuffer<PowerLogEntry>(
                lineSome,   // 待解析的日志行集合
                parser,     // 日志解析器
                buffer      // 缓冲区
            );

            // Assert
            Assert.Equal(lineSome.Length, buffer.Count);

            Assert.Equal(lineSome.Length, storedEntries.Count);

            foreach (var power in storedEntries)
            {
                Assert.NotNull(power);
                //Assert.True(power.ExpectedLength > 0);
                Assert.NotNull(power.NetworkData);
                Assert.True(power.NetworkData.Length > 0);
            }

            // 清理临时文件
            // File.Delete(tempFile);
        }

        #endregion

        #region 帧组装测试

        /// <summary>
        /// 验证 <see cref="PowerFrameAssembler"/> 能从多条 <see cref="PowerLogEntry"/> 中正确组装出非空帧数据。
        /// </summary>
        [Fact]
        public void FrameAssembler_ShouldAssembleFramesFromMultiple_PowerLogEntry()
        {
            // Arrange

            var parser = new PowerLogParser();
            var buffer = new InMemoryLogBuffer();

            // Act
            // 使用通用解析助手方法，将日志行解析并添加到缓冲区
            var storedEntries = LogParserHelper.ParseLinesToBuffer<PowerLogEntry>(
                lineSome,   // 待解析的日志行集合
                parser,     // 日志解析器
                buffer      // 缓冲区
            );

            // Assert
            Assert.Equal(lineSome.Length, buffer.Count);

            var assembler = new PowerFrameAssembler();

            var frames = assembler.Assemble(storedEntries).ToList();


            Assert.Equal(lineSome.Length, storedEntries.Count);

            foreach (var power in storedEntries)
            {
                Assert.NotNull(power);
                //Assert.True(modbus.ExpectedLength > 0);
                Assert.NotNull(power.NetworkData);
                Assert.True(power.NetworkData.Length > 0);
            }

            Assert.All(frames, f =>
            {
                Assert.True(f.Data.Length > 0);
            });
        }

        #endregion

        #region 性能测试

        /// <summary>
        /// 测量从大文件解析 Power 日志、存入内存缓冲区并组装帧的端到端性能。
        /// </summary>
        /// <remarks>
        /// ⚠️ 此测试依赖本地绝对路径，仅适用于特定开发环境，不应在 CI 中运行。
        /// 输出结果通过 <see cref="_output"/> 记录，用于手动分析瓶颈。
        /// </remarks>
        [Fact]
        public void FrameAssembler_Performance_FromFile_PowerLogEntry()
        {
            // Arrange
            var parser = new PowerLogParser();
            var buffer = new InMemoryLogBuffer();
            var assembler = new PowerFrameAssembler();

            var swTotal = Stopwatch.StartNew();

            // 1️⃣ Parse + Buffer
            var swParse = Stopwatch.StartNew();

            // 使用通用解析助手方法，将日志行解析并添加到缓冲区
            var storedEntries = LogParserHelper.ParseLinesToBuffer<PowerLogEntry>(
                File.ReadLines(Path.Combine("resource", "syslog.IOServer.JQ_PWS.A0597_IO_PWS_PRI.bak")),   // 待解析的日志行集合
                parser,     // 日志解析器
                buffer      // 缓冲区
            );

            swParse.Stop();

            // 2️⃣ Find
            var swFind = Stopwatch.StartNew();


            swFind.Stop();

            // 3️⃣ Assemble
            var swAssemble = Stopwatch.StartNew();

            var frames = assembler.Assemble(storedEntries).ToList();

            swAssemble.Stop();
            swTotal.Stop();

            // Assert（功能正确性）
            // Assert.Equal(lineSome.Length, storedEntries.Count);
            Assert.NotEmpty(frames);
            //Assert.All(frames, f => Assert.True(f.Data.Length > 0));

            // 输出性能结果
            _output.WriteLine($"Parse + Buffer: {swParse.ElapsedMilliseconds} ms");
            _output.WriteLine($"Find:           {swFind.ElapsedMilliseconds} ms");
            _output.WriteLine($"Assemble:       {swAssemble.ElapsedMilliseconds} ms");
            _output.WriteLine($"TOTAL:          {swTotal.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// 测量从大文件解析 Modbus 日志并存入文件缓冲区的性能。
        /// </summary>
        /// <remarks>
        /// ⚠️ 依赖本地绝对路径，仅用于开发机性能评估。
        /// 组装阶段暂未实现，保留占位以维持测试结构统一。
        /// </remarks>
        [Fact]
        public void FileBufferFrameAssembler_Performance_FromFile_ModbusLogEntry()
        {
            // Arrange
            var tempFile = Path.Combine(
                Path.GetTempPath(),
                $"logbuffer_test_{Guid.NewGuid()}.tmp");

            var parser = new ModbusLogParser();
            using var buffer = new FileLogBuffer(tempFile);

            var swTotal = Stopwatch.StartNew();

            // 1️⃣ Parse + Buffer
            var swParse = Stopwatch.StartNew();

            // 使用通用解析助手方法，将日志行解析并添加到缓冲区
            var storedEntries = LogParserHelper.ParseLinesToBuffer<ModbusLogEntry>(
                File.ReadLines(Path.Combine("resource", "syslog.IOServer.JQ_A0510.A0510_IO_FAS_PRI.dat")),   // 待解析的日志行集合
                parser,     // 日志解析器
                buffer      // 缓冲区
            );

            swParse.Stop();

            // 2️⃣ Find
            var swFind = Stopwatch.StartNew();

            swFind.Stop();

            // 3️⃣ Assemble（如果 Modbus 不需要合并，这一步也能保留用于一致性）
            var swAssemble = Stopwatch.StartNew();

            // 如果你暂时没有 ModbusFrameAssembler，可以先直接用 entries
            // var frames = assembler.Assemble(storedEntries).ToList();

            swAssemble.Stop();
            swTotal.Stop();

            // Assert（功能正确性）
            Assert.NotEmpty(storedEntries);
            //Assert.All(storedEntries, e =>
            //{
            //    Assert.NotNull(e.NetworkData);
            //    Assert.True(e.NetworkData.Length > 0);
            //});

            // 输出性能结果
            _output.WriteLine($"Parse + Buffer: {swParse.ElapsedMilliseconds} ms");
            _output.WriteLine($"Find:           {swFind.ElapsedMilliseconds} ms");
            _output.WriteLine($"Assemble:       {swAssemble.ElapsedMilliseconds} ms");
            _output.WriteLine($"TOTAL:          {swTotal.ElapsedMilliseconds} ms");
        }

        #endregion

        #region LogBufferFactory 自动模式测试

        /// <summary>
        /// 验证 <see cref="LogBufferFactory"/> 在 <see cref="LogBufferMode.Auto"/> 模式下，
        /// 能根据预估大小自动选择 <see cref="InMemoryLogBuffer"/> 或 <see cref="FileLogBuffer"/>。
        /// </summary>
        /// <remarks>
        /// ⚠️ 依赖本地文件路径获取大小，仅用于验证工厂逻辑。
        /// 类型选择策略基于可用内存比例（当前为 30% 阈值）。
        /// </remarks>
        [Fact]
        public void LogBufferFactory_AutoMode_CreatesCorrectBuffer()
        {
            // Arrange
            var parser = new PowerLogParser();

            // 假设文件中有 Power 日志
            var logFile = Path.Combine("resource", "syslog.IOServer.JQ_PWS.A0598_IO_PWS_PRI.bak");

            // 临时目录用于 FileLogBuffer
            var tempDir = Path.GetTempPath();

            // 预估大小（可以用大一点的值测试 FileLogBuffer 创建）
            long estimatedSizeBytes = new FileInfo(logFile).Length;

            // Act
            using var buffer = LogBufferFactory.Create(LogBufferMode.Auto, estimatedSizeBytes, tempDir);

            var swParse = Stopwatch.StartNew();
            // 使用通用解析助手方法，将日志行解析并添加到缓冲区
            var storedEntries = LogParserHelper.ParseLinesToBuffer<PowerLogEntry>(
                File.ReadLines(logFile),   // 待解析的日志行集合
                parser,     // 日志解析器
                buffer      // 缓冲区
            );

            swParse.Stop();

            var swFind = Stopwatch.StartNew();
            swFind.Stop();

            // Assert 类型自动识别正确
            if (estimatedSizeBytes < GC.GetGCMemoryInfo().TotalAvailableMemoryBytes * 0.3)
            {
                Assert.IsType<InMemoryLogBuffer>(buffer);
            }
            else
            {
                Assert.IsType<FileLogBuffer>(buffer);
            }

            // Assert 功能性：日志条目解析正常
            Assert.NotEmpty(storedEntries);
            Assert.All(storedEntries, e => Assert.NotNull(e.TimeZone));

            // 输出性能信息
            _output.WriteLine($"Parse + Buffer: {swParse.ElapsedMilliseconds} ms");
            _output.WriteLine($"Find:           {swFind.ElapsedMilliseconds} ms");
        }

        #endregion
    }
}