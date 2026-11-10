/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RecordType.cs
수정일 : 2026-10-29

# 설명
영속 기록의 종류와 계약 버전을 식별하고 실제 기록이 지원 계약과 일치하는지 검증한다.
Document의 TypeID/Version과 미래 Save의 SchemaID/Version을 도메인별로 매핑할 수 있다.

# 특이사항
파일 Envelope의 물리적 Format Version, 문서 표시 이름, Save Slot, 저장 시각은 포함하지 않는다.
Version은 단순 문자열 계약이며 Migration·범위 비교·자동 버전 승격을 제공하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.Persistence
{
    // ============================================================
    /// <summary>
    /// <br/> 저장된 기록이 따라야 하는 종류 및 버전 계약.
    /// <br/> 실제 Metadata의 클래스나 직렬화 형식을 강제하지 않는다.
    /// </summary>
    // ============================================================
    public sealed class RecordType
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 계약이 지원하는 기록 종류 식별자.
        /// </summary>
        // ------------------------------------------------------------
        public string TypeID { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 계약이 지원하는 기록 버전. 형식과 해석은 호출 도메인이 결정한다.
        /// </summary>
        // ------------------------------------------------------------
        public string Version { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 공백이 아닌 기록 종류와 지원 버전으로 계약을 정의한다.
        /// </summary>
        /// <param name="typeID">기록 종류 식별자.</param>
        /// <param name="version">기록 버전 문자열.</param>
        // ------------------------------------------------------------
        public RecordType(string typeID, string version)
        {
            if (string.IsNullOrWhiteSpace(typeID))
            {
                throw new ArgumentException("기록 종류 식별자가 비어 있습니다.", nameof(typeID));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("기록 버전이 비어 있습니다.", nameof(version));
            }

            TypeID = typeID;
            Version = version;
        }

    #endregion

    #region 검사

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 기록의 종류와 버전이 이 계약에 정확히 일치하는지 확인한다.
        /// </summary>
        /// <param name="typeID">검사할 실제 기록 종류.</param>
        /// <param name="version">검사할 실제 기록 버전.</param>
        // ------------------------------------------------------------
        public bool IsMatch(string typeID, string version)
        {
            return string.Equals(TypeID, typeID, StringComparison.Ordinal) &&
                   string.Equals(Version, version, StringComparison.Ordinal);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Metadata가 이 기록 종류·버전 계약에 일치하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsMatch(IRecordMetadata metadata)
        {
            return metadata != null && IsMatch(metadata.TypeID, metadata.Version);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 공통 Metadata 계약을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Validate(IRecordMetadata metadata)
        {
            if (metadata == null)
            {
                throw new InvalidOperationException("기록 Metadata가 비어 있습니다.");
            }

            Validate(metadata.TypeID, metadata.Version);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Metadata와 Body를 갖춘 Record가 지원 계약을 만족하는지 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Validate<TBody>(IRecord<TBody> record)
        {
            if (record == null || record.Body is null)
            {
                throw new InvalidOperationException("기록 또는 Body가 비어 있습니다.");
            }

            Validate(record.Metadata);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 실제 기록이 지원 종류와 버전을 따르는지 검사하며, 불일치 시 예외를 발생시킨다.
        /// </summary>
        /// <param name="typeID">검사할 실제 기록 종류.</param>
        /// <param name="version">검사할 실제 기록 버전.</param>
        // ----------------------------------------------------------------------
        public void Validate(string typeID, string version)
        {
            if (!string.Equals(TypeID, typeID, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("기록 TypeID가 지원 계약과 일치하지 않습니다.");
            }

            if (!string.Equals(Version, version, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("기록 Version이 지원 계약과 일치하지 않습니다.");
            }
        }

    #endregion

    }
}
