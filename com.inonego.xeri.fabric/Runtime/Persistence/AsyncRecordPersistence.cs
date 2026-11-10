/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : AsyncRecordPersistence.cs
수정일 : 2026-11-11

# 설명
비동기 IO reader/writer에서 기록을 읽고 쓴 뒤 도메인 검증과 IO ReleaseHandle 회수를 관리한다.
타입 변환은 기존 AsyncMappedDataReader/Writer에서 수행하며 직렬화 전용 래퍼를 만들지 않는다.

# 주의
읽어 반환한 기록은 원본 IO ReleaseHandle에 의존하지 않는 독립 데이터여야 한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Threading;
using System.Threading.Tasks;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;

namespace inonego.Xeri.Persistence
{
    // ============================================================
    /// <summary>
    /// 비동기 IO를 사용하는 단일 영속 기록의 검증과 읽기 수명을 관리한다.
    /// </summary>
    /// <typeparam name="TLocation">IO 위치 타입.</typeparam>
    /// <typeparam name="TRecord">도메인이 정의한 기록 단위 타입.</typeparam>
    // ============================================================
    public sealed class AsyncRecordPersistence<TLocation, TRecord>
    {

    #region 필드

        private readonly IAsyncDataReader<TLocation, TRecord> reader = null;
        private readonly IAsyncDataWriter<TLocation, TRecord> writer = null;
        private readonly Action<TRecord> validateRecord = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 비동기 IO reader/writer와 도메인의 기록 검증 규칙을 조합한다.
        /// </summary>
        // ------------------------------------------------------------
        public AsyncRecordPersistence
        (
            IAsyncDataReader<TLocation, TRecord> reader,
            IAsyncDataWriter<TLocation, TRecord> writer,
            Action<TRecord> validateRecord = null
        )
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.validateRecord = validateRecord;
        }

    #endregion

    #region 읽기

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 기록을 비동기로 읽어 검증한 뒤 원본 IO ReleaseHandle을 종료한다.
        /// <br/> 반환 Record는 원본 리소스의 수명에 의존하지 않아야 한다.
        /// <br/> 취소 요청은 실패 응답으로 전달한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public async Task<ReadResponse<TRecord>> LoadAsync
        (
            TLocation location,
            CancellationToken cancellationToken = default
        )
        {
            ReadResponse<TRecord> response;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                response = await reader.ReadAsync(location, cancellationToken);
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested && !ContainsCancellation(exception, cancellationToken))
                {
                    // 원본 Reader가 응답 대신 예외를 던진 경우에도 취소 원인을 보존한다.
                    var cancellationError = new OperationCanceledException(cancellationToken);
                    var combined = new AggregateException
                    (
                        "기록 읽기 실패와 취소가 함께 발생했습니다.",
                        exception,
                        cancellationError
                    );

                    return ReadResponse<TRecord>.Fail(combined.Message, combined);
                }

                return ReadResponse<TRecord>.Fail(exception.Message, exception);
            }

            ReadResponse<TRecord> result;

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    var cancellationError = new OperationCanceledException(cancellationToken);

                    if (!response.Success)
                    {
                        // 읽기 실패와 취소가 겹쳐도 원본 실패 정보를 버리지 않는다.
                        var readError = response.Exception ??
                                        new InvalidOperationException(response.Error);

                        if (ContainsCancellation(readError, cancellationToken))
                        {
                            // IO Mapping이 이미 취소를 보존한 경우 같은 원인을 중복 추가하지 않는다.
                            result = ReadResponse<TRecord>.Fail(response.Error, response.Exception);
                        }
                        else
                        {
                            var combined = new AggregateException
                            (
                                "기록 읽기 실패와 취소가 함께 발생했습니다.",
                                readError,
                                cancellationError
                            );

                            result = ReadResponse<TRecord>.Fail(combined.Message, combined);
                        }
                    }
                    else
                    {
                        result = ReadResponse<TRecord>.Fail
                        (
                            cancellationError.Message,
                            cancellationError
                        );
                    }
                }
                else if (!response.Success)
                {
                    result = ReadResponse<TRecord>.Fail(response.Error, response.Exception);
                }
                else
                {
                    Validate(response.Value);
                    cancellationToken.ThrowIfCancellationRequested();
                    result = ReadResponse<TRecord>.Succeed(response.Value);
                }
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested && !ContainsCancellation(exception, cancellationToken))
                {
                    // 검증 도중 실패와 취소가 겹쳐도 두 원인을 모두 보존한다.
                    var cancellationError = new OperationCanceledException(cancellationToken);
                    var combined = new AggregateException
                    (
                        "기록 검증 실패와 취소가 함께 발생했습니다.",
                        exception,
                        cancellationError
                    );

                    result = ReadResponse<TRecord>.Fail(combined.Message, combined);
                }
                else
                {
                    result = ReadResponse<TRecord>.Fail(exception.Message, exception);
                }
            }

            try
            {
                response.ReleaseHandle?.Dispose();
            }
            catch (Exception exception)
            {
                if (!result.Success)
                {
                    // 읽기 오류와 ReleaseHandle 정리 오류를 모두 호출자에게 전달한다.
                    var readError = result.Exception ??
                                    new InvalidOperationException(result.Error);

                    var combined = new AggregateException
                    (
                        "기록 읽기와 IO ReleaseHandle 정리에 모두 실패했습니다.",
                        readError,
                        exception
                    );

                    return ReadResponse<TRecord>.Fail(combined.Message, combined);
                }

                return ReadResponse<TRecord>.Fail(exception.Message, exception);
            }

            return result;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// IO 응답의 예외에 취소 원인이 이미 포함돼 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool ContainsCancellation(Exception exception, CancellationToken cancellationToken)
        {
            if (exception == null)
            {
                return false;
            }

            if (exception is OperationCanceledException cancelled &&
                cancelled.CancellationToken == cancellationToken)
            {
                return true;
            }

            if (exception is AggregateException combined)
            {
                foreach (var failure in combined.InnerExceptions)
                {
                    if (ContainsCancellation(failure, cancellationToken))
                    {
                        return true;
                    }
                }

                return false;
            }

            return ContainsCancellation(exception.InnerException, cancellationToken);
        }

    #endregion

    #region 쓰기

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 기록을 먼저 검증한 뒤 비동기 writer에 전달한다.
        /// <br/> 호출 전 취소된 요청은 실제 쓰기를 시작하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public async Task<WriteResponse> SaveAsync
        (
            TLocation location,
            TRecord record,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                Validate(record);
                cancellationToken.ThrowIfCancellationRequested();

                return await writer.WriteAsync(location, record, cancellationToken);
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

    #endregion

    #region 검증

        // ------------------------------------------------------------
        /// <summary>
        /// null 기록을 거부하고 도메인 검증 규칙을 실행한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Validate(TRecord record)
        {
            if (record is null)
            {
                throw new InvalidOperationException("영속 기록이 null입니다.");
            }

            validateRecord?.Invoke(record);
        }

    #endregion

    }
}
