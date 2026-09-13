/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentSessionRecovery.cs
수정일 : 2026-07-01

# 설명
하나의 DocumentSession을 recovery record로 만들고, 하나의 session recovery record에서 session 후보를 복구한다.
Workspace 목록 추가, 중복 session 판별 같은 container 처리는 담당하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections.Generic;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 하나의 document session에 대한 recovery record 생성과 복구를 담당한다.
   /// </summary>
   // ============================================================
   internal sealed class DocumentSessionRecovery
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 종류별 handler 매핑.
      /// </summary>
      // ------------------------------------------------------------
      private readonly IReadOnlyDictionary<string, IDocumentHandler> handlers = null;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Document session recovery를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSessionRecovery
      (
         IReadOnlyDictionary<string, IDocumentHandler> handlers
      ) : base()
      {
         this.handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
      }

   #endregion

   #region Record

      // ------------------------------------------------------------
      /// <summary>
      /// 하나의 session recovery record를 만든다.
      /// </summary>
      // ------------------------------------------------------------
      internal DocumentSessionRecordResponse Record(IDocumentSession session)
      {
         if (session == null)
         {
            return DocumentSessionRecordResponse.Fail(null, "session이 없습니다.");
         }

         if (session.Document == null)
         {
            return DocumentSessionRecordResponse.Fail(session, "session document가 없습니다.");
         }

         var handler = FindHandlerByTypeID(session.Document.TypeID);
         if (handler == null)
         {
            return DocumentSessionRecordResponse.Fail(session, "document type을 record할 handler를 찾을 수 없습니다.");
         }

         var bodyRecord = RecordSessionBody(session, handler);
         if (bodyRecord == null)
         {
            return DocumentSessionRecordResponse.Fail(session, "session body record를 만들 수 없습니다.");
         }

         var locationRecord = session.Location?.Record();
         if (session.Location != null && locationRecord == null)
         {
            return DocumentSessionRecordResponse.Fail(session, "location recovery record를 만들 수 없습니다.");
         }

         var record = new DocumentSessionRecoveryRecord
         (
            session.Document.TypeID,
            session.Document.Version,
            session.Document.Name,
            locationRecord,
            bodyRecord,
            session.IsDirty
         );

         return DocumentSessionRecordResponse.Succeed(session, record);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Handler를 통해 session body recovery record를 만든다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentBodyRecoveryRecord RecordSessionBody
      (
         IDocumentSession session,
         IDocumentHandler handler
      )
      {
         if (session?.Document == null)
         {
            return null;
         }

         if (handler == null)
         {
            return null;
         }

         return handler.RecordSessionBody(session);
      }

   #endregion

   #region Recover

      // ------------------------------------------------------------
      /// <summary>
      /// 하나의 session recovery record에서 session 후보를 복구한다.
      /// </summary>
      // ------------------------------------------------------------
      internal DocumentSessionRecoveryResponse Recover(DocumentSessionRecoveryRecord record)
      {
         if (record == null)
         {
            return DocumentSessionRecoveryResponse.Fail("session recovery record가 없습니다.");
         }

         if (record.Body == null)
         {
            return DocumentSessionRecoveryResponse.Fail("session을 복구할 body record가 없습니다.");
         }

         if (string.IsNullOrEmpty(record.TypeID))
         {
            return DocumentSessionRecoveryResponse.Fail("session recovery record의 document TypeID가 비어 있습니다.");
         }

         var handler = FindHandlerByTypeID(record.TypeID);
         if (handler == null)
         {
            return DocumentSessionRecoveryResponse.Fail("document type을 복구할 handler를 찾을 수 없습니다.");
         }

         var document = new Document(record.TypeID, record.Version, record.Name);
         var loc = record.Location?.Recover();
         if (record.Location != null && loc == null)
         {
            return DocumentSessionRecoveryResponse.Fail("session recovery record의 location을 복구할 수 없습니다.");
         }

         var openResponse = handler.RecoverSession(document, record.Body, loc);
         if (!openResponse.Success)
         {
            return DocumentSessionRecoveryResponse.Fail(openResponse.Error);
         }

         var session = openResponse.Session;
         var validationError = ValidateSessionForHandler(session, handler);
         if (!string.IsNullOrEmpty(validationError))
         {
            return DocumentSessionRecoveryResponse.Fail(validationError);
         }

         if (record.IsDirty)
         {
            session.SetDirty();
         }
         else
         {
            session.ClearDirty();
         }

         return DocumentSessionRecoveryResponse.Succeed(session, DocumentSessionRecoveryKind.Recovered);
      }
   #endregion

   #region Handler 관리와 검증

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

   #endregion

   }
}
