/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_ScopedFileSave.cs
수정일 : 2026-11-14

# 설명
Scope 아래 독립 Entry의 선택적 파일 저장·복원·삭제와 실패 격리를 확인한다.
같은 Schema와 Persistence 기반을 FileSave에서 그대로 재사용하는지 실증한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Persistence;
using inonego.Xeri.Serializable;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.Save.TEST
{
    // ============================================================
    /// <summary>
    /// ScopedFileSave의 Scope·Entry 독립성 및 경로 계약 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_ScopedFileSave
    {

    #region 테스트 객체

        // ============================================================
        /// <summary>
        /// 예시 Entry 본문.
        /// </summary>
        // ============================================================
        [Serializable]
        private sealed class EntryBody
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 독립 저장 상태값.
            /// </summary>
            // ------------------------------------------------------------
            public int Count;
        }

    #endregion

    #region 픽스처

        private string directory = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 각 테스트의 독립 디렉터리를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine
            (
                Path.GetTempPath(),
                "XeriScopedFileSave_" + Guid.NewGuid().ToString("N")
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
        }

    #endregion

    #region Entry 저장

        // ------------------------------------------------------------
        /// <summary>
        /// 두 Entry를 별도로 기록한 뒤 원하는 Entry만 복원할 수 있다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScopedFileSave_Entry_독립_저장과_복원()
        {
            var save = CreateSave();

            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-a",
                    "player",
                    new EntryBody
                    {
                        Count = 8
                    }
                ).Success
            );
            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-a",
                    "region-02",
                    new EntryBody
                    {
                        Count = 17
                    }
                ).Success
            );

            Assert.IsTrue
            (
                File.Exists(Path.Combine(directory, "world-a", "player.save.json"))
            );
            Assert.IsTrue
            (
                File.Exists(Path.Combine(directory, "world-a", "region-02.save.json"))
            );
            Assert.IsFalse(File.Exists(Path.Combine(directory, "player.save.json")));
            Assert.AreEqual(8, save.ReadEntry("world-a", "player").Value.Count);
            Assert.AreEqual(17, save.ReadEntry("world-a", "region-02").Value.Count);
            Assert.IsTrue(save.HasEntry("world-a", "player"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 EntryID라도 Scope가 다르면 독립된 기록을 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScopedFileSave_Entry_Scope_분리()
        {
            var save = CreateSave();

            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-a",
                    "player",
                    new EntryBody
                    {
                        Count = 5
                    }
                ).Success
            );
            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-b",
                    "player",
                    new EntryBody
                    {
                        Count = 9
                    }
                ).Success
            );

            Assert.AreEqual(5, save.ReadEntry("world-a", "player").Value.Count);
            Assert.AreEqual(9, save.ReadEntry("world-b", "player").Value.Count);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 하나의 Entry 삭제가 같은 Scope의 다른 Entry를 제거하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScopedFileSave_Entry_선택_삭제()
        {
            var save = CreateSave();
            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-a",
                    "player",
                    new EntryBody
                    {
                        Count = 5
                    }
                ).Success
            );
            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-a",
                    "quest",
                    new EntryBody
                    {
                        Count = 9
                    }
                ).Success
            );

            Assert.IsTrue(save.DeleteEntry("world-a", "player").Success);
            Assert.IsFalse(save.HasEntry("world-a", "player"));
            Assert.IsTrue(save.HasEntry("world-a", "quest"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 목록은 해당 Scope의 실제 Entry 파일명만 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScopedFileSave_Entry_목록()
        {
            var save = CreateSave();

            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-a",
                    "z-region",
                    new EntryBody
                    {
                        Count = 1
                    }
                ).Success
            );
            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-a",
                    "a-region",
                    new EntryBody
                    {
                        Count = 2
                    }
                ).Success
            );

            var entries = save.ListEntries("world-a");

            Assert.IsTrue(entries.Success, entries.Error);
            Assert.AreEqual(2, entries.Value.Length);
            Assert.AreEqual("a-region", entries.Value[0]);
            Assert.AreEqual("z-region", entries.Value[1]);
            Assert.IsEmpty(save.ListEntries("world-b").Value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Record 로드는 Entry의 Metadata와 Body를 모두 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScopedFileSave_Entry_Metadata_포함_복원()
        {
            var save = CreateSave();
            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-a",
                    "quest",
                    new EntryBody
                    {
                        Count = 27
                    }
                ).Success
            );

            var record = save.ReadEntryRecord("world-a", "quest");

            Assert.IsTrue(record.Success, record.Error);
            Assert.AreEqual("quest", record.Value.Metadata.SaveKey);
            Assert.AreEqual(27, record.Value.Body.Count);
        }

    #endregion

    #region 입력 검증

        // ------------------------------------------------------------
        /// <summary>
        /// Scope·EntryID 경로 이탈을 거절하고 저장 디렉터리 밖에 쓰지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_ScopedFileSave_Entry_잘못된_ID_거절()
        {
            var save = CreateSave();

            Assert.IsFalse
            (
                save.WriteEntry
                (
                    "../outside",
                    "player",
                    new EntryBody
                    {
                        Count = 1
                    }
                ).Success
            );
            Assert.IsFalse
            (
                save.WriteEntry
                (
                    "world-a",
                    "../outside",
                    new EntryBody
                    {
                        Count = 1
                    }
                ).Success
            );
            Assert.IsFalse(save.HasEntry("world-a", "../outside"));
            Assert.IsFalse(save.ListEntries("../outside").Success);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 같은 EntryID라도 다른 Scope로 파일을 복사하면 Metadata 검증에서 거절한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_ScopedFileSave_Entry_다른_Scope_파일_복원_거절()
        {
            var save = CreateSave();

            Assert.IsTrue
            (
                save.WriteEntry
                (
                    "world-a",
                    "player",
                    new EntryBody
                    {
                        Count = 9
                    }
                ).Success
            );

            var destination = Path.Combine(directory, "world-b");
            Directory.CreateDirectory(destination);

            File.Copy
            (
                Path.Combine(directory, "world-a", "player.save.json"),
                Path.Combine(destination, "player.save.json")
            );

            Assert.IsFalse(save.ReadEntry("world-b", "player").Success);
            Assert.AreEqual(9, save.ReadEntry("world-a", "player").Value.Count);
        }

    #endregion

    #region 헬퍼

        // ------------------------------------------------------------
        /// <summary>
        /// 공통 RecordType/Serializer를 사용하는 Entry Save를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private ScopedFileSave<EntryBody> CreateSave()
        {
            return new ScopedFileSave<EntryBody>
            (
                directory,
                new RecordType("game.entry", "1"),
                UnityJsonSerializer.Default
            );
        }

    #endregion

    }
}
