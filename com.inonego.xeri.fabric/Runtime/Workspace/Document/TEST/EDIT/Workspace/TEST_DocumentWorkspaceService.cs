/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_DocumentWorkspaceService.cs
수정일 : 2026-07-30

# 설명
DocumentWorkspaceService의 create/open/save/close 실행 계약을 검증한다.
Unity Test Runner (Edit Mode) 에서 실행한다.

# 테스트 구성
 F: Flow - create/open/save 대표 흐름
 O: Open - 이미 열린 session 재사용 흐름
 S: Save - save/saveAs/saveTo 상태 변화
 R: Recovery - workspace/session 복구 흐름
 H: Handler - handler 등록과 반환값 검증
 X: Failure - 실패 응답과 실패 후 상태 보존
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;

using NUnit.Framework;

using inonego.Xeri.IO;
using inonego.Xeri.Serializable;
using inonego.Xeri.Workspace.Document;

namespace inonego.Xeri.TEST.Workspace._Document
{
   // ============================================================
   /// <summary>
   /// DocumentWorkspaceService 실행 흐름 테스트.
   /// </summary>
   // ============================================================
   public class TEST_DocumentWorkspaceService : TEST_DocumentWorkspaceBase
   {

   #region 헬퍼

      // ============================================================
      /// <summary>
      /// Reader Lease 종료 횟수를 관찰하는 테스트용 문자열 IO.
      /// </summary>
      // ============================================================
      private sealed class LeaseTextIO :
         IDataReader<string, string>,
         IDataWriter<string, string>
      {
      #region 필드

         public int DisposeCount { get; private set; }

         private readonly bool readSuccess;

      #endregion

      #region 생성자

         // ------------------------------------------------------------
         /// <summary>
         /// 지정한 Reader 성공 여부로 테스트 IO를 생성합니다.
         /// </summary>
         // ------------------------------------------------------------
         public LeaseTextIO(bool readSuccess) : base()
         {
            this.readSuccess = readSuccess;
         }

      #endregion

      #region IDataReader 구현

         // ------------------------------------------------------------
         /// <summary>
         /// Lease를 포함한 성공 또는 실패 응답을 반환합니다.
         /// </summary>
         // ------------------------------------------------------------
         public ReadResponse<string> Read(string location)
         {
            var lease = new Lease
            (
               () =>
               {
                  DisposeCount++;
               }
            );

            return readSuccess
               ? ReadResponse<string>.Succeed("body", lease)
               : ReadResponse<string>.Fail("read failure", lease: lease);
         }

      #endregion

      #region IDataWriter 구현

         // ------------------------------------------------------------
         /// <summary>
         /// 테스트에서 사용하지 않는 쓰기 작업을 성공으로 반환합니다.
         /// </summary>
         // ------------------------------------------------------------
         public WriteResponse Write(string location, string value)
         {
            return WriteResponse.Succeed();
         }

      #endregion

      }

      // ------------------------------------------------------------
      /// <summary>
      /// 테스트용 파일 document service를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private static DocumentWorkspaceService CreateFileService(out DocumentWorkspace workspace)
      {
         workspace = new DocumentWorkspace();

         return new DocumentWorkspaceService(workspace, new IDocumentHandler[] { new FileDocumentHandler() });
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 테스트용 임시 파일 경로를 만든다.
      /// </summary>
      // ------------------------------------------------------------
      private static string CreateTempPath()
      {
         return Path.Combine(Path.GetTempPath(), "UniXeri_" + Guid.NewGuid().ToString("N") + ".json");
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 테스트 Document location을 문자열 IO location으로 변환합니다.
      /// </summary>
      // ------------------------------------------------------------
      private static bool TryMapTextLocation
      (
         IDocumentLocation location,
         out string ioLocation
      )
      {
         ioLocation = location?.Name;
         return location != null;
      }

   #endregion

   #region F-1: Create / SaveAs / Open 라운드트립

      // --------------------------------------------------------------------------------
      /// <summary>
      /// Create한 문서를 SaveAs로 저장한 뒤 닫고 같은 location을 Open하면 payload가 복원된다.
      /// </summary>
      // --------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Create_SaveAs_Open_라운드트립()
      {
         var service = CreateService(out var workspace);
         var loc = CreateLocation("Roundtrip");

         var createResponse = service.Create(TypeID, "Roundtrip");

         Assert.IsTrue(createResponse.Success, createResponse.Error);
         Assert.AreEqual(1, workspace.Sessions.Count);
         Assert.AreSame(createResponse.Session, workspace.Sessions[0]);

         SetPayload(createResponse.Session, "hello", 42);

         var saveResponse = service.SaveAs(createResponse.Session, loc);

         Assert.IsTrue(saveResponse.Success, saveResponse.Error);
         Assert.AreSame(loc, createResponse.Session.Location);
         Assert.IsFalse(createResponse.Session.IsDirty);
         Assert.IsFalse(string.IsNullOrEmpty(GetIOLocation(loc).Value));

         Assert.IsTrue(service.Close(createResponse.Session));
         Assert.AreEqual(0, workspace.Sessions.Count);

         var openResponse = service.Open(TypeID, loc);

         Assert.IsTrue(openResponse.Success, openResponse.Error);
         Assert.AreEqual(DocumentOpenKind.NewSession, openResponse.Kind);
         Assert.AreEqual(1, workspace.Sessions.Count);
         Assert.AreNotSame(createResponse.Session, openResponse.Session);
         Assert.AreSame(loc, openResponse.Session.Location);
         Assert.IsFalse(openResponse.Session.IsDirty);
         Assert.AreEqual(TypeID, openResponse.Session.Document.TypeID);
         Assert.AreEqual(Version, openResponse.Session.Document.Version);
         Assert.AreEqual(loc.Name, openResponse.Session.Document.Name);
         Assert.AreEqual("hello", GetPayload(openResponse.Session).Text);
         Assert.AreEqual(42, GetPayload(openResponse.Session).Count);
      }

   #endregion

   #region F-2: Open의 Reader Lease 종료

      // --------------------------------------------------------------------------------
      /// <summary>
      /// Open 성공과 Reader 조기 실패 모두 전달받은 Lease를 정확히 한 번 종료합니다.
      /// </summary>
      // --------------------------------------------------------------------------------
      [TestCase(true)]
      [TestCase(false)]
      public void TEST_DocumentWorkspaceService_Open성공과_조기실패는_ReaderLease를_한번만종료
      (
         bool readSuccess
      )
      {
         var io = new LeaseTextIO(readSuccess);
         var handler = new RawTextHandler<string>
         (
            TypeID,
            Version,
            io,
            io,
            TryMapTextLocation
         );
         var service = new DocumentWorkspaceService
         (
            new DocumentWorkspace(),
            new IDocumentHandler[] { handler }
         );

         var response = service.Open(TypeID, CreateLocation("Lease"));

         Assert.AreEqual(readSuccess, response.Success, response.Error);
         Assert.AreEqual(1, io.DisposeCount);
      }

   #endregion

   #region O-1: 이미 열린 session 재사용 흐름

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Open은 같은 typeID와 location을 가진 session이 이미 열려 있으면 기존 session을 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Open_같은TypeID와Location_기존Session반환()
      {
         var service = CreateService(out var workspace);
         var loc = CreateLocation("AlreadyOpen");
         var session = CreateSavedSession(service, loc, "base", 1);

         SetPayload(session, "dirty", 2);

         var response = service.Open(TypeID, new MemoryDocumentLocation("Alias", loc.Key));

         Assert.IsTrue(response.Success, response.Error);
         Assert.AreEqual(DocumentOpenKind.AlreadyOpen, response.Kind);
         Assert.AreSame(session, response.Session);
         Assert.AreEqual(1, workspace.Sessions.Count);
         Assert.IsTrue(session.IsDirty);
         Assert.AreEqual("dirty", GetPayload(session).Text);
         Assert.AreEqual(2, GetPayload(session).Count);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Open은 같은 location이라도 typeID가 다르면 기존 session으로 판단하지 않는다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Open_같은Location_다른TypeID_기존Session아님()
      {
         var workspace = new DocumentWorkspace();
         var loc = CreateLocation("SameLocation");
         var seedService = CreateService(out var seedWorkspace);
         var seedSession = CreateSavedSession(seedService, loc, "seed", 1);
         var foreignSession = new DocumentSession<DocumentBody>(
            new Document(ForeignTypeID, Version, "Foreign"),
            new DocumentBody(new Payload("foreign", 1)),
            loc
         );
         var service = new DocumentWorkspaceService
         (
            workspace,
            new IDocumentHandler[]
            {
               new DocumentHandler(),
               new TEST_Handler(ForeignTypeID, openSession: foreignSession),
            }
         );

         Assert.IsTrue(seedService.Close(seedSession));
         Assert.AreEqual(0, seedWorkspace.Sessions.Count);
         Assert.IsTrue(workspace.AddSession(foreignSession));

         var response = service.Open(TypeID, loc);

         Assert.IsTrue(response.Success, response.Error);
         Assert.AreEqual(DocumentOpenKind.NewSession, response.Kind);
         Assert.AreEqual(2, workspace.Sessions.Count);
         Assert.AreNotSame(foreignSession, response.Session);
         Assert.AreEqual(TypeID, response.Session.Document.TypeID);
      }

   #endregion

   #region S-1: Save / SaveTo location과 dirty 상태

      // ------------------------------------------------------------
      /// <summary>
      /// Save는 기준 location에 저장하고 성공하면 dirty를 해제한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Save_기준Location에_저장_Dirty해제()
      {
         var service = CreateService(out _);
         var loc = CreateLocation("Save");
         var session = CreateSavedSession(service, loc, "before", 1);
         var ioLoc = GetIOLocation(loc);
         var beforeText = ioLoc.Value;

         SetPayload(session, "after", 2);

         var response = service.Save(session);

         Assert.IsTrue(response.Success, response.Error);
         Assert.AreSame(loc, session.Location);
         Assert.IsFalse(session.IsDirty);
         Assert.AreNotEqual(beforeText, ioLoc.Value);
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// SaveTo는 다른 location에 저장하지만 기준 location과 dirty 상태는 유지한다.
      /// </summary>
      // ----------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_SaveTo_다른Location저장_기준Location과Dirty유지()
      {
         var service = CreateService(out _);
         var baseLoc = CreateLocation("Base");
         var copyLoc = CreateLocation("Copy");
         var session = CreateSavedSession(service, baseLoc, "base", 1);
         var baseText = GetIOLocation(baseLoc).Value;

         SetPayload(session, "copy", 2);

         var response = service.SaveTo(session, copyLoc);

         Assert.IsTrue(response.Success, response.Error);
         Assert.AreSame(baseLoc, session.Location);
         Assert.IsTrue(session.IsDirty);
         Assert.AreEqual(baseText, GetIOLocation(baseLoc).Value);
         Assert.IsFalse(string.IsNullOrEmpty(GetIOLocation(copyLoc).Value));
         Assert.AreNotEqual(baseText, GetIOLocation(copyLoc).Value);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// SaveAs 성공 후에는 새 location이 open session lookup 기준이 된다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_SaveAs_성공_새Location으로OpenSession재사용()
      {
         var service = CreateService(out var workspace);
         var baseLoc = CreateLocation("Base");
         var nextLoc = CreateLocation("Next");
         var session = CreateSavedSession(service, baseLoc, "base", 1);

         SetPayload(session, "next", 2);

         var saveAsResponse = service.SaveAs(session, nextLoc);

         Assert.IsTrue(saveAsResponse.Success, saveAsResponse.Error);
         Assert.AreSame(nextLoc, session.Location);

         var nextOpenResponse = service.Open(TypeID, nextLoc);

         Assert.IsTrue(nextOpenResponse.Success, nextOpenResponse.Error);
         Assert.AreEqual(DocumentOpenKind.AlreadyOpen, nextOpenResponse.Kind);
         Assert.AreSame(session, nextOpenResponse.Session);
         Assert.AreEqual(1, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// SaveTo 성공 후에는 기준 location이 유지되므로 복사 location open은 새 session을 만든다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_SaveTo_성공_복사Location은새Session생성()
      {
         var service = CreateService(out var workspace);
         var baseLoc = CreateLocation("Base");
         var copyLoc = CreateLocation("Copy");
         var session = CreateSavedSession(service, baseLoc, "base", 1);

         SetPayload(session, "copy", 2);

         var saveToResponse = service.SaveTo(session, copyLoc);

         Assert.IsTrue(saveToResponse.Success, saveToResponse.Error);
         Assert.AreSame(baseLoc, session.Location);

         var baseOpenResponse = service.Open(TypeID, baseLoc);

         Assert.IsTrue(baseOpenResponse.Success, baseOpenResponse.Error);
         Assert.AreEqual(DocumentOpenKind.AlreadyOpen, baseOpenResponse.Kind);
         Assert.AreSame(session, baseOpenResponse.Session);
         Assert.AreEqual(1, workspace.Sessions.Count);

         var copyOpenResponse = service.Open(TypeID, copyLoc);

         Assert.IsTrue(copyOpenResponse.Success, copyOpenResponse.Error);
         Assert.AreEqual(DocumentOpenKind.NewSession, copyOpenResponse.Kind);
         Assert.AreNotSame(session, copyOpenResponse.Session);
         Assert.AreEqual(2, workspace.Sessions.Count);
         Assert.AreSame(copyLoc, copyOpenResponse.Session.Location);
      }

   #endregion

   #region R-1: Recovery record 생성과 복구

      // --------------------------------------------------------------------------------
      /// <summary>
      /// Location이 없는 dirty session도 serialized recovery record로 복구된다.
      /// </summary>
      // --------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_RecordRecovery_UnsavedDirtySession_Record와Dirty복구()
      {
         var sourceService = CreateService(out _);
         var createResponse = sourceService.Create(TypeID, "Unsaved");

         Assert.IsTrue(createResponse.Success, createResponse.Error);

         SetPayload(createResponse.Session, "draft", 15);

         var recordResponse = sourceService.RecordRecovery();
         Assert.IsTrue(recordResponse.Success, recordResponse.Error);

         var record = recordResponse.Record;
         var targetService = CreateService(out var targetWorkspace);
         var recoverResponse = targetService.Recover(record);

         Assert.IsTrue(recoverResponse.Success, recoverResponse.Error);
         Assert.AreEqual(1, targetWorkspace.Sessions.Count);

         var session = targetWorkspace.Sessions[0];
         var payload = GetPayload(session);

         Assert.IsNull(session.Location);
         Assert.IsTrue(session.IsDirty);
         Assert.AreEqual("draft", payload.Text);
         Assert.AreEqual(15, payload.Count);
      }

      // --------------------------------------------------------------------------------
      /// <summary>
      /// FileDocumentLocation을 가진 session은 location과 record를 함께 복구한다.
      /// </summary>
      // --------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_RecordRecovery_FileSession_파일Location과Record복구()
      {
         var path = CreateTempPath();

         try
         {
            var sourceService = CreateFileService(out _);
            var loc = new FileDocumentLocation(path, "FileRecovery");
            var createResponse = sourceService.Create(TypeID, "FileRecovery");

            Assert.IsTrue(createResponse.Success, createResponse.Error);

            SetPayload(createResponse.Session, "file", 27);

            var saveResponse = sourceService.SaveAs(createResponse.Session, loc);

            Assert.IsTrue(saveResponse.Success, saveResponse.Error);

            var recordResponse = sourceService.RecordRecovery();
            Assert.IsTrue(recordResponse.Success, recordResponse.Error);

            var record = recordResponse.Record;
            var restoredRecord = UnityJsonSerializer.Pretty.Deserialize<DocumentWorkspaceRecoveryRecord>(record);

            Assert.AreEqual(1, restoredRecord.Sessions.Count);
            Assert.AreEqual(TypeID, restoredRecord.Sessions[0].TypeID);
            Assert.AreEqual(Version, restoredRecord.Sessions[0].Version);
            Assert.IsNotNull(restoredRecord.Sessions[0].Location);
            Assert.IsInstanceOf<FileDocumentLocationRecord>(restoredRecord.Sessions[0].Location);
            Assert.IsNotNull(restoredRecord.Sessions[0].Body);
            Assert.IsFalse(string.IsNullOrEmpty(restoredRecord.Sessions[0].Body.Record));

            var targetService = CreateFileService(out var targetWorkspace);
            var recoverResponse = targetService.Recover(record);

            Assert.IsTrue(recoverResponse.Success, recoverResponse.Error);
            Assert.AreEqual(1, targetWorkspace.Sessions.Count);

            var session = targetWorkspace.Sessions[0];
            var payload = GetPayload(session);

            Assert.IsFalse(session.IsDirty);
            Assert.AreEqual(DocumentSessionRecoveryKind.Recovered, recoverResponse.Sessions[0].Kind);
            Assert.IsTrue(session.Location.Equals(loc));
            Assert.AreEqual("file", payload.Text);
            Assert.AreEqual(27, payload.Count);
         }
         finally
         {
            if (File.Exists(path))
            {
               File.Delete(path);
            }
         }
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// 저장 이후 변경된 file session은 recovery record의 body와 dirty 상태를 함께 복구한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_RecordRecovery_DirtyFileSession_Record와Dirty복구()
      {
         var path = CreateTempPath();

         try
         {
            var sourceService = CreateFileService(out _);
            var loc = new FileDocumentLocation(path, "DirtyFileRecovery");
            var createResponse = sourceService.Create(TypeID, "DirtyFileRecovery");

            Assert.IsTrue(createResponse.Success, createResponse.Error);

            SetPayload(createResponse.Session, "saved", 31);

            var saveResponse = sourceService.SaveAs(createResponse.Session, loc);

            Assert.IsTrue(saveResponse.Success, saveResponse.Error);

            SetPayload(createResponse.Session, "dirty", 32);

            var recordResponse = sourceService.RecordRecovery();
            Assert.IsTrue(recordResponse.Success, recordResponse.Error);

            var record = recordResponse.Record;
            var targetService = CreateFileService(out var targetWorkspace);
            var recoverResponse = targetService.Recover(record);

            Assert.IsTrue(recoverResponse.Success, recoverResponse.Error);
            Assert.AreEqual(1, targetWorkspace.Sessions.Count);
            Assert.AreEqual(DocumentSessionRecoveryKind.Recovered, recoverResponse.Sessions[0].Kind);

            var session = targetWorkspace.Sessions[0];
            var payload = GetPayload(session);

            Assert.IsTrue(session.IsDirty);
            Assert.IsTrue(session.Location.Equals(loc));
            Assert.AreEqual("dirty", payload.Text);
            Assert.AreEqual(32, payload.Count);
         }
         finally
         {
            if (File.Exists(path))
            {
               File.Delete(path);
            }
         }
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Recovery record를 문자열로 저장했다가 다시 읽어도 file session과 dirty 상태가 복구된다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_RecordRecovery_JsonRoundTrip_파일Session복구()
      {
         var path = CreateTempPath();

         try
         {
            var sourceService = CreateFileService(out _);
            var loc = new FileDocumentLocation(path, "JsonRoundTripRecovery");
            var createResponse = sourceService.Create(TypeID, "JsonRoundTripRecovery");

            Assert.IsTrue(createResponse.Success, createResponse.Error);

            SetPayload(createResponse.Session, "saved", 51);

            var saveResponse = sourceService.SaveAs(createResponse.Session, loc);

            Assert.IsTrue(saveResponse.Success, saveResponse.Error);

            SetPayload(createResponse.Session, "serialized", 52);

            var recordResponse = sourceService.RecordRecovery();
            Assert.IsTrue(recordResponse.Success, recordResponse.Error);

            var record = recordResponse.Record;
            var targetService = CreateFileService(out var targetWorkspace);
            var recoverResponse = targetService.Recover(record);

            Assert.IsTrue(recoverResponse.Success, recoverResponse.Error);
            Assert.AreEqual(1, targetWorkspace.Sessions.Count);
            Assert.AreEqual(DocumentSessionRecoveryKind.Recovered, recoverResponse.Sessions[0].Kind);

            var session = targetWorkspace.Sessions[0];
            var payload = GetPayload(session);

            Assert.IsTrue(session.IsDirty);
            Assert.IsTrue(session.Location.Equals(loc));
            Assert.AreEqual("serialized", payload.Text);
            Assert.AreEqual(52, payload.Count);
         }
         finally
         {
            if (File.Exists(path))
            {
               File.Delete(path);
            }
         }
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Recovery record는 Unity JSON round-trip 이후에도 file location record 타입을 유지한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_RecordRecovery_JsonRoundTrip_LocationRecord타입보존()
      {
         var path = CreateTempPath();

         try
         {
            var sourceService = CreateFileService(out _);
            var loc = new FileDocumentLocation(path, "LocationRecordType");
            var createResponse = sourceService.Create(TypeID, "LocationRecordType");

            Assert.IsTrue(createResponse.Success, createResponse.Error);

            SetPayload(createResponse.Session, "location", 61);

            var saveResponse = sourceService.SaveAs(createResponse.Session, loc);

            Assert.IsTrue(saveResponse.Success, saveResponse.Error);

            var recordResponse = sourceService.RecordRecovery();
            Assert.IsTrue(recordResponse.Success, recordResponse.Error);

            var record = recordResponse.Record;
            var restoredRecord = UnityJsonSerializer.Pretty.Deserialize<DocumentWorkspaceRecoveryRecord>(record);
            var targetService = CreateFileService(out var targetWorkspace);
            var recoverResponse = targetService.Recover(record);

            Assert.AreEqual(1, restoredRecord.Sessions.Count);
            Assert.IsNotNull(restoredRecord.Sessions[0].Location);
            Assert.IsInstanceOf<FileDocumentLocationRecord>(restoredRecord.Sessions[0].Location);
            Assert.IsTrue(recoverResponse.Success, recoverResponse.Error);
            Assert.AreEqual(1, targetWorkspace.Sessions.Count);

            var session = targetWorkspace.Sessions[0];
            var payload = GetPayload(session);

            Assert.IsTrue(session.Location.Equals(loc));
            Assert.AreEqual("location", payload.Text);
            Assert.AreEqual(61, payload.Count);
         }
         finally
         {
            if (File.Exists(path))
            {
               File.Delete(path);
            }
         }
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// 같은 file location이 이미 열려 있으면 recovery는 새 session을 추가하지 않는다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Recover_AlreadyOpenLocation_기존Session반환()
      {
         var path = CreateTempPath();

         try
         {
            var sourceService = CreateFileService(out _);
            var loc = new FileDocumentLocation(path, "AlreadyOpenRecovery");
            var createResponse = sourceService.Create(TypeID, "AlreadyOpenRecovery");

            Assert.IsTrue(createResponse.Success, createResponse.Error);

            SetPayload(createResponse.Session, "file", 41);

            var saveResponse = sourceService.SaveAs(createResponse.Session, loc);

            Assert.IsTrue(saveResponse.Success, saveResponse.Error);

            var recordResponse = sourceService.RecordRecovery();
            Assert.IsTrue(recordResponse.Success, recordResponse.Error);

            var record = recordResponse.Record;
            var targetService = CreateFileService(out var targetWorkspace);
            var openResponse = targetService.Open(TypeID, loc);

            Assert.IsTrue(openResponse.Success, openResponse.Error);

            var recoverResponse = targetService.Recover(record);

            Assert.IsTrue(recoverResponse.Success, recoverResponse.Error);
            Assert.AreEqual(1, targetWorkspace.Sessions.Count);
            Assert.AreEqual(DocumentSessionRecoveryKind.AlreadyOpen, recoverResponse.Sessions[0].Kind);
            Assert.AreSame(openResponse.Session, recoverResponse.Sessions[0].Session);
         }
         finally
         {
            if (File.Exists(path))
            {
               File.Delete(path);
            }
         }
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Body recovery는 transport와 분리되므로 같은 TypeID handler면 다른 transport preset에서도 복구된다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Recover_같은TypeID_다른TransportPreset_Body복구()
      {
         var sourceService = CreateService(out _);
         var createResponse = sourceService.Create(TypeID, "TransportSeparated");

         Assert.IsTrue(createResponse.Success, createResponse.Error);

         SetPayload(createResponse.Session, "memory", 52);

         var recordResponse = sourceService.RecordRecovery();
         Assert.IsTrue(recordResponse.Success, recordResponse.Error);

         var record = recordResponse.Record;
         var targetService = CreateFileService(out _);
         var recoverResponse = targetService.Recover(record);

         Assert.IsTrue(recoverResponse.Success, recoverResponse.Error);
         Assert.AreEqual(1, recoverResponse.Sessions.Count);
         Assert.AreEqual(DocumentSessionRecoveryKind.Recovered, recoverResponse.Sessions[0].Kind);
         Assert.IsNull(recoverResponse.Sessions[0].Session.Location);
         Assert.AreEqual("memory", GetPayload(recoverResponse.Sessions[0].Session).Text);
         Assert.AreEqual(52, GetPayload(recoverResponse.Sessions[0].Session).Count);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// 기본 recovery를 제공하지 않는 location은 저장 위치 없는 session으로 조용히 바꾸지 않고 record 실패로 드러낸다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_RecordRecovery_UnsupportedLocation_Record실패()
      {
         var sourceService = CreateService(out _);
         var loc = CreateLocation("MemoryRecovery");

         CreateSavedSession(sourceService, loc, "memory", 8);

         var recordResponse = sourceService.RecordRecovery();
         var restoredRecord = UnityJsonSerializer.Pretty.Deserialize<DocumentWorkspaceRecoveryRecord>(recordResponse.Record);

         Assert.IsFalse(recordResponse.Success);
         Assert.AreEqual(0, restoredRecord.Sessions.Count);
      }

   #endregion

   #region H-1: Handler 등록 검증

      // ------------------------------------------------------------
      /// <summary>
      /// 중복된 handler TypeID는 service 생성 단계에서 실패한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_중복TypeID_등록실패()
      {
         var workspace = new DocumentWorkspace();
         var handlers = new IDocumentHandler[]
         {
            new DocumentHandler(),
            new DocumentHandler(),
         };

         Assert.Throws<ArgumentException>
         (
            () => new DocumentWorkspaceService(workspace, handlers)
         );
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// null handler는 service 생성 단계에서 실패한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_Null_등록실패()
      {
         var workspace = new DocumentWorkspace();
         var handlers = new IDocumentHandler[] { null };

         Assert.Throws<ArgumentException>
         (
            () => new DocumentWorkspaceService(workspace, handlers)
         );
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 비어 있는 handler TypeID는 service 생성 단계에서 실패한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_빈TypeID_등록실패()
      {
         var workspace = new DocumentWorkspace();
         var handlers = new IDocumentHandler[] { new TEST_Handler("") };

         Assert.Throws<ArgumentException>
         (
            () => new DocumentWorkspaceService(workspace, handlers)
         );
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

   #endregion

   #region H-2: Handler 반환값 검증

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 다른 TypeID의 session을 반환하면 workspace에 추가하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_다른TypeIDSession_추가실패()
      {
         var workspace = new DocumentWorkspace();
         var session = new DocumentSession<DocumentBody>(
            new Document(ForeignTypeID, Version, "Foreign"),
            new DocumentBody(new Payload("foreign", 1)),
            null
         );
         var service = new DocumentWorkspaceService
         (
            workspace,
            new IDocumentHandler[] { new TEST_Handler(TypeID, session) }
         );

         var response = service.Create(TypeID, "Invalid");

         Assert.IsFalse(response.Success);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 Open에서 다른 TypeID의 session을 반환하면 workspace에 추가하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_Open_다른TypeIDSession_추가실패()
      {
         var workspace = new DocumentWorkspace();
         var loc = CreateLocation("InvalidOpen");
         var session = new DocumentSession<DocumentBody>(
            new Document(ForeignTypeID, Version, "Foreign"),
            new DocumentBody(new Payload("foreign", 1)),
            loc
         );
         var service = new DocumentWorkspaceService
         (
            workspace,
            new IDocumentHandler[] { new TEST_Handler(TypeID, openSession: session) }
         );

         var response = service.Open(TypeID, loc);

         Assert.IsFalse(response.Success);
         Assert.AreEqual(DocumentOpenKind.None, response.Kind);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 Open에서 location 없는 session을 반환하면 workspace에 추가하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_Open_Location없는Session_추가실패()
      {
         var workspace = new DocumentWorkspace();
         var loc = CreateLocation("InvalidOpen");
         var session = new DocumentSession<DocumentBody>(
            new Document(TypeID, Version, "NoLocation"),
            new DocumentBody(new Payload("invalid", 1)),
            null
         );
         var service = new DocumentWorkspaceService
         (
            workspace,
            new IDocumentHandler[] { new TEST_Handler(TypeID, openSession: session) }
         );

         var response = service.Open(TypeID, loc);

         Assert.IsFalse(response.Success);
         Assert.AreEqual(DocumentOpenKind.None, response.Kind);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 Open에서 요청 location과 다른 session을 반환하면 workspace에 추가하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_Open_다른LocationSession_추가실패()
      {
         var workspace = new DocumentWorkspace();
         var loc = CreateLocation("Requested");
         var otherLoc = CreateLocation("Other");
         var session = new DocumentSession<DocumentBody>(
            new Document(TypeID, Version, "Other"),
            new DocumentBody(new Payload("invalid", 1)),
            otherLoc
         );
         var service = new DocumentWorkspaceService
         (
            workspace,
            new IDocumentHandler[] { new TEST_Handler(TypeID, openSession: session) }
         );

         var response = service.Open(TypeID, loc);

         Assert.IsFalse(response.Success);
         Assert.AreEqual(DocumentOpenKind.None, response.Kind);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 document 없는 session을 반환하면 workspace에 추가하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_Document없는Session_추가실패()
      {
         var workspace = new DocumentWorkspace();
         var session = new BrokenSession
         (
            null,
            new DocumentBody(new Payload("broken", 1)),
            null
         );
         var service = new DocumentWorkspaceService
         (
            workspace,
            new IDocumentHandler[] { new TEST_Handler(TypeID, session) }
         );

         var response = service.Create(TypeID, "Invalid");

         Assert.IsFalse(response.Success);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 body 없는 session을 반환하면 workspace에 추가하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_Body없는Session_추가실패()
      {
         var workspace = new DocumentWorkspace();
         var session = new BrokenSession
         (
            new Document(TypeID, Version, "NoBody"),
            null,
            null
         );
         var service = new DocumentWorkspaceService
         (
            workspace,
            new IDocumentHandler[] { new TEST_Handler(TypeID, session) }
         );

         var response = service.Create(TypeID, "Invalid");

         Assert.IsFalse(response.Success);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 Open에서 body 없는 session을 반환하면 workspace에 추가하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Handler_Open_Body없는Session_추가실패()
      {
         var workspace = new DocumentWorkspace();
         var loc = CreateLocation("NoBodyOpen");
         var session = new BrokenSession
         (
            new Document(TypeID, Version, "NoBody"),
            null,
            loc
         );
         var service = new DocumentWorkspaceService
         (
            workspace,
            new IDocumentHandler[] { new TEST_Handler(TypeID, openSession: session) }
         );

         var response = service.Open(TypeID, loc);

         Assert.IsFalse(response.Success);
         Assert.AreEqual(DocumentOpenKind.None, response.Kind);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

   #endregion

   #region X-1: 실패 응답과 상태 보존

      // ------------------------------------------------------------
      /// <summary>
      /// 등록되지 않은 문서 종류 Create는 실패하고 session을 추가하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Create_지원하지않는TypeID_실패()
      {
         var service = CreateService(out var workspace);

         var response = service.Create(ForeignTypeID, "Unsupported");

         Assert.IsFalse(response.Success);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Location 없는 새 문서 Save는 실패하고 session 상태를 유지한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Save_Location없는_새문서_실패()
      {
         var service = CreateService(out _);
         var createResponse = service.Create(TypeID, "NoLocation");

         Assert.IsTrue(createResponse.Success, createResponse.Error);

         createResponse.Session.SetDirty();

         var response = service.Save(createResponse.Session);

         Assert.IsFalse(response.Success);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(createResponse.Session.Location);
         Assert.IsTrue(createResponse.Session.IsDirty);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// SaveAs 실패는 기준 location과 dirty 상태를 유지한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_SaveAs_실패_기준Location과Dirty유지()
      {
         var service = CreateService(out _);
         var baseLoc = CreateLocation("Base");
         var unsupportedLoc = new MemoryDocumentLocation("Unsupported", "Unsupported", new object());
         var session = CreateSavedSession(service, baseLoc, "base", 1);
         var baseText = GetIOLocation(baseLoc).Value;

         SetPayload(session, "changed", 2);

         var response = service.SaveAs(session, unsupportedLoc);

         Assert.IsFalse(response.Success);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.AreSame(baseLoc, session.Location);
         Assert.IsTrue(session.IsDirty);
         Assert.AreEqual(baseText, GetIOLocation(baseLoc).Value);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// SaveTo 실패는 기준 location과 dirty 상태를 유지한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_SaveTo_실패_기준Location과Dirty유지()
      {
         var service = CreateService(out _);
         var baseLoc = CreateLocation("Base");
         var unsupportedLoc = new MemoryDocumentLocation("Unsupported", "Unsupported", new object());
         var session = CreateSavedSession(service, baseLoc, "base", 1);
         var baseText = GetIOLocation(baseLoc).Value;

         SetPayload(session, "changed", 2);

         var response = service.SaveTo(session, unsupportedLoc);

         Assert.IsFalse(response.Success);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.AreSame(baseLoc, session.Location);
         Assert.IsTrue(session.IsDirty);
         Assert.AreEqual(baseText, GetIOLocation(baseLoc).Value);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 변환할 수 없는 location은 Open 실패로 처리된다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Open_지원하지않는Location_실패()
      {
         var service = CreateService(out var workspace);
         var loc = new ObjectDocumentLocation("Unsupported", new object());

         var response = service.Open(TypeID, loc);

         Assert.IsFalse(response.Success);
         Assert.AreEqual(DocumentOpenKind.None, response.Kind);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 지원하지 않는 body 타입은 Save 실패로 처리된다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Save_지원하지않는Body_실패()
      {
         var service = CreateService(out var workspace);
         var loc = CreateLocation("Foreign");
         var session = new DocumentSession<ForeignBody>(
            new Document(TypeID, Version, "Foreign"),
            new ForeignBody(),
            loc
         );

         workspace.AddSession(session);
         session.SetDirty();

         var response = service.Save(session);

         Assert.IsFalse(response.Success);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsTrue(session.IsDirty);
         Assert.AreSame(loc, session.Location);
         Assert.IsTrue(string.IsNullOrEmpty(GetIOLocation(loc).Value));
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace에 없는 session Close는 실패 반환하고 workspace 상태를 변경하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceService_Close_Workspace밖Session_실패()
      {
         var service = CreateService(out var workspace);
         var session = new DocumentSession<DocumentBody>(
            new Document(TypeID, Version, "Detached"),
            new DocumentBody(new Payload("detached", 1)),
            CreateLocation("Detached")
         );

         var response = service.Close(session);

         Assert.IsFalse(response);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

   #endregion

   }
}
