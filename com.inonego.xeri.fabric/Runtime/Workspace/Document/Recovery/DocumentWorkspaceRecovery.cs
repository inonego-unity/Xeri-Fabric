/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentWorkspaceRecovery.cs
수정일 : 2026-07-31

# 설명
DocumentWorkspace의 열린 session 목록을 recovery record로 만들고, workspace 단위 recovery record를 복구한다.
Session 하나의 record/recover 조립은 DocumentSessionRecovery에 위임한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections.Generic;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// Workspace 단위 recovery record 생성과 복구를 담당한다.
   /// </summary>
   // ============================================================
   internal sealed class DocumentWorkspaceRecovery
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// Recovery 대상 workspace.
      /// </summary>
      // ------------------------------------------------------------
      private readonly DocumentWorkspace workspace = null;

      // ------------------------------------------------------------
      /// <summary>
      /// Session 단위 recovery 처리기.
      /// </summary>
      // ------------------------------------------------------------
      private readonly DocumentSessionRecovery sessionRecovery = null;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Document workspace recovery를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentWorkspaceRecovery
      (
         DocumentWorkspace workspace,
         IReadOnlyDictionary<string, IDocumentHandler> handlers
      ) : base()
      {
         this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
         sessionRecovery = new DocumentSessionRecovery(handlers);
      }

   #endregion

   #region Record

      // ------------------------------------------------------------
      /// <summary>
      /// 현재 workspace의 열린 session 목록을 recovery record로 만든다.
      /// </summary>
      // ------------------------------------------------------------
      internal DocumentWorkspaceRecoveryRecordResponse Record()
      {
         var records = new List<DocumentSessionRecoveryRecord>();
         var responses = new List<DocumentSessionRecordResponse>();
         var success = true;
         var error = "";

         foreach (var session in workspace.Sessions)
         {
            var response = sessionRecovery.Record(session);
            responses.Add(response);

            if (response.Success)
            {
               records.Add(response.Record);
               continue;
            }

            success = false;
            if (string.IsNullOrEmpty(error))
            {
               error = response.Error;
            }
         }

         var record = new DocumentWorkspaceRecoveryRecord(records);

         return success
            ? DocumentWorkspaceRecoveryRecordResponse.Succeed(record, responses)
            : DocumentWorkspaceRecoveryRecordResponse.Fail(record, responses, error);
      }

   #endregion

   #region Recover

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> Recovery record에서 document sessions를 복구하고 성공한 session을 workspace에 추가한다.
      /// <br/> 이미 같은 location으로 열린 session이 있으면 새 session을 만들지 않고 기존 session을 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      internal DocumentWorkspaceRecoveryResponse Recover(DocumentWorkspaceRecoveryRecord record)
      {
         if (record == null)
         {
            throw new ArgumentNullException(nameof(record));
         }

         var responses = new List<DocumentSessionRecoveryResponse>();
         var success = true;
         var error = "";

         foreach (var sessionRecord in record.Sessions)
         {
            var response = RecoverSession(sessionRecord);

            responses.Add(response);

            if (response.Success) continue;

            success = false;
            if (string.IsNullOrEmpty(error))
            {
               error = response.Error;
            }
         }

         return success
            ? DocumentWorkspaceRecoveryResponse.Succeed(responses)
            : DocumentWorkspaceRecoveryResponse.Fail(responses, error);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 하나의 session recovery record를 복구하고 workspace에 반영한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentSessionRecoveryResponse RecoverSession(DocumentSessionRecoveryRecord record)
      {
         var response = sessionRecovery.Recover(record);
         if (!response.Success)
         {
            return response;
         }

         var session = response.Session;

         // Location을 가진 recovery는 Open과 같은 중복 기준을 따른다.
         // 새 candidate를 workspace에 공개하기 전에 확인해야 기존 session을 덮거나 중복 추가하지 않는다.
            if
            (
               session.Location != null &&
               workspace.TryFindOpenSession(session.Document.TypeID, session.Location, out var existingSession)
            )
         {
            return DocumentSessionRecoveryResponse.Succeed(existingSession, DocumentSessionRecoveryKind.AlreadyOpen);
         }

         if (workspace.HasSession(session))
         {
            return DocumentSessionRecoveryResponse.Fail("이미 workspace에 추가된 session입니다.");
         }

         workspace.AddSession(session);
         return response;
      }

   #endregion

   }
}
