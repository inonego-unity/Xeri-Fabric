/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : CallbackReleaseHandle.cs
수정일 : 2026-11-09

# 설명
Fabric의 IReleaseHandle에 콜백을 연결하는 범용 일회 리소스 해제 구현.
Addressables·테스트 등의 외부 리소스 수명 반환을 Fabric IO의 공통 계약으로 제공한다.

# 특이사항
Release/Dispose는 같은 해제 동작이며 최대 한 번만 콜백을 호출한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Threading;

namespace inonego.Xeri.IO
{
    // ============================================================
    /// <summary>
    /// Fabric IReleaseHandle의 단일 콜백 구현.
    /// </summary>
    // ============================================================
    public sealed class CallbackReleaseHandle : IReleaseHandle
    {

    #region 필드

        private readonly Action release = null;
        private int released = 0;

        // ------------------------------------------------------------
        /// <summary>
        /// 외부 리소스 반환이 시도되었는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsReleased => Volatile.Read(ref released) != 0;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 반환할 외부 리소스 해제 콜백을 제공한다.
        /// </summary>
        // ------------------------------------------------------------
        public CallbackReleaseHandle(Action release)
        {
            this.release = release ?? throw new ArgumentNullException(nameof(release));
        }

    #endregion

    #region 수명

        // ------------------------------------------------------------
        /// <summary>
        /// 등록된 리소스 해제 콜백을 정확히 한 번 호출한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Release()
        {
            if (Interlocked.Exchange(ref released, 1) != 0)
            {
                return;
            }

            release();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Release와 동일한 수명 해제 동작이다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            Release();
        }

    #endregion

    }
}
