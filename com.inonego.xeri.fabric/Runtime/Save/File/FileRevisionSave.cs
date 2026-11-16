/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FileRevisionSave.cs
수정일 : 2026-11-16

# 설명
Scope별 파일 Revision의 기록·조회·복원과 정리를 담당한다.
동형 및 이기종 Entry 저장은 같은 Revision 엔진을 재사용한다.

# 제약
Manifest와 Record를 기록한 뒤 current 포인터를 마지막에 교체한다.
포인터가 없는데 이전 Revision 자료가 남았다면 빈 저장소로 취급하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Persistence;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.Save
{

    // ======================================================================
    /// <summary>
    /// Scope 단위의 불변 Entry와 Manifest로 Revision을 확정하는 파일 Save.
    /// </summary>
    /// <typeparam name="TBody">하나의 Revision에 포함될 Entry Body 타입.</typeparam>
    // ======================================================================
    public sealed class FileRevisionSave<TBody>
    where TBody : class
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 지원하는 Manifest의 파일 형식 버전.
        /// </summary>
        // ------------------------------------------------------------
        private const string manifestFormatVersion = "1";

        private readonly string directory = string.Empty;
        private readonly RecordType schema = null;
        private readonly ISerializer serializer = null;
        private readonly IDataReader<string, string> reader = null;
        private readonly IDataWriter<string, string> writer = null;
        private readonly RecordPersistence<string, string> currentPointerStore = null;
        private readonly RecordPersistence<string, string> publishingPointerStore = null;
        private readonly object commitLock = new();
        private readonly bool isMixedRevision;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 FileSave와 같은 Serializer·Schema 및 파일 IO를 조합한다.
        /// </summary>
        // ------------------------------------------------------------
        public FileRevisionSave
        (
            string directory,
            RecordType schema,
            ISerializer serializer,
            IDataReader<string, string> reader = null,
            IDataWriter<string, string> writer = null
        ) : this(directory, schema, serializer, reader, writer, false)
        {
            // NONE
        }

        internal FileRevisionSave
        (
            string directory,
            RecordType schema,
            ISerializer serializer,
            IDataReader<string, string> reader,
            IDataWriter<string, string> writer,
            bool isMixedRevision
        )
        {
            this.isMixedRevision = isMixedRevision;
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Revision 저장소 디렉터리가 비어 있습니다.", nameof(directory));
            }

            this.directory = Path.GetFullPath(directory);
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.reader = reader ?? TextFileIO.Default;
            this.writer = writer ?? TextFileIO.Default;
            ValidateTextEncoding(this.reader, nameof(reader));
            ValidateTextEncoding(this.writer, nameof(writer));

            currentPointerStore = new RecordPersistence<string, string>
            (
                this.reader,
                new FileCommitWriter<string>(this.writer),
                ValidateRevisionID
            );

            // 일반 Commit만 이전 포인터를 백업한다. 명시적 복구는 백업을 덮지 않는다.
            publishingPointerStore = new RecordPersistence<string, string>
            (
                this.reader,
                new FileCommitWriter<string>(this.writer, preservePrevious: true),
                ValidateRevisionID
            );
        }

    #endregion

    #region Revision 시작

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Entry 변경을 모으는 Writer를 연다.
        /// <br/> expectedRevisionID가 null이면 사전 비교하지 않고, 빈 문자열이면 첫 Revision만
        /// <br/> 허용한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public FileRevisionWriter<TBody> BeginRevision
        (
            string scopeID,
            string expectedRevisionID = null
        )
        {
            ValidateScopeID(scopeID);

            if (expectedRevisionID != null &&
                expectedRevisionID.Length > 0 &&
                !SaveIdentifier.IsValid(expectedRevisionID))
            {
                throw new ArgumentException("예상 RevisionID가 올바르지 않습니다.", nameof(expectedRevisionID));
            }

            return new FileRevisionWriter<TBody>(this, scopeID, expectedRevisionID);
        }

    #endregion

    #region Revision 조회

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 게시된 current 포인터를 읽는다. 포인터가 없으면 빈 문자열이다.
        /// <br/> Scope 손상 여부는 변경 작업 시작 시 별도로 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ReadResponse<string> GetCurrentRevisionID(string scopeID)
        {
            try
            {
                ValidateScopeID(scopeID);
                EnsureStoreKind(scopeID, false);
                return ReadCurrent(scopeID);
            }
            catch (Exception exception)
            {
                return ReadResponse<string>.Fail(exception.Message, exception);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// current 포인터와 Manifest를 검증하고 현재 Revision을 고정해 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ReadResponse<FileRevisionSnapshot<TBody>> LoadRevision(string scopeID)
        {
            try
            {
                ValidateScopeID(scopeID);
                EnsureStoreKind(scopeID, false);
                var head = ReadCurrent(scopeID);

                if (!head.Success)
                {
                    return ReadResponse<FileRevisionSnapshot<TBody>>.Fail(head.Error, head.Exception);
                }

                if (string.IsNullOrEmpty(head.Value))
                {
                    return ReadResponse<FileRevisionSnapshot<TBody>>.Fail("현재 Revision이 없습니다.");
                }

                return OpenRevision(scopeID, head.Value);
            }
            catch (Exception exception)
            {
                return ReadResponse<FileRevisionSnapshot<TBody>>.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이전 RevisionID의 불변 Manifest를 직접 연다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<FileRevisionSnapshot<TBody>> OpenRevision
        (
            string scopeID,
            string revisionID
        )
        {
            try
            {
                ValidateScopeID(scopeID);
                ValidateRevisionID(revisionID);
                EnsureStoreKind(scopeID, false);

                var response = ReadManifest(scopeID, revisionID);

                if (!response.Success)
                {
                    return ReadResponse<FileRevisionSnapshot<TBody>>.Fail
                    (
                        response.Error, response.Exception
                    );
                }

                return ReadResponse<FileRevisionSnapshot<TBody>>.Succeed
                (
                    new FileRevisionSnapshot<TBody>(response.Value, CreateRecordStore(scopeID))
                );
            }
            catch (Exception exception)
            {
                return ReadResponse<FileRevisionSnapshot<TBody>>.Fail(exception.Message, exception);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 현재 Revision의 Entry 하나를 편리하게 읽는다.
        /// <br/> 여러 Entry를 일관되게 읽으려면 LoadRevision으로 Snapshot을 먼저 고정한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public ReadResponse<TBody> ReadEntry(string scopeID, string entryID)
        {
            var response = LoadRevision(scopeID);

            return response.Success
               ? response.Value.ReadEntry(entryID)
               : ReadResponse<TBody>.Fail(response.Error, response.Exception);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 보존 중인 RevisionID 목록을 생성 시각의 내림차순으로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<string[]> ListRevisions(string scopeID)
        {
            try
            {
                ValidateScopeID(scopeID);
                EnsureStoreKind(scopeID, false);
                var revisionDirectory = Path.Combine(directory, scopeID, "revisions");
                if (!System.IO.Directory.Exists(revisionDirectory))
                {
                    return ReadResponse<string[]>.Succeed(Array.Empty<string>());
                }

                var revisions = new List<FileRevisionManifest>();
                foreach (var path in System.IO.Directory.EnumerateFiles
                (
                    revisionDirectory, "*.json", SearchOption.TopDirectoryOnly
                ))
                {
                    var id = Path.GetFileNameWithoutExtension(path);
                    ValidateRevisionID(id);
                    var opened = ReadManifest(scopeID, id);
                    if (!opened.Success)
                    {
                        return ReadResponse<string[]>.Fail(opened.Error, opened.Exception);
                    }

                    revisions.Add(opened.Value);
                }

                revisions.Sort
                (
                    (left, right) =>
                    {
                        var order = string.Compare(right.SavedAtUTC, left.SavedAtUTC, StringComparison.Ordinal);
                        return order != 0
                   ? order
                   : string.Compare(right.RevisionID, left.RevisionID, StringComparison.Ordinal);
                    }
                );

                var ids = new string[revisions.Count];
                for (var index = 0; index < ids.Length; index++)
                {
                    ids[index] = revisions[index].RevisionID;
                }

                return ReadResponse<string[]>.Succeed(ids);
            }
            catch (Exception exception)
            {
                return ReadResponse<string[]>.Fail(exception.Message, exception);
            }
        }

    #endregion

    #region 백업 복원

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 직전 Commit 이전의 포인터가 가리키던 Revision을 연다.
        /// <br/> 첫 Commit에는 이전 포인터가 없으며 현재 포인터를 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ReadResponse<FileRevisionSnapshot<TBody>> LoadBackupRevision(string scopeID)
        {
            try
            {
                ValidateScopeID(scopeID);

                var path = GetBackupPath(scopeID);
                if (!File.Exists(path))
                {
                    return ReadResponse<FileRevisionSnapshot<TBody>>.Fail("복구할 이전 Revision 포인터가 없습니다.");
                }

                var backup = currentPointerStore.Load(path);
                if (!backup.Success)
                {
                    return ReadResponse<FileRevisionSnapshot<TBody>>.Fail(backup.Error, backup.Exception);
                }

                return OpenRevision(scopeID, backup.Value);
            }
            catch (Exception exception)
            {
                return ReadResponse<FileRevisionSnapshot<TBody>>.Fail(exception.Message, exception);
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 이전 포인터의 모든 Entry가 유효하면 current를 해당 Revision으로 복원한다.
        /// <br/> 손상된 current를 덮어쓸 수 있지만 이전 포인터는 유지한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public WriteResponse RestoreBackupRevision(string scopeID)
        {
            try
            {
                ValidateScopeID(scopeID);
                using var commitLease = AcquireScopeLock(scopeID);
                EnsureStoreKind(scopeID, false);

                var backup = LoadBackupRevision(scopeID);
                if (!backup.Success)
                {
                    return WriteResponse.Fail(backup.Error, backup.Exception);
                }

                // 손상된 Entry를 복구된 정상 Revision으로 게시하지 않는다.
                foreach (var entryID in backup.Value.ListEntries())
                {
                    var entry = backup.Value.ReadEntryRecord(entryID);
                    if (!entry.Success)
                    {
                        return WriteResponse.Fail(entry.Error, entry.Exception);
                    }
                }

                return currentPointerStore.Save(GetCurrentPath(scopeID), backup.Value.RevisionID);
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

    #endregion

    #region Revision Commit

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Scope 배타 잠금 아래 변경 Entry와 Manifest를 기록한 후 current를 교체한다.
        /// <br/> 포인터 교체 전 실패한 파일은 참조되지 않는 고아 파일로 남을 수 있다.
        /// </summary>
        // --------------------------------------------------------------------------------
        internal WriteResponse CommitRevision
        (
            string scopeID,
            string expectedRevisionID,
            IReadOnlyDictionary<string, TBody> updates,
            IReadOnlyCollection<string> removes
        )
        {
            lock (commitLock)
            {
                try
                {
                    ValidateScopeID(scopeID);

                    // Scope 파일 잠금은 다른 인스턴스와 프로세스의 Commit까지 배제한다.
                    using var commitLease = AcquireScopeLock(scopeID);

                    var before = ReadCurrentBeforeMutation(scopeID);
                    if (!before.Success)
                    {
                        return WriteResponse.Fail(before.Error, before.Exception);
                    }

                    if (expectedRevisionID != null &&
                        !string.Equals(expectedRevisionID, before.Value, StringComparison.Ordinal))
                    {
                        return WriteResponse.Fail("예상 RevisionID와 현재 RevisionID가 일치하지 않습니다.");
                    }

                    // Record·Manifest·current를 쓰기 전에 Scope의 기록 종류를 검증·예약한다.
                    EnsureStoreKind(scopeID, true);

                    var entries = new SortedDictionary<string, FileRevisionManifest.Entry>(StringComparer.Ordinal);

                    if (!string.IsNullOrEmpty(before.Value))
                    {
                        var previous = ReadManifest(scopeID, before.Value);
                        if (!previous.Success)
                        {
                            return WriteResponse.Fail(previous.Error, previous.Exception);
                        }

                        foreach (var item in previous.Value.Entries)
                        {
                            entries.Add(item.EntryID, item);
                        }
                    }

                    foreach (var entryID in removes)
                    {
                        entries.Remove(entryID);
                    }

                    var records = CreateRecordStore(scopeID);

                    foreach (var item in updates)
                    {
                        var recordID = CreateUnusedID(Path.Combine(directory, scopeID, "records"), ".save.json");
                        var response = records.Save(recordID, item.Value);

                        if (!response.Success)
                        {
                            return response;
                        }

                        var recordPath = Path.Combine(directory, scopeID, "records", recordID + ".save.json");
                        var hash = RevisionRecordValidator.Compute(recordPath, out var byteLength);
                        var entry = new FileRevisionManifest.Entry
                        {
                            EntryID = item.Key,
                            RecordID = recordID,
                            ContentHash = hash,
                            ByteLength = byteLength
                        };

                        // 게시할 실제 파일이 Snapshot 읽기 계약으로 복원되는지 확인한다.
                        var loaded = FileRevisionSnapshot<TBody>.ReadRecord(records, entry);
                        if (!loaded.Success)
                        {
                            return WriteResponse.Fail(loaded.Error, loaded.Exception);
                        }

                        entries[item.Key] = entry;
                    }

                    var revisionID = CreateUnusedID(Path.Combine(directory, scopeID, "revisions"), ".json");
                    var manifest = new FileRevisionManifest
                    {
                        FormatVersion = manifestFormatVersion,
                        ScopeID = scopeID,
                        RevisionID = revisionID,
                        ParentRevisionID = before.Value,
                        SavedAtUTC = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        Entries = CreateEntries(entries)
                    };

                    // 새 Manifest를 불변 경로에 먼저 완성한다.
                    var manifestResponse = CreateManifestStore(scopeID).Save
                    (
                        GetManifestPath(scopeID, revisionID), manifest
                    );

                    if (!manifestResponse.Success)
                    {
                        return manifestResponse;
                    }

                    // 협력하지 않는 외부 Writer의 변경까지 감지하도록 포인터를 다시 확인한다.
                    // 실제 Commit 간 배타성은 Scope 파일 잠금으로 보장한다.
                    var head = ReadCurrent(scopeID);
                    if (!head.Success)
                    {
                        return WriteResponse.Fail(head.Error, head.Exception);
                    }

                    if (!string.Equals(before.Value, head.Value, StringComparison.Ordinal))
                    {
                        return WriteResponse.Fail("Commit 도중 현재 Revision이 변경되었습니다.");
                    }

                    // current 교체가 성공할 때에만 직전 정상 포인터가 백업된다.
                    return publishingPointerStore.Save(GetCurrentPath(scopeID), revisionID);
                }
                catch (Exception exception)
                {
                    return WriteResponse.Fail(exception.Message, exception);
                }
            }
        }

    #endregion

    #region Revision 정리

        // ============================================================
        /// <summary>
        /// 삭제 전 확정된 Manifest·Record 경로 집합을 보유한다.
        /// </summary>
        // ============================================================
        private sealed class PrunePlan
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 보존하지 않는 Manifest의 경로 목록.
            /// </summary>
            // ------------------------------------------------------------
            public string[] ManifestsToDelete { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 모든 Manifest 삭제 후 회수할 Record 경로 목록.
            /// </summary>
            // ------------------------------------------------------------
            public string[] RecordsToDelete { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 정렬된 삭제 계획을 고정한다.
            /// </summary>
            // ------------------------------------------------------------
            public PrunePlan(string[] manifestsToDelete, string[] recordsToDelete)
            {
                ManifestsToDelete = manifestsToDelete;
                RecordsToDelete = recordsToDelete;
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 이전 Revision Manifest를 명시적으로 정리한다.
        /// <br/> 호출자는 같은 Scope의 활성 Snapshot 열람이 없는 유지보수 구간을 확보해야 한다.
        /// <br/> Manifest 정리 후 미참조 Record도 같은 Scope 잠금에서 회수한다.
        /// <br/> 삭제 도중 실패하면 일부 파일은 이미 제거됐을 수 있다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public WriteResponse PruneRevisions
        (
            string scopeID,
            int keepLatest,
            IReadOnlyCollection<string> protectedRevisionIDs = null
        )
        {
            try
            {
                ValidateScopeID(scopeID);
                if (keepLatest < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(keepLatest));
                }

                using var commitLease = AcquireScopeLock(scopeID);
                EnsureStoreKind(scopeID, false);
                var head = ReadCurrentBeforeMutation(scopeID);
                if (!head.Success)
                {
                    return WriteResponse.Fail(head.Error, head.Exception);
                }

                var backupPath = GetBackupPath(scopeID);
                var backup = File.Exists(backupPath)
                   ? currentPointerStore.Load(backupPath)
                   : ReadResponse<string>.Succeed(string.Empty);
                if (!backup.Success)
                {
                    return WriteResponse.Fail(backup.Error, backup.Exception);
                }

                var revisionDirectory = Path.Combine(directory, scopeID, "revisions");
                var manifests = new Dictionary<string, FileRevisionManifest>(StringComparer.Ordinal);
                if (System.IO.Directory.Exists(revisionDirectory))
                {
                    foreach (var path in System.IO.Directory.EnumerateFiles
                    (
                        revisionDirectory, "*.json", SearchOption.TopDirectoryOnly
                    ))
                    {
                        var id = Path.GetFileNameWithoutExtension(path);
                        ValidateRevisionID(id);

                        var opened = ReadManifest(scopeID, id);
                        if (!opened.Success)
                        {
                            return WriteResponse.Fail(opened.Error, opened.Exception);
                        }

                        manifests.Add(id, opened.Value);
                    }
                }

                if (!string.IsNullOrEmpty(head.Value) && !manifests.ContainsKey(head.Value) ||
                    !string.IsNullOrEmpty(backup.Value) && !manifests.ContainsKey(backup.Value))
                {
                    return WriteResponse.Fail("현재 또는 백업 Revision Manifest가 없습니다.");
                }

                if (head.Value.Length == 0 && manifests.Count > 0)
                {
                    return WriteResponse.Fail("Revision 목록에 대응하는 현재 포인터가 없습니다.");
                }

                var retained = new HashSet<string>(StringComparer.Ordinal);
                if (!string.IsNullOrEmpty(backup.Value))
                {
                    retained.Add(backup.Value);
                }

                if (protectedRevisionIDs != null)
                {
                    foreach (var id in protectedRevisionIDs)
                    {
                        ValidateRevisionID(id);
                        if (!manifests.ContainsKey(id))
                        {
                            return WriteResponse.Fail("보존 대상 Revision이 없습니다.");
                        }

                        retained.Add(id);
                    }
                }

                // Head의 부모 Revision을 따라 최신 N개 세대를 유지한다.
                var cursor = head.Value;
                for (var index = 0; index < keepLatest && cursor.Length > 0; index++)
                {
                    if (!manifests.TryGetValue(cursor, out var manifest))
                    {
                        return WriteResponse.Fail("Revision 부모 링크가 손상되었습니다.");
                    }

                    retained.Add(cursor);
                    cursor = manifest.ParentRevisionID;
                    if (cursor != null && cursor.Length > 0 && !manifests.ContainsKey(cursor))
                    {
                        // 이미 보존 정책에 의해 정리된 부모 세대는 이력 경계다.
                        break;
                    }
                }

                // 삭제할 두 파일 집합을 변경 전에 확정한다.
                var plan = CreatePrunePlan(scopeID, manifests, retained);

                // 삭제 대상 계산 후 살아남을 Record의 무결성을 확인한다.
                var validation = ValidateRetainedRecords(scopeID, manifests, retained);
                if (!validation.Success)
                {
                    return validation;
                }

                return ExecutePrunePlan(plan);
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 보존할 고유 Record의 해시·크기 및 참조 일관성을 검사한다.
        /// <br/> Record 역직렬화 없이 삭제 가능 여부만 판단한다.
        /// </summary>
        // ------------------------------------------------------------
        private WriteResponse ValidateRetainedRecords
        (
            string scopeID,
            IReadOnlyDictionary<string, FileRevisionManifest> manifests,
            IReadOnlyCollection<string> retained
        )
        {
            var verified = new Dictionary<string, (long ByteLength, string ContentHash)>(StringComparer.Ordinal);

            foreach (var id in retained)
            {
                foreach (var entry in manifests[id].Entries)
                {
                    if (verified.TryGetValue(entry.RecordID, out var previous))
                    {
                        if (previous.ByteLength != entry.ByteLength ||
                            !string.Equals
                            (
                                previous.ContentHash, entry.ContentHash,
                                StringComparison.Ordinal
                            ))
                        {
                            return WriteResponse.Fail
                            (
                                "같은 RecordID에 서로 다른 해시 또는 크기가 기록됐습니다."
                            );
                        }

                        continue;
                    }

                    var path = Path.Combine
                    (
                        directory, scopeID, "records", entry.RecordID + ".save.json"
                    );
                    if (!RevisionRecordValidator.Verify
                    (
                        path, entry.ByteLength, entry.ContentHash
                    ))
                    {
                        return WriteResponse.Fail
                        (
                            "보존할 Revision Record의 파일 해시 또는 크기가 일치하지 않습니다."
                        );
                    }

                    verified.Add(entry.RecordID, (entry.ByteLength, entry.ContentHash));
                }
            }

            return WriteResponse.Succeed();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 남길 Manifest를 제외한 파일의 삭제 계획을 만든다.
        /// <br/> 이 단계에서는 어떠한 파일도 삭제하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        private PrunePlan CreatePrunePlan
        (
            string scopeID,
            IReadOnlyDictionary<string, FileRevisionManifest> manifests,
            IReadOnlyCollection<string> retained
        )
        {
            var retainedIDs = new HashSet<string>(retained, StringComparer.Ordinal);
            var manifestsToDelete = new List<string>();
            var referencedRecords = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in manifests)
            {
                if (!retainedIDs.Contains(item.Key))
                {
                    manifestsToDelete.Add(GetManifestPath(scopeID, item.Key));
                    continue;
                }

                foreach (var entry in item.Value.Entries)
                {
                    referencedRecords.Add(entry.RecordID);
                }
            }

            var recordsToDelete = new List<string>();
            var recordDirectory = Path.Combine(directory, scopeID, "records");
            const string suffix = ".save.json";

            if (System.IO.Directory.Exists(recordDirectory))
            {
                foreach (var path in System.IO.Directory.EnumerateFiles
                (
                    recordDirectory, "*" + suffix, SearchOption.TopDirectoryOnly
                ))
                {
                    var name = Path.GetFileName(path);
                    var recordID = name.Substring(0, name.Length - suffix.Length);
                    if (Guid.TryParseExact(recordID, "N", out _) &&
                        SaveIdentifier.IsValid(recordID) &&
                        !referencedRecords.Contains(recordID))
                    {
                        recordsToDelete.Add(path);
                    }
                }
            }

            manifestsToDelete.Sort(StringComparer.Ordinal);
            recordsToDelete.Sort(StringComparer.Ordinal);
            return new PrunePlan(manifestsToDelete.ToArray(), recordsToDelete.ToArray());
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Manifest 삭제 완료 후 Record 회수를 시작한다.
        /// <br/> 재실행은 남은 파일에서 새 계획을 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private static WriteResponse ExecutePrunePlan(PrunePlan plan)
        {
            try
            {
                // Manifest 삭제가 하나라도 실패하면 Record에는 손대지 않는다.
                foreach (var path in plan.ManifestsToDelete)
                {
                    File.Delete(path);
                }

                foreach (var path in plan.RecordsToDelete)
                {
                    File.Delete(path);
                }

                return WriteResponse.Succeed();
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

    #endregion

    #region Scope·포인터 IO

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Scope의 종류 표식을 검증하고 최초 Commit에서 예약한다.
        /// <br/> Commit 및 정리 작업의 배타성은 호출자의 잠금 소유권에 따른다.
        /// </summary>
        // ------------------------------------------------------------
        private void EnsureStoreKind(string scopeID, bool reserve)
        {
            RevisionStoreKind.EnsureFile
            (
                Path.Combine(directory, scopeID),
                isMixedRevision,
                reserve,
                reader,
                writer
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Scope별 파일 잠금을 획득하며 잠금 파일은 삭제하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        private FileStream AcquireScopeLock(string scopeID)
        {
            var scopeDirectory = Path.Combine(directory, scopeID);
            System.IO.Directory.CreateDirectory(scopeDirectory);

            return new FileStream
            (
                Path.Combine(scopeDirectory, "commit.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// current 포인터의 원시 값을 읽는다.
        /// </summary>
        // ------------------------------------------------------------
        private ReadResponse<string> ReadCurrent(string scopeID)
        {
            var path = GetCurrentPath(scopeID);
            return File.Exists(path)
                ? currentPointerStore.Load(path)
                : ReadResponse<string>.Succeed(string.Empty);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 새 작업에 앞서 current 누락과 빈 Scope를 구별한다.
        /// <br/> 잔존 Manifest나 백업이 있으면 자동으로 새 이력을 시작하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        private ReadResponse<string> ReadCurrentBeforeMutation(string scopeID)
        {
            var response = ReadCurrent(scopeID);
            if (!response.Success || !string.IsNullOrEmpty(response.Value))
            {
                return response;
            }

            // 첫 Commit 실패와 정상 Revision 포인터 유실은 파일만으로 구별할 수 없다.
            if (File.Exists(GetBackupPath(scopeID)))
            {
                return ReadResponse<string>.Fail
                (
                    "current 포인터는 없지만 백업이 남아 있어 빈 Scope로 취급할 수 없습니다."
                );
            }

            var revisions = Path.Combine(directory, scopeID, "revisions");
            if (System.IO.Directory.Exists(revisions))
            {
                using var iterator = System.IO.Directory.EnumerateFiles
                (
                    revisions, "*.json", SearchOption.TopDirectoryOnly
                ).GetEnumerator();
                if (iterator.MoveNext())
                {
                    return ReadResponse<string>.Fail
                    (
                        "current 포인터는 없지만 Revision 자료가 남아 있어 빈 Scope로 취급할 수 없습니다."
                    );
                }
            }

            return response;
        }

    #endregion

    #region Record·Manifest IO

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> File Revision의 문자열 IO는 BOM 없는 UTF-8만 허용한다.
        /// <br/> 인코딩을 알 수 없는 주입 IO의 실제 기록은 게시 전에 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ValidateTextEncoding(object io, string parameterName)
        {
            if (io is TextFileIO textIO &&
                (textIO.Encoding.CodePage != Encoding.UTF8.CodePage ||
                 textIO.Encoding.GetPreamble().Length > 0))
            {
                throw new ArgumentException
                (
                    "File Revision 내부 파일은 BOM 없는 UTF-8로 기록해야 합니다.",
                    parameterName
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Entry의 불변 Record 파일을 작성하거나 읽는 기존 FileSave 구성.
        /// </summary>
        // ------------------------------------------------------------
        private FileSave<TBody> CreateRecordStore(string scopeID)
        {
            return new FileSave<TBody>
            (
                Path.Combine(directory, scopeID, "records"),
                schema,
                serializer,
                reader,
                writer,
                scopeID
            );
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Manifest에 대해서도 공통 RecordPersistence와 IO Mapping을 사용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private RecordPersistence<string, FileRevisionManifest> CreateManifestStore(string scopeID)
        {
            return new RecordPersistence<string, FileRevisionManifest>
            (
                new MappedDataReader<string, string, FileRevisionManifest>
                (
                    reader, serializer.Deserialize<FileRevisionManifest>
                ),
                new MappedDataWriter<string, FileRevisionManifest, string>
                (
                    new FileCommitWriter<string>(writer), serializer.Serialize
                ),
                manifest => ValidateManifest(scopeID, manifest)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 Manifest가 현재 예상한 RevisionID와 동일한지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private ReadResponse<FileRevisionManifest> ReadManifest
        (
            string scopeID,
            string revisionID
        )
        {
            var response = CreateManifestStore(scopeID).Load(GetManifestPath(scopeID, revisionID));

            if (!response.Success)
            {
                return response;
            }

            return string.Equals(response.Value.RevisionID, revisionID, StringComparison.Ordinal)
               ? response
               : ReadResponse<FileRevisionManifest>.Fail("Manifest의 RevisionID가 파일명과 일치하지 않습니다.");
        }

    #endregion

    #region 파일 경로

        // ------------------------------------------------------------
        /// <summary>
        /// Scope의 current 포인터 파일 경로.
        /// </summary>
        // ------------------------------------------------------------
        private string GetCurrentPath(string scopeID)
        {
            return Path.Combine(directory, scopeID, "current.txt");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Commit 직전의 정상 Revision 포인터를 보존하는 파일 경로.
        /// </summary>
        // ------------------------------------------------------------
        private string GetBackupPath(string scopeID)
        {
            return GetCurrentPath(scopeID) + ".bak";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 불변 Revision Manifest 파일 경로.
        /// </summary>
        // ------------------------------------------------------------
        private string GetManifestPath(string scopeID, string revisionID)
        {
            return Path.Combine(directory, scopeID, "revisions", revisionID + ".json");
        }

    #endregion

    #region 식별자 생성·검증

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 기존 불변 파일의 ID를 재사용하지 않도록 기록 전 경로 충돌을 검사한다.
        /// <br/> 프로세스 사이의 동시 파일 생성을 차단하는 잠금은 아니다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static string CreateUnusedID(string targetDirectory, string suffix)
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var id = Guid.NewGuid().ToString("N");

                if (!File.Exists(Path.Combine(targetDirectory, id + suffix)))
                {
                    return id;
                }
            }

            throw new IOException("사용 가능한 Revision 파일 ID를 생성하지 못했습니다.");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 경로에 안전한 ScopeID를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ValidateScopeID(string scopeID)
        {
            if (!SaveIdentifier.IsValid(scopeID))
            {
                throw new ArgumentException("ScopeID가 올바르지 않습니다.", nameof(scopeID));
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// current와 Manifest에서 사용하는 불변 RevisionID를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ValidateRevisionID(string revisionID)
        {
            if (!SaveIdentifier.IsValid(revisionID))
            {
                throw new InvalidOperationException("RevisionID가 올바르지 않습니다.");
            }
        }

    #endregion

    #region Manifest 검증·변환

        // ----------------------------------------------------------------------
        /// <summary>
        /// Manifest의 물리 형식, Scope, Entry 중복 및 경로 안전성을 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ValidateManifest(string scopeID, FileRevisionManifest manifest)
        {
            if (manifest == null ||
                !string.Equals(manifest.FormatVersion, manifestFormatVersion, StringComparison.Ordinal) ||
                !string.Equals(manifest.ScopeID, scopeID, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Manifest 형식 또는 ScopeID가 일치하지 않습니다.");
            }

            ValidateRevisionID(manifest.RevisionID);

            if (!string.IsNullOrEmpty(manifest.ParentRevisionID))
            {
                ValidateRevisionID(manifest.ParentRevisionID);
                if (string.Equals(manifest.ParentRevisionID, manifest.RevisionID, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Manifest는 자신을 부모 Revision으로 가리킬 수 없습니다.");
                }
            }

            if (string.IsNullOrWhiteSpace(manifest.SavedAtUTC) ||
                !manifest.SavedAtUTC.EndsWith("Z", StringComparison.Ordinal) ||
                !DateTime.TryParseExact
                (
                    manifest.SavedAtUTC, "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out _
                ))
            {
                throw new InvalidOperationException("Manifest 저장 시각이 올바르지 않습니다.");
            }

            if (manifest.Entries == null)
            {
                throw new InvalidOperationException("Manifest Entry 목록이 비어 있습니다.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in manifest.Entries)
            {
                if (entry == null ||
                    !SaveIdentifier.IsValid(entry.EntryID) ||
                    !SaveIdentifier.IsValid(entry.RecordID) ||
                    !RevisionRecordValidator.IsValidHash(entry.ContentHash) ||
                    entry.ByteLength <= 0 ||
                    !seen.Add(entry.EntryID))
                {
                    throw new InvalidOperationException("Manifest의 EntryID 또는 RecordID가 올바르지 않습니다.");
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 오름차순 Entry map을 직렬화 가능한 Manifest 항목으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static FileRevisionManifest.Entry[] CreateEntries
        (
            SortedDictionary<string, FileRevisionManifest.Entry> entries
        )
        {
            var result = new FileRevisionManifest.Entry[entries.Count];
            var index = 0;

            foreach (var entry in entries)
            {
                result[index++] = entry.Value;
            }

            return result;
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// 이기종 Entry의 파일 Revision을 저장하고 복원한다.
    /// </summary>
    // ============================================================
    public sealed class FileRevisionSave
    {

    #region 필드

        private static readonly RecordType internalSchema = new
        (
            "xeri.revision.entry", "1"
        );

        private readonly FileRevisionSave<RevisionEntry> revision;
        private readonly ISerializer serializer;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 Revision 엔진의 안전한 File Commit 경계를 재사용한다.
        /// </summary>
        // ------------------------------------------------------------
        public FileRevisionSave
        (
            string directory,
            ISerializer serializer,
            IDataReader<string, string> reader = null,
            IDataWriter<string, string> writer = null
        )
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            revision = new FileRevisionSave<RevisionEntry>
            (
                directory, internalSchema, serializer, reader, writer, true
            );
        }

    #endregion

    #region Revision 시작

        // ------------------------------------------------------------
        /// <summary>
        /// 새 Revision 기록 작업을 시작합니다.
        /// </summary>
        // ------------------------------------------------------------
        public FileRevisionWriter BeginRevision
        (
            string scopeID, string expectedRevisionID = null
        )
        {
            return new FileRevisionWriter
            (
                revision.BeginRevision(scopeID, expectedRevisionID), serializer
            );
        }

    #endregion

    #region Revision 조회

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 확정된 Revision 식별자를 반환합니다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<string> GetCurrentRevisionID(string scopeID)
        {
            return revision.GetCurrentRevisionID(scopeID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 Revision의 기록을 읽습니다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<FileRevisionSnapshot> LoadRevision(string scopeID)
        {
            return Wrap(revision.LoadRevision(scopeID));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 Revision을 읽기용으로 엽니다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<FileRevisionSnapshot> OpenRevision
        (
            string scopeID, string revisionID
        )
        {
            return Wrap(revision.OpenRevision(scopeID, revisionID));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Revision의 특정 Entry를 요청 타입·Schema로 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<TBody> ReadEntry<TBody>
        (
            string scopeID, string entryID, RecordType schema
        )
        where TBody : class
        {
            var opened = LoadRevision(scopeID);
            return opened.Success
                ? opened.Value.ReadEntry<TBody>(entryID, schema)
                : ReadResponse<TBody>.Fail(opened.Error, opened.Exception);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 Revision 목록을 반환합니다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<string[]> ListRevisions(string scopeID)
        {
            return revision.ListRevisions(scopeID);
        }

    #endregion

    #region 백업 복원

        // ------------------------------------------------------------
        /// <summary>
        /// 백업 Revision의 기록을 읽습니다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<FileRevisionSnapshot> LoadBackupRevision(string scopeID)
        {
            return Wrap(revision.LoadBackupRevision(scopeID));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 백업 Revision을 현재 기록으로 복원합니다.
        /// </summary>
        // ------------------------------------------------------------
        public WriteResponse RestoreBackupRevision(string scopeID)
        {
            return revision.RestoreBackupRevision(scopeID);
        }

    #endregion

    #region Revision 정리

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 보존 정책대로 Manifest와 미참조 Record를 정리한다.
        /// <br/> 정리는 원자적이지 않으며 활성 Snapshot은 자동 보호하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public WriteResponse PruneRevisions
        (
            string scopeID, int keepLatest,
            IReadOnlyCollection<string> protectedRevisionIDs = null
        )
        {
            return revision.PruneRevisions(scopeID, keepLatest, protectedRevisionIDs);
        }

    #endregion

    #region 내부 변환

        private ReadResponse<FileRevisionSnapshot> Wrap
        (
            ReadResponse<FileRevisionSnapshot<RevisionEntry>> result
        )
        {
            return result.Success
                ? ReadResponse<FileRevisionSnapshot>.Succeed
                (
                    new FileRevisionSnapshot(result.Value, serializer)
                )
                : ReadResponse<FileRevisionSnapshot>.Fail(result.Error, result.Exception);
        }

    #endregion

    }
}
