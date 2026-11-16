/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SaveMetadata.cs
수정일 : 2026-11-14

# 설명
SaveRecord의 저장 키·스키마·형식·저장 시각을 기록하는 Metadata.
Document의 이름/Session과는 별개로 Save가 직접 소유한다.

# 주의
FormatVersion은 물리적 파일 포맷, SchemaVersion은 게임 데이터 구조를 뜻한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Persistence;

namespace inonego.Xeri.Save
{
    // ============================================================
    /// <summary>
    /// 저장소별 SaveRecord에 사용하는 Metadata.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class SaveMetadata : IRecordMetadata
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 Snapshot 형식 버전.
        /// </summary>
        // ------------------------------------------------------------
        public string FormatVersion = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// 게임 데이터 스키마 식별자.
        /// </summary>
        // ------------------------------------------------------------
        public string SchemaID = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// 게임 데이터 스키마 버전.
        /// </summary>
        // ------------------------------------------------------------
        public string SchemaVersion = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Save 파일의 논리 키.
        /// </summary>
        // ------------------------------------------------------------
        public string SaveKey = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// 기록을 구분하는 Scope. 일반 FileSave는 빈 문자열을 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        public string ScopeID = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// UTC ISO 8601 저장 시각.
        /// </summary>
        // ------------------------------------------------------------
        public string SavedAtUTC = string.Empty;

        // 게임 데이터 스키마를 공통 기록 계약에 매핑한다.
        string IRecordMetadata.TypeID => SchemaID;
        string IRecordMetadata.Version => SchemaVersion;

    #endregion

    }
}
