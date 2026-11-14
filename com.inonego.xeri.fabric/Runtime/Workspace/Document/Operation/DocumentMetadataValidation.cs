/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentMetadataValidation.cs
수정일 : 2026-10-28

# 설명
Document Handler의 기록 계약과 표시 이름을 검증하는 내부 정책.
기록 종류·버전 비교는 Persistence의 RecordType을 재사용한다.

# 특이사항
Name과 Document Session의 의미는 Document 전용이다.
공통 Persistence에 문서 표시 이름·Location·Dirty 규칙을 넣지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Persistence;

namespace inonego.Xeri.Workspace.Document
{
    // ======================================================================
    /// <summary>
    /// 공통 RecordType에 문서 표시 이름 검사를 더하는 Document 전용 기능.
    /// </summary>
    // ======================================================================
    internal static class DocumentMetadataValidation
    {

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// 문서의 종류·버전이 기록 계약과 일치하고 표시 이름이 존재하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal static bool IsMatch(IDocument document, RecordType recordType)
        {
            return document != null &&
                   IsMatch(document.TypeID, document.Version, document.Name, recordType);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 직렬화된 문서 설명 정보가 공통 계약과 문서 표시 이름 규칙을 만족하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal static bool IsMatch
        (
            string typeID,
            string version,
            string name,
            RecordType recordType
        )
        {
            if (recordType == null)
            {
                throw new ArgumentNullException(nameof(recordType));
            }

            return recordType.IsMatch(typeID, version) &&
                   !string.IsNullOrWhiteSpace(name);
        }

    #endregion

    }
}
