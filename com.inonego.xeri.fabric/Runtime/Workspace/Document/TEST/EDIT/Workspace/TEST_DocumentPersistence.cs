/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_DocumentPersistence.cs
수정일 : 2026-11-14

# 설명
Document 세 저장 형식(Raw Text, Body-root, Envelope)의 Persistence 조합과 문서 종류·버전 검증을 확인한다.
지원하지 않는 버전·손상된 기록의 거절과 파일 쓰기 실패 시 원본·Dirty 상태 보존을 검사한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

using NUnit;
using NUnit.Framework;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Serializable;
using inonego.Xeri.Workspace;
using inonego.Xeri.Workspace.Document;

namespace inonego.Xeri.TEST.Workspace._Document
{
    // ============================================================
    /// <summary>
    /// Document Handler의 기록 형식·지원 버전·원본 보존 계약을 검증한다.
    /// </summary>
    // ============================================================
    public sealed class TEST_DocumentPersistence
    {

    #region 테스트 데이터

        private const string TYPE_ID = "test.document.persistence";
        private const string VERSION = "1.0";

        // ============================================================
        /// <summary>
        /// 직렬화 가능한 단순 문서 본문.
        /// </summary>
        // ============================================================
        [Serializable]
        private sealed class TestBody
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 파일 왕복에서 확인할 데이터 값.
            /// </summary>
            // ------------------------------------------------------------
            public string Text = "";
        }

        // ============================================================
        /// <summary>
        /// XmlSerializer에서 사용하는 공개 Body 타입.
        /// </summary>
        // ============================================================
        public sealed class XmlTestBody
        {
            // ------------------------------------------------------------
            /// <summary>
            /// XML에 기록할 데이터.
            /// </summary>
            // ------------------------------------------------------------
            public string Text = "value";
        }

        // ============================================================
        /// <summary>
        /// 임시 파일에 내용을 쓴 뒤 실패를 반환하는 테스트용 Writer.
        /// </summary>
        // ============================================================
        private sealed class FailingFileWriter : IDataWriter<string, string>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 임시 파일 쓰기 실패를 재현한다.
            /// </summary>
            // ------------------------------------------------------------
            public WriteResponse Write(string location, string value)
            {
                File.WriteAllText(location, value);
                return WriteResponse.Fail("파일 기록 실패");
            }
        }

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 각 테스트가 소유하는 임시 파일 디렉터리.
        /// </summary>
        // ------------------------------------------------------------
        private string directory;

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트별 독립된 저장 위치를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine
            (
                Path.GetTempPath(),
                "XeriDocumentPersistence_" + Guid.NewGuid().ToString("N")
            );

            Directory.CreateDirectory(directory);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트가 생성한 파일을 정리한다.
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

    #region Envelope

        // ------------------------------------------------------------
        /// <summary>
        /// 올바른 Envelope는 동일한 TypeID와 Version을 유지하며 복원된다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_정상_기록_왕복()
        {
            var handler = CreateEnvelopeHandler();
            var loc = CreateLocation("envelope.json");
            var created = handler.Create("sample");

            Assert.IsTrue(created.Success, created.Error);
            Assert.IsTrue(handler.Save(created.Session, loc).Success);

            var loaded = handler.Open(loc);

            Assert.IsTrue(loaded.Success, loaded.Error);
            Assert.AreEqual(TYPE_ID, loaded.Session.Document.TypeID);
            Assert.AreEqual(VERSION, loaded.Session.Document.Version);
            Assert.AreEqual("sample", loaded.Session.Document.Name);

            // 같은 파일을 다시 저장할 때에는 완성된 새 내용으로 교체한다.
            ((IDocumentSession<TestBody>)created.Session).Body.Text = "updated";
            var replaced = handler.Save(created.Session, loc);
            var reloaded = handler.Open(loc);

            Assert.IsTrue(replaced.Success, replaced.Error);
            Assert.IsTrue(reloaded.Success, reloaded.Error);
            Assert.AreEqual("updated", ((IDocumentSession<TestBody>)reloaded.Session).Body.Text);
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 TypeID가 지원하는 문서 종류가 아니면 Open을 실패시킨다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_다른_TypeID_열기_거절()
        {
            var loc = WriteEnvelope("other.document", VERSION, "sample");
            var response = CreateEnvelopeHandler().Open(loc);

            Assert.IsFalse(response.Success);
            Assert.IsNotEmpty(response.Error);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 Version이 지원 버전과 일치하지 않으면 Open을 실패시킨다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_다른_Version_열기_거절()
        {
            var loc = WriteEnvelope(TYPE_ID, "2.0", "sample");
            var response = CreateEnvelopeHandler().Open(loc);

            Assert.IsFalse(response.Success);
            StringAssert.Contains("Version", response.Error);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 비어 있는 표시 이름의 Envelope를 읽지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_빈_Name_열기_거절()
        {
            var loc = WriteEnvelope(TYPE_ID, VERSION, "sample");
            var envelope = CreateEnvelope(TYPE_ID, VERSION, "sample");
            envelope.Metadata.Name = "";

            File.WriteAllText(loc.Path, UnityJsonSerializer.Default.Serialize(envelope));

            Assert.IsFalse(CreateEnvelopeHandler().Open(loc).Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Body가 누락된 Envelope를 읽지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_누락_Body_열기_거절()
        {
            var loc = CreateLocation("missing-body.json");
            var envelope = CreateEnvelope(TYPE_ID, VERSION, "sample");
            envelope.Body = null;

            File.WriteAllText(loc.Path, UnityJsonSerializer.Default.Serialize(envelope));

            Assert.IsFalse(CreateEnvelopeHandler().Open(loc).Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Metadata가 누락된 Envelope는 유효한 문서로 취급하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_누락_Metadata_열기_거절()
        {
            var loc = CreateLocation("missing-metadata.json");
            var envelope = CreateEnvelope(TYPE_ID, VERSION, "sample");
            envelope.Metadata = null;

            File.WriteAllText(loc.Path, UnityJsonSerializer.Default.Serialize(envelope));

            Assert.IsFalse(CreateEnvelopeHandler().Open(loc).Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 역직렬화할 수 없는 입력은 Open 실패 응답으로 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_손상된_직렬화_열기_거절()
        {
            var loc = CreateLocation("invalid.json");
            File.WriteAllText(loc.Path, "not a document envelope");

            Assert.IsFalse(CreateEnvelopeHandler().Open(loc).Success);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 다른 Version의 Session을 저장하려 해도 대상 파일을 변경하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void Envelope_잘못된_Version_저장_거절()
        {
            var handler = CreateEnvelopeHandler();
            var loc = CreateLocation("saved.json");
            File.WriteAllText(loc.Path, "original");

            var session = new DocumentSession<TestBody>
            (
                new Document(TYPE_ID, "2.0", "sample"),

                new TestBody
                {
                    Text = "changed"
                },

                loc
            );

            var saved = handler.Save(session, loc);

            Assert.IsFalse(saved.Success);
            Assert.AreEqual("original", File.ReadAllText(loc.Path));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Session Recovery는 지원 버전의 Document만 받아들인다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_잘못된_Version_복구_거절()
        {
            var handler = CreateEnvelopeHandler();
            var document = new Document(TYPE_ID, "2.0", "sample");
            var record = new DocumentBodyRecoveryRecord
            (
                UnityJsonSerializer.Default.Serialize
                (
                    new TestBody
                    {
                        Text = "recovered"
                    }
                )
            );

            Assert.IsFalse(handler.RecoverSession(document, record, null).Success);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// Envelope 문서의 Recovery Record는 Envelope가 아닌 Body만 직렬화하여 복원한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void Envelope_Recovery_Body_왕복()
        {
            var handler = CreateEnvelopeHandler();
            var created = handler.Create("sample");
            var loc = CreateLocation("recovered.json");

            Assert.IsTrue(created.Success, created.Error);

            var record = handler.RecordSessionBody(created.Session);
            var recovered = handler.RecoverSession(created.Session.Document, record, loc);

            Assert.IsNotNull(record);
            Assert.IsTrue(recovered.Success, recovered.Error);
            Assert.AreEqual("sample", ((IDocumentSession<TestBody>)recovered.Session).Body.Text);
            Assert.AreEqual(VERSION, recovered.Session.Document.Version);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Document Envelope 필드와 공통 Record 계약을 함께 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_공통_Record_Metadata_계약()
        {
            var envelope = CreateEnvelope(TYPE_ID, VERSION, "sample");
            var record = (inonego.Xeri.Persistence.IRecord<TestBody>)envelope;
            var expected = new inonego.Xeri.Persistence.RecordType(TYPE_ID, VERSION);

            expected.Validate(record);
            Assert.AreEqual("sample", envelope.Metadata.Name);
            Assert.AreEqual("body", record.Body.Text);
            Assert.AreEqual(TYPE_ID, record.Metadata.TypeID);

            // 공통 인터페이스 프로퍼티는 별도 직렬화 필드로 기록하지 않는다.
            var text = UnityJsonSerializer.Default.Serialize(envelope);
            StringAssert.Contains("TypeID", text);
            StringAssert.Contains("Name", text);
            StringAssert.Contains("HasBody", text);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Document의 XML 루트와 Metadata 속성 구조를 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void Envelope_XML_Metadata_구조_검증()
        {
            var serializer = new XmlSerializer(typeof(DocumentEnvelope<XmlTestBody>));
            var record = new DocumentEnvelope<XmlTestBody>
            (
                new Document(TYPE_ID, VERSION, "xml-name"),

                new XmlTestBody
                {
                    Text = "xml-body"
                }
            );

            string xml;

            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, record);
                xml = writer.ToString();
            }

            StringAssert.Contains("<Document", xml);
            StringAssert.Contains("TypeID=", xml);
            StringAssert.Contains("Version=", xml);
            StringAssert.Contains("Name=", xml);
            StringAssert.Contains("<HasBody>true</HasBody>", xml);
            Assert.IsFalse(xml.Contains("IRecordMetadata"));
            Assert.IsFalse(xml.Contains("IRecord"));

            using var reader = new StringReader(xml);
            var loaded = (DocumentEnvelope<XmlTestBody>)serializer.Deserialize(reader);

            Assert.AreEqual(TYPE_ID, loaded.Metadata.TypeID);
            Assert.AreEqual("xml-name", loaded.Metadata.Name);
            Assert.AreEqual("xml-body", loaded.Body.Text);
        }

    #endregion

    #region Body-root

        // ------------------------------------------------------------
        /// <summary>
        /// Envelope 없이 Body 자체를 기록하고 읽을 수 있다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void BodyRoot_정상_기록_왕복()
        {
            var handler = BodySerializedHandler.CreateForFile<TestBody>
            (
                TYPE_ID, VERSION, UnityJsonSerializer.Default
            );

            var loc = CreateLocation("body.json");
            var session = CreateBodySession(VERSION, loc);

            Assert.IsTrue(handler.Save(session, loc).Success);

            var opened = handler.Open(loc);

            Assert.IsTrue(opened.Success, opened.Error);
            Assert.AreEqual(VERSION, opened.Session.Document.Version);
            Assert.AreEqual("data", ((IDocumentSession<TestBody>)opened.Session).Body.Text);

            ((IDocumentSession<TestBody>)session).Body.Text = "updated";
            var replaced = handler.Save(session, loc);
            var reloaded = handler.Open(loc);

            Assert.IsTrue(replaced.Success, replaced.Error);
            Assert.IsTrue(reloaded.Success, reloaded.Error);
            Assert.AreEqual("updated", ((IDocumentSession<TestBody>)reloaded.Session).Body.Text);
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Body-root 문서 저장은 입력 Session의 지원 버전을 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void BodyRoot_잘못된_Version_저장_거절()
        {
            var handler = BodySerializedHandler.CreateForFile<TestBody>
            (
                TYPE_ID, VERSION, UnityJsonSerializer.Default
            );

            var loc = CreateLocation("body.json");
            var saved = handler.Save(CreateBodySession("2.0", loc), loc);

            Assert.IsFalse(saved.Success);
            Assert.IsFalse(File.Exists(loc.Path));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Body-root의 Session Recovery도 지원 버전을 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void BodyRoot_잘못된_Version_복구_거절()
        {
            var handler = BodySerializedHandler.CreateForFile<TestBody>
            (
                TYPE_ID, VERSION, UnityJsonSerializer.Default
            );

            var record = new DocumentBodyRecoveryRecord
            (
                UnityJsonSerializer.Default.Serialize
                (
                    new TestBody
                    {
                        Text = "data"
                    }
                )
            );

            var result = handler.RecoverSession(new Document(TYPE_ID, "2.0", "sample"), record, null);

            Assert.IsFalse(result.Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Body-root Recovery는 Body를 직렬화한 뒤 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void BodyRoot_Recovery_Body_왕복()
        {
            var handler = BodySerializedHandler.CreateForFile<TestBody>
            (
                TYPE_ID, VERSION, UnityJsonSerializer.Default
            );

            var loc = CreateLocation("recovered.json");
            var session = CreateBodySession(VERSION, loc);
            var record = handler.RecordSessionBody(session);
            var recovered = handler.RecoverSession(session.Document, record, loc);

            Assert.IsNotNull(record);
            Assert.IsTrue(recovered.Success, recovered.Error);
            Assert.AreEqual("data", ((IDocumentSession<TestBody>)recovered.Session).Body.Text);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 손상된 Body Recovery 문자열은 예외 누출 없이 실패 응답으로 변환된다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void BodyRoot_손상된_Recovery_거절()
        {
            var handler = BodySerializedHandler.CreateForFile<TestBody>
            (
                TYPE_ID, VERSION, UnityJsonSerializer.Default
            );

            var record = new DocumentBodyRecoveryRecord("not valid json");
            var recovered = handler.RecoverSession(new Document(TYPE_ID, VERSION, "sample"), record, null);

            Assert.IsFalse(recovered.Success);
        }

    #endregion

    #region Raw Text

        // ------------------------------------------------------------
        /// <summary>
        /// Raw Text 문서는 Envelope 없이 원본 텍스트를 저장하고 읽는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void RawText_텍스트_그대로_왕복()
        {
            var handler = RawTextHandler.CreateForFile(TYPE_ID, VERSION);
            var loc = CreateLocation("script.txt");
            var document = new Document(TYPE_ID, VERSION, "script");
            var body = new RawTextDocumentBody("hello\nraw");
            var session = new DocumentSession<RawTextDocumentBody>(document, body, loc);

            var saved = handler.Save(session, loc);
            var loaded = handler.Open(loc);

            Assert.IsTrue(saved.Success, saved.Error);
            Assert.AreEqual("hello\nraw", File.ReadAllText(loc.Path));
            Assert.IsTrue(loaded.Success, loaded.Error);
            Assert.AreEqual("hello\nraw", ((IDocumentSession<RawTextDocumentBody>)loaded.Session).Body.Text);

            body.Text = "updated";
            var replaced = handler.Save(session, loc);
            var reloaded = handler.Open(loc);

            Assert.IsTrue(replaced.Success, replaced.Error);
            Assert.IsTrue(reloaded.Success, reloaded.Error);
            Assert.AreEqual("updated", ((IDocumentSession<RawTextDocumentBody>)reloaded.Session).Body.Text);
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Raw Text 문서도 잘못된 Version의 Session은 저장하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void RawText_잘못된_Version_저장_거절()
        {
            var handler = RawTextHandler.CreateForFile(TYPE_ID, VERSION);
            var loc = CreateLocation("script.txt");
            var session = new DocumentSession<RawTextDocumentBody>
            (
                new Document(TYPE_ID, "2.0", "script"),
                new RawTextDocumentBody("changed"),
                loc
            );

            Assert.IsFalse(handler.Save(session, loc).Success);
            Assert.IsFalse(File.Exists(loc.Path));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Raw Text Session Recovery의 문서 버전도 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void RawText_잘못된_Version_복구_거절()
        {
            var handler = RawTextHandler.CreateForFile(TYPE_ID, VERSION);
            var record = new DocumentBodyRecoveryRecord("hello");

            var result = handler.RecoverSession(new Document(TYPE_ID, "2.0", "script"), record, null);

            Assert.IsFalse(result.Success);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// RawText는 빈 본문도 Session Recovery 대상으로 허용한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void RawText_빈_본문_Recovery_허용()
        {
            var handler = RawTextHandler.CreateForFile(TYPE_ID, VERSION);
            var document = new Document(TYPE_ID, VERSION, "empty");
            var recovered = handler.RecoverSession
            (
                document,
                new DocumentBodyRecoveryRecord(""),
                null
            );

            Assert.IsTrue(recovered.Success, recovered.Error);
            Assert.AreEqual("", ((IDocumentSession<RawTextDocumentBody>)recovered.Session).Body.Text);
        }

    #endregion

    #region 파일 저장 실패

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 세 파일 Handler 모두 쓰기 실패 시 원본과 Session Dirty를 보존하고
        /// <br/> 임시 파일을 정리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [TestCase("Envelope")]
        [TestCase("BodyRoot")]
        [TestCase("RawText")]
        public void TEST_DocumentPersistence_파일쓰기실패_원본과Dirty유지(string handlerKind)
        {
            var loc = CreateLocation("save-failure.txt");
            var originalService = new DocumentWorkspaceService
            (
                new DocumentWorkspace(),
                new IDocumentHandler[]
                {
                    CreateFileHandler(handlerKind, new FileCommitWriter<string>(TextFileIO.Default))
                }
            );
            var created = originalService.Create(TYPE_ID, "sample");

            Assert.IsTrue(created.Success, created.Error);

            var initialSave = originalService.SaveAs(created.Session, loc);

            Assert.IsTrue(initialSave.Success, initialSave.Error);

            var original = File.ReadAllText(loc.Path);
            var service = new DocumentWorkspaceService
            (
                new DocumentWorkspace(),
                new IDocumentHandler[]
                {
                    CreateFileHandler(handlerKind, new FileCommitWriter<string>(new FailingFileWriter()))
                }
            );
            var opened = service.Open(TYPE_ID, loc);

            Assert.IsTrue(opened.Success, opened.Error);

            if (opened.Session is IDocumentSession<TestBody> serialized)
            {
                serialized.Body.Text = "changed";
            }
            else
            {
                ((IDocumentSession<RawTextDocumentBody>)opened.Session).Body.Text = "changed";
            }

            opened.Session.SetDirty();

            var failed = service.Save(opened.Session);

            Assert.IsFalse(failed.Success);
            Assert.IsNotEmpty(failed.Error);
            Assert.AreSame(loc, opened.Session.Location);
            Assert.IsTrue(opened.Session.IsDirty);
            Assert.AreEqual(original, File.ReadAllText(loc.Path));
            Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        }

    #endregion

    #region 헬퍼

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 문서 종류·버전을 지원하는 Envelope Handler를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static EnvelopeSerializedHandler<TestBody, string> CreateEnvelopeHandler()
        {
            return EnvelopeSerializedHandler.CreateForFile<TestBody>
            (
                TYPE_ID,

                VERSION,

                UnityJsonSerializer.Default,

                name => new TestBody
                {
                    Text = name
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 Writer를 주입해 세 문서 형식의 Handler를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static IDocumentHandler CreateFileHandler
        (
            string handlerKind,
            IDataWriter<string, string> writer
        )
        {
            switch (handlerKind)
            {
                case "Envelope":
                    return new EnvelopeSerializedHandler<TestBody, string>
                    (
                        TYPE_ID, VERSION, UnityJsonSerializer.Default,
                        TextFileIO.Default, writer, TryMapFileLocation,
                        name => new TestBody
                        {
                            Text = name
                        }
                    );

                case "BodyRoot":
                    return new BodySerializedHandler<TestBody, string>
                    (
                        TYPE_ID, VERSION, UnityJsonSerializer.Default,
                        TextFileIO.Default, writer, TryMapFileLocation,
                        name => new TestBody
                        {
                            Text = name
                        }
                    );

                case "RawText":
                    return new RawTextHandler<string>
                    (
                        TYPE_ID, VERSION,
                        TextFileIO.Default, writer, TryMapFileLocation
                    );

                default:
                    throw new ArgumentOutOfRangeException(nameof(handlerKind));
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// FileDocumentLocation을 실제 파일 경로로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool TryMapFileLocation(IDocumentLocation location, out string ioLocation)
        {
            if (location is FileDocumentLocation fileLocation)
            {
                ioLocation = fileLocation.Path;
                return true;
            }

            ioLocation = null;
            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 문서 정보로 Envelope를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static DocumentEnvelope<TestBody> CreateEnvelope
        (
            string typeID,
            string version,
            string name
        )
        {
            return new DocumentEnvelope<TestBody>
            (
                new Document(typeID, version, name),

                new TestBody
                {
                    Text = "body"
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 문서 정보의 Envelope를 파일에 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        private FileDocumentLocation WriteEnvelope(string typeID, string version, string name)
        {
            var loc = CreateLocation("envelope.json");
            File.WriteAllText(loc.Path, UnityJsonSerializer.Default.Serialize(CreateEnvelope(typeID, version, name)));

            return loc;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 버전으로 Body-root Session을 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static DocumentSession<TestBody> CreateBodySession(string version, FileDocumentLocation loc)
        {
            return new DocumentSession<TestBody>
            (
                new Document(TYPE_ID, version, "sample"),

                new TestBody
                {
                    Text = "data"
                },

                loc
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테스트마다 고유한 파일 경로를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private FileDocumentLocation CreateLocation(string fileName)
        {
            return new FileDocumentLocation(Path.Combine(directory, fileName));
        }

    #endregion

    }
}
