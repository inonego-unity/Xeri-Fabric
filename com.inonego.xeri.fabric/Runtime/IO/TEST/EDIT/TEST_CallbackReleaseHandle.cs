/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_CallbackReleaseHandle.cs
수정일 : 2026-11-09

# 설명
Fabric IReleaseHandle에 대한 CallbackReleaseHandle의 일회 해제 계약을 검증한다.

# 테스트 구성
 L: Lifetime - 중복 해제와 실패 후 수명
========================================================================= BLOCK_HEADER_END */

using System;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.IO.TEST
{
    // ============================================================
    /// <summary>
    /// CallbackReleaseHandle의 중복 해제 및 예외 수명 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_CallbackReleaseHandle
    {

    #region L-1: 일회 해제와 실패 후 상태

        // ------------------------------------------------------------
        /// <summary>
        /// Release와 Dispose가 연속으로 호출되어도 콜백은 한 번만 실행된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_CallbackReleaseHandle_Release와_Dispose_동일한_수명()
        {
            var count = 0;
            var handle = new CallbackReleaseHandle(() => count++);

            handle.Release();
            handle.Dispose();
            handle.Release();

            Assert.IsTrue(handle.IsReleased);
            Assert.AreEqual(1, count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 해제 콜백이 예외를 발생시켜도 다시 호출하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_CallbackReleaseHandle_해제_실패_재시도_안함()
        {
            var count = 0;
            var error = new InvalidOperationException("release failed");
            var handle = new CallbackReleaseHandle
            (
                () =>
                {
                    count++;
                    throw error;
                }
            );

            Assert.AreSame(error, Assert.Throws<InvalidOperationException>(() => handle.Release()));
            Assert.IsTrue(handle.IsReleased);

            handle.Dispose();

            Assert.AreEqual(1, count);
        }

    #endregion

    }
}
