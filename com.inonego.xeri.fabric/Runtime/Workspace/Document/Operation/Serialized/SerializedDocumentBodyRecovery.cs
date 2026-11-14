/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SerializedDocumentBodyRecovery.cs
수정일 : 2026-10-29

# 설명
Serialized Document Handler가 Session Body를 Recovery Record 문자열로 변환하고 복원하는 공통 처리.
Session·Dirty·Location·지원 Version 검사는 호출한 Document Handler와 Recovery가 담당한다.

# 특이사항
RawText는 직렬화하지 않으므로 이 타입을 사용하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.Workspace.Document
{
    // ======================================================================
    /// <summary>
    /// 두 Serialized Document Handler에서 사용하는 Body Recovery 직렬화 처리.
    /// </summary>
    /// <typeparam name="TBody">Document Session의 본문 타입.</typeparam>
    // ======================================================================
    internal sealed class SerializedDocumentBodyRecovery<TBody>
    where TBody : class
    {

    #region 필드

        private readonly ISerializer serializer = null;

    #endregion

    #region 생성자

        // ----------------------------------------------------------------------
        /// <summary>
        /// 기존 Document Serializer를 사용해 Body Recovery 변환기를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal SerializedDocumentBodyRecovery(ISerializer serializer) : base()
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// Body를 Recovery Record로 변환한다. 변환할 수 없으면 null을 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal DocumentBodyRecoveryRecord Record(TBody body)
        {
            if (body == null)
            {
                return null;
            }

            try
            {
                return new DocumentBodyRecoveryRecord(serializer.Serialize(body));
            }
            catch
            {
                // Session Recovery 기록 실패는 기존 Handler 계약에 따라 null로 표현한다.
                return null;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Recovery Record에서 Body를 복원한다. 손상되거나 비어 있으면 예외를 발생시킨다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal TBody Recover(DocumentBodyRecoveryRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.Record))
            {
                throw new InvalidOperationException("복구할 document body record가 비어 있습니다.");
            }

            var body = serializer.Deserialize<TBody>(record.Record);

            if (body == null)
            {
                throw new InvalidOperationException("복구한 document body가 비어 있습니다.");
            }

            return body;
        }

    #endregion

    }
}
