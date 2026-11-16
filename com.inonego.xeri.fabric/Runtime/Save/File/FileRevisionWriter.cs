/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FileRevisionWriter.cs
수정일 : 2026-11-15

# 설명
한 Revision의 Entry 변경·삭제를 수집하고 Commit 시작 시 변경 집합을 분리한다.
동형·이기종 Writer의 재진입과 종료 상태를 검증하고 파일 엔진으로 확정을 위임한다.

# 제약
Dispose는 자동으로 Commit하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Persistence;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.Save
{

    // ============================================================
    /// <summary>
    /// 여러 Entry 변경을 하나의 Revision으로 확정하도록 준비한다.
    /// </summary>
    /// <typeparam name="TBody">Entry Snapshot Body 타입.</typeparam>
    // ============================================================
    public sealed class FileRevisionWriter<TBody> : IDisposable
    where TBody : class
    {

    #region 필드

        private readonly FileRevisionSave<TBody> owner = null;
        private readonly string scopeID = string.Empty;
        private readonly string expectedRevisionID = null;
        private readonly Dictionary<string, TBody> updates = new(StringComparer.Ordinal);
        private readonly HashSet<string> removes = new(StringComparer.Ordinal);
        private readonly object stateLock = new();
        private bool completed = false;
        private bool capturing = false;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Scope와 선택적인 예상 Revision을 고정한다.
        /// </summary>
        // ------------------------------------------------------------
        internal FileRevisionWriter
        (
            FileRevisionSave<TBody> owner,
            string scopeID,
            string expectedRevisionID
        )
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.scopeID = scopeID;
            this.expectedRevisionID = expectedRevisionID;
        }

    #endregion

    #region 변경 수집

        // ------------------------------------------------------------
        /// <summary>
        /// Entry의 새로운 Snapshot을 다음 Revision에 포함한다.
        /// </summary>
        // ------------------------------------------------------------
        public void PutEntry(string entryID, TBody body)
        {
            lock (stateLock)
            {
                ValidatePutEntry(entryID);

                if (body == null)
                {
                    throw new ArgumentNullException(nameof(body));
                }

                updates[entryID] = body;
                removes.Remove(entryID);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Entry가 다음 Revision에 나타나지 않도록 삭제를 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        public void RemoveEntry(string entryID)
        {
            lock (stateLock)
            {
                EnsureActive();

                if (!SaveIdentifier.IsValid(entryID))
                {
                    throw new ArgumentException("EntryID가 올바르지 않습니다.", nameof(entryID));
                }

                updates.Remove(entryID);
                removes.Add(entryID);
            }
        }

    #endregion

    #region 변경 캡처·검증

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Body 캡처 중 Writer 재진입을 차단한다.
        /// <br/> Writer가 유효할 때만 변경을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        internal void PutCapturedEntry(string entryID, Func<TBody> capture)
        {
            lock (stateLock)
            {
                ValidatePutEntry(entryID);

                if (capture == null)
                {
                    throw new ArgumentNullException(nameof(capture));
                }

                capturing = true;
            }

            try
            {
                // Serializer의 사용자 콜백은 잠금을 보유하지 않고 실행한다.
                var body = capture();

                lock (stateLock)
                {
                    if (completed)
                    {
                        throw new InvalidOperationException("이미 종료된 Revision Writer입니다.");
                    }

                    if (body == null)
                    {
                        throw new InvalidOperationException("Entry Body 캡처 결과가 비어 있습니다.");
                    }

                    updates[entryID] = body;
                    removes.Remove(entryID);
                }
            }
            finally
            {
                lock (stateLock)
                {
                    capturing = false;
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Writer 상태와 EntryID를 변경 전에 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ValidatePutEntry(string entryID)
        {
            EnsureActive();

            if (!SaveIdentifier.IsValid(entryID))
            {
                throw new ArgumentException("EntryID가 올바르지 않습니다.", nameof(entryID));
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 확정 또는 폐기된 Writer를 다시 사용하지 않도록 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private void EnsureActive()
        {
            if (completed)
            {
                throw new InvalidOperationException("이미 Commit 또는 Dispose한 Revision Writer입니다.");
            }

            if (capturing)
            {
                throw new InvalidOperationException("Entry Body 캡처 중에는 Revision Writer를 변경할 수 없습니다.");
            }
        }

    #endregion

    #region 확정·폐기

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 모든 변경을 새 Record·Manifest로 작성한 뒤 current 포인터를 마지막에 교체한다.
        /// <br/> 이 Writer는 성공·실패와 무관하게 한 번만 Commit할 수 있다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public WriteResponse Commit()
        {
            IReadOnlyDictionary<string, TBody> pendingUpdates;
            IReadOnlyCollection<string> pendingRemoves;

            lock (stateLock)
            {
                EnsureActive();

                // 저장 중 Writer의 Dispose가 보류 컬렉션을 변경하지 않도록 소유권을 분리한다.
                pendingUpdates = new Dictionary<string, TBody>(updates, StringComparer.Ordinal);
                pendingRemoves = new HashSet<string>(removes, StringComparer.Ordinal);
                completed = true;
                updates.Clear();
                removes.Clear();
            }

            try
            {
                return owner.CommitRevision(scopeID, expectedRevisionID, pendingUpdates, pendingRemoves);
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 보류 중인 변경을 폐기한다. 파일 Commit을 실행하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            lock (stateLock)
            {
                completed = true;
                updates.Clear();
                removes.Clear();
            }
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// 이기종 Entry의 보류 변경과 단일 Commit 수명을 관리한다.
    /// </summary>
    // ============================================================
    public sealed class FileRevisionWriter : IDisposable
    {

    #region 필드

        private readonly FileRevisionWriter<RevisionEntry> writer;
        private readonly ISerializer serializer;

    #endregion

    #region 생성자

        internal FileRevisionWriter
        (
            FileRevisionWriter<RevisionEntry> writer,
            ISerializer serializer
        )
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

    #endregion

    #region 변경 수집

        // ------------------------------------------------------------
        /// <summary>
        /// Entry의 타입·Schema와 직렬화된 값을 보류 Revision에 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void PutEntry<TBody>(string entryID, TBody body, RecordType schema)
        where TBody : class
        {
            writer.PutCapturedEntry(entryID, () => RevisionEntry.Capture(body, schema, serializer));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Revision에서 지정한 Entry를 제거합니다.
        /// </summary>
        // ------------------------------------------------------------
        public void RemoveEntry(string entryID)
        {
            writer.RemoveEntry(entryID);
        }

    #endregion

    #region 확정·폐기

        // ------------------------------------------------------------
        /// <summary>
        /// 준비된 변경을 저장소에 확정하고 결과를 반환합니다.
        /// </summary>
        // ------------------------------------------------------------
        public WriteResponse Commit()
        {
            return writer.Commit();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 보유한 리소스나 구독을 해제합니다.
        /// </summary>
        // ------------------------------------------------------------
        public void Dispose()
        {
            writer.Dispose();
        }

    #endregion

    }
}
