/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : MappedDataReader.cs
수정일 : 2026-11-09

# 설명
기존 reader가 읽은 response value를 다른 값으로 변환해 반환하는 동기 IO reader adapter를 정의한다.
원본 Reader/변환 예외를 실패 응답으로 전달하며, 전달받은 ReleaseHandle의 소유권을 유지한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.IO
{
    // ================================================================================
    /// <summary>
    /// <br/> source reader가 읽은 response value를 지정한 변환 함수로 변환해 반환하는
    /// <br/> reader adapter.
    /// </summary>
    // ================================================================================
    [Serializable]
    public sealed class MappedDataReader<TLocation, TSource, TValue> : IDataReader<TLocation, TValue>
    {

    #region 필드

        private readonly IDataReader<TLocation, TSource> sourceReader = null;
        private readonly Func<TSource, TValue> map = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// MappedDataReader를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public MappedDataReader
        (
            IDataReader<TLocation, TSource> sourceReader,
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
        /// source reader에서 read response를 받은 뒤 value를 변환해 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ReadResponse<TValue> Read(TLocation location)
        {
            ReadResponse<TSource> sourceResponse;

            try
            {
                sourceResponse = sourceReader.Read(location);
            }
            catch (Exception exception)
            {
                return ReadResponse<TValue>.Fail(exception.Message, exception);
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

                return ReadResponse<TValue>.Succeed(value, sourceResponse.ReleaseHandle);
            }
            catch (Exception exception)
            {
                return ReadResponse<TValue>.Fail
                (
                    exception.Message,
                    exception,
                    sourceResponse.ReleaseHandle
                );
            }
        }

    #endregion

    }
}
