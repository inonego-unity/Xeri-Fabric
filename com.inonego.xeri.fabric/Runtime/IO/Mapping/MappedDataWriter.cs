/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : MappedDataWriter.cs
수정일 : 2026-11-09

# 설명
기존 writer가 받는 원본 값으로 입력값을 변환하는 범용 IO writer adapter.
영속 데이터의 형식·스키마·검증·Commit 정책을 포함하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.IO
{
    // ============================================================
    /// <summary>
    /// 입력값을 변환하여 source writer에 전달하는 범용 writer adapter.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class MappedDataWriter<TLocation, TValue, TSource> : IDataWriter<TLocation, TValue>
    {

    #region 필드

        private readonly IDataWriter<TLocation, TSource> sourceWriter = null;
        private readonly Func<TValue, TSource> map = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 writer와 입력 변환 함수로 adapter를 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        public MappedDataWriter
        (
            IDataWriter<TLocation, TSource> sourceWriter,
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
        /// <br/> 입력값을 source 값으로 변환한 다음 위치에 기록한다.
        /// <br/> 변환 실패 시 source writer를 호출하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public WriteResponse Write(TLocation location, TValue value)
        {
            try
            {
                var source = map(value);
                return sourceWriter.Write(location, source);
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

    #endregion

    }
}
