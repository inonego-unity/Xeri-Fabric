/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : AsyncMappedDataReader.cs
수정일 : 2026-11-10

# 설명
원본 비동기 Reader를 값 변환과 조합하고 예외·취소를 읽기 실패 응답으로 전달한다.
Source 응답 후 취소 시 ReleaseHandle을 종료하고 읽기·취소·해제 실패가 겹치면 원인을 보존한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Threading;
using System.Threading.Tasks;

namespace inonego.Xeri.IO
{
    // ================================================================================
    /// <summary>
    /// <br/> async source reader가 읽은 response value를 지정한 변환 함수로 변환해
    /// <br/> 반환하는 reader adapter.
    /// </summary>
    // ================================================================================
    [Serializable]
    public sealed class AsyncMappedDataReader<TLocation, TSource, TValue> : IAsyncDataReader<TLocation, TValue>
    {

    #region 필드

        private readonly IAsyncDataReader<TLocation, TSource> sourceReader = null;
        private readonly Func<TSource, TValue> map = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// AsyncMappedDataReader를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public AsyncMappedDataReader
        (
            IAsyncDataReader<TLocation, TSource> sourceReader,
            Func<TSource, TValue> map
        )
        {
            this.sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
            this.map = map ?? throw new ArgumentNullException(nameof(map));
        }

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// async source reader에서 read response를 받은 뒤 value를 변환해 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public async Task<ReadResponse<TValue>> ReadAsync
        (
            TLocation location,
            CancellationToken cancellationToken = default
        )
        {
            ReadResponse<TSource> sourceResponse;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                sourceResponse = await sourceReader.ReadAsync(location, cancellationToken);
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested && !ContainsCancellation(exception, cancellationToken))
                {
                    // 원본 Reader가 응답 대신 예외를 던져도 취소 원인을 잃지 않는다.
                    var cancellationError = new OperationCanceledException(cancellationToken);
                    var combined = new AggregateException
                    (
                        "원본 IO 읽기 실패와 취소가 함께 발생했습니다.",
                        exception,
                        cancellationError
                    );

                    return ReadResponse<TValue>.Fail(combined.Message, combined);
                }

                return ReadResponse<TValue>.Fail(exception.Message, exception);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return CancelSourceResponse(sourceResponse, cancellationToken);
            }

            if (!sourceResponse.Success)
            {
                return ReadResponse<TValue>.Fail
                (
                    sourceResponse.Error,
                    sourceResponse.Exception,
                    sourceResponse.ReleaseHandle
                );
            }

            try
            {
                var value = map(sourceResponse.Value);

                if (cancellationToken.IsCancellationRequested)
                {
                    return CancelSourceResponse(sourceResponse, cancellationToken);
                }

                return ReadResponse<TValue>.Succeed(value, sourceResponse.ReleaseHandle);
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return CancelSourceResponse(sourceResponse, cancellationToken, exception);
                }

                return ReadResponse<TValue>.Fail
                (
                    exception.Message,
                    exception,
                    sourceResponse.ReleaseHandle
                );
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 취소 후 호출자에게 이전하지 않을 원본 ReleaseHandle과 실패 원인을 정리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static ReadResponse<TValue> CancelSourceResponse
        (
            ReadResponse<TSource> sourceResponse,
            CancellationToken cancellationToken,
            Exception mappingFailure = null
        )
        {
            Exception failure = new OperationCanceledException(cancellationToken);

            if (!sourceResponse.Success)
            {
                var sourceFailure = sourceResponse.Exception ??
                                new InvalidOperationException(sourceResponse.Error);
                failure = ContainsCancellation(sourceFailure, cancellationToken)
                    ? sourceFailure
                    : new AggregateException
                    (
                        "원본 IO 읽기 실패와 취소가 함께 발생했습니다.",
                        sourceFailure,
                        failure
                    );
            }

            if (mappingFailure != null)
            {
                failure = ContainsCancellation(mappingFailure, cancellationToken)
                    ? mappingFailure
                    : new AggregateException
                    (
                        "값 변환 실패와 취소가 함께 발생했습니다.",
                        mappingFailure,
                        failure
                    );
            }

            try
            {
                sourceResponse.ReleaseHandle?.Dispose();
            }
            catch (Exception exception)
            {
                failure = new AggregateException
                (
                    "읽기 취소 중 IO ReleaseHandle 정리에도 실패했습니다.",
                    failure,
                    exception
                );
            }

            return ReadResponse<TValue>.Fail(failure.Message, failure);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 예외에 이미 취소 원인이 포함되어 있으면 중복 추가하지 않는다.
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

    }
}
