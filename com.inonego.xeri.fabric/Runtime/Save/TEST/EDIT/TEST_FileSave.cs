/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_FileSave.cs
수정일 : 2026-11-14

# 설명
Save의 파일 Snapshot·SaveKey·Schema·Format 검증과 IO Commit 실패 시 정상 기록 보존을 검증한다.
기존 Document와 동일한 Persistence 기록 처리 계약을 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;
using System.Globalization;

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
    /// FileSave의 Snapshot 저장·복원·실패 처리 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_FileSave
    {

    #region 테스트 객체

        // ============================================================
        /// <summary>
        /// 게임 상태 Snapshot 예시.
        /// </summary>
        // ============================================================
        [Serializable]
        private sealed class TestSnapshot
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 저장할 플레이어 상태 값.
            /// </summary>
            // ------------------------------------------------------------
            public int Level;
        }

        // ============================================================
        /// <summary>
        /// 임시 파일 쓰기에 실패하는 Writer.
        /// </summary>
        // ============================================================
        private sealed class FailingWriter : IDataWriter<string, string>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 파일을 일부 작성한 뒤 실패를 보고한다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string location, string value)
            {
                File.WriteAllText(location, value);
                return WriteResponse.Fail("저장 실패");
            }
        }

    #endregion

    #region 픽스처

        private const string SCHEMA_ID = "game.snapshot";
        private const string SCHEMA_VERSION = "1";
        private string directory;

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트별 독립된 파일 저장소를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine
            (
                Path.GetTempPath(),
                "XeriFileSaveTest_" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(directory);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 파일을 모두 정리한다.
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

    #region 저장·복원

        // ------------------------------------------------------------
        /// <summary>
        /// Snapshot의 최초 저장과 동일 SaveKey의 재저장·교체를 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_저장과_복원_왕복()
        {
            var save = CreateSave();
            var saved = save.Save
            (
                "manual-1",
                new TestSnapshot
                {
                    Level = 25
                }
            );
            var loaded = save.Load("manual-1");

            Assert.IsTrue(saved.Success, saved.Error);
            Assert.IsTrue(loaded.Success, loaded.Error);
            Assert.AreEqual(25, loaded.Value.Level);
            Assert.IsTrue(save.Exists("manual-1"));

            // 같은 SaveKey에 정상 재저장하면 최신 Snapshot으로 교체된다.
            var replaced = save.Save
            (
                "manual-1",
                new TestSnapshot
                {
                    Level = 26
                }
            );
            var reloaded = save.Load("manual-1");

            Assert.IsTrue(replaced.Success, replaced.Error);
            Assert.IsTrue(reloaded.Success, reloaded.Error);
            Assert.AreEqual(26, reloaded.Value.Level);
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 두 SaveKey는 서로 독립된 기록이다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_여러_SaveKey_분리()
        {
            var save = CreateSave();
            Assert.IsTrue
            (
                save.Save
                (
                    "manual-1",
                    new TestSnapshot
                    {
                        Level = 10
                    }
                ).Success
            );
            Assert.IsTrue
            (
                save.Save
                (
                    "auto",
                    new TestSnapshot
                    {
                        Level = 40
                    }
                ).Success
            );

            Assert.AreEqual(10, save.Load("manual-1").Value.Level);
            Assert.AreEqual(40, save.Load("auto").Value.Level);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 저장한 SaveKey를 삭제하면 다른 SaveKey를 유지한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_SaveKey_삭제()
        {
            var save = CreateSave();
            Assert.IsTrue
            (
                save.Save
                (
                    "manual-1",
                    new TestSnapshot
                    {
                        Level = 5
                    }
                ).Success
            );
            Assert.IsTrue
            (
                save.Save
                (
                    "auto",
                    new TestSnapshot
                    {
                        Level = 15
                    }
                ).Success
            );

            var response = save.Delete("manual-1");

            Assert.IsTrue(response.Success, response.Error);
            Assert.IsFalse(save.Exists("manual-1"));
            Assert.IsTrue(save.Exists("auto"));
            Assert.AreEqual(15, save.Load("auto").Value.Level);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 존재하지 않는 SaveKey는 복원을 실패로 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_없는_SaveKey_복원_실패()
        {
            var response = CreateSave().Load("notfound");

            Assert.IsFalse(response.Success);
        }

    #endregion

    #region 계약 검사

        // ------------------------------------------------------------
        /// <summary>
        /// 경로 이탈·대소문자 충돌 가능성이 있는 SaveKey는 기록하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileSave_SaveKey_부적절한_입력_거절()
        {
            var save = CreateSave();

            foreach (var saveKey in new[]
            {
                "",
                "../escape",
                "one/two",
                "Manual-1",
                "key.name"
            })
            {
                Assert.IsFalse
                (
                    save.Save
                    (
                        saveKey,
                        new TestSnapshot
                        {
                            Level = 1
                        }
                    ).Success
                );
                Assert.IsFalse(save.Exists(saveKey));
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지원하지 않는 SchemaID 또는 SchemaVersion의 파일은 복원을 거절한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [TestCase("other.snapshot", SCHEMA_VERSION)]
        [TestCase(SCHEMA_ID, "2")]
        public void TEST_FileSave_Snapshot_스키마_ID또는버전_불일치_거절(string schemaID, string schemaVersion)
        {
            WriteRecord("manual-1", schemaID, schemaVersion, "1");

            Assert.IsFalse(CreateSave().Load("manual-1").Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지원하지 않는 FormatVersion의 파일은 복원을 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_파일_형식_불일치_거절()
        {
            WriteRecord("manual-1", SCHEMA_ID, SCHEMA_VERSION, "99");
            Assert.IsFalse(CreateSave().Load("manual-1").Success);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 실제 파일에 기록된 SaveKey가 요청 SaveKey와 다르면 복원을 거절한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_SaveKey_불일치_거절()
        {
            WriteRecord("manual-1", SCHEMA_ID, SCHEMA_VERSION, "1", "auto");
            Assert.IsFalse(CreateSave().Load("manual-1").Success);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 손상된 파일·Body 누락·잘못된 저장 시각은 정상 Snapshot으로 취급하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_손상된_기록_거절()
        {
            var path = GetPath("manual-1");
            File.WriteAllText(path, "not valid save json");

            Assert.IsFalse(CreateSave().Load("manual-1").Success);

            var record = CreateRecord("manual-1", SCHEMA_ID, SCHEMA_VERSION, "1");
            record.Body = null;
            File.WriteAllText(path, UnityJsonSerializer.Default.Serialize(record));

            Assert.IsFalse(CreateSave().Load("manual-1").Success);

            // 필수 Metadata가 유효하지 않은 기록도 복원을 거절한다.
            var invalidTimestamp = CreateRecord
            (
                "manual-1", SCHEMA_ID, SCHEMA_VERSION, "1"
            );
            invalidTimestamp.Metadata.SavedAtUTC = "invalid-date";
            File.WriteAllText(path, UnityJsonSerializer.Default.Serialize(invalidTimestamp));

            Assert.IsFalse(CreateSave().Load("manual-1").Success);
        }

    #endregion

    #region 파일 보존

        // ----------------------------------------------------------------------
        /// <summary>
        /// 새 Snapshot 작성 중 임시 파일 기록에 실패하면 이전 정상 파일은 유지된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_파일_쓰기_실패_이전_기록_보존()
        {
            var save = CreateSave();
            Assert.IsTrue
            (
                save.Save
                (
                    "manual-1",
                    new TestSnapshot
                    {
                        Level = 100
                    }
                ).Success
            );

            var failedSave = new FileSave<TestSnapshot>
            (
                directory,
                new RecordType(SCHEMA_ID, SCHEMA_VERSION),
                UnityJsonSerializer.Default,
                TextFileIO.Default,
                new FailingWriter()
            );

            var response = failedSave.Save
            (
                "manual-1",
                new TestSnapshot
                {
                    Level = 200
                }
            );

            Assert.IsFalse(response.Success);
            Assert.AreEqual(100, save.Load("manual-1").Value.Level);
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }

    #endregion

    #region 기본 SaveKey와 목록

        // ------------------------------------------------------------
        /// <summary>
        /// 별도 SaveKey를 지정하지 않는 Snapshot의 저장·복원·삭제를 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_기본_SaveKey_왕복()
        {
            var save = CreateSave();

            Assert.IsTrue
            (
                save.Save
                (
                    new TestSnapshot
                    {
                        Level = 31
                    }
                ).Success
            );
            Assert.IsTrue(save.Exists());
            Assert.AreEqual(31, save.Load().Value.Level);

            Assert.IsTrue(save.Delete().Success);
            Assert.IsFalse(save.Exists());
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 목록은 유효한 SaveKey 파일명만 반환하며 정렬된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_SaveKey_목록()
        {
            var save = CreateSave();
            Assert.IsTrue
            (
                save.Save
                (
                    "z-key",
                    new TestSnapshot
                    {
                        Level = 1
                    }
                ).Success
            );
            Assert.IsTrue
            (
                save.Save
                (
                    "a-key",
                    new TestSnapshot
                    {
                        Level = 2
                    }
                ).Success
            );

            File.WriteAllText(Path.Combine(directory, "INVALID.save.json"), "invalid");
            File.WriteAllText(Path.Combine(directory, "z-key.save.json.tmp"), "stale");

            var keys = save.ListKeys();

            Assert.IsTrue(keys.Success, keys.Error);
            Assert.AreEqual(2, keys.Value.Length);
            Assert.AreEqual("a-key", keys.Value[0]);
            Assert.AreEqual("z-key", keys.Value[1]);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 저장소가 비어 있을 때 목록은 빈 배열로 반환된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_비어있는_목록()
        {
            var save = CreateSave();
            var keys = save.ListKeys();

            Assert.IsTrue(keys.Success, keys.Error);
            Assert.IsEmpty(keys.Value);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// LoadRecord는 현재 Snapshot과 Save 전용 Metadata를 함께 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_Metadata_포함_복원()
        {
            var save = CreateSave();

            Assert.IsTrue
            (
                save.Save
                (
                    "manual-1",
                    new TestSnapshot
                    {
                        Level = 15
                    }
                ).Success
            );

            var response = save.LoadRecord("manual-1");

            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual(SCHEMA_ID, response.Value.Metadata.SchemaID);
            Assert.AreEqual(SCHEMA_VERSION, response.Value.Metadata.SchemaVersion);
            Assert.AreEqual("manual-1", response.Value.Metadata.SaveKey);
            Assert.AreEqual(15, response.Value.Body.Level);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Unity의 영속 저장 경로에서 최초 저장·덮어쓰기·새 인스턴스 복원을 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_Unity_영속경로_저장과_재복원()
        {
            var persistentPath = UnityEngine.Application.persistentDataPath;
            if (string.IsNullOrWhiteSpace(persistentPath))
            {
                Assert.Ignore("이 플랫폼에서는 Application.persistentDataPath를 지원하지 않습니다.");
            }

            var root = Path.Combine
            (
                persistentPath,
                "XeriSaveTest_" + Guid.NewGuid().ToString("N")
            );
            var path = Path.Combine(root, "Saves", "Snapshots");
            var schema = new RecordType(SCHEMA_ID, SCHEMA_VERSION);

            try
            {
                var save = new FileSave<TestSnapshot>
                (
                    path, schema, UnityJsonSerializer.Default
                );
                var first = save.Save
                (
                    "key-1",
                    new TestSnapshot
                    {
                        Level = 23
                    }
                );
                Assert.IsTrue(first.Success, first.Error);

                var filename = Path.Combine(path, "key-1.save.json");
                Assert.IsTrue(File.Exists(filename));

                var reopened = new FileSave<TestSnapshot>
                (
                    path, schema, UnityJsonSerializer.Default
                );
                var loaded = reopened.Load("key-1");
                Assert.IsTrue(loaded.Success, loaded.Error);
                Assert.AreEqual(23, loaded.Value.Level);

                var replaced = reopened.Save
                (
                    "key-1",
                    new TestSnapshot
                    {
                        Level = 24
                    }
                );
                Assert.IsTrue(replaced.Success, replaced.Error);

                var latest = save.Load("key-1");
                Assert.IsTrue(latest.Success, latest.Error);
                Assert.AreEqual(24, latest.Value.Level);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 저장 경로를 디렉터리로 만들 수 없을 때 실패를 반환하고 기존 파일을 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileSave_Snapshot_디렉터리_접근_실패_격리()
        {
            var blockingFile = Path.Combine(directory, "blocked");
            File.WriteAllText(blockingFile, "keep");

            var save = new FileSave<TestSnapshot>
            (
                Path.Combine(blockingFile, "Saves"),
                new RecordType(SCHEMA_ID, SCHEMA_VERSION),
                UnityJsonSerializer.Default
            );
            var response = save.Save
            (
                "key-1",
                new TestSnapshot
                {
                    Level = 1
                }
            );

            Assert.IsFalse(response.Success);
            Assert.AreEqual("keep", File.ReadAllText(blockingFile));
        }

    #endregion

    #region 헬퍼

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트 스키마를 사용하는 파일 Save를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private FileSave<TestSnapshot> CreateSave()
        {
            return new FileSave<TestSnapshot>
            (
                directory,
                new RecordType(SCHEMA_ID, SCHEMA_VERSION),
                UnityJsonSerializer.Default
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 SaveKey·스키마·포맷 값으로 Save Record를 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static SaveRecord<TestSnapshot> CreateRecord
        (
            string saveKey,
            string schemaID,
            string schemaVersion,
            string formatVersion
        )
        {
            var metadata = new SaveMetadata
            {
                FormatVersion = formatVersion,
                SchemaID = schemaID,
                SchemaVersion = schemaVersion,
                SaveKey = saveKey,
                SavedAtUTC = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };

            return new SaveRecord<TestSnapshot>
            (
                metadata,
                new TestSnapshot
                {
                    Level = 7
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 잘못된 Metadata를 직접 파일에 기록해 복원 검증을 시험한다.
        /// </summary>
        // ------------------------------------------------------------
        private void WriteRecord
        (
            string pathSaveKey,
            string schemaID,
            string schemaVersion,
            string formatVersion,
            string metadataSaveKey = null
        )
        {
            var record = CreateRecord
            (
                metadataSaveKey ?? pathSaveKey,
                schemaID,
                schemaVersion,
                formatVersion
            );

            File.WriteAllText(GetPath(pathSaveKey), UnityJsonSerializer.Default.Serialize(record));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 해당 SaveKey 파일의 실제 경로를 구한다.
        /// </summary>
        // ------------------------------------------------------------
        private string GetPath(string saveKey)
        {
            return Path.Combine(directory, saveKey + ".save.json");
        }

    #endregion

    }
}
