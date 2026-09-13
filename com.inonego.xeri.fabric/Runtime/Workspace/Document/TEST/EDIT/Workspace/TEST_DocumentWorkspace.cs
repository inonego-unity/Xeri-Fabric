/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_DocumentWorkspace.cs
수정일 : 2026-06-28

# 설명
DocumentWorkspace의 session container 계약을 검증한다.
Unity Test Runner (Edit Mode) 에서 실행한다.

# 테스트 구성
 L: Location - location 동일성 계약
 S: Session - session 목록 계약
 E: Event - session 상태 변경 이벤트
 O: Open - 열린 session lookup 계약
 V: Event - workspace session add/remove 이벤트
========================================================================= BLOCK_HEADER_END */

using System.Collections.Generic;
using System.IO;

using NUnit.Framework;

using inonego.Xeri;
using inonego.Xeri.Workspace.Document;

namespace inonego.Xeri.TEST.Workspace._Document
{
   // ============================================================
   /// <summary>
   /// DocumentWorkspace session container 테스트.
   /// </summary>
   // ============================================================
   public class TEST_DocumentWorkspace : TEST_DocumentWorkspaceBase
   {

   #region L-1: Location 동일성 계약

      // ------------------------------------------------------------
      /// <summary>
      /// MemoryDocumentLocation은 표시 이름이 달라도 같은 key면 같은 location으로 판단한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_MemoryLocation_같은Key_동일Location()
      {
         var a = new MemoryDocumentLocation("A", "same-key");
         var b = new MemoryDocumentLocation("B", "same-key");
         var c = new MemoryDocumentLocation("C", "other-key");

         Assert.IsTrue(a.Equals(b));
         Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
         Assert.IsFalse(a.Equals(c));
      }

      // ------------------------------------------------------------
      /// <summary>
      /// FileDocumentLocation은 정규화된 파일 경로가 같으면 같은 location으로 판단한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_FileLocation_정규화Path_동일Location()
      {
         var basePath = Path.Combine(Path.GetTempPath(), "Xeri", "Sample.json");
         var samePath = Path.Combine(Path.GetTempPath(), "Xeri", ".", "Sample.json");
         var otherPath = Path.Combine(Path.GetTempPath(), "Xeri", "Other.json");
         var a = new FileDocumentLocation(basePath, "A");
         var b = new FileDocumentLocation(samePath, "B");
         var c = new FileDocumentLocation(otherPath, "C");

         Assert.IsTrue(a.Equals(b));
         Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
         Assert.IsFalse(a.Equals(c));
      }

      // ------------------------------------------------------------
      /// <summary>
      /// ObjectDocumentLocation은 같은 객체 참조면 같은 location으로 판단한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_ObjectLocation_같은참조_동일Location()
      {
         var value = new object();
         var a = new ObjectDocumentLocation("A", value);
         var b = new ObjectDocumentLocation("B", value);
         var c = new ObjectDocumentLocation("C", new object());

         Assert.IsTrue(a.Equals(b));
         Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
         Assert.IsFalse(a.Equals(c));
      }

   #endregion

   #region S-1: Workspace session 목록 계약

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace Sessions는 목록 변경을 막고 작업 session handle을 그대로 제공한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_Sessions_목록계약_유지()
      {
         var service = CreateService(out var workspace);
         var response = service.Create(TypeID, "Session");

         Assert.IsTrue(response.Success, response.Error);

         IReadOnlyList<IDocumentSession> sessions = workspace.Sessions;
         IDocumentSession session = sessions[0];

         Assert.AreEqual(1, sessions.Count);
         Assert.AreSame(response.Session, session);
         Assert.AreSame(response.Session.Document, session.Document);
         Assert.AreSame(response.Session.Body, session.Body);
         Assert.IsTrue(workspace.HasSession(session));
      }

   #endregion

   #region E-1: Session 상태 변경 이벤트

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Dirty 상태 변경 이벤트는 dirty 값이 실제로 바뀔 때만 이전/현재 값을 전달한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_Session_OnDirtyChange_상태변경시에만발화()
      {
         var service = CreateService(out _);
         var createResponse = service.Create(TypeID, "DirtyEvent");
         var session = createResponse.Session;
         var argsList = new List<ValueChangeEventArgs<bool>>();

         Assert.IsTrue(createResponse.Success, createResponse.Error);

         session.OnDirtyChange += (sender, args) => argsList.Add(args);

         session.SetDirty();
         session.SetDirty();
         session.ClearDirty();
         session.ClearDirty();

         Assert.AreEqual(2, argsList.Count);
         Assert.IsFalse(argsList[0].Previous);
         Assert.IsTrue(argsList[0].Current);
         Assert.IsTrue(argsList[1].Previous);
         Assert.IsFalse(argsList[1].Current);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Location 변경 이벤트는 같은 location 재설정 시 발생하지 않고 다른 location에서만 발생한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_Session_OnLocationChange_다른Location시에만발화()
      {
         var service = CreateService(out _);
         var baseLoc = CreateLocation("Base");
         var nextLoc = CreateLocation("Next");
         var session = CreateSavedSession(service, baseLoc, "base", 1);
         var argsList = new List<ValueChangeEventArgs<IDocumentLocation>>();

         session.OnLocationChange += (sender, args) => argsList.Add(args);

         session.SetLocation(new MemoryDocumentLocation("Alias", baseLoc.Key));
         session.SetLocation(nextLoc);

         Assert.AreEqual(1, argsList.Count);
         Assert.AreSame(baseLoc, argsList[0].Previous);
         Assert.AreSame(nextLoc, argsList[0].Current);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Document 변경 이벤트는 같은 document 재설정 시 발생하지 않고 다른 document에서만 발생한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_Session_OnDocumentChange_다른Document시에만발화()
      {
         var service = CreateService(out _);
         var createResponse = service.Create(TypeID, "DocumentEvent");
         var session = createResponse.Session;
         var previous = session.Document;
         var next = new Document(TypeID, Version, "NextDocument");
         var argsList = new List<ValueChangeEventArgs<IDocument>>();

         Assert.IsTrue(createResponse.Success, createResponse.Error);

         session.OnDocumentChange += (sender, args) => argsList.Add(args);

         session.SetDocument(previous);
         session.SetDocument(next);

         Assert.AreEqual(1, argsList.Count);
         Assert.AreSame(previous, argsList[0].Previous);
         Assert.AreSame(next, argsList[0].Current);
      }

   #endregion

   #region O-1: Open session lookup 계약

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Workspace는 같은 document type과 location을 가진 열린 session을 찾을 수 있다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_TryFindOpenSession_같은TypeID와Location_Session반환()
      {
         var service = CreateService(out var workspace);
         var loc = CreateLocation("Lookup");
         var session = CreateSavedSession(service, loc, "lookup", 1);

         var found = workspace.TryFindOpenSession(TypeID, new MemoryDocumentLocation("Alias", loc.Key), out var result);

         Assert.IsTrue(found);
         Assert.AreSame(session, result);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Workspace는 같은 location이라도 document type이 다르면 기존 session으로 판단하지 않는다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_TryFindOpenSession_다른TypeID_검색제외()
      {
         var service = CreateService(out var workspace);
         var loc = CreateLocation("Lookup");

         CreateSavedSession(service, loc, "lookup", 1);

         var found = workspace.TryFindOpenSession(ForeignTypeID, loc, out var result);

         Assert.IsFalse(found);
         Assert.IsNull(result);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// Workspace는 location이 없는 새 문서를 open session lookup 대상에서 제외한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_TryFindOpenSession_Location없는Session_검색제외()
      {
         var service = CreateService(out var workspace);
         var createResponse = service.Create(TypeID, "Unsaved");

         Assert.IsTrue(createResponse.Success, createResponse.Error);

         var found = workspace.TryFindOpenSession(TypeID, CreateLocation("Unsaved"), out var result);

         Assert.IsFalse(found);
         Assert.IsNull(result);
      }

   #endregion

   #region V-1: Workspace session add/remove 이벤트

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace session add/remove 이벤트는 작업 session 인자를 전달한다.
      /// </summary>
      // ------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_AddRemove_EventSession_전달()
      {
         var service = CreateService(out var workspace);
         object addSender = null;
         object removeSender = null;
         IDocumentSession addedSession = null;
         IDocumentSession removedSession = null;
         var addCount = 0;
         var removeCount = 0;

         workspace.OnSessionAdd += (sender, args) =>
         {
            addSender = sender;
            addedSession = args.Session;
            addCount++;
         };

         workspace.OnSessionRemove += (sender, args) =>
         {
            removeSender = sender;
            removedSession = args.Session;
            removeCount++;
         };

         var response = service.Create(TypeID, "Event");

         Assert.IsTrue(response.Success, response.Error);
         Assert.AreEqual(1, addCount);
         Assert.AreSame(workspace, addSender);
         Assert.AreSame(response.Session, addedSession);

         var session = workspace.Sessions[0];
         Assert.IsTrue(service.Close(session));
         Assert.AreEqual(1, removeCount);
         Assert.AreSame(workspace, removeSender);
         Assert.AreSame(response.Session, removedSession);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// 중복 추가와 없는 session 제거는 실패하고 add/remove 이벤트를 발생시키지 않는다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      [Test]
      public void TEST_DocumentWorkspace_AddRemove_상태변경없음_Event미발생()
      {
         var service = CreateService(out var workspace);
         var response = service.Create(TypeID, "Event");
         var session = response.Session;
         var detachedSession = new DocumentSession<DocumentBody>(
            new Document(TypeID, Version, "Detached"),
            new DocumentBody(new Payload("detached", 1)),
            CreateLocation("Detached")
         );
         var addCount = 0;
         var removeCount = 0;

         Assert.IsTrue(response.Success, response.Error);

         workspace.OnSessionAdd += (sender, args) => addCount++;
         workspace.OnSessionRemove += (sender, args) => removeCount++;

         var added = workspace.AddSession(session);
         var removed = workspace.RemoveSession(detachedSession);

         Assert.IsFalse(added);
         Assert.IsFalse(removed);
         Assert.AreEqual(0, addCount);
         Assert.AreEqual(0, removeCount);
         Assert.AreEqual(1, workspace.Sessions.Count);
         Assert.AreSame(session, workspace.Sessions[0]);
      }

   #endregion

   }
}
