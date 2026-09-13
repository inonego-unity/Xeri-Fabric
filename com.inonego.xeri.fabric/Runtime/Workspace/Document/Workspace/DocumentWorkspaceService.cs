/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentWorkspaceService.cs
수정일 : 2026-07-01

# 설명
DocumentWorkspace와 IDocumentHandler를 조율하여 문서 create/open/save/close 흐름을 수행한다.

# 특이사항
DocumentWorkspace는 session container로 유지하고, 사용자 작업의 의미는 이 service에서 부여한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using inonego.Xeri.Serializable;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// Document workspace의 create, open, save, close 흐름을 조율하는 서비스.
   /// </summary>
   // ============================================================
   public sealed class DocumentWorkspaceService
   {
      
   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 서비스가 조율하는 document workspace.
      /// </summary>
      // ------------------------------------------------------------
      private readonly DocumentWorkspace workspace = null;

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 종류별 handler 매핑.
      /// </summary>
      // ------------------------------------------------------------
      public IReadOnlyDictionary<string, IDocumentHandler> Handlers => handlers;

      private readonly Dictionary<string, IDocumentHandler> handlers = new Dictionary<string, IDocumentHandler>();

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace recovery 흐름 처리기.
      /// </summary>
      // ------------------------------------------------------------
      private readonly DocumentWorkspaceRecovery recovery = null;

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace recovery record 문자열 변환 serializer.
      /// </summary>
      // ------------------------------------------------------------
      private readonly ISerializer recoverySerializer = UnityJsonSerializer.Pretty;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Document workspace service를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentWorkspaceService(DocumentWorkspace workspace) : this(workspace, null)
      {

      }

      // ------------------------------------------------------------
      /// <summary>
      /// Document workspace service를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentWorkspaceService
      (
         DocumentWorkspace workspace,
         IEnumerable<IDocumentHandler> handlers
      ) : base()
      {
         this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

         CopyHandlers(handlers);

         recovery = new DocumentWorkspaceRecovery(this.workspace, this.handlers);
      }

   #endregion

   #region Session 생성과 열기

      // ------------------------------------------------------------
      /// <summary>
      /// 새 문서 세션을 생성하고 성공하면 workspace에 추가한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentCreateResponse Create(string _TypeID, string name)
      {
         if (string.IsNullOrEmpty(_TypeID))
         {
            throw new ArgumentException("문서 종류 식별자가 비어 있습니다.", nameof(_TypeID));
         }

         var handler = FindHandlerByTypeID(_TypeID);
         if (handler == null)
         {
            return DocumentCreateResponse.Fail("문서 종류를 생성할 수 있는 handler를 찾을 수 없습니다.");
         }

         var response = handler.Create(name);
         if (!response.Success)
         {
            return response;
         }

         // Handler가 만든 session만 workspace에 공개한다.
         var addError = AddSessionFromHandler(response.Session, handler);
         return string.IsNullOrEmpty(addError) ? response : DocumentCreateResponse.Fail(addError);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 문서 종류와 location을 열고 성공하면 workspace에 session을 추가한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentOpenResponse Open(string _TypeID, IDocumentLocation location)
      {
         if (string.IsNullOrEmpty(_TypeID))
         {
            throw new ArgumentException("문서 종류 식별자가 비어 있습니다.", nameof(_TypeID));
         }

         if (location == null)
         {
            throw new ArgumentNullException(nameof(location));
         }

         if (workspace.TryFindOpenSession(_TypeID, location, out var existingSession))
         {
            return DocumentOpenResponse.Succeed(existingSession, DocumentOpenKind.AlreadyOpen);
         }

         var handler = FindHandlerByTypeID(_TypeID);
         if (handler == null)
         {
            return DocumentOpenResponse.Fail("문서 종류를 열 수 있는 handler를 찾을 수 없습니다.");
         }

         var response = handler.Open(location);
         if (!response.Success)
         {
            return response;
         }

         var addError = AddOpenSessionFromHandler(response.Session, handler, location);
         return string.IsNullOrEmpty(addError) ? response : DocumentOpenResponse.Fail(addError);
      }

   #endregion

   #region Session 저장과 닫기

      // ------------------------------------------------------------
      /// <summary>
      /// Service가 조율하는 workspace에 지정 session이 포함되어 있는지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool HasSession(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return workspace.HasSession(session);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> Session의 현재 location에 저장하고 성공하면 dirty 상태를 해제한다.
      /// <br/> Location이 없는 새 문서는 실패하며, 사용자-facing 저장 흐름은 상위 계층에서 SaveAs로 전환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      public DocumentSaveResponse Save(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         if (!workspace.HasSession(session))
         {
            return DocumentSaveResponse.Fail("workspace에 포함되지 않은 session입니다.");
         }

         if (session.Location == null)
         {
            return DocumentSaveResponse.Fail("저장할 location이 없습니다.");
         }

         var response = SaveCore(session, session.Location);
         if (!response.Success)
         {
            return response;
         }

         session.ClearDirty();
         return response;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 location에 저장하고 성공하면 session의 기준 location을 변경한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSaveResponse SaveAs(IDocumentSession session, IDocumentLocation location)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         if (location == null)
         {
            throw new ArgumentNullException(nameof(location));
         }

         if (!workspace.HasSession(session))
         {
            return DocumentSaveResponse.Fail("workspace에 포함되지 않은 session입니다.");
         }

         var response = SaveCore(session, location);
         if (!response.Success)
         {
            return response;
         }

         // 저장 성공 후에만 SaveAs의 기준 location 변경을 확정한다.
         session.SetLocation(location);
         session.ClearDirty();
         return response;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 location에 저장하되 session의 기준 location과 dirty 상태는 변경하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSaveResponse SaveTo(IDocumentSession session, IDocumentLocation location)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         if (location == null)
         {
            throw new ArgumentNullException(nameof(location));
         }

         if (!workspace.HasSession(session))
         {
            return DocumentSaveResponse.Fail("workspace에 포함되지 않은 session입니다.");
         }

         return SaveCore(session, location);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace에서 지정한 session을 제거한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool Close(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return workspace.RemoveSession(session);
      }

   #endregion

   #region Recovery Record

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> 현재 workspace의 열린 session 목록을 recovery record 문자열로 만든다.
      /// <br/> 외부 host adapter는 이 문자열을 domain reload boundary 밖에 보관한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      public DocumentWorkspaceRecordResponse RecordRecovery()
      {
         var response = recovery.Record();
         var record = recoverySerializer.Serialize(response.Record);

         return response.Success
            ? DocumentWorkspaceRecordResponse.Succeed(record)
            : DocumentWorkspaceRecordResponse.Fail(record, response.Error);
      }

   #endregion

   #region Recovery Recover

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> Recovery record 문자열에서 document sessions를 복구하고 성공한 session을 workspace에 추가한다.
      /// <br/> 이미 같은 location으로 열린 session이 있으면 새 session을 만들지 않고 기존 session을 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      public DocumentWorkspaceRecoveryResponse Recover(string record)
      {
         if (string.IsNullOrEmpty(record))
         {
            return DocumentWorkspaceRecoveryResponse.Fail
            (
               Array.Empty<DocumentSessionRecoveryResponse>(),
               "workspace recovery record가 비어 있습니다."
            );
         }

         DocumentWorkspaceRecoveryRecord recoveryRecord = null;

         try
         {
            recoveryRecord = recoverySerializer.Deserialize<DocumentWorkspaceRecoveryRecord>(record);
         }
         catch (Exception exception)
         {
            return DocumentWorkspaceRecoveryResponse.Fail
            (
               Array.Empty<DocumentSessionRecoveryResponse>(),
               exception.Message
            );
         }

         if (recoveryRecord == null)
         {
            return DocumentWorkspaceRecoveryResponse.Fail
            (
               Array.Empty<DocumentSessionRecoveryResponse>(),
               "workspace recovery record를 복원할 수 없습니다."
            );
         }

         return Recover(recoveryRecord);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> Recovery record DTO에서 document sessions를 복구하고 성공한 session을 workspace에 추가한다.
      /// <br/> 문자열 API가 외부 계약이고, DTO 기반 복구는 내부 조립 흐름에서만 사용한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      private DocumentWorkspaceRecoveryResponse Recover(DocumentWorkspaceRecoveryRecord record)
      {
         return recovery.Recover(record);
      }

   #endregion

   #region Handler 관리

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 문서 종류를 처리하는 handler를 찾는다.
      /// </summary>
      // ------------------------------------------------------------
      private IDocumentHandler FindHandlerByTypeID(string _TypeID)
      {
         if (string.IsNullOrEmpty(_TypeID))
         {
            return null;
         }

         return handlers.TryGetValue(_TypeID, out var handler) ? handler : null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 생성자 입력 handler 목록을 내부 목록으로 복사한다.
      /// </summary>
      // ------------------------------------------------------------
      private void CopyHandlers(IEnumerable<IDocumentHandler> source)
      {
         if (source == null)
         {
            return;
         }

         foreach (var handler in source)
         {
            if (handler == null)
            {
               throw new ArgumentException("handler 목록에 null이 포함되어 있습니다.", nameof(source));
            }

            if (string.IsNullOrEmpty(handler.TypeID))
            {
               throw new ArgumentException("handler TypeID가 비어 있습니다.", nameof(source));
            }

            // 하나의 document type은 하나의 handler만 책임진다.
            if (handlers.ContainsKey(handler.TypeID))
            {
               throw new ArgumentException("중복된 handler TypeID가 포함되어 있습니다.", nameof(source));
            }

            handlers.Add(handler.TypeID, handler);
         }
      }

   #endregion

   #region Session 검증과 반영

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 반환한 session을 검증하고 workspace에 추가한다.
      /// </summary>
      // ------------------------------------------------------------
      private string AddSessionFromHandler(IDocumentSession session, IDocumentHandler handler)
      {
         var validationError = ValidateSessionForHandler(session, handler);
         if (!string.IsNullOrEmpty(validationError))
         {
            return validationError;
         }

         if (workspace.HasSession(session))
         {
            return "이미 workspace에 추가된 session입니다.";
         }

         workspace.AddSession(session);
         return "";
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 연 session을 검증하고 workspace에 추가한다.
      /// </summary>
      // ------------------------------------------------------------
      private string AddOpenSessionFromHandler
      (
         IDocumentSession session,
         IDocumentHandler handler,
         IDocumentLocation location
      )
      {
         var validationError = ValidateOpenSessionForHandler(session, handler, location);
         if (!string.IsNullOrEmpty(validationError))
         {
            return validationError;
         }

         if (workspace.HasSession(session))
         {
            return "이미 workspace에 추가된 session입니다.";
         }

         workspace.AddSession(session);
         return "";
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 반환한 session의 기본 계약이 handler와 일치하는지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      private string ValidateSessionForHandler(IDocumentSession session, IDocumentHandler handler)
      {
         if (session == null)
         {
            return "handler가 반환한 session이 없습니다.";
         }

         if (session.Document == null)
         {
            return "handler가 반환한 session에 document가 없습니다.";
         }

         if (session.Body == null)
         {
            return "handler가 반환한 session에 body가 없습니다.";
         }

         if (string.IsNullOrEmpty(session.Document.TypeID))
         {
            return "handler가 반환한 session의 document TypeID가 비어 있습니다.";
         }

         if (session.Document.TypeID != handler.TypeID)
         {
            return "handler의 TypeID와 session document TypeID가 일치하지 않습니다.";
         }

         return "";
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> Handler가 연 session의 공통 session 계약과 요청 location 일치 여부를 확인한다.
      /// <br/> Open 성공 session의 location은 이후 AlreadyOpen lookup 기준이므로 요청 location과 같아야 한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      private string ValidateOpenSessionForHandler
      (
         IDocumentSession session,
         IDocumentHandler handler,
         IDocumentLocation location
      )
      {
         var validationError = ValidateSessionForHandler(session, handler);
         if (!string.IsNullOrEmpty(validationError))
         {
            return validationError;
         }

         if (session.Location == null)
         {
            return "handler가 연 session에 location이 없습니다.";
         }

         if (!session.Location.Equals(location))
         {
            return "handler가 연 session location이 요청 location과 일치하지 않습니다.";
         }

         return "";
      }

   #endregion

   #region 저장 내부 흐름

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace 소속 검증이 끝난 저장 흐름의 handler 검증과 호출을 수행한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentSaveResponse SaveCore(IDocumentSession session, IDocumentLocation location)
      {
         if (session.Document == null)
         {
            return DocumentSaveResponse.Fail("session에 document가 없습니다.");
         }

         var handler = FindHandlerByTypeID(session.Document.TypeID);
         if (handler == null)
         {
            return DocumentSaveResponse.Fail("session document type을 처리할 수 있는 handler를 찾을 수 없습니다.");
         }

         return handler.Save(session, location);
      }

   #endregion

   }
}
