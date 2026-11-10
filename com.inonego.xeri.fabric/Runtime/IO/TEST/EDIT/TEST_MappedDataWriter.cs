/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_MappedDataWriter.cs
수정일 : 2026-11-09

# 설명
범용 IO Mapping Writer의 값 변환·실패 처리·비동기 취소를 확인한다.
RecordPersistence·문서 Session 및 파일 Commit 정책은 검증 대상이 아니다.

# 테스트 구성
 W: Writer - 동기·비동기 값 변환과 원본 오류 전달
 C: Cancellation - 비동기 기록 취소와 원본 호출 차단
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.IO.TEST
{
    // ============================================================
    /// <summary>
    /// 동기·비동기 MappedDataWriter의 범용 값 변환 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_MappedDataWriter
    {

    #region 테스트 객체

        // ============================================================
        /// <summary>
        /// 마지막으로 전달받은 값과 호출 횟수를 기록하는 Writer.
        /// </summary>
        // ============================================================
        private sealed class TestWriter : IDataWriter<string, string>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 원본 동기 Writer의 응답 또는 예외를 주입한다.
            /// </summary>
            // ------------------------------------------------------------
            public Func<WriteResponse> OnWrite;

            // ------------------------------------------------------------
            /// <summary>
            /// 쓰기 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count;

            // ------------------------------------------------------------
            /// <summary>
            /// 최종 기록 값.
            /// </summary>
            // ------------------------------------------------------------
            public string Value;

            // ------------------------------------------------------------
            /// <summary>
            /// 입력값을 보관하고 쓰기 성공을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string location, string value)
            {
                Count++;
                Value = value;
                return OnWrite?.Invoke() ?? WriteResponse.Succeed();
            }
        }

        // ============================================================
        /// <summary>
        /// 마지막으로 전달받은 값을 보관하는 비동기 Writer.
        /// </summary>
        // ============================================================
        private sealed class TestAsyncWriter : IAsyncDataWriter<string, string>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 원본 비동기 Writer의 응답 또는 예외를 주입한다.
            /// </summary>
            // ------------------------------------------------------------
            public Func<CancellationToken, Task<WriteResponse>> OnWrite;

            // ------------------------------------------------------------
            /// <summary>
            /// 쓰기 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count;

            // ------------------------------------------------------------
            /// <summary>
            /// 최종 기록 값.
            /// </summary>
            // ------------------------------------------------------------
            public string Value;

            // ------------------------------------------------------------
            /// <summary>
            /// 입력값을 보관하고 쓰기 성공을 비동기로 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public Task<WriteResponse> WriteAsync
            (
                string location,
                string value,
                CancellationToken cancellationToken = default
            )
            {
                Count++;
                Value = value;
                return OnWrite?.Invoke(cancellationToken) ?? Task.FromResult(WriteResponse.Succeed());
            }
        }

    #endregion

    #region W-1: 동기 Writer 변환·실패 응답

        // ------------------------------------------------------------
        /// <summary>
        /// 변환된 값만 원본 Writer로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_MappedDataWriter_변환한_값_기록()
        {
            var source = new TestWriter();
            var writer = new MappedDataWriter<string, int, string>
            (
                source,
                value => value.ToString()
            );

            var response = writer.Write("unused", 42);

            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual(1, source.Count);
            Assert.AreEqual("42", source.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 변환 실패 시 원본 Writer를 호출하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_MappedDataWriter_변환_실패_기록_차단()
        {
            var source = new TestWriter();
            var writer = new MappedDataWriter<string, int, string>
            (
                source,
                value => throw new InvalidOperationException("변환 실패")
            );

            var response = writer.Write("unused", 42);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<InvalidOperationException>(response.Exception);
            Assert.AreEqual(0, source.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 원본 Writer가 예외를 던져도 실패 응답으로 정규화한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_MappedDataWriter_원본_예외는_실패_응답()
        {
            var failure = new InvalidOperationException("source writer error");
            var source = new TestWriter
            {
                OnWrite = () => throw failure
            };
            var writer = new MappedDataWriter<string, int, string>
            (
                source,
                value => value.ToString()
            );

            var response = writer.Write("unused", 42);

            Assert.IsFalse(response.Success);
            Assert.AreSame(failure, response.Exception);
            Assert.AreEqual(1, source.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 원본 Writer가 반환한 실패 응답을 그대로 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_MappedDataWriter_원본_실패_응답_전달()
        {
            var failure = new IOException("source write failed");
            var source = new TestWriter
            {
                OnWrite = () => WriteResponse.Fail(failure.Message, failure)
            };
            var writer = new MappedDataWriter<string, int, string>
            (
                source,
                value => value.ToString()
            );

            var response = writer.Write("unused", 42);

            Assert.IsFalse(response.Success);
            Assert.AreSame(failure, response.Exception);
            Assert.AreEqual(1, source.Count);
        }

    #endregion

    #region W-2: 비동기 Writer 정상 변환

        // ------------------------------------------------------------
        /// <summary>
        /// 비동기 Writer에 변환된 값을 전달한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataWriter_변환한_값_기록()
        {
            var source = new TestAsyncWriter();
            var writer = new AsyncMappedDataWriter<string, int, string>
            (
                source,
                value => value.ToString()
            );

            var response = await writer.WriteAsync("unused", 42);

            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual(1, source.Count);
            Assert.AreEqual("42", source.Value);
        }

    #endregion

    #region W-3: 비동기 Writer 변환·원본 실패 처리

        // ------------------------------------------------------------
        /// <summary>
        /// 비동기 변환이 실패하면 원본 Writer를 호출하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataWriter_변환_실패_기록_차단()
        {
            var source = new TestAsyncWriter();
            var failure = new FormatException("invalid mapping");
            var writer = new AsyncMappedDataWriter<string, int, string>
            (
                source,
                value => throw failure
            );

            var response = await writer.WriteAsync("unused", 42);

            Assert.IsFalse(response.Success);
            Assert.AreSame(failure, response.Exception);
            Assert.AreEqual(0, source.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 원본 Writer의 비동기 예외는 실패 응답으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataWriter_원본_예외는_실패_응답()
        {
            var failure = new InvalidOperationException("source writer error");
            var source = new TestAsyncWriter
            {
                OnWrite = token => Task.FromException<WriteResponse>(failure)
            };
            var writer = new AsyncMappedDataWriter<string, int, string>
            (
                source,
                value => value.ToString()
            );

            var response = await writer.WriteAsync("unused", 42);

            Assert.IsFalse(response.Success);
            Assert.AreSame(failure, response.Exception);
            Assert.AreEqual(1, source.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 원본 Writer가 반환한 실패 응답의 원인을 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataWriter_원본_실패_응답_전달()
        {
            var failure = new IOException("source write failed");
            var source = new TestAsyncWriter
            {
                OnWrite = token => Task.FromResult(WriteResponse.Fail(failure.Message, failure))
            };
            var writer = new AsyncMappedDataWriter<string, int, string>
            (
                source,
                value => value.ToString()
            );

            var response = await writer.WriteAsync("unused", 42);

            Assert.IsFalse(response.Success);
            Assert.AreSame(failure, response.Exception);
            Assert.AreEqual(1, source.Count);
        }

    #endregion

    #region C-1: 비동기 취소 시 원본 기록 차단

        // ------------------------------------------------------------
        /// <summary>
        /// 취소된 요청은 원본 비동기 Writer를 호출하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataWriter_사전_취소_기록_차단()
        {
            var source = new TestAsyncWriter();
            var writer = new AsyncMappedDataWriter<string, int, string>
            (
                source,
                value => value.ToString()
            );

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var response = await writer.WriteAsync("unused", 42, cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<OperationCanceledException>(response.Exception);
            Assert.AreEqual(0, source.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 값 변환 중 요청이 취소되면 원본 Writer를 호출하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataWriter_변환_도중_취소_기록_차단()
        {
            var source = new TestAsyncWriter();
            using var cancellation = new CancellationTokenSource();
            var writer = new AsyncMappedDataWriter<string, int, string>
            (
                source,
                value =>
                {
                    cancellation.Cancel();
                    return value.ToString();
                }
            );

            var response = await writer.WriteAsync("unused", 42, cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<OperationCanceledException>(response.Exception);
            Assert.AreEqual(0, source.Count);
        }

    #endregion

    }
}
