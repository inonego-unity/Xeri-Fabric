/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : LocalizedStringRow.cs
수정일 : 2026-09-02

# 설명
LocalizedString에 안정 Key를 부여해 DataPackage Table row로 사용할 수 있게 한다.
프로젝트별 별도 Text row 타입 없이 REF<LocalizedStringRow>로 공통 문자열을 참조한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using inonego.Xeri.Data;

namespace inonego.Xeri.Localization
{
    // ============================================================
    /// <summary>
    /// Key로 참조 가능한 LocalizedString Table row.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class LocalizedStringRow : ITableValue
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// DataPackage에서 이 row를 식별하는 안정 Key.
        /// </summary>
        // ------------------------------------------------------------
        public string Key => key;

        [SerializeField]
        private string key = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// 유효한 Key가 설정되어 있는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool HasKey => !string.IsNullOrWhiteSpace(key);

        // ------------------------------------------------------------
        /// <summary>
        /// locale code별 문자열 값.
        /// </summary>
        // ------------------------------------------------------------
        public LocalizedString Value => value;

        [SerializeField]
        private LocalizedString value = new();

    #endregion

    }
}
