/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_DocumentWorkspaceController.cs
수정일 : 2026-06-28

# 설명
DocumentWorkspaceController의 사용자-facing 문서 흐름 테스트.
Unity Test Runner (Edit Mode) 에서 실행한다.

# 테스트 구성
 O: Open - 생성과 열기 response 변환
 S: Save - 저장 대상 검증, location 입력 필요 분기, 저장 response 변환
 C: Close - 닫기 대상 검증, dirty 확인 대기, 닫기 response 변환
========================================================================= BLOCK_HEADER_END */

using NUnit.Framework;

using inonego.Xeri.Workspace.Document;

namespace inonego.Xeri.TEST.Workspace._Document
{
   // ============================================================
   /// <summary>
   /// DocumentWorkspaceController 사용자 흐름 테스트.
   /// </summary>
   // ============================================================
   public class TEST_DocumentWorkspaceController : TEST_DocumentWorkspaceBase
   {

   #region O-1: 생성과 열기 입력 검증

      // ------------------------------------------------------------
      /// <summary>
      /// Create 성공은 생성된 session을 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Create_성공_Session반환()
      {
         var controller = CreateController(out var workspace);

         var response = controller.Create(TypeID, "Created");

         Assert.IsTrue(response.Success, response.Error);
         Assert.IsNotNull(response.Session);
         Assert.AreSame(response.Session, workspace.Sessions[0]);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Open의 null location은 location 입력 분기가 아니라 실패로 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Open_NullLocation_Failed반환()
      {
         var controller = CreateController(out var workspace);

         var response = controller.Open(TypeID, null);

         Assert.AreEqual(DocumentOpenFlowKind.Failed, response.Kind);
         Assert.IsTrue(response.Failed);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지원하지 않는 문서 종류 Open은 사용자-facing 실패 response로 변환한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Open_지원하지않는TypeID_Failed반환()
      {
         var controller = CreateController(out var workspace);
         var loc = CreateLocation("Unsupported");

         var response = controller.Open(ForeignTypeID, loc);

         Assert.AreEqual(DocumentOpenFlowKind.Failed, response.Kind);
         Assert.IsTrue(response.Failed);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.IsNull(response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

   #endregion

   #region O-2: 이미 열린 session 재사용 흐름

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Open은 같은 typeID와 location이 이미 열려 있으면 AlreadyOpen으로 기존 session을 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Open_같은TypeID와Location_기존Session반환()
      {
         var controller = CreateController(out var workspace);
         var loc = CreateLocation("AlreadyOpen");
         var session = CreateSavedSession(controller, loc, "base", 1);

         SetPayload(session, "dirty", 2);

         var response = controller.Open(TypeID, new MemoryDocumentLocation("Alias", loc.Key));

         Assert.AreEqual(DocumentOpenFlowKind.AlreadyOpen, response.Kind, response.Error);
         Assert.AreSame(session, response.Session);
         Assert.AreEqual(1, workspace.Sessions.Count);
         Assert.IsTrue(session.IsDirty);
      }

   #endregion

   #region S-1: 저장 대상 session 검증

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Workspace에 없는 session은 location 입력 대기가 아니라 실패로 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Save_Workspace밖Session_Failed반환()
      {
         var controller = CreateController(out var workspace);
         var session = new DocumentSession<DocumentBody>(
            new Document(TypeID, Version, "Detached"),
            new DocumentBody(new Payload("detached", 1)),
            null
         );

         session.SetDirty();

         var response = controller.Save(session);

         Assert.AreEqual(DocumentSaveFlowKind.Failed, response.Kind);
         Assert.IsTrue(response.Failed);
         Assert.IsFalse(response.NeedLoc);
         Assert.AreSame(session, response.Session);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Workspace에 없는 session은 SaveAs location 입력 대기가 아니라 실패로 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_SaveAs_Workspace밖Session_Failed반환()
      {
         var controller = CreateController(out var workspace);
         var session = new DocumentSession<DocumentBody>(
            new Document(TypeID, Version, "Detached"),
            new DocumentBody(new Payload("detached", 1)),
            CreateLocation("Detached")
         );

         session.SetDirty();

         var response = controller.SaveAs(session, null);

         Assert.AreEqual(DocumentSaveFlowKind.Failed, response.Kind);
         Assert.IsTrue(response.Failed);
         Assert.IsFalse(response.NeedLoc);
         Assert.AreSame(session, response.Session);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Workspace에 없는 session은 SaveTo location 입력 대기가 아니라 실패로 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_SaveTo_Workspace밖Session_Failed반환()
      {
         var controller = CreateController(out var workspace);
         var session = new DocumentSession<DocumentBody>(
            new Document(TypeID, Version, "Detached"),
            new DocumentBody(new Payload("detached", 1)),
            CreateLocation("Detached")
         );

         session.SetDirty();

         var response = controller.SaveTo(session, null);

         Assert.AreEqual(DocumentSaveFlowKind.Failed, response.Kind);
         Assert.IsTrue(response.Failed);
         Assert.IsFalse(response.NeedLoc);
         Assert.AreSame(session, response.Session);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

   #endregion

   #region S-2: Location 입력 필요 분기

      // ------------------------------------------------------------
      /// <summary>
      /// 기준 location이 없는 Save는 실패가 아니라 NeedLoc으로 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Save_Location없음_NeedLoc반환()
      {
         var controller = CreateController(out _);
         var createResponse = controller.Create(TypeID, "Unsaved");
         var session = createResponse.Session;

         session.SetDirty();

         var response = controller.Save(session);

         Assert.AreEqual(DocumentSaveFlowKind.NeedLoc, response.Kind);
         Assert.IsTrue(response.NeedLoc);
         Assert.IsFalse(response.Failed);
         Assert.IsTrue(string.IsNullOrEmpty(response.Error));
         Assert.AreSame(session, response.Session);
         Assert.IsNull(session.Location);
         Assert.IsTrue(session.IsDirty);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// SaveAs의 null location은 session을 유지한 NeedLoc으로 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_SaveAs_NullLocation_NeedLoc반환()
      {
         var controller = CreateController(out _);
         var loc = CreateLocation("Base");
         var session = CreateSavedSession(controller, loc, "base", 1);

         SetPayload(session, "changed", 2);

         var response = controller.SaveAs(session, null);

         Assert.AreEqual(DocumentSaveFlowKind.NeedLoc, response.Kind);
         Assert.AreSame(session, response.Session);
         Assert.AreSame(loc, session.Location);
         Assert.IsTrue(session.IsDirty);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// SaveTo의 null location은 session을 유지한 NeedLoc으로 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_SaveTo_NullLocation_NeedLoc반환()
      {
         var controller = CreateController(out _);
         var loc = CreateLocation("Base");
         var session = CreateSavedSession(controller, loc, "base", 1);

         SetPayload(session, "changed", 2);

         var response = controller.SaveTo(session, null);

         Assert.AreEqual(DocumentSaveFlowKind.NeedLoc, response.Kind);
         Assert.AreSame(session, response.Session);
         Assert.AreSame(loc, session.Location);
         Assert.IsTrue(session.IsDirty);
      }

   #endregion

   #region S-3: 저장 상태 변화

      // ------------------------------------------------------------
      /// <summary>
      /// Save 성공은 기준 location에 저장하고 dirty를 해제한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Save_성공_Dirty해제()
      {
         var controller = CreateController(out _);
         var loc = CreateLocation("Save");
         var session = CreateSavedSession(controller, loc, "base", 1);

         SetPayload(session, "changed", 2);

         var response = controller.Save(session);

         Assert.AreEqual(DocumentSaveFlowKind.Saved, response.Kind, response.Error);
         Assert.AreSame(session, response.Session);
         Assert.AreSame(loc, session.Location);
         Assert.IsFalse(session.IsDirty);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// SaveAs 성공은 기준 location을 새 location으로 변경하고 dirty를 해제한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_SaveAs_성공_Location변경과Dirty해제()
      {
         var controller = CreateController(out _);
         var baseLoc = CreateLocation("Base");
         var nextLoc = CreateLocation("Next");
         var session = CreateSavedSession(controller, baseLoc, "base", 1);

         SetPayload(session, "next", 2);

         var response = controller.SaveAs(session, nextLoc);

         Assert.AreEqual(DocumentSaveFlowKind.Saved, response.Kind, response.Error);
         Assert.AreSame(session, response.Session);
         Assert.AreSame(nextLoc, session.Location);
         Assert.IsFalse(session.IsDirty);
         Assert.IsFalse(string.IsNullOrEmpty(GetIOLocation(nextLoc).Value));
      }

      // ------------------------------------------------------------
      /// <summary>
      /// SaveTo 성공은 기준 location과 dirty 상태를 유지한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_SaveTo_성공_Location과Dirty유지()
      {
         var controller = CreateController(out _);
         var baseLoc = CreateLocation("Base");
         var copyLoc = CreateLocation("Copy");
         var session = CreateSavedSession(controller, baseLoc, "base", 1);
         var baseText = GetIOLocation(baseLoc).Value;

         SetPayload(session, "copy", 2);

         var response = controller.SaveTo(session, copyLoc);

         Assert.AreEqual(DocumentSaveFlowKind.Saved, response.Kind, response.Error);
         Assert.AreSame(session, response.Session);
         Assert.AreSame(baseLoc, session.Location);
         Assert.IsTrue(session.IsDirty);
         Assert.AreEqual(baseText, GetIOLocation(baseLoc).Value);
         Assert.IsFalse(string.IsNullOrEmpty(GetIOLocation(copyLoc).Value));
      }

   #endregion

   #region C-1: 닫기 대상 session 검증

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Workspace에 없는 dirty session은 사용자 결정 대기가 아니라 실패로 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Close_Workspace밖DirtySession_Failed반환()
      {
         var controller = CreateController(out var workspace);
         var session = new DocumentSession<DocumentBody>(
            new Document(TypeID, Version, "Detached"),
            new DocumentBody(new Payload("detached", 1)),
            CreateLocation("Detached")
         );

         session.SetDirty();

         var response = controller.Close(session);

         Assert.AreEqual(DocumentCloseFlowKind.Failed, response.Kind);
         Assert.IsTrue(response.Failed);
         Assert.IsFalse(response.PendingUser);
         Assert.AreSame(session, response.Session);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Workspace에 없는 session은 변경 폐기 닫기도 실패로 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_CloseDiscardingChanges_Workspace밖Session_Failed반환()
      {
         var controller = CreateController(out var workspace);
         var session = new DocumentSession<DocumentBody>(
            new Document(TypeID, Version, "Detached"),
            new DocumentBody(new Payload("detached", 1)),
            CreateLocation("Detached")
         );

         session.SetDirty();

         var response = controller.CloseDiscardingChanges(session);

         Assert.AreEqual(DocumentCloseFlowKind.Failed, response.Kind);
         Assert.IsTrue(response.Failed);
         Assert.AreSame(session, response.Session);
         Assert.IsFalse(string.IsNullOrEmpty(response.Error));
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

   #endregion

   #region C-2: 닫기 사용자 흐름

      // ------------------------------------------------------------
      /// <summary>
      /// 깨끗한 session close는 workspace에서 session을 제거한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Close_깨끗한Session_닫기성공()
      {
         var controller = CreateController(out var workspace);
         var loc = CreateLocation("CloseClean");
         var session = CreateSavedSession(controller, loc, "base", 1);

         var response = controller.Close(session);

         Assert.AreEqual(DocumentCloseFlowKind.Closed, response.Kind, response.Error);
         Assert.IsTrue(response.Closed);
         Assert.AreSame(session, response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Dirty session close는 즉시 제거하지 않고 사용자 결정 대기 상태를 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_Close_DirtySession_PendingUser반환()
      {
         var controller = CreateController(out var workspace);
         var loc = CreateLocation("CloseDirty");
         var session = CreateSavedSession(controller, loc, "base", 1);

         SetPayload(session, "dirty", 2);

         var response = controller.Close(session);

         Assert.AreEqual(DocumentCloseFlowKind.PendingUser, response.Kind, response.Error);
         Assert.IsTrue(response.PendingUser);
         Assert.AreSame(session, response.Session);
         Assert.AreEqual(1, workspace.Sessions.Count);
         Assert.IsTrue(session.IsDirty);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 사용자가 변경 폐기를 승인한 뒤에는 dirty session도 닫을 수 있다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspaceController_CloseDiscardingChanges_DirtySession_닫기성공()
      {
         var controller = CreateController(out var workspace);
         var loc = CreateLocation("CloseDiscard");
         var session = CreateSavedSession(controller, loc, "base", 1);

         SetPayload(session, "dirty", 2);

         var response = controller.CloseDiscardingChanges(session);

         Assert.AreEqual(DocumentCloseFlowKind.Closed, response.Kind, response.Error);
         Assert.IsTrue(response.Closed);
         Assert.AreSame(session, response.Session);
         Assert.AreEqual(0, workspace.Sessions.Count);
      }

   #endregion

   }
}
