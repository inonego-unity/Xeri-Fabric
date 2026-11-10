/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_MappedDataReader.cs
수정일 : 2026-11-10

# 설명
동기·비동기 IO Mapping Reader의 원본 예외, 값 변환, 취소와 ReleaseHandle 소유권 전이를 검증한다.

# 테스트 구성
 R: Reader - 동기·비동기 변환, 실패 응답과 수명 전달
 C: Cancellation - 비동기 취소 시 수명 종료와 오류 보존
========================================================================= BLOCK_HEADER_END */

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.IO.TEST
{

    // ============================================================
    /// <summary>
    /// MappedDataReader의 값 변환과 수명 전달을 검증합니다.
    /// </summary>
    // ============================================================
    public sealed class TEST_MappedDataReader
    {

    #region 테스트 객체

        private sealed class TestReader : IDataReader<string, string>
        {
            private readonly Func<ReadResponse<string>> read;

            // ------------------------------------------------------------
            /// <summary>
            /// 동기 Reader의 호출 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정한 응답 함수를 사용하는 동기 테스트 Reader를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestReader(Func<ReadResponse<string>> read)
            {
                this.read = read;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 호출 횟수를 기록하고 준비된 동기 읽기 응답을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadResponse<string> Read(string location)
            {
                Count++;
                return read();
            }
        }

        private sealed class TestAsyncReader : IAsyncDataReader<string, string>
        {
            private readonly Func<CancellationToken, Task<ReadResponse<string>>> read;

            // ------------------------------------------------------------
            /// <summary>
            /// 비동기 Reader의 호출 횟수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정한 응답 함수를 사용하는 비동기 테스트 Reader를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestAsyncReader(Func<CancellationToken, Task<ReadResponse<string>>> read)
            {
                this.read = read;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 요청 취소 토큰을 전달하고 준비된 비동기 응답을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public Task<ReadResponse<string>> ReadAsync
            (
                string location,
                CancellationToken cancellationToken = default
            )
            {
                Count++;
                return read(cancellationToken);
            }
        }

    #endregion

    #region R-1: 동기 Reader 변환·실패·수명 전달

        // ------------------------------------------------------------
        /// <summary>
        /// 원본 Reader의 예외가 실패 응답으로 변환되는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_MappedDataReader_원본_예외는_실패_응답()
        {
            var error = new InvalidOperationException("read failed");
            var source = new TestReader(() => throw error);
            var reader = new MappedDataReader<string, string, int>(source, value => value.Length);

            var response = reader.Read("source");

            Assert.IsFalse(response.Success);
            Assert.AreSame(error, response.Exception);
            Assert.AreEqual(1, source.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 정상 값 변환 후 원본 ReleaseHandle이 전달되는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_MappedDataReader_정상_변환은_원본_ReleaseHandle_전달()
        {
            var releases = 0;
            var handle = new CallbackReleaseHandle(() => releases++);
            var source = new TestReader(() => ReadResponse<string>.Succeed("value", handle));
            var reader = new MappedDataReader<string, string, int>(source, value => value.Length);

            var response = reader.Read("source");

            Assert.IsTrue(response.Success);
            Assert.AreEqual(5, response.Value);
            Assert.AreSame(handle, response.ReleaseHandle);
            Assert.IsFalse(handle.IsReleased);

            response.ReleaseHandle.Dispose();
            Assert.IsTrue(handle.IsReleased);
            Assert.AreEqual(1, releases);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 값 변환에 실패해도 원본 ReleaseHandle을 전달하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_MappedDataReader_변환_실패는_원본_ReleaseHandle_전달()
        {
            var failure = new FormatException("invalid mapping");
            var releases = 0;
            var handle = new CallbackReleaseHandle(() => releases++);
            var source = new TestReader(() => ReadResponse<string>.Succeed("value", handle));
            var reader = new MappedDataReader<string, string, int>
            (
                source,
                value => throw failure
            );

            var response = reader.Read("source");

            Assert.IsFalse(response.Success);
            Assert.AreSame(failure, response.Exception);
            Assert.AreSame(handle, response.ReleaseHandle);
            Assert.IsFalse(handle.IsReleased);

            response.ReleaseHandle.Dispose();
            Assert.AreEqual(1, releases);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 원본 실패 응답의 예외와 ReleaseHandle을 보존하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_MappedDataReader_원본_실패_ReleaseHandle과_원인_보존()
        {
            var error = new InvalidOperationException("source failure");
            var handle = new CallbackReleaseHandle(() => { });
            var source = new TestReader(() => ReadResponse<string>.Fail(error.Message, error, handle));
            var reader = new MappedDataReader<string, string, int>(source, value => value.Length);

            var response = reader.Read("source");

            Assert.IsFalse(response.Success);
            Assert.AreSame(error, response.Exception);
            Assert.AreSame(handle, response.ReleaseHandle);
            Assert.IsFalse(handle.IsReleased);
            response.ReleaseHandle.Dispose();
            Assert.IsTrue(handle.IsReleased);
        }

    #endregion

    #region R-2: 비동기 Reader 원본 예외 전달

        // ------------------------------------------------------------
        /// <summary>
        /// 비동기 원본 Reader의 예외가 실패 응답으로 변환되는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_원본_예외는_실패_응답()
        {
            var error = new InvalidOperationException("source failure");
            var source = new TestAsyncReader
            (
                token => Task.FromException<ReadResponse<string>>(error)
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);

            var response = await reader.ReadAsync("source");

            Assert.IsFalse(response.Success);
            Assert.AreSame(error, response.Exception);
            Assert.AreEqual(1, source.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 원본 Reader의 예외와 동시에 발생한 취소를 모두 실패 응답에 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_원본_예외와_취소_동시발생_원인보존()
        {
            using var cancellation = new CancellationTokenSource();
            var error = new InvalidOperationException("source threw");
            var source = new TestAsyncReader
            (
                token =>
                {
                    cancellation.Cancel();
                    return Task.FromException<ReadResponse<string>>(error);
                }
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            var reasons = ((AggregateException)response.Exception).Flatten().InnerExceptions;
            Assert.IsTrue(reasons.Contains(error));
            Assert.AreEqual(1, reasons.Count(reason => reason is OperationCanceledException));
            Assert.AreEqual(1, source.Count);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 원본 Reader가 취소 예외를 직접 던졌다면 별도의 취소 원인을 추가하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_원본_취소예외_중복없음()
        {
            using var cancellation = new CancellationTokenSource();
            var cancelled = new OperationCanceledException(cancellation.Token);
            var source = new TestAsyncReader
            (
                token =>
                {
                    cancellation.Cancel();
                    return Task.FromException<ReadResponse<string>>(cancelled);
                }
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.AreSame(cancelled, response.Exception);
            Assert.AreEqual(1, source.Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이미 취소 원인을 포함한 실패 응답은 정리 시 중복된 취소를 만들지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_실패응답의_기존취소원인_중복없음()
        {
            using var cancellation = new CancellationTokenSource();
            var readError = new InvalidOperationException("read failed");
            var cancelled = new OperationCanceledException(cancellation.Token);
            var combined = new AggregateException("source failed", readError, cancelled);
            var released = 0;
            var source = new TestAsyncReader
            (
                token =>
                {
                    cancellation.Cancel();
                    return Task.FromResult
                    (
                        ReadResponse<string>.Fail
                        (
                            combined.Message,
                            combined,
                            new CallbackReleaseHandle(() => released++)
                        )
                    );
                }
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.AreSame(combined, response.Exception);
            Assert.AreEqual(1, released);
            Assert.IsNull(response.ReleaseHandle);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 중첩 예외 내부에 이미 취소가 있으면 두 번째 취소 원인을 만들지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_중첩취소예외_중복없음()
        {
            using var cancellation = new CancellationTokenSource();
            var cancelled = new OperationCanceledException(cancellation.Token);
            var sourceError = new AggregateException
            (
                "nested source failure",
                new InvalidOperationException("wrapped", cancelled),
                new FormatException("invalid source")
            );
            var source = new TestAsyncReader
            (
                token =>
                {
                    cancellation.Cancel();
                    return Task.FromException<ReadResponse<string>>(sourceError);
                }
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.AreSame(sourceError, response.Exception);
            Assert.AreEqual(1, source.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 원본의 다른 취소 토큰은 현재 요청의 취소 원인으로 취급하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_다른_취소토큰의_원인_모두보존()
        {
            using var sourceCancellation = new CancellationTokenSource();
            using var requestCancellation = new CancellationTokenSource();
            sourceCancellation.Cancel();

            var sourceError = new OperationCanceledException(sourceCancellation.Token);
            var source = new TestAsyncReader
            (
                token =>
                {
                    requestCancellation.Cancel();
                    return Task.FromException<ReadResponse<string>>(sourceError);
                }
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);

            var response = await reader.ReadAsync("source", requestCancellation.Token);

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
        }

    #endregion

    #region R-3: 비동기 Reader 변환과 ReleaseHandle 전달

        // ------------------------------------------------------------
        /// <summary>
        /// 비동기 정상 변환에서 원본 ReleaseHandle을 전달하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_정상_변환은_원본_ReleaseHandle_전달()
        {
            var handle = new CallbackReleaseHandle(() => { });
            var source = new TestAsyncReader
            (
                token => Task.FromResult(ReadResponse<string>.Succeed("value", handle))
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);

            var response = await reader.ReadAsync("source");

            Assert.IsTrue(response.Success);
            Assert.AreEqual(5, response.Value);
            Assert.AreSame(handle, response.ReleaseHandle);
            Assert.IsFalse(handle.IsReleased);
            response.ReleaseHandle.Dispose();
            Assert.IsTrue(handle.IsReleased);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 비동기 변환 실패에서도 원본 ReleaseHandle을 전달하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_변환_실패는_ReleaseHandle_전달()
        {
            var failure = new FormatException("invalid value");
            var handle = new CallbackReleaseHandle(() => { });
            var source = new TestAsyncReader
            (
                token => Task.FromResult(ReadResponse<string>.Succeed("value", handle))
            );
            var reader = new AsyncMappedDataReader<string, string, int>
            (
                source,
                value => throw failure
            );

            var response = await reader.ReadAsync("source");

            Assert.IsFalse(response.Success);
            Assert.AreSame(failure, response.Exception);
            Assert.AreSame(handle, response.ReleaseHandle);
            Assert.IsFalse(handle.IsReleased);
            response.ReleaseHandle.Dispose();
            Assert.IsTrue(handle.IsReleased);
        }

    #endregion

    #region C-1: 비동기 사전·응답 후 취소와 ReleaseHandle 종료

        // ------------------------------------------------------------
        /// <summary>
        /// 사전 취소된 요청은 원본 Reader를 호출하지 않는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_사전_취소는_원본_호출하지_않음()
        {
            var source = new TestAsyncReader
            (
                token => Task.FromResult(ReadResponse<string>.Succeed("value"))
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<OperationCanceledException>(response.Exception);
            Assert.AreEqual(0, source.Count);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 응답 직후 취소 시 값 변환 없이 ReleaseHandle을 해제하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_응답_후_취소는_ReleaseHandle_해제와_실패_응답()
        {
            using var cancellation = new CancellationTokenSource();
            var releaseCount = 0;
            var mapCount = 0;
            var handle = new CallbackReleaseHandle(() => releaseCount++);
            var source = new TestAsyncReader
            (
                token =>
                {
                    cancellation.Cancel();
                    return Task.FromResult(ReadResponse<string>.Succeed("value", handle));
                }
            );
            var reader = new AsyncMappedDataReader<string, string, int>
            (
                source,
                value =>
                {
                    mapCount++;
                    return value.Length;
                }
            );

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<OperationCanceledException>(response.Exception);
            Assert.IsNull(response.ReleaseHandle);
            Assert.IsTrue(handle.IsReleased);
            Assert.AreEqual(1, releaseCount);
            Assert.AreEqual(0, mapCount);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 취소와 ReleaseHandle 정리 실패의 원인을 함께 보존하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_취소와_ReleaseHandle_해제_실패를_모두_보존()
        {
            using var cancellation = new CancellationTokenSource();
            var releaseFailure = new InvalidOperationException("release failed");
            var handle = new CallbackReleaseHandle(() => throw releaseFailure);
            var source = new TestAsyncReader
            (
                token =>
                {
                    cancellation.Cancel();
                    return Task.FromResult(ReadResponse<string>.Succeed("value", handle));
                }
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsTrue(handle.IsReleased);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            var failures = ((AggregateException)response.Exception).Flatten().InnerExceptions;
            Assert.IsTrue(failures.Contains(releaseFailure));
            Assert.IsTrue(failures.Any(failure => failure is OperationCanceledException));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 원본 읽기 실패와 취소가 겹칠 때 원인을 모두 보존하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_원본_실패와_취소를_모두_보존()
        {
            using var cancellation = new CancellationTokenSource();
            var failure = new InvalidOperationException("source failed");
            var handle = new CallbackReleaseHandle(() => { });
            var source = new TestAsyncReader
            (
                token =>
                {
                    cancellation.Cancel();
                    return Task.FromResult(ReadResponse<string>.Fail(failure.Message, failure, handle));
                }
            );
            var reader = new AsyncMappedDataReader<string, string, int>(source, value => value.Length);

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsTrue(handle.IsReleased);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            var failures = ((AggregateException)response.Exception).Flatten().InnerExceptions;
            Assert.IsTrue(failures.Contains(failure));
            Assert.IsTrue(failures.Any(error => error is OperationCanceledException));
        }

    #endregion

    #region C-2: 비동기 변환 중 취소와 원인 보존

        // ------------------------------------------------------------
        /// <summary>
        /// 변환 중 취소되면 원본 ReleaseHandle을 해제하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_변환_도중_취소는_ReleaseHandle_해제()
        {
            using var cancellation = new CancellationTokenSource();
            var releases = 0;
            var handle = new CallbackReleaseHandle(() => releases++);
            var source = new TestAsyncReader
            (
                token => Task.FromResult(ReadResponse<string>.Succeed("value", handle))
            );
            var reader = new AsyncMappedDataReader<string, string, int>
            (
                source,
                value =>
                {
                    cancellation.Cancel();
                    return value.Length;
                }
            );

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<OperationCanceledException>(response.Exception);
            Assert.IsNull(response.ReleaseHandle);
            Assert.IsTrue(handle.IsReleased);
            Assert.AreEqual(1, releases);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 변환 예외와 취소가 겹칠 때 두 원인을 모두 보존하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AsyncMappedDataReader_변환_오류와_취소를_모두_보존()
        {
            using var cancellation = new CancellationTokenSource();
            var failure = new FormatException("mapping failed");
            var handle = new CallbackReleaseHandle(() => { });
            var source = new TestAsyncReader
            (
                token => Task.FromResult(ReadResponse<string>.Succeed("value", handle))
            );
            var reader = new AsyncMappedDataReader<string, string, int>
            (
                source,
                value =>
                {
                    cancellation.Cancel();
                    throw failure;
                }
            );

            var response = await reader.ReadAsync("source", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsNull(response.ReleaseHandle);
            Assert.IsTrue(handle.IsReleased);
            Assert.IsInstanceOf<AggregateException>(response.Exception);
            var errors = ((AggregateException)response.Exception).Flatten().InnerExceptions;
            Assert.IsTrue(errors.Contains(failure));
            Assert.IsTrue(errors.Any(error => error is OperationCanceledException));
        }

    #endregion

    }
}
