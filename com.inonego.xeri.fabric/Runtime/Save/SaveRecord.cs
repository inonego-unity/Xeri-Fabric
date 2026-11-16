/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SaveRecord.cs
수정일 : 2026-11-14

# 설명
단일 Snapshot 저장에 사용하는 Save Metadata와 Body의 직렬화 경계.
Save Metadata와 Body를 함께 직렬화하고 필수 Body 존재를 검증한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Persistence;

namespace inonego.Xeri.Save
{
    // ============================================================
    /// <summary>
    /// 파일·SQL·Remote 저장소가 사용하는 Save 기록.
    /// </summary>
    /// <typeparam name="TBody">게임이 제공하는 Snapshot 데이터 타입.</typeparam>
    // ============================================================
    [Serializable]
    public sealed class SaveRecord<TBody> : IRecord<TBody>, ISerializationCallbackReceiver
    where TBody : class
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// SaveKey와 Schema를 포함하는 기록 Metadata.
        /// </summary>
        // ------------------------------------------------------------
        public SaveMetadata Metadata = new SaveMetadata();

        // ------------------------------------------------------------
        /// <summary>
        /// 사용자 Snapshot 데이터.
        /// </summary>
        // ------------------------------------------------------------
        public TBody Body;

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Unity JSON이 null Body를 기본 객체로 복원하는 경우를 구분하는 직렬화 존재 표시.
        /// </summary>
        // --------------------------------------------------------------------------------
        public bool HasBody = false;

        // Metadata와 Body를 공통 Record 계약에 연결한다.
        IRecordMetadata IRecord<TBody>.Metadata => Metadata;
        TBody IRecord<TBody>.Body => Body;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 직렬화를 위한 기본 Save 기록을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public SaveRecord()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Metadata와 Snapshot 본문으로 Save 기록을 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        public SaveRecord(SaveMetadata metadata, TBody body) : this()
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Body = body ?? throw new ArgumentNullException(nameof(body));
            HasBody = true;
        }

    #endregion

    #region 직렬화

        // ----------------------------------------------------------------------
        /// <summary>
        /// Unity JsonUtility가 null을 빈 객체로 치환하기 전에 원본 존재 여부를 기록한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void OnBeforeSerialize()
        {
            HasBody = Body != null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기록의 유효성은 복원 후 RecordPersistence에서 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnAfterDeserialize()
        {
            // NONE
        }

    #endregion

    }
}
