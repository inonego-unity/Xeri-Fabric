/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_AddressablesAssetReader.cs
수정일 : 2026-11-09

# 설명
Addressables 입력원의 비동기 사전 취소가 실제 Asset 로드를 시작하지 않는 실패 경계를 검증한다.

# 테스트 구성
 C: Cancellation - Addressables 사전 취소 계약
========================================================================= BLOCK_HEADER_END */

using System;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.IO.TEST
{

    // ============================================================
    /// <summary>
    /// Addressables 읽기와 핸들 해제 동작을 검증합니다.
    /// </summary>
    // ============================================================
    public sealed class TEST_AddressablesAssetReader
    {

    #region C-1: 비동기 Reader 사전 취소

        // ------------------------------------------------------------
        /// <summary>
        /// 사전 취소 요청은 원본 Addressables 로드 없이 실패 응답을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public async Task TEST_AddressablesAssetReader_사전_취소는_실패_응답()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var reader = AddressablesAssetReader<TextAsset>.Default;
            var response = await reader.ReadAsync("unused-addressable", cancellation.Token);

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<OperationCanceledException>(response.Exception);
            Assert.IsNull(response.ReleaseHandle);
        }

    #endregion

    }
}
