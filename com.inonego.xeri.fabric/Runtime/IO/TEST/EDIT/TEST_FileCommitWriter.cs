/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_FileCommitWriter.cs
수정일 : 2026-11-09

# 설명
단일 파일 Commit Writer의 기존 대상 파일 보존, 정상 교체 및 백업 세대 유지를 검증한다.
Persistence의 Record 검증·ReleaseHandle 수명 정책은 이 테스트 범위에 포함하지 않는다.

# 테스트 구성
 F: File - 임시 파일 교체, 실패 시 원본 보존과 경로 검증
 B: Backup - 교체 전후 백업 세대 보존
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.IO.TEST
{
    // ============================================================
    /// <summary>
    /// FileCommitWriter의 파일 교체와 임시 파일 정리 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_FileCommitWriter
    {

    #region 테스트 객체

        // ============================================================
        /// <summary>
        /// 임시 파일에 일부 내용을 기록한 뒤 실패를 반환하는 Writer.
        /// </summary>
        // ============================================================
        private sealed class FailingFileWriter : IDataWriter<string, string>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 실제 임시 파일을 작성하지만 실패 결과를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string location, string value)
            {
                File.WriteAllText(location, value);
                return WriteResponse.Fail("기록 실패");
            }
        }

    #endregion

    #region 픽스처

        private string directory;

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트용 독립 디렉터리를 만든다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine
            (
                Path.GetTempPath(),
                "XeriFileCommitTest_" + Guid.NewGuid().ToString("N")
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

    #region F-1: 임시 파일 작성과 교체·실패

        // ------------------------------------------------------------
        /// <summary>
        /// 임시 파일 쓰기 실패는 기존 대상 파일을 변경하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileCommitWriter_실패_기존_파일_보존()
        {
            var path = Path.Combine(directory, "record.txt");
            File.WriteAllText(path, "original");

            var writer = new FileCommitWriter<string>(new FailingFileWriter());
            var response = writer.Write(path, "replacement");

            Assert.IsFalse(response.Success);
            Assert.AreEqual("original", File.ReadAllText(path));
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상 파일이 있을 때 정상적으로 교체하고 임시 파일을 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileCommitWriter_성공_기존_파일_교체()
        {
            var path = Path.Combine(directory, "record.txt");
            File.WriteAllText(path, "original");

            var writer = new FileCommitWriter<string>(TextFileIO.Default);
            var response = writer.Write(path, "replacement");

            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual("replacement", File.ReadAllText(path));
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 대상 파일이 없으면 최초 쓰기에 성공한 파일이 지정한 위치로 이동한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileCommitWriter_신규_파일_기록()
        {
            var path = Path.Combine(directory, "new.txt");
            var writer = new FileCommitWriter<string>(TextFileIO.Default);

            var response = writer.Write(path, "first");

            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual("first", File.ReadAllText(path));
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 잘못된 경로는 성공으로 취급하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileCommitWriter_빈_경로_거절()
        {
            var writer = new FileCommitWriter<string>(TextFileIO.Default);
            var response = writer.Write("", "data");

            Assert.IsFalse(response.Success);
            Assert.IsInstanceOf<ArgumentException>(response.Exception);
        }

    #endregion

    #region B-1: 백업 세대 갱신과 실패 시 보존

        // ------------------------------------------------------------
        /// <summary>
        /// 정상 교체일 때만 이전 파일이 백업되며 다음 교체에서 세대가 갱신된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileCommitWriter_백업_정상_교체_이전_세대_보존()
        {
            var path = Path.Combine(directory, "record.txt");
            File.WriteAllText(path, "first");
            var writer = new FileCommitWriter<string>(TextFileIO.Default, preservePrevious: true);

            Assert.IsTrue(writer.Write(path, "second").Success);
            Assert.AreEqual("first", File.ReadAllText(path + ".bak"));

            Assert.IsTrue(writer.Write(path, "third").Success);
            Assert.AreEqual("third", File.ReadAllText(path));
            Assert.AreEqual("second", File.ReadAllText(path + ".bak"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 백업을 요청해도 임시 쓰기가 실패하면 기존 백업을 보존한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_FileCommitWriter_백업_쓰기_실패_세대_유지()
        {
            var path = Path.Combine(directory, "record.txt");
            File.WriteAllText(path, "first");
            var writer = new FileCommitWriter<string>(TextFileIO.Default, preservePrevious: true);
            Assert.IsTrue(writer.Write(path, "second").Success);

            var failing = new FileCommitWriter<string>(new FailingFileWriter(), preservePrevious: true);
            Assert.IsFalse(failing.Write(path, "third").Success);

            Assert.AreEqual("second", File.ReadAllText(path));
            Assert.AreEqual("first", File.ReadAllText(path + ".bak"));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 대상 파일이 없으면 새로운 백업 세대를 만들거나 기존 복구본을 지우지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_FileCommitWriter_신규_파일은_기존_백업을_보존()
        {
            var path = Path.Combine(directory, "record.txt");
            File.WriteAllText(path + ".bak", "previous-backup");

            var writer = new FileCommitWriter<string>(TextFileIO.Default, preservePrevious: true);
            var response = writer.Write(path, "new-value");

            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual("new-value", File.ReadAllText(path));
            Assert.AreEqual("previous-backup", File.ReadAllText(path + ".bak"));
        }

    #endregion

    }
}
