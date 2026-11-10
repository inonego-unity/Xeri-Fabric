/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : AsyncMappedDataWriter.cs
수정일 : 2026-11-09

# 설명
기존 async writer에 전달할 값을 변환하는 범용 비동기 IO writer adapter.
변환 함수는 동기로 실행되며, 실제 쓰기 작업은 주입한 writer에 위임한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Threading;
using System.Threading.Tasks;

namespace inonego.Xeri.IO
{
    // ======================================================================
    /// <summary>
    /// 입력값 변환과 비동기 source writer를 조합하는 범용 writer adapter.
    /// </summary>
    // ======================================================================
    [Serializable]
    public sealed class AsyncMappedDataWriter<TLocation, TValue, TSource> : IAsyncDataWriter<TLocation, TValue>
    {

    #region 필드

        private readonly IAsyncDataWriter<TLocation, TSource> sourceWriter = null;
        private readonly Func<TValue, TSource> map = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 비동기 writer와 변환 함수로 adapter를 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        public AsyncMappedDataWriter
        (
            IAsyncDataWriter<TLocation, TSource> sourceWriter,
            Func<TValue, TSource> map
        )
        {
            this.sourceWriter = sourceWriter ?? throw new ArgumentNullException(nameof(sourceWriter));
            this.map = map ?? throw new ArgumentNullException(nameof(map));
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 값을 변환하여 비동기 writer에 전달한다.
        /// <br/> 취소가 확인되면 source writer를 호출하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public async Task<WriteResponse> WriteAsync
        (
            TLocation location,
            TValue value,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var source = map(value);

                cancellationToken.ThrowIfCancellationRequested();

                return await sourceWriter.WriteAsync(location, source, cancellationToken);
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

    #endregion

    }
}
