/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_RecordPersistence.cs
수정일 : 2026-11-11

# 설명
RecordPersistence의 Record 읽기·쓰기, 기존 IO Mapping 조합, 유효성 검증 및 ReleaseHandle 수명과 비동기 취소를 검증한다.
Document/Save 전용 Metadata·Session·Slot은 이 테스트의 공통 계약에 포함되지 않는다.

# 테스트 구성
 P: Persistence - 동기·비동기 검증과 ReleaseHandle 수명
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Serializable;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.Persistence.TEST
{
    // ============================================================
    /// <summary>
    /// RecordPersistence의 검증 및 IO 수명 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_RecordPersistence
    {

    #region 테스트 객체

        // ============================================================
        /// <summary>
        /// 직렬화 대상 테스트 기록.
        /// </summary>
        // ============================================================
        [Serializable]
        private sealed class TestRecord
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 기록 식별 정보.
            /// </summary>
            // ------------------------------------------------------------
            public string Name = "";

            // ------------------------------------------------------------
            /// <summary>
            /// 기록 값.
            /// </summary>
            // ------------------------------------------------------------
            public int Value = 0;
        }

        // ============================================================
        /// <summary>
        /// 미리 정의한 IO 응답을 돌려주는 Reader.
        /// </summary>
        // ============================================================
        private sealed class TestReader<T> : IDataReader<string, T>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 호출 시 반환할 IO 응답.
            /// </summary>
            // ------------------------------------------------------------
            public ReadResponse<T> Response;

            // ------------------------------------------------------------
            /// <summary>
            /// 요청 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count;

            // ------------------------------------------------------------
            /// <summary>
            /// 미리 설정한 IO 응답을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadResponse<T> Read(string location)
            {
                Count++;
                return Response;
            }
        }

        // ============================================================
        /// <summary>
        /// 호출 횟수와 기록을 수집하는 Writer.
        /// </summary>
        // ============================================================
        private sealed class TestWriter<T> : IDataWriter<string, T>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 기록 호출 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count;

            // ------------------------------------------------------------
            /// <summary>
            /// 마지막 기록 값.
            /// </summary>
            // ------------------------------------------------------------
            public T Value;

            // ------------------------------------------------------------
            /// <summary>
            /// 기록 요청을 수집한 후 성공 응답을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string location, T value)
            {
                Count++;
                Value = value;
                return WriteResponse.Succeed();
            }
        }

        // ============================================================
        /// <summary>
        /// 비동기 Reader의 테스트 구현.
        /// </summary>
        // ============================================================
        private sealed class TestAsyncReader<T> : IAsyncDataReader<string, T>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 비동기 Reader가 돌려줄 응답.
            /// </summary>
            // ------------------------------------------------------------
            public ReadResponse<T> Response;

            // ------------------------------------------------------------
            /// <summary>
            /// 응답을 반환하기 직전에 실행할 테스트 동작.
            /// </summary>
            // ------------------------------------------------------------
            public Action OnRead;

            // ------------------------------------------------------------
            /// <summary>
            /// Reader의 호출 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count;

            // ------------------------------------------------------------
            /// <summary>
            /// 설정된 결과를 비동기로 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public Task<ReadResponse<T>> ReadAsync
            (
                string location,
                CancellationToken cancellationToken = default
            )
            {
                Count++;
                OnRead?.Invoke();
                return Task.FromResult(Response);
            }
        }

        // ============================================================
        /// <summary>
        /// 비동기 Writer의 테스트 구현.
        /// </summary>
        // ============================================================
        private sealed class TestAsyncWriter<T> : IAsyncDataWriter<string, T>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 기록 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count;

            // ------------------------------------------------------------
            /// <summary>
            /// 기록된 마지막 값.
            /// </summary>
            // ------------------------------------------------------------
            public T Value;

            // ------------------------------------------------------------
            /// <summary>
            /// 전달받은 값을 기록하고 성공을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public Task<WriteResponse> WriteAsync
            (
                string location,
                T value,
                CancellationToken cancellationToken = default
            )
            {
                Count++;
                Value = value;
                return Task.FromResult(WriteResponse.Succeed());
            }
        }

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 각 테스트의 독립적인 임시 디렉터리.
        /// </summary>
        // ------------------------------------------------------------
        private string directory;

        // ------------------------------------------------------------
        /// <summary>
        /// 임시 디렉터리를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine
            (
                Path.GetTempPath(),
                "XeriPersistenceTest_" + Guid.NewGuid().ToString("N")
            );

            Directory.CreateDirectory(directory);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 임시 파일을 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

    #endregion

    #region P-1: 동기 Record의 기록·검증·수명 종료

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Mapping과 파일 IO를 조합하여 타입 기록을 저장하고 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Record_직렬화_왕복()
        {
            var serializer = UnityJsonSerializer.Default;
            var fileIO = TextFileIO.Default;
            var reader = new MappedDataReader<string, string, TestRecord>
            (
                fileIO,
                serializer.Deserialize<TestRecord>
            );

            var writer = new MappedDataWriter<string, TestRecord, string>
            (
                new FileCommitWriter<string>(fileIO),
                serializer.Serialize
            );

            var record = new RecordPersistence<string, TestRecord>
            (
                reader, writer, ValidateRecord
            );

            var path = Path.Combine(directory, "record.json");
            var saved = record.Save
            (
                path,
                new TestRecord
                {
                    Name = "saved",
                    Value = 22
                }
            );
            var loaded = record.Load(path);

            Assert.IsTrue(saved.Success, saved.Error);
            Assert.IsTrue(loaded.Success, loaded.Error);
            Assert.AreEqual("saved", loaded.Value.Name);
            Assert.AreEqual(22, loaded.Value.Value);
            Assert.IsNull(loaded.ReleaseHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Mapping에서 전달한 ReleaseHandle을 Record 검증까지 유지한다.
        /// <br/> 독립 Record 반환 전 핸들을 한 번만 종료한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void Record_Mapping_검증_후_원본_ReleaseHandle_종료()
        {
            var released = 0;
            var source = new TestReader<string>
            {
                Response = ReadResponse<string>.Succeed
                (
                    "saved",
                    new CallbackReleaseHandle(() => released++)
                )
            };
            var reader = new MappedDataReader<string, string, TestRecord>
            (
                source,
                value => new TestRecord
                {
                    Name = value,
                    Value = 42
                }
            );
            var persistence = new RecordPersistence<string, TestRecord>
            (
                reader,
                new TestWriter<TestRecord>(),
                record =>
                {
                    Assert.AreEqual(0, released);
                    ValidateRecord(record);
                }
            );

            var response = persistence.Load("unused");

            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual(1, source.Count);
            Assert.AreEqual(1, released);
            Assert.IsNull(response.ReleaseHandle);
            Assert.AreEqual("saved", response.Value.Name);
            Assert.AreEqual(42, response.Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 검증 실패 시 Writer를 호출하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Record_검증_실패_기록_방지()
        {
            var writer = new TestWriter<TestRecord>();
            var record = new RecordPersistence<string, TestRecord>
            (
                new TestReader<TestRecord>(), writer, ValidateRecord
            );

            var response = record.Save
            (
                "unused",
                new TestRecord
                {
                    Name = "",
                    Value = 11
                }
            );

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<InvalidOperationException>(response.Exception);
            Assert.AreEqual(0, writer.Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 원본 IO가 실패해도 ReleaseHandle을 한 번만 반환하고 오류를 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void Record_읽기_실패_ReleaseHandle_반환()
        {
            var release = 0;
            var error = new IOException("읽기 실패");
            var reader = new TestReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Fail
                (
                    error.Message, error, new CallbackReleaseHandle(() => release++)
                )
            };

            var record = new RecordPersistence<string, TestRecord>
            (
                reader, new TestWriter<TestRecord>(), ValidateRecord
            );

            var response = record.Load("unused");

            Assert.IsFalse(response.Success);
            Assert.AreSame(error, response.Exception);
            Assert.AreEqual(1, release);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Record의 유효성 검사 실패 뒤에도 IO ReleaseHandle이 반환된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Record_유효성_실패_ReleaseHandle_반환()
        {
            var release = 0;
            var reader = new TestReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Succeed
                (
                    new TestRecord
                    {
                        Name = ""
                    },

                    new CallbackReleaseHandle(() => release++)
                )
            };

            var record = new RecordPersistence<string, TestRecord>
            (
                reader, new TestWriter<TestRecord>(), ValidateRecord
            );

            var response = record.Load("unused");

            Assert.IsFalse(response.Success);
            Assert.AreEqual(1, release);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// IO 읽기 실패와 ReleaseHandle 정리 실패가 겹치면 두 예외를 모두 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void Record_읽기와_ReleaseHandle_정리_동시_실패()
        {
            var readError = new IOException("read failed");
            var cleanupError = new InvalidOperationException("release failed");
            var reader = new TestReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Fail
                (
                    readError.Message,
                    readError,
                    new CallbackReleaseHandle(() => throw cleanupError)
                )
            };

            var record = new RecordPersistence<string, TestRecord>
            (
                reader, new TestWriter<TestRecord>(), ValidateRecord
            );

            var response = record.Load("unused");

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);

            var combined = (AggregateException)response.Exception;
            Assert.AreSame(readError, combined.InnerExceptions[0]);
            Assert.AreSame(cleanupError, combined.InnerExceptions[1]);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 응답이 원래 실패한 경우에도 ReleaseHandle 정리 오류가 누락되지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void Record_문자열_실패와_ReleaseHandle_정리_동시_실패()
        {
            var reader = new TestReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Fail
                (
                    "source failed",
                    null,
                    new CallbackReleaseHandle(() => throw new IOException("release failed"))
                )
            };

            var record = new RecordPersistence<string, TestRecord>
            (
                reader, new TestWriter<TestRecord>(), ValidateRecord
            );

            var response = record.Load("unused");

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            StringAssert.Contains("source failed", response.Error);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// IO ReleaseHandle 종료 실패는 읽기 실패로 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Record_성공_ReleaseHandle_정리_실패()
        {
            var failure = new IOException("release failed");
            var reader = new TestReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Succeed
                (
                    new TestRecord
                    {
                        Name = "source"
                    },
                    new CallbackReleaseHandle(() => throw failure)
                )
            };
            var record = new RecordPersistence<string, TestRecord>
            (
                reader, new TestWriter<TestRecord>(), ValidateRecord
            );

            var response = record.Load("unused");

            Assert.IsFalse(response.Success);
            Assert.AreSame(failure, response.Exception);
            Assert.IsNull(response.ReleaseHandle);
        }

    #endregion

    #region P-2: 비동기 Record의 기록·검증·취소

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Async Mapping과 기록 검증 경계를 조합한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_변환과_검증_성공()
        {
            var serializer = UnityJsonSerializer.Default;
            var readSource = new TestAsyncReader<string>
            {
                Response = ReadResponse<string>.Succeed("{\"Name\":\"async\",\"Value\":5}")
            };

            var writeSource = new TestAsyncWriter<string>();
            var reader = new AsyncMappedDataReader<string, string, TestRecord>
            (
                readSource, serializer.Deserialize<TestRecord>
            );
            var writer = new AsyncMappedDataWriter<string, TestRecord, string>
            (
                writeSource, serializer.Serialize
            );
            var record = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, writer, ValidateRecord
            );

            var loaded = await record.LoadAsync("unused");
            var saved = await record.SaveAsync
            (
                "unused", new TestRecord
                {
                    Name = "new",
                    Value = 5
                }
            );

            Assert.IsTrue(loaded.Success, loaded.Error);
            Assert.AreEqual("async", loaded.Value.Name);
            Assert.IsTrue(saved.Success, saved.Error);
            StringAssert.Contains("new", writeSource.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 비동기 Mapping 핸들을 검증 완료까지 유지한다.
        /// <br/> 반환 전 원본 핸들을 한 번만 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_Mapping_검증_후_원본_ReleaseHandle_종료()
        {
            var released = 0;
            var source = new TestAsyncReader<string>
            {
                Response = ReadResponse<string>.Succeed
                (
                    "async",
                    new CallbackReleaseHandle(() => released++)
                )
            };
            var reader = new AsyncMappedDataReader<string, string, TestRecord>
            (
                source,
                value => new TestRecord
                {
                    Name = value,
                    Value = 17
                }
            );
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader,
                new TestAsyncWriter<TestRecord>(),
                record =>
                {
                    Assert.AreEqual(0, released);
                    ValidateRecord(record);
                }
            );

            var response = await persistence.LoadAsync("unused");

            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual(1, source.Count);
            Assert.AreEqual(1, released);
            Assert.IsNull(response.ReleaseHandle);
            Assert.AreEqual("async", response.Value.Name);
            Assert.AreEqual(17, response.Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 비동기 Reader 요청 이전에 취소되면 IO를 시작하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_사전_취소()
        {
            var reader = new TestAsyncReader<TestRecord>();
            var writer = new TestAsyncWriter<TestRecord>();
            var record = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, writer, ValidateRecord
            );

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var read = await record.LoadAsync("unused", cancellation.Token);
            var write = await record.SaveAsync
            (
                "unused",
                new TestRecord
                {
                    Name = "ok"
                },
                cancellation.Token
            );

            Assert.IsFalse(read.Success);
            Assert.IsInstanceOf<OperationCanceledException>(read.Exception);
            Assert.IsFalse(write.Success);
            Assert.IsInstanceOf<OperationCanceledException>(write.Exception);
            Assert.AreEqual(0, reader.Count);
            Assert.AreEqual(0, writer.Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 비동기 읽기 완료 직후 취소되어도 원본 ReleaseHandle은 정확히 한 번 종료한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_읽기_후_취소_ReleaseHandle_반환()
        {
            using var cancellation = new CancellationTokenSource();
            var released = 0;
            var reader = new TestAsyncReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Succeed
                (
                    new TestRecord
                    {
                        Name = "source"
                    },
                    new CallbackReleaseHandle(() => released++)
                ),
                OnRead = () => cancellation.Cancel()
            };

            var record = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>(), ValidateRecord
            );

            var response = await record.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<OperationCanceledException>(response.Exception);
            Assert.AreEqual(1, released);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 검증 중 취소되면 읽기 성공 대신 취소 실패를 반환하고 원본 수명을 종료한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_검증_중_취소_ReleaseHandle_회수()
        {
            using var cancellation = new CancellationTokenSource();
            var released = 0;
            var reader = new TestAsyncReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Succeed
                (
                    new TestRecord
                    {
                        Name = "source"
                    },
                    new CallbackReleaseHandle(() => released++)
                )
            };
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader,
                new TestAsyncWriter<TestRecord>(),
                record => cancellation.Cancel()
            );

            var response = await persistence.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<OperationCanceledException>(response.Exception);
            Assert.AreEqual(1, released);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 기록 검증 오류와 요청 취소가 겹치면 두 원인을 모두 보존한다.
        /// <br/> 검증에 앞서 획득한 원본 수명은 정확히 한 번 종료한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_검증_오류와_취소_동시발생_원인보존()
        {
            using var cancellation = new CancellationTokenSource();
            var released = 0;
            var validationError = new FormatException("invalid record");
            var reader = new TestAsyncReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Succeed
                (
                    new TestRecord
                    {
                        Name = "source"
                    },
                    new CallbackReleaseHandle(() => released++)
                )
            };
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader,
                new TestAsyncWriter<TestRecord>(),
                record =>
                {
                    cancellation.Cancel();
                    throw validationError;
                }
            );

            var response = await persistence.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            var reasons = ((AggregateException)response.Exception).Flatten().InnerExceptions;
            Assert.AreSame(validationError, reasons[0]);
            Assert.AreEqual
            (
                1,
                reasons.Count
                (
                    failure => failure is OperationCanceledException cancelled &&
                               cancelled.CancellationToken == cancellation.Token
                )
            );
            Assert.AreEqual(1, released);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 비동기 읽기의 원본 오류와 ReleaseHandle 정리 오류를 함께 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_읽기와_ReleaseHandle_정리_동시_실패()
        {
            var readError = new IOException("async read failed");
            var cleanupError = new IOException("release failed");
            var reader = new TestAsyncReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Fail
                (
                    readError.Message,
                    readError,
                    new CallbackReleaseHandle(() => throw cleanupError)
                )
            };

            var record = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>(), ValidateRecord
            );

            var response = await record.LoadAsync("unused");

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);

            var combined = (AggregateException)response.Exception;
            Assert.AreSame(readError, combined.InnerExceptions[0]);
            Assert.AreSame(cleanupError, combined.InnerExceptions[1]);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 비동기 IO ReleaseHandle 종료 실패는 읽기 실패로 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_성공_ReleaseHandle_정리_실패()
        {
            var failure = new IOException("release failed");
            var reader = new TestAsyncReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Succeed
                (
                    new TestRecord
                    {
                        Name = "source"
                    },
                    new CallbackReleaseHandle(() => throw failure)
                )
            };
            var record = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>(), ValidateRecord
            );

            var response = await record.LoadAsync("unused");

            Assert.IsFalse(response.Success);
            Assert.AreSame(failure, response.Exception);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 비동기 Reader가 실패한 직후 취소되어도 원본 읽기 오류를 보존한다.
        /// <br/> 응답의 ReleaseHandle은 정확히 한 번 종료한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_읽기_실패와_취소_원본오류_보존()
        {
            using var cancellation = new CancellationTokenSource();
            var released = 0;
            var readError = new IOException("original read failed");
            var reader = new TestAsyncReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Fail
                (
                    readError.Message,
                    readError,
                    new CallbackReleaseHandle(() => released++)
                ),
                OnRead = () => cancellation.Cancel()
            };
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>()
            );

            var response = await persistence.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            var combined = (AggregateException)response.Exception;
            Assert.AreSame(readError, combined.InnerExceptions[0]);
            Assert.IsInstanceOf<OperationCanceledException>(combined.InnerExceptions[1]);
            Assert.AreEqual(1, released);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 비동기 읽기 실패·취소·해제 실패가 함께 발생하면 모든 원인을 보존한다.
        /// <br/> 정리 콜백은 실패하더라도 한 번만 실행한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_읽기_취소_해제_동시실패_원인보존()
        {
            using var cancellation = new CancellationTokenSource();
            var released = 0;
            var readError = new IOException("read failed");
            var releaseError = new IOException("release failed");
            var reader = new TestAsyncReader<TestRecord>
            {
                Response = ReadResponse<TestRecord>.Fail
                (
                    readError.Message,
                    readError,
                    new CallbackReleaseHandle
                    (
                        () =>
                        {
                            released++;
                            throw releaseError;
                        }
                    )
                ),
                OnRead = () => cancellation.Cancel()
            };
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>()
            );

            var response = await persistence.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);

            var combined = ((AggregateException)response.Exception).Flatten();
            var hasReadError = false;
            var hasCancellation = false;
            var hasReleaseError = false;

            foreach (var failure in combined.InnerExceptions)
            {
                hasReadError |= ReferenceEquals(failure, readError);
                hasCancellation |= failure is OperationCanceledException;
                hasReleaseError |= ReferenceEquals(failure, releaseError);
            }

            Assert.IsTrue(hasReadError);
            Assert.IsTrue(hasCancellation);
            Assert.IsTrue(hasReleaseError);
            Assert.AreEqual(1, released);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// IO Mapping이 보존한 원본 읽기 오류는 Persistence에서도 소실되지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_매핑_읽기실패와_취소_원본오류_보존()
        {
            using var cancellation = new CancellationTokenSource();
            var released = 0;
            var readError = new IOException("mapped input failed");
            var sourceReader = new TestAsyncReader<string>
            {
                Response = ReadResponse<string>.Fail
                (
                    readError.Message,
                    readError,
                    new CallbackReleaseHandle(() => released++)
                ),
                OnRead = () => cancellation.Cancel()
            };
            var reader = new AsyncMappedDataReader<string, string, TestRecord>
            (
                sourceReader, UnityJsonSerializer.Default.Deserialize<TestRecord>
            );
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>()
            );

            var response = await persistence.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);

            var combined = ((AggregateException)response.Exception).Flatten();
            var hasReadError = false;
            var hasCancellation = false;
            var cancellationCount = 0;

            foreach (var failure in combined.InnerExceptions)
            {
                hasReadError |= ReferenceEquals(failure, readError);
                hasCancellation |= failure is OperationCanceledException;
                if (failure is OperationCanceledException)
                {
                    cancellationCount++;
                }
            }

            Assert.IsTrue(hasReadError);
            Assert.IsTrue(hasCancellation);
            Assert.AreEqual(1, cancellationCount);
            Assert.AreEqual(1, released);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Reader가 응답 대신 예외를 던진 때에도 원본 실패와 취소를 함께 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_Reader_예외와_취소_동시발생_원인보존()
        {
            using var cancellation = new CancellationTokenSource();
            var error = new IOException("read threw");
            var reader = new TestAsyncReader<TestRecord>
            {
                OnRead = () =>
                {
                    cancellation.Cancel();
                    throw error;
                }
            };
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>()
            );

            var response = await persistence.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            var reasons = ((AggregateException)response.Exception).Flatten().InnerExceptions;
            Assert.AreSame(error, reasons[0]);
            Assert.AreEqual(1, reasons.Count(reason => reason is OperationCanceledException));
            Assert.AreEqual(1, reader.Count);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 원본 Reader가 취소 예외를 직접 던졌다면 별도의 취소 원인을 추가하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_Reader_취소예외_중복없음()
        {
            using var cancellation = new CancellationTokenSource();
            var cancelled = new OperationCanceledException(cancellation.Token);
            var reader = new TestAsyncReader<TestRecord>
            {
                OnRead = () =>
                {
                    cancellation.Cancel();
                    throw cancelled;
                }
            };
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>()
            );

            var response = await persistence.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.AreSame(cancelled, response.Exception);
            Assert.AreEqual(1, reader.Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Mapping Reader의 직접 예외와 취소가 Persistence를 거쳐도 중복되지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_매핑Reader_예외와_취소_원인보존()
        {
            using var cancellation = new CancellationTokenSource();
            var error = new IOException("mapping source threw");
            var source = new TestAsyncReader<string>
            {
                OnRead = () =>
                {
                    cancellation.Cancel();
                    throw error;
                }
            };
            var mapped = new AsyncMappedDataReader<string, string, TestRecord>
            (
                source, UnityJsonSerializer.Default.Deserialize<TestRecord>
            );
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                mapped, new TestAsyncWriter<TestRecord>()
            );

            var response = await persistence.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            var reasons = ((AggregateException)response.Exception).Flatten().InnerExceptions;
            Assert.AreEqual(1, reasons.Count(reason => ReferenceEquals(reason, error)));
            Assert.AreEqual(1, reasons.Count(reason => reason is OperationCanceledException));
            Assert.AreEqual(1, source.Count);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 읽기 예외의 내부에 이미 취소가 포함됐으면 취소 원인을 중복 추가하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_중첩_취소원인_중복없음()
        {
            using var cancellation = new CancellationTokenSource();
            var cancelled = new OperationCanceledException(cancellation.Token);
            var readError = new AggregateException
            (
                "nested IO failure",
                new IOException("wrapped read", cancelled),
                new FormatException("invalid data")
            );
            var reader = new TestAsyncReader<TestRecord>
            {
                OnRead = () =>
                {
                    cancellation.Cancel();
                    throw readError;
                }
            };
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>()
            );

            var response = await persistence.LoadAsync("unused", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.AreSame(readError, response.Exception);
            Assert.AreEqual(1, reader.Count);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 원본의 다른 취소 토큰과 현재 기록 요청의 취소를 각각 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task AsyncRecord_다른_취소토큰의_원인_모두보존()
        {
            using var sourceCancellation = new CancellationTokenSource();
            using var requestCancellation = new CancellationTokenSource();
            sourceCancellation.Cancel();

            var sourceError = new OperationCanceledException(sourceCancellation.Token);
            var reader = new TestAsyncReader<TestRecord>
            {
                OnRead = () =>
                {
                    requestCancellation.Cancel();
                    throw sourceError;
                }
            };
            var persistence = new AsyncRecordPersistence<string, TestRecord>
            (
                reader, new TestAsyncWriter<TestRecord>()
            );

            var response = await persistence.LoadAsync("unused", requestCancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            var reasons = ((AggregateException)response.Exception).Flatten().InnerExceptions;
            Assert.AreEqual(1, reasons.Count(reason => ReferenceEquals(reason, sourceError)));
            Assert.AreEqual
            (
                1,
                reasons.Count
                (
                    reason => reason is OperationCanceledException cancelled &&
                              cancelled.CancellationToken == requestCancellation.Token
                )
            );
            Assert.AreEqual(1, reader.Count);
            Assert.IsNull(response.ReleaseHandle);
        }

    #endregion

    #region 검증

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 기록에 유효한 이름이 존재하는지 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ValidateRecord(TestRecord record)
        {
            if (string.IsNullOrEmpty(record.Name))
            {
                throw new InvalidOperationException("기록 이름이 비어 있습니다.");
            }
        }

    #endregion

    }
}
