/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RevisionEntry.cs
수정일 : 2026-11-14

# 설명
이기종 Revision의 개별 항목에 Schema와 직렬화된 Body를 보관하는 내부 기록.
File/SQL Revision 엔진의 단일 Body 계약을 재사용한다.

# 제약
Schema와 DTO 타입의 대응 관계는 호출자가 관리한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Persistence;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.Save
{
    // ============================================================
    /// <summary>
    /// 이기종 Revision의 항목별 Schema와 직렬화된 Body를 보관한다.
    /// </summary>
    // ============================================================
    [Serializable]
    internal sealed class RevisionEntry
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 저장 기록의 파일 형식 버전입니다.
        /// </summary>
        // ------------------------------------------------------------
        public string FormatVersion = "1";

        // ------------------------------------------------------------
        /// <summary>
        /// 저장 데이터의 스키마 식별자입니다.
        /// </summary>
        // ------------------------------------------------------------
        public string SchemaID = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// 저장 데이터의 스키마 버전입니다.
        /// </summary>
        // ------------------------------------------------------------
        public string SchemaVersion = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// 직렬화된 데이터 본문입니다.
        /// </summary>
        // ------------------------------------------------------------
        public string Payload = string.Empty;

    #endregion

    #region 기록 변환

        // ------------------------------------------------------------
        /// <summary>
        /// 호출 시점에 입력 Body를 직렬화해 변경 영향을 차단한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static RevisionEntry Capture<TBody>
        (
            TBody body, RecordType schema, ISerializer serializer
        )
        where TBody : class
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            var payload = serializer.Serialize(body);
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new InvalidOperationException("Entry Body 직렬화 결과가 비어 있습니다.");
            }

            return new RevisionEntry
            {
                FormatVersion = "1",
                SchemaID = schema.TypeID,
                SchemaVersion = schema.Version,
                Payload = payload
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기록의 형식과 요청 Schema를 검증한 뒤 Body를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        internal TBody Restore<TBody>(RecordType schema, ISerializer serializer)
        where TBody : class
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (FormatVersion != "1" || string.IsNullOrWhiteSpace(Payload))
            {
                throw new InvalidOperationException("Revision Entry 기록 형식이 올바르지 않습니다.");
            }

            schema.Validate(SchemaID, SchemaVersion);
            var body = serializer.Deserialize<TBody>(Payload);
            return body ?? throw new InvalidOperationException("Revision Entry Body가 비어 있습니다.");
        }

    #endregion

    }
}
