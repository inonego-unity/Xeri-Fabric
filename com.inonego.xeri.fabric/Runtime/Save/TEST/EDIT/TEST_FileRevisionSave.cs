/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_FileRevisionSave.cs
수정일 : 2026-11-16

# 설명
FileRevisionSave의 동형·이기종 Entry와 Manifest/포인터의 성공·실패 경계를 검증한다.
단일 파일 교체가 다중 파일 원자성 및 전원 손실 내구성을 보장한다고 가정하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;
using System.Text;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Persistence;
using inonego.Xeri.Serializable;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.Save.TEST
{
    // ============================================================
    /// <summary>
    /// FileRevisionSave의 Snapshot/Copy-on-write/실패 보존 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_FileRevisionSave
    {

    #region 테스트 객체

        // ============================================================
        /// <summary>
        /// 직렬화 가능한 Entry Snapshot 예시.
        /// </summary>
        // ============================================================
        [Serializable]
        private sealed class TestEntry
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 게임 상태 값.
            /// </summary>
            // ------------------------------------------------------------
            public int Value;
        }

        // ============================================================
        /// <summary>
        /// current 포인터의 임시 파일 쓰기를 실패시키는 Writer.
        /// </summary>
        // ============================================================
        private sealed class FailingCurrentWriter : IDataWriter<string, string>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// current 파일을 변경하지 않고 실패시킨다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string path, string value)
            {
                if (path.Contains("current.txt.", StringComparison.Ordinal))
                {
                    File.WriteAllText(path, "partial");
                    return WriteResponse.Fail("current write failed");
                }

                return TextFileIO.Default.Write(path, value);
            }
        }

        // ============================================================
        /// <summary>
        /// Entry Record를 쓰기 전에 실패하는 Writer.
        /// </summary>
        // ============================================================
        private sealed class FailingEntryWriter : IDataWriter<string, string>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// records 디렉터리의 파일 작성 요청을 거절한다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string path, string value)
            {
                return path.Contains(".save.json.", StringComparison.Ordinal)
                   ? WriteResponse.Fail("entry write failed")
                   : TextFileIO.Default.Write(path, value);
            }
        }

        // ============================================================
        /// <summary>
        /// Record를 잘못 기록하면서도 성공을 반환하는 Writer.
        /// </summary>
        // ============================================================
        private sealed class CorruptingRecordWriter : IDataWriter<string, string>
        {
            private readonly bool corruptSchema;

            // ------------------------------------------------------------
            /// <summary>
            /// 기록을 훼손할 방식을 선택한다.
            /// </summary>
            // ------------------------------------------------------------
            public CorruptingRecordWriter(bool corruptSchema)
            {
                this.corruptSchema = corruptSchema;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 잘못된 JSON 또는 Schema를 정상 쓰기처럼 게시한다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string path, string value)
            {
                if (!path.Contains(".save.json.", StringComparison.Ordinal))
                {
                    return TextFileIO.Default.Write(path, value);
                }

                if (!corruptSchema)
                {
                    return TextFileIO.Default.Write(path, "{ invalid json");
                }

                var record = UnityJsonSerializer.Default.Deserialize<SaveRecord<TestEntry>>(value);
                record.Metadata.SchemaID = "invalid.schema";
                return TextFileIO.Default.Write
                (
                    path, UnityJsonSerializer.Default.Serialize(record)
                );
            }
        }

        // ============================================================
        /// <summary>
        /// Revision Manifest 파일의 작성을 거절하는 Writer.
        /// </summary>
        // ============================================================
        private sealed class FailingManifestWriter : IDataWriter<string, string>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Manifest 경로에서만 저장 실패를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string path, string value)
            {
                return path.Contains("revisions", StringComparison.Ordinal) &&
                       path.Contains(".json.", StringComparison.Ordinal)
                   ? WriteResponse.Fail("manifest write failed")
                   : TextFileIO.Default.Write(path, value);
            }
        }

        // ============================================================
        /// <summary>
        /// Record 작성 중 Writer의 수명 종료를 재현한다.
        /// </summary>
        // ============================================================
        private sealed class CallbackRecordWriter : IDataWriter<string, string>
        {
            internal Action OnRecordWrite;
            internal Action OnCurrentWrite;

            // ------------------------------------------------------------
            /// <summary>
            /// Entry Record 기록 시 콜백을 한 번 실행한다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string path, string value)
            {
                if (path.Contains(".save.json.", StringComparison.Ordinal))
                {
                    var callback = OnRecordWrite;
                    OnRecordWrite = null;
                    callback?.Invoke();
                }

                if (path.Contains("current.txt.", StringComparison.Ordinal))
                {
                    var callback = OnCurrentWrite;
                    OnCurrentWrite = null;
                    callback?.Invoke();
                }

                return TextFileIO.Default.Write(path, value);
            }
        }

        // ============================================================
        /// <summary>
        /// Reader 결과에서 전달된 ReleaseHandle의 회수를 추적한다.
        /// </summary>
        // ============================================================
        private sealed class MarkerLeaseReader : IDataReader<string, string>
        {
            internal bool FailMarker;
            internal int MarkerReads;
            internal int Released;

            // ------------------------------------------------------------
            /// <summary>
            /// 종류 표식 읽기에만 추적용 Handle을 추가한다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadResponse<string> Read(string location)
            {
                var result = TextFileIO.Default.Read(location);

                if (!location.EndsWith("revision.kind", StringComparison.Ordinal))
                {
                    return result;
                }

                MarkerReads++;
                var lease = new MarkerLease(() => Released++);

                return FailMarker
                    ? ReadResponse<string>.Fail("marker read failed", releaseHandle: lease)
                    : ReadResponse<string>.Succeed(result.Value, lease);
            }
        }

        // ============================================================
        /// <summary>
        /// 원본 파일과 다른 정상 JSON을 반환하는 Reader.
        /// </summary>
        // ============================================================
        private sealed class ReplacedRecordReader : IDataReader<string, string>
        {
            internal int RecordReads;

            // ------------------------------------------------------------
            /// <summary>
            /// Record 경로에서만 복원할 Body 값을 바꾼다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadResponse<string> Read(string location)
            {
                var result = TextFileIO.Default.Read(location);
                if (!result.Success || !location.EndsWith(".save.json", StringComparison.Ordinal))
                {
                    return result;
                }

                RecordReads++;
                var record = UnityJsonSerializer.Default.Deserialize<SaveRecord<TestEntry>>(result.Value);
                record.Body.Value = 99;
                return ReadResponse<string>.Succeed(UnityJsonSerializer.Default.Serialize(record));
            }
        }

        // ============================================================
        /// <summary>
        /// Prune에서 동일한 불변 Record를 몇 번 읽는지 측정한다.
        /// </summary>
        // ============================================================
        private sealed class CountingRecordReader : IDataReader<string, string>
        {
            internal int RecordReads;

            // ------------------------------------------------------------
            /// <summary>
            /// Record 파일 읽기만 세고 원본 IO에 위임한다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadResponse<string> Read(string location)
            {
                if (location.EndsWith(".save.json", StringComparison.Ordinal))
                {
                    RecordReads++;
                }

                return TextFileIO.Default.Read(location);
            }
        }

        // ============================================================
        /// <summary>
        /// Reader의 명시적 해제 호출을 측정하는 Handle.
        /// </summary>
        // ============================================================
        private sealed class MarkerLease : IReleaseHandle
        {
            private readonly Action onRelease;

            // ------------------------------------------------------------
            /// <summary>
            /// Handle이 이미 회수됐는지 확인한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool IsReleased { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 리소스 해제 추적 콜백을 등록한다.
            /// </summary>
            // ------------------------------------------------------------
            public MarkerLease(Action onRelease)
            {
                this.onRelease = onRelease;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Handle을 한 번만 회수한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Release()
            {
                if (!IsReleased)
                {
                    IsReleased = true;
                    onRelease?.Invoke();
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Handle을 해제한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Dispose()
            {
                Release();
            }
        }

        // ============================================================
        /// <summary>
        /// Unity 직렬화 콜백에서 Writer 재진입을 검증하는 Body.
        /// </summary>
        // ============================================================
        [Serializable]
        private sealed class CallbackEntry : ISerializationCallbackReceiver
        {
            internal static Action OnSerialize;
            internal static Action OnDeserialize;

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트용 직렬화 Body 값.
            /// </summary>
            // ------------------------------------------------------------
            public int Value;

            // ------------------------------------------------------------
            /// <summary>
            /// 직렬화 중 재진입 콜백을 실행한다.
            /// </summary>
            // ------------------------------------------------------------
            public void OnBeforeSerialize()
            {
                OnSerialize?.Invoke();
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 역직렬화 후 별도 처리는 하지 않는다.
            /// </summary>
            // ------------------------------------------------------------
            public void OnAfterDeserialize()
            {
                OnDeserialize?.Invoke();
            }
        }

        [Serializable]
        private sealed class PlayerEntry
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 플레이어의 레벨.
            /// </summary>
            // ------------------------------------------------------------
            public int Level;
        }

        [Serializable]
        private sealed class QuestEntry
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 퀘스트 제목.
            /// </summary>
            // ------------------------------------------------------------
            public string Title;
        }

        private readonly RecordType playerSchema = new("game.player", "1");
        private readonly RecordType questSchema = new("game.quest", "2");

    #endregion

    #region 픽스처

        private string directory = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 독립 임시 저장소를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine
            (
                Path.GetTempPath(),
                "XeriRevisionTest_" + Guid.NewGuid().ToString("N")
            );

            Directory.CreateDirectory(directory);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 파일을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            CallbackEntry.OnSerialize = null;
            CallbackEntry.OnDeserialize = null;
        }

    #endregion

    #region Commit 및 복원

        // ------------------------------------------------------------
        /// <summary>
        /// 첫 Revision에서 여러 Entry를 하나의 Manifest로 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_첫_Commit_여러_Entry_왕복()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 7
                    }
                );
                writer.PutEntry
                (
                    "quest",
                    new TestEntry
                    {
                        Value = 13
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var revision = save.LoadRevision("world-a");

            Assert.IsTrue(revision.Success, revision.Error);
            Assert.IsTrue(Guid.TryParseExact(revision.Value.RevisionID, "N", out _));
            Assert.AreEqual(7, revision.Value.ReadEntry("player").Value.Value);
            Assert.AreEqual(13, revision.Value.ReadEntry("quest").Value.Value);
            Assert.AreEqual(2, revision.Value.ListEntries().Length);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 변경되지 않은 Entry의 Record를 공유하고 과거 Revision을 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_변경된_Entry만_교체하고_이전_읽기()
        {
            var save = CreateSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                writer.PutEntry
                (
                    "quest",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var previous = save.LoadRevision("world-a").Value;
            using (var writer = save.BeginRevision("world-a", previous.RevisionID))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 11
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var current = save.LoadRevision("world-a").Value;

            Assert.AreEqual(1, previous.ReadEntry("player").Value.Value);
            Assert.AreEqual(2, previous.ReadEntry("quest").Value.Value);
            Assert.AreEqual(11, current.ReadEntry("player").Value.Value);
            Assert.AreEqual(2, current.ReadEntry("quest").Value.Value);
            Assert.AreEqual(1, save.OpenRevision("world-a", previous.RevisionID).Value.ReadEntry("player").Value.Value);
            Assert.IsFalse(string.Equals(previous.RevisionID, current.RevisionID, StringComparison.Ordinal));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// RemoveEntry는 신규 Manifest에서만 삭제되고 과거 Revision은 유지된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_삭제는_새_Revision에만_반영()
        {
            var save = CreateSave();
            using (var writer = save.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 9
                    }
                );
                writer.PutEntry
                (
                    "quest",
                    new TestEntry
                    {
                        Value = 4
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var previous = save.LoadRevision("world-a").Value;

            using (var writer = save.BeginRevision("world-a", previous.RevisionID))
            {
                writer.RemoveEntry("quest");
                Assert.IsTrue(writer.Commit().Success);
            }

            var current = save.LoadRevision("world-a").Value;

            Assert.AreEqual(1, current.ListEntries().Length);
            Assert.IsFalse(current.ReadEntry("quest").Success);
            Assert.AreEqual(4, previous.ReadEntry("quest").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Revision을 다시 읽을 때 다른 객체에서도 같은 파일을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_새로운_인스턴스에서_재오픈()
        {
            var first = CreateSave();

            using (var writer = first.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 42
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var second = CreateSave();

            Assert.AreEqual(42, second.ReadEntry("world-a", "player").Value.Value);
            Assert.IsTrue(second.GetCurrentRevisionID("world-a").Success);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 이전 Revision을 기반으로 한 Writer는 최신 Revision을 덮지 못한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_예상_ID_불일치_실패()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 99
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(1, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Dispose만 수행한 Writer는 디스크에 Revision을 만들지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Commit_없는_Dispose_무변경()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 5
                    }
                );
            }

            Assert.AreEqual(string.Empty, save.GetCurrentRevisionID("world-a").Value);
            Assert.IsFalse(save.LoadRevision("world-a").Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Commit 중 Dispose에도 확정된 변경 집합을 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Commit_중_Dispose_변경_집합_보존()
        {
            var source = new CallbackRecordWriter();
            var save = CreateSave(source);

            using var writer = save.BeginRevision("world-a", string.Empty);
            writer.PutEntry
            (
                "player",
                new TestEntry
                {
                    Value = 7
                }
            );
            writer.PutEntry
            (
                "quest",
                new TestEntry
                {
                    Value = 11
                }
            );

            source.OnRecordWrite = writer.Dispose;

            var result = writer.Commit();
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(7, save.ReadEntry("world-a", "player").Value.Value);
            Assert.AreEqual(11, save.ReadEntry("world-a", "quest").Value.Value);
        }

    #endregion

    #region 실패 경계

        // ------------------------------------------------------------
        /// <summary>
        /// 포인터의 임시 쓰기가 실패해도 이전 Revision은 그대로 유지된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_current_작성_실패_이전_상태_유지()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var before = save.GetCurrentRevisionID("world-a").Value;
            var failing = CreateSave(new FailingCurrentWriter());

            using (var writer = failing.BeginRevision("world-a", before))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(before, save.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(1, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Entry 쓰기가 실패해도 current 포인터 파일은 생성하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Entry_작성_실패_Commit_안함()
        {
            var save = CreateSave(new FailingEntryWriter());

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 5
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(string.Empty, save.GetCurrentRevisionID("world-a").Value);
            Assert.IsFalse(File.Exists(Path.Combine(directory, "world-a", "current.txt")));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Writer가 훼손한 Record는 성공 반환 후에도 게시하지 않는다.
        /// <br/> 첫 Commit과 후속 Commit의 JSON·Schema 손상을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TEST_FileRevisionSave_손상_Record_게시_전_차단
        (
            bool hasPreviousRevision, bool corruptSchema
        )
        {
            var save = CreateSave();

            if (hasPreviousRevision)
            {
                using (var initial = save.BeginRevision("world-a", string.Empty))
                {
                    initial.PutEntry
                    (
                        "player", new TestEntry
                        {
                            Value = 10
                        }
                    );
                    Assert.IsTrue(initial.Commit().Success);
                }
            }

            var before = save.GetCurrentRevisionID("world-a");
            Assert.IsTrue(before.Success, before.Error);

            var failing = CreateSave(new CorruptingRecordWriter(corruptSchema));
            using (var writer = failing.BeginRevision("world-a", before.Value))
            {
                writer.PutEntry
                (
                    "player", new TestEntry
                    {
                        Value = 99
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(before.Value, save.GetCurrentRevisionID("world-a").Value);
            var manifests = save.ListRevisions("world-a");
            Assert.IsTrue(manifests.Success, manifests.Error);
            Assert.AreEqual(hasPreviousRevision ? 1 : 0, manifests.Value.Length);

            if (hasPreviousRevision)
            {
                Assert.AreEqual(10, save.ReadEntry("world-a", "player").Value.Value);
            }
            else
            {
                Assert.IsFalse(save.LoadRevision("world-a").Success);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Manifest 작성 실패는 current를 변경하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Manifest_작성_실패_이전_상태_유지()
        {
            var original = CreateSave();
            using (var writer = original.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 6
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var before = original.GetCurrentRevisionID("world-a").Value;
            var failing = CreateSave(new FailingManifestWriter());

            using (var writer = failing.BeginRevision("world-a", before))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 99
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(before, original.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(6, original.ReadEntry("world-a", "player").Value.Value);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Manifest에 존재하는 Entry의 파일이 없으면 해당 Entry 읽기만 실패한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_참조한_Record_누락_실패()
        {
            var save = CreateSave();
            using (var writer = save.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var currentID = save.GetCurrentRevisionID("world-a").Value;
            var manifestPath = Path.Combine(directory, "world-a", "revisions", currentID + ".json");
            var manifest = UnityJsonSerializer.Default.Deserialize<FileRevisionManifest>
            (
                File.ReadAllText(manifestPath)
            );

            var recordID = manifest.Entries[0].RecordID;
            File.Delete(Path.Combine(directory, "world-a", "records", recordID + ".save.json"));

            var snapshot = save.LoadRevision("world-a");

            Assert.IsTrue(snapshot.Success, snapshot.Error);
            Assert.IsFalse(snapshot.Value.ReadEntry("player").Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Manifest가 Entry 형식 버전 1로 저장되는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Manifest_버전_1_기록()
        {
            var save = CreateSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 8
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var revisionID = save.GetCurrentRevisionID("world-a").Value;
            var path = Path.Combine(directory, "world-a", "revisions", revisionID + ".json");
            var manifest = UnityJsonSerializer.Default.Deserialize<FileRevisionManifest>
            (
                File.ReadAllText(path)
            );

            Assert.AreEqual("1", manifest.FormatVersion);
            Assert.AreEqual(1, manifest.Entries.Length);
            Assert.AreEqual("player", manifest.Entries[0].EntryID);


        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 EntryID가 중복된 손상 Manifest는 열지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Manifest_중복_EntryID_거절()
        {
            var save = CreateSave();
            var revisionID = "rev0001";
            var scope = Path.Combine(directory, "world-a");
            var revisions = Path.Combine(scope, "revisions");
            Directory.CreateDirectory(revisions);
            File.WriteAllText(Path.Combine(scope, "current.txt"), revisionID);

            var manifest = new FileRevisionManifest
            {
                FormatVersion = "1",
                ScopeID = "world-a",
                RevisionID = revisionID,
                Entries = new[]
                {
                    new FileRevisionManifest.Entry
                    {
                        EntryID = "player",
                        RecordID = "a"
                    },
                    new FileRevisionManifest.Entry
                    {
                        EntryID = "player",
                        RecordID = "b"
                    }
                }
            };

            File.WriteAllText
            (
                Path.Combine(revisions, revisionID + ".json"),
                UnityJsonSerializer.Default.Serialize(manifest)
            );

            Assert.IsFalse(save.LoadRevision("world-a").Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 손상된 current 포인터가 경로 이탈을 유발하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_잘못된_current_거절()
        {
            var save = CreateSave();
            var scope = Path.Combine(directory, "world-a");
            Directory.CreateDirectory(scope);
            File.WriteAllText(Path.Combine(scope, "current.txt"), "../outside");

            Assert.IsFalse(save.LoadRevision("world-a").Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 손상되거나 다른 Scope를 담은 Manifest를 복원하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Manifest_손상_거절()
        {
            var save = CreateSave();
            var revisionID = "revision1";
            var revisions = Path.Combine(directory, "world-a", "revisions");
            Directory.CreateDirectory(revisions);
            File.WriteAllText(Path.Combine(directory, "world-a", "current.txt"), revisionID);

            var manifest = new FileRevisionManifest
            {
                FormatVersion = "1",
                ScopeID = "world-b",
                RevisionID = revisionID
            };

            File.WriteAllText
            (
                Path.Combine(revisions, revisionID + ".json"),
                UnityJsonSerializer.Default.Serialize(manifest)
            );

            Assert.IsFalse(save.LoadRevision("world-a").Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 없는 Entry와 잘못된 ScopeID는 정상 복원으로 취급하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_잘못된_입력_거절()
        {
            var save = CreateSave();

            Assert.Throws<ArgumentException>(() => save.BeginRevision("../escape"));
            Assert.IsFalse(save.LoadRevision("../escape").Success);
            Assert.IsFalse(save.ReadEntry("world-a", "../escape").Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 성공한 Writer를 다시 Commit할 수 없다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Writer_중복_Commit_거절()
        {
            var save = CreateSave();
            using var writer = save.BeginRevision("world-a");
            writer.PutEntry
            (
                "player",
                new TestEntry
                {
                    Value = 1
                }
            );

            Assert.IsTrue(writer.Commit().Success);
            Assert.Throws<InvalidOperationException>(() => writer.Commit());
        }

    #endregion

    #region 복구·정리

        // ------------------------------------------------------------
        /// <summary>
        /// 다른 인스턴스가 Scope 잠금을 보유하면 새 Commit을 시작하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Scope_잠금_중_Commit_거절()
        {
            var save = CreateSave();
            var scope = Path.Combine(directory, "world-a");
            Directory.CreateDirectory(scope);

            using (var lease = new FileStream
            (
                Path.Combine(scope, "commit.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None
            ))
            {
                using var writer = save.BeginRevision("world-a", string.Empty);
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );

                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.IsFalse(save.LoadRevision("world-a").Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 손상된 current는 이전 확정 Revision으로 명시적으로 복원할 수 있다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_current_손상_이전_포인터_복구()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var previousID = save.GetCurrentRevisionID("world-a").Value;

            using (var writer = save.BeginRevision("world-a", previousID))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var backup = save.LoadBackupRevision("world-a");
            Assert.IsTrue(backup.Success, backup.Error);
            Assert.AreEqual(previousID, backup.Value.RevisionID);
            Assert.AreEqual(1, backup.Value.ReadEntry("player").Value.Value);

            File.WriteAllText(Path.Combine(directory, "world-a", "current.txt"), "../damaged");

            Assert.IsFalse(save.LoadRevision("world-a").Success);
            Assert.IsTrue(save.RestoreBackupRevision("world-a").Success);
            Assert.AreEqual(previousID, save.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(1, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패한 Commit은 이전 정상 백업 포인터를 덮어쓰지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_current_쓰기_실패_백업_세대_보존()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var firstID = save.GetCurrentRevisionID("world-a").Value;
            using (var writer = save.BeginRevision("world-a", firstID))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var secondID = save.GetCurrentRevisionID("world-a").Value;
            var failing = CreateSave(new FailingCurrentWriter());
            using (var writer = failing.BeginRevision("world-a", secondID))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 3
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(secondID, save.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(firstID, save.LoadBackupRevision("world-a").Value.RevisionID);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 복구 후보의 Entry가 손상되면 current를 변경하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_복구_Entry_손상_거절()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var previousID = save.GetCurrentRevisionID("world-a").Value;
            using (var writer = save.BeginRevision("world-a", previousID))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var currentID = save.GetCurrentRevisionID("world-a").Value;
            var previousManifest = Path.Combine(directory, "world-a", "revisions", previousID + ".json");
            var manifest = UnityJsonSerializer.Default.Deserialize<FileRevisionManifest>
            (
                File.ReadAllText(previousManifest)
            );

            var recordID = manifest.Entries[0].RecordID;
            File.Delete(Path.Combine(directory, "world-a", "records", recordID + ".save.json"));

            Assert.IsFalse(save.RestoreBackupRevision("world-a").Success);
            Assert.AreEqual(currentID, save.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(2, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 첫 Commit이 current 기록 전에 실패해 Manifest만 남으면,
        /// <br/> 포인터 손실을 구별할 수 없어 Prune을 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_현재_포인터_없는_Manifest_보존()
        {
            var failing = CreateSave(new FailingCurrentWriter());
            using (var writer = failing.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            var scope = Path.Combine(directory, "world-a");
            var manifests = Path.Combine(scope, "revisions");
            var records = Path.Combine(scope, "records");
            Assert.AreEqual(1, Directory.GetFiles(manifests, "*.json").Length);
            Assert.AreEqual(1, Directory.GetFiles(records, "*.save.json").Length);

            var save = CreateSave();
            Assert.AreEqual(string.Empty, save.GetCurrentRevisionID("world-a").Value);
            Assert.IsFalse(save.PruneRevisions("world-a", 1).Success);

            // Manifest는 물리 이력 목록에 남지만 자동으로 Commit된 것으로 간주하지 않는다.
            var revisions = save.ListRevisions("world-a");
            Assert.IsTrue(revisions.Success, revisions.Error);
            Assert.AreEqual(1, revisions.Value.Length);

            using (var writer = save.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "new-entry",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(1, Directory.GetFiles(manifests, "*.json").Length);
            Assert.AreEqual(1, Directory.GetFiles(records, "*.save.json").Length);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 첫 Commit의 Manifest 작성 실패로 Record만 남아도 재시도할 수 있다.
        /// <br/> 남은 Record는 다음 Prune에서 미참조 기록으로 회수한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_초기_Manifest_실패_재시도_허용()
        {
            var failed = CreateSave(new FailingManifestWriter());
            using (var writer = failed.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            var save = CreateSave();
            Assert.AreEqual(string.Empty, save.GetCurrentRevisionID("world-a").Value);

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var records = Path.Combine(directory, "world-a", "records");
            Assert.AreEqual(2, Directory.GetFiles(records, "*.save.json").Length);
            Assert.AreEqual(2, save.ReadEntry("world-a", "player").Value.Value);
            Assert.IsTrue(save.PruneRevisions("world-a", 1).Success);
            Assert.AreEqual(1, Directory.GetFiles(records, "*.save.json").Length);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 정상 Revision의 current가 유실되면 새 이력을 시작하지 않는다.
        /// <br/> 보존된 백업에서 명시적으로 복원할 수 있다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_current_유실_새_Commit_거절_백업_복원()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var originalID = save.GetCurrentRevisionID("world-a").Value;
            using (var writer = save.BeginRevision("world-a", originalID))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var scope = Path.Combine(directory, "world-a");
            File.Delete(Path.Combine(scope, "current.txt"));

            Assert.AreEqual(string.Empty, save.GetCurrentRevisionID("world-a").Value);
            Assert.IsFalse(save.LoadRevision("world-a").Success);

            using (var writer = save.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 3
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(2, save.ListRevisions("world-a").Value.Length);
            Assert.IsTrue(save.RestoreBackupRevision("world-a").Success);
            Assert.AreEqual(originalID, save.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(1, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패한 Manifest 이전에 기록된 미참조 Record만 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_실패_Commit의_고아_Record_회수()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var failing = CreateSave(new FailingManifestWriter());
            using (var writer = failing.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            var records = Path.Combine(directory, "world-a", "records");
            Assert.AreEqual(2, Directory.GetFiles(records, "*.save.json").Length);

            Assert.IsTrue(save.PruneRevisions("world-a", 1).Success);
            Assert.AreEqual(1, Directory.GetFiles(records, "*.save.json").Length);
            Assert.AreEqual(1, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 과거 Manifest가 참조하는 Record는 고아 파일로 판단하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_과거_기록_보존()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var previousID = save.GetCurrentRevisionID("world-a").Value;
            using (var writer = save.BeginRevision("world-a", previousID))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            Assert.IsTrue(save.PruneRevisions("world-a", 2).Success);
            Assert.AreEqual
            (
                2, Directory.GetFiles(Path.Combine(directory, "world-a", "records"), "*.save.json").Length
            );
            Assert.AreEqual(1, save.OpenRevision("world-a", previousID).Value.ReadEntry("player").Value.Value);
            Assert.AreEqual(2, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 손상 Manifest가 있으면 참조 여부를 판단할 수 없으므로 Record를 삭제하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_손상_Manifest_삭제_거절()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var failing = CreateSave(new FailingManifestWriter());
            using (var writer = failing.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            var records = Path.Combine(directory, "world-a", "records");
            Assert.AreEqual(2, Directory.GetFiles(records, "*.save.json").Length);

            var currentID = save.GetCurrentRevisionID("world-a").Value;
            var manifestPath = Path.Combine(directory, "world-a", "revisions", currentID + ".json");
            File.WriteAllText(manifestPath, "{damaged manifest");

            Assert.IsFalse(save.PruneRevisions("world-a", 1).Success);
            Assert.AreEqual(2, Directory.GetFiles(records, "*.save.json").Length);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 세 번째 정상 Commit은 기존 백업을 바로 직전 Revision으로 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_세_세대_Commit_직전_포인터_백업()
        {
            var save = CreateSave();
            var revisionIDs = new string[3];

            for (var index = 0; index < revisionIDs.Length; index++)
            {
                var expected = index == 0 ? string.Empty : revisionIDs[index - 1];

                using (var writer = save.BeginRevision("world-a", expected))
                {
                    writer.PutEntry
                    (
                        "player",
                        new TestEntry
                        {
                            Value = index + 1
                        }
                    );
                    var result = writer.Commit();
                    Assert.IsTrue(result.Success, result.Error);
                }

                revisionIDs[index] = save.GetCurrentRevisionID("world-a").Value;
            }

            var backup = save.LoadBackupRevision("world-a");
            Assert.IsTrue(backup.Success, backup.Error);
            Assert.AreEqual(revisionIDs[1], backup.Value.RevisionID);
            Assert.AreEqual(2, backup.Value.ReadEntry("player").Value.Value);
            Assert.AreEqual(3, save.ReadEntry("world-a", "player").Value.Value);
            Assert.AreEqual(1, save.OpenRevision("world-a", revisionIDs[0]).Value.ReadEntry("player").Value.Value);

            Assert.IsTrue(save.RestoreBackupRevision("world-a").Success);
            Assert.AreEqual(revisionIDs[1], save.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(2, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 손상된 백업 포인터가 있으면 GC와 복구를 실패시켜 기존 파일을 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_손상_백업_정리와_복구_거절()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var currentID = save.GetCurrentRevisionID("world-a").Value;
            var failing = CreateSave(new FailingManifestWriter());

            using (var writer = failing.BeginRevision("world-a", currentID))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            var scope = Path.Combine(directory, "world-a");
            File.WriteAllText(Path.Combine(scope, "current.txt.bak"), "../damaged");
            var records = Path.Combine(scope, "records");

            Assert.AreEqual(2, Directory.GetFiles(records, "*.save.json").Length);
            Assert.IsFalse(save.PruneRevisions("world-a", 1).Success);
            Assert.IsFalse(save.RestoreBackupRevision("world-a").Success);
            Assert.AreEqual(2, Directory.GetFiles(records, "*.save.json").Length);
            Assert.AreEqual(currentID, save.GetCurrentRevisionID("world-a").Value);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 잠금 충돌이 발생한 이후 잠금이 해제되면 새 Writer로 정상 Commit할 수 있다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_잠금_충돌_후_재시도_성공()
        {
            var save = CreateSave();
            var scope = Path.Combine(directory, "world-a");
            Directory.CreateDirectory(scope);

            using (var lease = new FileStream
            (
                Path.Combine(scope, "commit.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None
            ))
            {
                using var blocked = save.BeginRevision("world-a", string.Empty);
                blocked.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsFalse(blocked.Commit().Success);
            }

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 2
                    }
                );
                var result = writer.Commit();
                Assert.IsTrue(result.Success, result.Error);
            }

            Assert.AreEqual(2, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Manifest 삭제 실패 시 Record는 건드리지 않는다.
        /// <br/> 재실행은 남은 파일만 회수한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_Manifest_부분_삭제_재실행()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("Windows의 읽기 전용 파일 삭제 거절을 이용하는 테스트입니다.");
            }

            var save = CreateSave();
            var ids = CommitLinearRevisions(save, 4);
            var scope = Path.Combine(directory, "world-a");
            var manifestDirectory = Path.Combine(scope, "revisions");
            var recordDirectory = Path.Combine(scope, "records");

            var obsolete = new[]
            {
                Path.Combine(manifestDirectory, ids[0] + ".json"),
                Path.Combine(manifestDirectory, ids[1] + ".json")
            };
            Array.Sort(obsolete, StringComparer.Ordinal);

            File.SetAttributes(obsolete[1], FileAttributes.ReadOnly);
            try
            {
                Assert.IsFalse(save.PruneRevisions("world-a", 1).Success);
                Assert.IsFalse(File.Exists(obsolete[0]));
                Assert.IsTrue(File.Exists(obsolete[1]));
                Assert.AreEqual(4, Directory.GetFiles(recordDirectory, "*.save.json").Length);
                Assert.AreEqual(ids[3], save.GetCurrentRevisionID("world-a").Value);
                Assert.AreEqual(4, save.ReadEntry("world-a", "player").Value.Value);
                Assert.AreEqual
                (
                    3, save.LoadBackupRevision("world-a").Value.ReadEntry("player").Value.Value
                );
            }
            finally
            {
                if (File.Exists(obsolete[1]))
                {
                    File.SetAttributes(obsolete[1], FileAttributes.Normal);
                }
            }

            Assert.IsTrue(save.PruneRevisions("world-a", 1).Success);
            Assert.AreEqual(2, Directory.GetFiles(manifestDirectory, "*.json").Length);
            Assert.AreEqual(2, Directory.GetFiles(recordDirectory, "*.save.json").Length);
            Assert.IsTrue(save.PruneRevisions("world-a", 1).Success);
            Assert.AreEqual(4, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> Record 삭제 실패 중에도 Head와 Backup을 보존한다.
        /// <br/> 재실행은 남은 고아 Record만 회수한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_Record_부분_삭제_재실행()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("Windows의 읽기 전용 파일 삭제 거절을 이용하는 테스트입니다.");
            }

            var save = CreateSave();
            var ids = CommitLinearRevisions(save, 4);
            var scope = Path.Combine(directory, "world-a");
            var manifestDirectory = Path.Combine(scope, "revisions");
            var recordDirectory = Path.Combine(scope, "records");
            var obsolete = new string[2];

            for (var index = 0; index < obsolete.Length; index++)
            {
                var manifest = UnityJsonSerializer.Default.Deserialize<FileRevisionManifest>
                (
                    File.ReadAllText(Path.Combine(manifestDirectory, ids[index] + ".json"))
                );
                obsolete[index] = Path.Combine
                (
                    recordDirectory, manifest.Entries[0].RecordID + ".save.json"
                );
            }

            Array.Sort(obsolete, StringComparer.Ordinal);
            File.SetAttributes(obsolete[1], FileAttributes.ReadOnly);
            try
            {
                Assert.IsFalse(save.PruneRevisions("world-a", 1).Success);
                Assert.IsFalse(File.Exists(obsolete[0]));
                Assert.IsTrue(File.Exists(obsolete[1]));
                Assert.AreEqual(2, Directory.GetFiles(manifestDirectory, "*.json").Length);
                Assert.AreEqual(ids[3], save.GetCurrentRevisionID("world-a").Value);
                Assert.AreEqual(4, save.ReadEntry("world-a", "player").Value.Value);
                Assert.AreEqual
                (
                    3, save.LoadBackupRevision("world-a").Value.ReadEntry("player").Value.Value
                );
            }
            finally
            {
                if (File.Exists(obsolete[1]))
                {
                    File.SetAttributes(obsolete[1], FileAttributes.Normal);
                }
            }

            Assert.IsTrue(save.PruneRevisions("world-a", 1).Success);
            Assert.AreEqual(2, Directory.GetFiles(recordDirectory, "*.save.json").Length);
            Assert.IsTrue(save.PruneRevisions("world-a", 1).Success);
            Assert.AreEqual(4, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 최신/백업/보호 세대만 남겨 Record를 회수하고 이후 Commit도 유지한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_명시적_보존_정리와_후속_Commit()
        {
            var save = CreateSave();
            var ids = new string[4];

            for (var index = 0; index < ids.Length; index++)
            {
                using (var writer = save.BeginRevision
                (
                    "world-a", index == 0 ? string.Empty : ids[index - 1]
                ))
                {
                    writer.PutEntry
                    (
                        "player",
                        new TestEntry
                        {
                            Value = index + 1
                        }
                    );
                    var written = writer.Commit();
                    Assert.IsTrue(written.Success, written.Error);
                }

                ids[index] = save.GetCurrentRevisionID("world-a").Value;
            }

            var originalRecords = Path.Combine(directory, "world-a", "records");
            Assert.AreEqual(4, Directory.GetFiles(originalRecords, "*.save.json").Length);
            Assert.AreEqual(4, save.ListRevisions("world-a").Value.Length);

            // 최신 1개, 직전 백업 1개, 명시적으로 보호한 첫 Revision을 유지한다.
            Assert.IsTrue
            (
                save.PruneRevisions
                (
                    "world-a",
                    1,
                    new[]
                    {
                        ids[0]
                    }
                ).Success
            );
            Assert.AreEqual(3, save.ListRevisions("world-a").Value.Length);
            Assert.IsFalse(save.OpenRevision("world-a", ids[1]).Success);
            Assert.AreEqual(3, Directory.GetFiles(originalRecords, "*.save.json").Length);
            Assert.AreEqual(1, save.OpenRevision("world-a", ids[0]).Value.ReadEntry("player").Value.Value);
            Assert.AreEqual(4, save.ReadEntry("world-a", "player").Value.Value);

            Assert.IsTrue(save.PruneRevisions("world-a", 1).Success);
            Assert.AreEqual(2, save.ListRevisions("world-a").Value.Length);
            Assert.AreEqual(2, Directory.GetFiles(originalRecords, "*.save.json").Length);
            Assert.AreEqual(3, save.LoadBackupRevision("world-a").Value.ReadEntry("player").Value.Value);

            using (var writer = save.BeginRevision("world-a", ids[3]))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 5
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            Assert.AreEqual(5, save.ReadEntry("world-a", "player").Value.Value);
            Assert.AreEqual(3, save.ListRevisions("world-a").Value.Length);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 잘못된 유지 개수나 없는 보호 Revision에서는 기존 파일을 삭제하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_보존_정리_잘못된_요청_불변()
        {
            var save = CreateSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 7
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var originalID = save.GetCurrentRevisionID("world-a").Value;
            Assert.IsFalse(save.PruneRevisions("world-a", 0).Success);
            Assert.IsFalse
            (
                save.PruneRevisions
                (
                    "world-a",
                    1,
                    new[]
                    {
                        "missing"
                    }
                ).Success
            );
            Assert.AreEqual(originalID, save.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(1, save.ListRevisions("world-a").Value.Length);
            Assert.AreEqual(7, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 최초 Commit이 current를 게시하기 전에는 아직 확정된 Revision이 없다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_첫_Commit_게시_직전_조회_빈_Head()
        {
            var source = new CallbackRecordWriter();
            var save = CreateSave(source);
            var observed = false;
            source.OnCurrentWrite = () =>
            {
                observed = true;
                var head = save.GetCurrentRevisionID("world-a");
                Assert.IsTrue(head.Success, head.Error);
                Assert.AreEqual(string.Empty, head.Value);
                Assert.IsFalse(save.LoadRevision("world-a").Success);
            };

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 13
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            Assert.IsTrue(observed);
            Assert.AreEqual(13, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Record는 파일 검증 Reader를 사용해 외부 문자열 주입을 차단한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_주입_Reader_Record_대체_차단()
        {
            var save = CreateSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 17
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var reader = new ReplacedRecordReader();
            var substituted = new FileRevisionSave<TestEntry>
            (
                directory, new RecordType("game.entry", "1"),
                UnityJsonSerializer.Default, reader
            );

            var loaded = substituted.ReadEntry("world-a", "player");
            Assert.IsTrue(loaded.Success, loaded.Error);
            Assert.AreEqual(17, loaded.Value.Value);
            Assert.AreEqual(0, reader.RecordReads);
            Assert.AreEqual(17, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 내부 파일의 비표준 인코딩 설정을 생성 시점에 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [TestCase(true)]
        [TestCase(false)]
        public void TEST_FileRevisionSave_비표준_인코딩_구성_거절(bool useUtf16)
        {
            Encoding encoding = useUtf16
                ? new UnicodeEncoding(false, false)
                : new UTF8Encoding(true);
            var io = new TextFileIO(encoding);

            Assert.Throws<ArgumentException>
            (
                () => new FileRevisionSave<TestEntry>
                (
                    directory, new RecordType("game.entry", "1"),
                    UnityJsonSerializer.Default, io, io
                )
            );

            Assert.IsFalse(Directory.Exists(Path.Combine(directory, "world-a")));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 Revision Record는 BOM 없는 UTF-8로 저장된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Record_기본_UTF8_무BOM()
        {
            var save = CreateSave();

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player", new TestEntry
                    {
                        Value = 42
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var files = Directory.GetFiles
            (
                Path.Combine(directory, "world-a", "records"), "*.save.json"
            );
            Assert.AreEqual(1, files.Length);

            var bytes = File.ReadAllBytes(files[0]);
            Assert.AreEqual((byte)'{', bytes[0]);
            Assert.AreEqual(42, save.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 Manifest의 공유 Record와 Reader 경계를 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_공유_Record_보존_및_Reader_경계()
        {
            var save = CreateSave();
            var currentID = string.Empty;

            for (var index = 0; index < 3; index++)
            {
                using (var writer = save.BeginRevision("world-a", currentID))
                {
                    if (index == 0)
                    {
                        writer.PutEntry
                        (
                            "quest", new TestEntry
                            {
                                Value = 20
                            }
                        );
                    }

                    writer.PutEntry
                    (
                        "player", new TestEntry
                        {
                            Value = index + 1
                        }
                    );
                    Assert.IsTrue(writer.Commit().Success);
                }

                currentID = save.GetCurrentRevisionID("world-a").Value;
            }

            var counter = new CountingRecordReader();
            var reading = new FileRevisionSave<TestEntry>
            (
                directory, new RecordType("game.entry", "1"),
                UnityJsonSerializer.Default, counter
            );

            Assert.IsTrue(reading.PruneRevisions("world-a", 3).Success);
            Assert.AreEqual(0, counter.RecordReads);
            var recordFiles = Directory.GetFiles
            (
                Path.Combine(directory, "world-a", "records"), "*.save.json"
            );
            Assert.AreEqual(4, recordFiles.Length);
            Assert.AreEqual(20, reading.ReadEntry("world-a", "quest").Value.Value);
            Assert.AreEqual(3, reading.ReadEntry("world-a", "player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Prune의 물리 검증은 게임 DTO 역직렬화 콜백을 호출하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_게임_역직렬화_미실행()
        {
            var save = new FileRevisionSave<CallbackEntry>
            (
                directory, new RecordType("game.entry", "1"),
                UnityJsonSerializer.Default
            );
            var deserializeCount = 0;
            CallbackEntry.OnDeserialize = () => deserializeCount++;

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player", new CallbackEntry
                    {
                        Value = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var previous = save.GetCurrentRevisionID("world-a").Value;
            using (var writer = save.BeginRevision("world-a", previous))
            {
                writer.PutEntry
                (
                    "player", new CallbackEntry
                    {
                        Value = 2
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            // 게시 전 재복원은 콜백을 실행하므로 Prune만의 증가분을 비교한다.
            var beforePrune = deserializeCount;
            Assert.Greater(beforePrune, 0);
            Assert.IsTrue(save.PruneRevisions("world-a", 1).Success);
            Assert.AreEqual(beforePrune, deserializeCount);
            Assert.AreEqual(2, save.ReadEntry("world-a", "player").Value.Value);
            Assert.Greater(deserializeCount, beforePrune);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 남길 최신 Record가 손상됐다면 정상 과거 Revision을 삭제하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_Prune_보존_Record_손상_삭제_거절()
        {
            var save = CreateSave();
            var ids = new string[3];

            for (var index = 0; index < ids.Length; index++)
            {
                using (var writer = save.BeginRevision
                (
                    "world-a", index == 0 ? string.Empty : ids[index - 1]
                ))
                {
                    writer.PutEntry
                    (
                        "player",
                        new TestEntry
                        {
                            Value = index + 1
                        }
                    );
                    Assert.IsTrue(writer.Commit().Success);
                }

                ids[index] = save.GetCurrentRevisionID("world-a").Value;
            }

            var manifestPath = Path.Combine(directory, "world-a", "revisions", ids[2] + ".json");
            var manifest = UnityJsonSerializer.Default.Deserialize<FileRevisionManifest>
            (
                File.ReadAllText(manifestPath)
            );
            var recordPath = Path.Combine
            (
                directory, "world-a", "records", manifest.Entries[0].RecordID + ".save.json"
            );
            File.AppendAllText(recordPath, " ");

            var before = save.ListRevisions("world-a");
            Assert.IsTrue(before.Success, before.Error);
            Assert.IsFalse(save.PruneRevisions("world-a", 1).Success);

            var after = save.ListRevisions("world-a");
            Assert.IsTrue(after.Success, after.Error);
            Assert.AreEqual(before.Value.Length, after.Value.Length);
            Assert.AreEqual(1, save.OpenRevision("world-a", ids[0]).Value.ReadEntry("player").Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 본문이 유효한 JSON으로 바뀌어도 저장 시 파일 해시와 다르면 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_유효한_JSON_본문_변조_해시_거절()
        {
            var save = CreateSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 17
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var revisionID = save.GetCurrentRevisionID("world-a").Value;
            var manifestPath = Path.Combine(directory, "world-a", "revisions", revisionID + ".json");
            var manifest = UnityJsonSerializer.Default.Deserialize<FileRevisionManifest>
            (
                File.ReadAllText(manifestPath)
            );

            var path = Path.Combine
            (
                directory, "world-a", "records", manifest.Entries[0].RecordID + ".save.json"
            );
            var record = UnityJsonSerializer.Default.Deserialize<SaveRecord<TestEntry>>
            (
                File.ReadAllText(path)
            );
            record.Body.Value = 18;
            File.WriteAllText(path, UnityJsonSerializer.Default.Serialize(record));

            Assert.IsFalse(save.ReadEntry("world-a", "player").Success);
            Assert.IsFalse(save.OpenRevision("world-a", revisionID).Value.ReadEntry("player").Success);
        }

    #endregion

    #region 이기종 Entry

        // ------------------------------------------------------------
        /// <summary>
        /// 서로 다른 Body와 Schema를 한 Revision으로 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_서로_다른_Entry_타입을_같이_확정()
        {
            var save = CreateMultiSchemaSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 7
                    },
                    playerSchema
                );
                writer.PutEntry
                (
                    "quests",
                    new QuestEntry
                    {
                        Title = "start"
                    },
                    questSchema
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var opened = save.LoadRevision("world-a");
            Assert.IsTrue(opened.Success, opened.Error);
            Assert.AreEqual(2, opened.Value.ListEntries().Length);

            var player = opened.Value.ReadEntry<PlayerEntry>("player", playerSchema);
            var quest = opened.Value.ReadEntry<QuestEntry>("quests", questSchema);

            Assert.IsTrue(player.Success, player.Error);
            Assert.IsTrue(quest.Success, quest.Error);
            Assert.AreEqual(7, player.Value.Level);
            Assert.AreEqual("start", quest.Value.Title);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Entry의 스키마와 요청 타입이 불일치하면 복원을 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_Schema가_일치하지_않으면_복원_거절()
        {
            var save = CreateMultiSchemaSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 3
                    },
                    playerSchema
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var wrongVersion = new RecordType("game.player", "2");
            var wrongSchema = new RecordType("game.quest", "2");
            Assert.IsFalse(save.ReadEntry<PlayerEntry>("world-a", "player", wrongVersion).Success);
            Assert.IsFalse(save.ReadEntry<QuestEntry>("world-a", "player", wrongSchema).Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// PutEntry 호출 이후 입력 객체 변경은 저장 결과에 영향을 주지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_기록_시점에_입력_Body를_고정()
        {
            var save = CreateMultiSchemaSave();
            var player = new PlayerEntry
            {
                Level = 10
            };
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry("player", player, playerSchema);
                player.Level = 999;
                Assert.IsTrue(writer.Commit().Success);
            }

            var loaded = save.ReadEntry<PlayerEntry>("world-a", "player", playerSchema);
            Assert.IsTrue(loaded.Success, loaded.Error);
            Assert.AreEqual(10, loaded.Value.Level);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 직렬화 콜백에서 Commit 재진입을 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_직렬화_중_Commit_재진입_거절()
        {
            var save = CreateMultiSchemaSave();

            using var writer = save.BeginRevision("world-a", string.Empty);
            var called = false;
            CallbackEntry.OnSerialize = () =>
            {
                called = true;
                Assert.Throws<InvalidOperationException>(() => writer.Commit());
            };

            writer.PutEntry
            (
                "callback", new CallbackEntry
                {
                    Value = 9
                },
                new RecordType("game.callback", "1")
            );

            Assert.IsTrue(called, "Unity 직렬화 콜백이 실행되지 않았습니다.");
            Assert.IsTrue(writer.Commit().Success);
            var loaded = save.ReadEntry<CallbackEntry>
            (
                "world-a", "callback", new RecordType("game.callback", "1")
            );
            Assert.IsTrue(loaded.Success, loaded.Error);
            Assert.AreEqual(9, loaded.Value.Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료한 Writer는 Body 캡처 전에 재사용을 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_종료한_Writer의_직렬화_거절()
        {
            var save = CreateMultiSchemaSave();
            using var writer = save.BeginRevision("world-a", string.Empty);
            writer.Dispose();

            Assert.Throws<InvalidOperationException>
            (
                () => writer.PutEntry<PlayerEntry>("player", null, playerSchema)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 잘못된 EntryID는 Body 캡처 전에 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_잘못된_EntryID의_직렬화_거절()
        {
            var save = CreateMultiSchemaSave();
            using var writer = save.BeginRevision("world-a", string.Empty);

            Assert.Throws<ArgumentException>
            (
                () => writer.PutEntry<PlayerEntry>("Invalid-ID", null, playerSchema)
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 변경하지 않은 Entry와 이전 Revision을 함께 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_변경하지_않은_Entry와_과거_Revision_보존()
        {
            var save = CreateMultiSchemaSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 1
                    },
                    playerSchema
                );
                writer.PutEntry
                (
                    "quests",
                    new QuestEntry
                    {
                        Title = "keep"
                    },
                    questSchema
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var before = save.GetCurrentRevisionID("world-a");
            Assert.IsTrue(before.Success);

            using (var writer = save.BeginRevision("world-a", before.Value))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 2
                    },
                    playerSchema
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var old = save.OpenRevision("world-a", before.Value);
            var current = save.LoadRevision("world-a");
            Assert.IsTrue(old.Success, old.Error);
            Assert.IsTrue(current.Success, current.Error);
            Assert.AreEqual(1, old.Value.ReadEntry<PlayerEntry>("player", playerSchema).Value.Level);
            Assert.AreEqual(2, current.Value.ReadEntry<PlayerEntry>("player", playerSchema).Value.Level);
            Assert.AreEqual("keep", current.Value.ReadEntry<QuestEntry>("quests", questSchema).Value.Title);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 예상 Revision이 불일치하면 경쟁 Commit을 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_예상_Revision이_달라지면_확정_거절()
        {
            var save = CreateMultiSchemaSave();
            using var first = save.BeginRevision("world-a", string.Empty);
            using var second = save.BeginRevision("world-a", string.Empty);
            first.PutEntry
            (
                "player",
                new PlayerEntry
                {
                    Level = 1
                },
                playerSchema
            );
            second.PutEntry
            (
                "player",
                new PlayerEntry
                {
                    Level = 2
                },
                playerSchema
            );

            Assert.IsTrue(first.Commit().Success);
            Assert.IsFalse(second.Commit().Success);
            Assert.AreEqual
            (
                1, save.ReadEntry<PlayerEntry>("world-a", "player", playerSchema).Value.Level
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Revision에서 Entry를 삭제해도 과거 기록은 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_Entry_삭제_후_과거_Revision은_유지()
        {
            var save = CreateMultiSchemaSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 1
                    },
                    playerSchema
                );
                writer.PutEntry
                (
                    "quests",
                    new QuestEntry
                    {
                        Title = "old"
                    },
                    questSchema
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var before = save.GetCurrentRevisionID("world-a").Value;
            using (var writer = save.BeginRevision("world-a", before))
            {
                writer.RemoveEntry("quests");
                Assert.IsTrue(writer.Commit().Success);
            }

            var current = save.LoadRevision("world-a");
            var previous = save.OpenRevision("world-a", before);
            Assert.IsTrue(current.Success, current.Error);
            Assert.IsTrue(previous.Success, previous.Error);
            Assert.AreEqual(1, current.Value.ListEntries().Length);
            Assert.IsFalse(current.Value.ReadEntry<QuestEntry>("quests", questSchema).Success);
            Assert.AreEqual("old", previous.Value.ReadEntry<QuestEntry>("quests", questSchema).Value.Title);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 백업 Revision의 Entry를 검증하고 current를 복구한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_백업_Revision_검증_후_복원()
        {
            var save = CreateMultiSchemaSave();
            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 3
                    },
                    playerSchema
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            using (var writer = save.BeginRevision("world-a"))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 9
                    },
                    playerSchema
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var backup = save.LoadBackupRevision("world-a");
            Assert.IsTrue(backup.Success, backup.Error);
            Assert.AreEqual(3, backup.Value.ReadEntry<PlayerEntry>("player", playerSchema).Value.Level);
            Assert.IsTrue(save.RestoreBackupRevision("world-a").Success);
            Assert.AreEqual(3, save.ReadEntry<PlayerEntry>("world-a", "player", playerSchema).Value.Level);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 동형 저장소에 이기종 기록을 혼합하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_동형_Scope에서_이기종_쓰기_거절()
        {
            var generic = new FileRevisionSave<PlayerEntry>
            (
                directory, playerSchema, UnityJsonSerializer.Default
            );
            using (var writer = generic.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 5
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var before = generic.GetCurrentRevisionID("world-a").Value;
            var mixed = CreateMultiSchemaSave();
            using (var writer = mixed.BeginRevision("world-a", before))
            {
                writer.PutEntry
                (
                    "quests",
                    new QuestEntry
                    {
                        Title = "blocked"
                    },
                    questSchema
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(before, generic.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(5, generic.ReadEntry("world-a", "player").Value.Level);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이기종 저장소에 동형 기록을 혼합하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_Scope에서_동형_쓰기_거절()
        {
            var mixed = CreateMultiSchemaSave();
            using (var writer = mixed.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 12
                    },
                    playerSchema
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var before = mixed.GetCurrentRevisionID("world-a").Value;
            var generic = new FileRevisionSave<PlayerEntry>
            (
                directory, playerSchema, UnityJsonSerializer.Default
            );
            using (var writer = generic.BeginRevision("world-a", before))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 100
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(before, mixed.GetCurrentRevisionID("world-a").Value);
            Assert.AreEqual(12, mixed.ReadEntry<PlayerEntry>("world-a", "player", playerSchema).Value.Level);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Revision 종류 표식이 없으면 기존 데이터를 건드리지 않고 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_표식_없는_저장소_접근_거절()
        {
            var generic = new FileRevisionSave<PlayerEntry>
            (
                directory, playerSchema, UnityJsonSerializer.Default
            );
            using (var writer = generic.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 1
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            var scopeDirectory = Path.Combine(directory, "world-a");
            var marker = Path.Combine(scopeDirectory, "revision.kind");
            var currentPath = Path.Combine(scopeDirectory, "current.txt");
            var previousCurrent = File.ReadAllText(currentPath);

            File.Delete(marker);

            Assert.IsFalse(generic.GetCurrentRevisionID("world-a").Success);
            Assert.IsFalse(generic.LoadRevision("world-a").Success);

            var mixed = CreateMultiSchemaSave();
            using (var writer = mixed.BeginRevision("world-a", previousCurrent))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 999
                    },
                    playerSchema
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            using (var writer = generic.BeginRevision("world-a", previousCurrent))
            {
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 2
                    }
                );
                Assert.IsFalse(writer.Commit().Success);
            }

            Assert.AreEqual(previousCurrent, File.ReadAllText(currentPath));
            Assert.IsFalse(File.Exists(marker));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 잘못된 Entry 스키마는 해당 읽기 호출의 실패로 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_이기종_잘못된_Schema는_오류_범위로_제한()
        {
            var mixed = CreateMultiSchemaSave();
            using (var writer = mixed.BeginRevision("world-a", string.Empty))
            {
                Assert.Throws<ArgumentNullException>
                (
                    () => writer.PutEntry<PlayerEntry>("player", null, playerSchema)
                );
                writer.PutEntry
                (
                    "player",
                    new PlayerEntry
                    {
                        Level = 7
                    },
                    playerSchema
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            Assert.IsFalse
            (
                mixed.ReadEntry<PlayerEntry>
                (
                    "world-a", "player", new RecordType("game.player", "99")
                ).Success
            );
            Assert.IsFalse
            (
                mixed.ReadEntry<QuestEntry>
                (
                    "world-a", "player", questSchema
                ).Success
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종류 표식 읽기에서 ReleaseHandle을 회수한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileRevisionSave_종류_표식_읽기_ReleaseHandle_회수()
        {
            var reader = new MarkerLeaseReader();
            var save = new FileRevisionSave<TestEntry>
            (
                directory, new RecordType("game.entry", "1"),
                UnityJsonSerializer.Default, reader
            );

            using (var writer = save.BeginRevision("world-a", string.Empty))
            {
                writer.PutEntry
                (
                    "player",
                    new TestEntry
                    {
                        Value = 4
                    }
                );
                Assert.IsTrue(writer.Commit().Success);
            }

            Assert.IsTrue(save.GetCurrentRevisionID("world-a").Success);
            Assert.AreEqual(reader.MarkerReads, reader.Released);
            Assert.Greater(reader.MarkerReads, 0);

            // Reader 실패 응답에 포함된 Handle도 Persistence가 회수한다.
            reader.FailMarker = true;
            Assert.IsFalse(save.GetCurrentRevisionID("world-a").Success);
            Assert.AreEqual(reader.MarkerReads, reader.Released);

            // 저장소 종류가 불일치하는 경우에도 Handle을 반환한다.
            reader.FailMarker = false;
            var mixed = new FileRevisionSave(directory, UnityJsonSerializer.Default, reader);
            Assert.IsFalse(mixed.GetCurrentRevisionID("world-a").Success);
            Assert.AreEqual(reader.MarkerReads, reader.Released);
        }

    #endregion

    #region 헬퍼

        // ------------------------------------------------------------
        /// <summary>
        /// 동일한 Schema와 Serializer로 Save를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private FileRevisionSave<TestEntry> CreateSave(IDataWriter<string, string> writer = null)
        {
            return new FileRevisionSave<TestEntry>
            (
                directory,
                new RecordType("game.entry", "1"),
                UnityJsonSerializer.Default,
                TextFileIO.Default,
                writer
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 수의 연속된 Revision을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private string[] CommitLinearRevisions(FileRevisionSave<TestEntry> save, int count)
        {
            var ids = new string[count];

            for (var index = 0; index < count; index++)
            {
                using (var writer = save.BeginRevision
                (
                    "world-a", index == 0 ? string.Empty : ids[index - 1]
                ))
                {
                    writer.PutEntry
                    (
                        "player", new TestEntry
                        {
                            Value = index + 1
                        }
                    );

                    var response = writer.Commit();
                    Assert.IsTrue(response.Success, response.Error);
                }

                ids[index] = save.GetCurrentRevisionID("world-a").Value;
            }

            return ids;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 각 Entry마다 서로 다른 Schema를 사용하는 Revision을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private FileRevisionSave CreateMultiSchemaSave()
        {
            return new FileRevisionSave(directory, UnityJsonSerializer.Default);
        }
    #endregion

    }
}
