using LogParsing.Core.Internal;
using LogParsing.Core.Internal.Buffers;
using LogParsing.Core.Internal.Parsers;
using LogParsing.Core.Models;
using LogParsing.Core.Processing;
using LogParsing.Protocols.IEC104;
using LogParsing.Protocols.IEC104.Results;
using System.Diagnostics;
using Xunit.Abstractions;

namespace LogParsing.Core.Tests
{
    /// <summary>
    /// IEC 60870-5-104 协议日志解析与帧组装的集成测试类。
    /// <para>
    /// 包含多行日志解析、APDU 帧组装、性能基准等场景。
    /// </para>
    /// </summary>
    public class Iec104logtest
    {
        /// <summary>
        /// 用于在 xUnit 测试中输出调试信息的辅助接口。
        /// </summary>
        private readonly ITestOutputHelper _output;
        /// <summary>
        /// IEC 104 应用协议数据单元（APDU）解析器实例。
        /// </summary>
        private readonly Iec104ApduParser _parser;
        /// <summary>
        /// 示例日志行集合，模拟包含接收/发送的 IEC 104 原始十六进制数据的多行日志，
        /// 用于测试帧组装和完整解析流程。
        /// </summary>
        private readonly string[] lineSome = new[]
            {

                @"2023-07-17 07:42:22.161	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3611]	Channel (0) : Received 172 bytes of data",
                @"2023-07-17 07:42:22.161	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  aa  ee  db  ee  0f  0d  14  03  00  01  00  32  40  00  c2",
                @"2023-07-17 07:42:22.161	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	f5  68  3f  00  3c  40  00  85  eb  51  3f  00  46  40  00  f5",
                @"2023-07-17 07:42:22.161	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	28  5c  3f  00  57  40  00  3d  0a  d7  3d  00  60  40  00  00",
                @"2023-07-17 07:42:22.161	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	60  cc  44  00  62  40  00  00  c0  cb  44  00  64  40  00  00",
                @"2023-07-17 07:42:22.162	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	60  cb  44  00  66  40  00  00  00  cc  44  00  68  40  00  00",
                @"2023-07-17 07:42:22.162	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  cc  44  00  75  40  00  00  00  00  00  00  9c  40  00  33",
                @"2023-07-17 07:42:22.162	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	33  13  41  00  a6  40  00  00  00  00  00  00  ac  40  00  85",
                @"2023-07-17 07:42:22.162	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	cf  ce  43  00  ad  40  00  5c  cf  9f  41  00  b6  40  00  70",
                @"2023-07-17 07:42:22.162	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	81  ca  43  00  b7  40  00  c2  15  5a  c2  00  0c  41  00  00",
                @"2023-07-17 07:42:22.162	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  00  00  00  0d  41  00  00  00  00  00  00  16  41  00  b8",
                @"2023-07-17 07:42:22.162	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	72  cc  43  00  17  41  00  b8  26  74  c3  00",
                @"2023-07-17 07:42:22.187	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3607]	Channel (0) : Sending 6 bytes of data",
                @"2023-07-17 07:42:22.187	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  04  01  00  f0  db",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3611]	Channel (0) : Received 228 bytes of data",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  e2  f0  db  ee  0f  0d  1b  03  00  01  00  47  40  00  34",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	33  d3  3f  00  48  40  00  34  33  d3  3f  00  49  40  00  9a",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	99  c9  3f  00  5c  40  00  00  60  cc  44  00  6e  40  00  00",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  a0  41  00  a6  40  00  00  00  80  3f  00  a8  40  00  00",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  00  40  00  ac  40  00  5c  b7  cf  43  00  ad  40  00  7b",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	ec  91  43  00  b0  40  00  00  00  00  00  00  b6  40  00  cc",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	34  c9  43  00  b7  40  00  00  1c  b6  43  00  ba  40  00  00",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	40  c7  43  00  bb  40  00  00  80  c7  43  00  c1  40  00  cd",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	8c  c7  43  00  0c  41  00  47  39  c2  43  00  0d  41  00  cc",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	58  40  c4  00  16  41  00  c2  a1  cc  43  00  17  41  00  f5",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	88  4e  43  00  42  41  00  00  00  c5  43  00  43  41  00  00",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	80  c5  43  00  44  41  00  00  00  c5  43  00  47  41  00  00",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  2b  43  00  48  41  00  00  00  d8  42  00  4a  41  00  00",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  a0  41  00  4c  41  00  00  80  c5  43  00  4d  41  00  00",
                @"2023-07-17 07:42:22.414	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	00  c5  43  00"
            };
        /// <summary>
        /// 示例日志行集合，模拟包含 U 帧（控制帧）交互的日志，
        /// 用于验证帧组装器对短控制帧的处理能力。
        /// </summary>
        private readonly string[] lineSomeU = new[]
            {
                @"2023-07-17 07:42:25.852	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3607]	Channel (0) : Sending 6 bytes of data",
                @"2023-07-17 07:42:25.852	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  04  43  00  00  00",
                @"2023-07-17 07:42:25.853	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3611]	Channel (0) : Received 6 bytes of data",
                @"2023-07-17 07:42:25.853	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  04  83  00  00  00",
                @"2023-07-17 07:42:26.852	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3607]	Channel (1) : Sending 6 bytes of data",
                @"2023-07-17 07:42:26.852	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  04  13  00  00  00",
                @"2023-07-17 07:42:26.853	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3611]	Channel (1) : Received 6 bytes of data",
                @"2023-07-17 07:42:26.853	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  04  23  00  00  00",
                @"2023-07-17 07:42:27.852	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3607]	Channel (0) : Sending 6 bytes of data",
                @"2023-07-17 07:42:27.852	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  04  07  00  00  00",
                @"2023-07-17 07:42:27.853	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3611]	Channel (0) : Received 6 bytes of data",
                @"2023-07-17 07:42:27.853	+08:00	[DEBUG]	[PROT       ]	[0x5430]	[iec870ip        ]	[(GLOBAL)        ]	[Citect::Drivers::IEC870IP::ProtoChannel::LogNetworkData()]	[.\protocol.cpp                ]	[3635]	68  04  0B  00  00  00",
            };
        /// <summary>
        /// 初始化 <see cref="Iec104logtest"/> 类的新实例。
        /// </summary>
        /// <param name="output">xUnit 提供的测试输出辅助接口，用于记录调试信息。</param>
        public Iec104logtest(ITestOutputHelper output)
        {
            _output = output;
            _parser = new Iec104ApduParser();
        }

        #region 帧组装测试

        /// <summary>
        /// 验证 <see cref="PowerFrameAssembler"/> 能够从多条 <see cref="PowerLogEntry"/> 日志条目中正确组装出完整的 IEC 104 帧。
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
                lineSomeU,   // 待解析的日志行集合
                parser,     // 日志解析器
                buffer      // 缓冲区
            );

            // Assert
            Assert.Equal(lineSomeU.Length, buffer.Count);

            var assembler = new PowerFrameAssembler();

            var frames = assembler.Assemble(storedEntries).ToList();


            Assert.Equal(lineSomeU.Length, storedEntries.Count);

            foreach (var power in storedEntries)
            {
                Assert.NotNull(power);
                //Assert.True(modbus.ExpectedLength > 0);
                Assert.NotNull(power.NetworkData);
                Assert.True(power.NetworkData.Length > 0);

            }

            var rs = new List<Iec104ParsedResult>();
            foreach (var power in frames)
            {
                var results = _parser.Parse(power.Data, power.Timestamp.DateTime);
                foreach (var result in results)
                {
                    rs.Add(result);
                    //_output.WriteLine(result.ToString());
                }
            }
            var allResults = rs;

            var statusResults = allResults.OfType<StatusResult>().ToList();
            var measurementResults = allResults.OfType<MeasurementResult>().ToList();
            var controlResults = allResults.OfType<ControlCommandResult>().ToList();
            var interrogationCommandResult = allResults.OfType<InterrogationCommandResult>().ToList();
            var timeSyncCommandResult = allResults.OfType<TimeSyncCommandResult>().ToList();
            var iec104CommandResult = allResults.OfType<Iec104CommandResult>().ToList();
            var sFrames = rs.OfType<SFrameResult>().ToList();
            var uFrames = rs.OfType<UFrameResult>().ToList();

            Assert.All(frames, f =>
            {

                Assert.True(f.Data.Length > 0);
            });
        }

        #endregion

        /// <summary>
        /// 对 <see cref="PowerFrameAssembler"/> 进行端到端性能基准测试，
        /// 使用真实日志文件验证大规模日志解析、帧组装及 APDU 解析的性能表现。
        /// </summary>
        /// <remarks>
        /// 此测试依赖外部文件路径，请确保测试环境存在指定日志文件。
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
                File.ReadLines(Path.Combine("resource", "syslog.IOServer.JQ_PWS.A0520_IO_PWS_PRI.dat")),   // 待解析的日志行集合
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


            // Assert（功能正确性）
            // Assert.Equal(lineSome.Length, storedEntries.Count);
            Assert.NotEmpty(frames);
            //Assert.All(frames, f => Assert.True(f.Data.Length > 0));

            // 4   _parser
            var swparser = Stopwatch.StartNew();
            var rs = new List<Iec104ParsedResult>();

            for (int i = 0; i < frames.Count; i++)
            {
                if (i == 24781)
                {
                    Console.WriteLine("1");
                }
                var results = _parser.Parse(frames[i].Data, frames[i].Timestamp.DateTime);
                foreach (var result in results)
                {
                    rs.Add(result);
                    //_output.WriteLine(result.ToString());
                }
            }
            var allResults = rs;

            var statusResults = allResults.OfType<StatusResult>().ToList();
            var measurementResults = allResults.OfType<MeasurementResult>().ToList();
            var controlResults = allResults.OfType<ControlCommandResult>().ToList();
            var interrogationCommandResult = allResults.OfType<InterrogationCommandResult>().ToList();
            var timeSyncCommandResult = allResults.OfType<TimeSyncCommandResult>().ToList();
            var iec104CommandResult = allResults.OfType<Iec104CommandResult>().ToList();
            var sFrames = rs.OfType<SFrameResult>().ToList();
            var uFrames = rs.OfType<UFrameResult>().ToList();

            //_output.WriteLine(rs.ToString());
            swparser.Stop();
            swTotal.Stop();

            // 输出性能结果
            _output.WriteLine($"Parse + Buffer: {swParse.ElapsedMilliseconds} ms");
            _output.WriteLine($"Find:           {swFind.ElapsedMilliseconds} ms");
            _output.WriteLine($"Assemble:       {swAssemble.ElapsedMilliseconds} ms");
            _output.WriteLine($"swparser:       {swparser.ElapsedMilliseconds} ms");
            _output.WriteLine($"TOTAL:          {swTotal.ElapsedMilliseconds} ms");
        }
    }
}
