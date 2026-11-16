/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FileRevisionSnapshot.cs
수정일 : 2026-11-16

# 설명
고정된 Revision을 읽기 위한 Snapshot을 제공한다.
동형 및 이기종 Entry를 해당 Schema에 맞게 복원한다.

# 제약
Snapshot은 current 포인터 갱신과 독립적으로 보존된 기록을 참조한다.
Revision Record는 해시를 검증한 물리 파일의 원문을 복원하며 외부 문자열 Reader로 대체하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Persistence;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.Save
{

    // ============================================================
    /// <summary>
    /// 한 Revision에 고정된 Entry 목록과 선택적 로드 뷰.
    /// </summary>
    /// <typeparam name="TBody">Revision의 Entry Body 타입.</typeparam>
    // ============================================================
    public sealed class FileRevisionSnapshot<TBody>
    where TBody : class
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// Revision이 포함된 ScopeID.
        /// </summary>
        // ------------------------------------------------------------
        public string ScopeID { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 불변 RevisionID.
        /// </summary>
        // ------------------------------------------------------------
        public string RevisionID { get; }

        private readonly FileSave<TBody> records = null;
        private readonly Dictionary<string, FileRevisionManifest.Entry> entries = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 검증이 완료된 Manifest의 Entry 맵을 고정한다.
        /// </summary>
        // ------------------------------------------------------------
        internal FileRevisionSnapshot
        (
            FileRevisionManifest manifest,
            FileSave<TBody> records
        )
        {
            ScopeID = manifest.ScopeID;
            RevisionID = manifest.RevisionID;
            this.records = records ?? throw new ArgumentNullException(nameof(records));
            entries = new Dictionary<string, FileRevisionManifest.Entry>(StringComparer.Ordinal);

            foreach (var entry in manifest.Entries)
            {
                entries.Add(entry.EntryID, entry);
            }
        }

    #endregion

    #region 읽기

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Revision이 참조하는 특정 Entry의 Snapshot을 읽는다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<TBody> ReadEntry(string entryID)
        {
            var response = ReadEntryRecord(entryID);

            return response.Success
               ? ReadResponse<TBody>.Succeed(response.Value.Body)
               : ReadResponse<TBody>.Fail(response.Error, response.Exception);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Revision의 특정 Entry에서 Metadata와 Body를 모두 읽는다.
        /// </summary>
        // ------------------------------------------------------------
        internal ReadResponse<SaveRecord<TBody>> ReadEntryRecord(string entryID)
        {
            if (!SaveIdentifier.IsValid(entryID))
            {
                return ReadResponse<SaveRecord<TBody>>.Fail("EntryID가 올바르지 않습니다.");
            }

            if (!entries.TryGetValue(entryID, out var entry))
            {
                return ReadResponse<SaveRecord<TBody>>.Fail("Revision에 해당 Entry가 없습니다.");
            }

            try
            {
                return ReadRecord(records, entry);
            }
            catch (Exception exception)
            {
                return ReadResponse<SaveRecord<TBody>>.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Commit 게시 전과 Snapshot 복원에 동일한 기록 검증을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static ReadResponse<SaveRecord<TBody>> ReadRecord
        (
            FileSave<TBody> records, FileRevisionManifest.Entry entry
        )
        {
            return records.LoadRecord
            (
                entry.RecordID,
                new VerifiedRecordReader(entry.ByteLength, entry.ContentHash)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Revision에 속한 EntryID를 오름차순으로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public string[] ListEntries()
        {
            var result = new string[entries.Count];
            entries.Keys.CopyTo(result, 0);
            Array.Sort(result, StringComparer.Ordinal);

            return result;
        }

    #endregion

    #region Record 읽기 경계

        // ============================================================
        /// <summary>
        /// 검증한 파일 원문만 Mapping과 Persistence에 전달한다.
        /// </summary>
        // ============================================================
        private sealed class VerifiedRecordReader : IDataReader<string, string>
        {
            private readonly long byteLength;
            private readonly string contentHash;

            // ------------------------------------------------------------
            /// <summary>
            /// Entry가 요구하는 파일 형식과 무결성 정보를 고정한다.
            /// </summary>
            // ------------------------------------------------------------
            public VerifiedRecordReader(long byteLength, string contentHash)
            {
                this.byteLength = byteLength;
                this.contentHash = contentHash;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 검증한 바이트에서 바로 복원한 문자열을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadResponse<string> Read(string location)
            {
                return RevisionRecordValidator.ReadVerifiedText
                (
                    location, byteLength, contentHash
                );
            }
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// 서로 다른 타입의 Entry를 읽을 수 있는 고정 Revision 뷰.
    /// </summary>
    // ============================================================
    public sealed class FileRevisionSnapshot
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 데이터를 구분하는 저장 범위 식별자입니다.
        /// </summary>
        // ------------------------------------------------------------
        public string ScopeID => snapshot.ScopeID;

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 Revision의 식별자입니다.
        /// </summary>
        // ------------------------------------------------------------
        public string RevisionID => snapshot.RevisionID;

        private readonly FileRevisionSnapshot<RevisionEntry> snapshot;
        private readonly ISerializer serializer;

    #endregion

    #region 생성자

        internal FileRevisionSnapshot
        (
            FileRevisionSnapshot<RevisionEntry> snapshot,
            ISerializer serializer
        )
        {
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

    #endregion

    #region 읽기

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 Entry의 Schema와 요청 타입 계약을 검사해 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<TBody> ReadEntry<TBody>(string entryID, RecordType schema)
        where TBody : class
        {
            try
            {
                var result = snapshot.ReadEntry(entryID);
                if (!result.Success)
                {
                    return ReadResponse<TBody>.Fail(result.Error, result.Exception);
                }

                return ReadResponse<TBody>.Succeed(result.Value.Restore<TBody>(schema, serializer));
            }
            catch (Exception exception)
            {
                return ReadResponse<TBody>.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 Revision에 포함된 Entry 목록을 반환합니다.
        /// </summary>
        // ------------------------------------------------------------
        public string[] ListEntries()
        {
            return snapshot.ListEntries();
        }

    #endregion

    }
}
