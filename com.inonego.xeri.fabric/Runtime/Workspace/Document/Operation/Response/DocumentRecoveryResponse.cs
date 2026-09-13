/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentRecoveryResponse.cs
수정일 : 2026-07-31

# 설명
Workspace document recovery record 생성과 복구 요청의 성공 여부를 담는 응답 값을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// Workspace recovery record 생성 결과.
   /// </summary>
   // ============================================================
   public readonly struct DocumentWorkspaceRecordResponse
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 모든 session record 생성 성공 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Success { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// Host lifecycle boundary를 넘겨 보관할 recovery record 문자열.
      /// </summary>
      // ------------------------------------------------------------
      public string Record { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 시 사용할 대표 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      public string Error { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace recovery record 생성 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentWorkspaceRecordResponse
      (
         bool success,
         string record,
         string error
      ) : this()
      {
         Success = success;
         Record  = record ?? "";
         Error   = error ?? "";
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentWorkspaceRecordResponse Succeed
      (
         string record
      )
      {
         return new DocumentWorkspaceRecordResponse(true, record, "");
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentWorkspaceRecordResponse Fail
      (
         string record,
         string error
      )
      {
         return new DocumentWorkspaceRecordResponse(false, record, error);
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// Workspace recovery record DTO 생성 결과.
   /// </summary>
   // ============================================================
   internal readonly struct DocumentWorkspaceRecoveryRecordResponse
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 모든 session record 생성 성공 여부.
      /// </summary>
      // ------------------------------------------------------------
      internal bool Success { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 성공한 session record를 담은 workspace recovery record.
      /// </summary>
      // ------------------------------------------------------------
      internal DocumentWorkspaceRecoveryRecord Record { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// Session별 record 생성 결과.
      /// </summary>
      // ------------------------------------------------------------
      internal IReadOnlyList<DocumentSessionRecordResponse> Sessions { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 시 사용할 대표 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      internal string Error { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace recovery record DTO 생성 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentWorkspaceRecoveryRecordResponse
      (
         bool success,
         DocumentWorkspaceRecoveryRecord record,
         IReadOnlyList<DocumentSessionRecordResponse> sessions,
         string error
      ) : this()
      {
         Success  = success;
         Record   = record ?? new DocumentWorkspaceRecoveryRecord();
         Sessions = sessions ?? Array.Empty<DocumentSessionRecordResponse>();
         Error    = error ?? "";
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      internal static DocumentWorkspaceRecoveryRecordResponse Succeed
      (
         DocumentWorkspaceRecoveryRecord record,
         IReadOnlyList<DocumentSessionRecordResponse> sessions
      )
      {
         return new DocumentWorkspaceRecoveryRecordResponse(true, record, sessions, "");
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      internal static DocumentWorkspaceRecoveryRecordResponse Fail
      (
         DocumentWorkspaceRecoveryRecord record,
         IReadOnlyList<DocumentSessionRecordResponse> sessions,
         string error
      )
      {
         return new DocumentWorkspaceRecoveryRecordResponse(false, record, sessions, error);
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// 하나의 session recovery record 생성 결과.
   /// </summary>
   // ============================================================
   internal readonly struct DocumentSessionRecordResponse
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// Session record 생성 성공 여부.
      /// </summary>
      // ------------------------------------------------------------
      internal bool Success { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// Record 생성 대상 session.
      /// </summary>
      // ------------------------------------------------------------
      internal IDocumentSession Session { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 생성된 session recovery record.
      /// </summary>
      // ------------------------------------------------------------
      internal DocumentSessionRecoveryRecord Record { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 시 사용할 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      internal string Error { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Session recovery record 생성 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentSessionRecordResponse
      (
         bool success,
         IDocumentSession session,
         DocumentSessionRecoveryRecord record,
         string error
      ) : this()
      {
         Success = success;
         Session = session;
         Record  = record;
         Error   = error ?? "";
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      internal static DocumentSessionRecordResponse Succeed
      (
         IDocumentSession session,
         DocumentSessionRecoveryRecord record
      )
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         if (record == null)
         {
            throw new ArgumentNullException(nameof(record));
         }

         return new DocumentSessionRecordResponse(true, session, record, "");
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      internal static DocumentSessionRecordResponse Fail
      (
         IDocumentSession session,
         string error
      )
      {
         return new DocumentSessionRecordResponse(false, session, null, error);
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// Session recovery 결과 종류.
   /// </summary>
   // ============================================================
   public enum DocumentSessionRecoveryKind
   {
      None        = 0,
      Recovered   = 1,
      AlreadyOpen = 2,
      Failed      = 3,
   }

   // ============================================================
   /// <summary>
   /// Workspace recovery 요청 결과.
   /// </summary>
   // ============================================================
   public readonly struct DocumentWorkspaceRecoveryResponse
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 모든 session recovery 성공 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Success { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// Session별 recovery 결과.
      /// </summary>
      // ------------------------------------------------------------
      public IReadOnlyList<DocumentSessionRecoveryResponse> Sessions { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 시 사용할 대표 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      public string Error { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace recovery 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentWorkspaceRecoveryResponse
      (
         bool success,
         IReadOnlyList<DocumentSessionRecoveryResponse> sessions,
         string error
      ) : this()
      {
         Success  = success;
         Sessions = sessions ?? Array.Empty<DocumentSessionRecoveryResponse>();
         Error    = error ?? "";
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentWorkspaceRecoveryResponse Succeed
      (
         IReadOnlyList<DocumentSessionRecoveryResponse> sessions
      )
      {
         return new DocumentWorkspaceRecoveryResponse(true, sessions, "");
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentWorkspaceRecoveryResponse Fail
      (
         IReadOnlyList<DocumentSessionRecoveryResponse> sessions,
         string error
      )
      {
         return new DocumentWorkspaceRecoveryResponse(false, sessions, error);
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// 하나의 session recovery 요청 결과.
   /// </summary>
   // ============================================================
   public readonly struct DocumentSessionRecoveryResponse
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// Session recovery 성공 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Success { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// Session recovery 결과 종류.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSessionRecoveryKind Kind { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 복구되었거나 이미 열려 있던 session.
      /// </summary>
      // ------------------------------------------------------------
      public IDocumentSession Session { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 시 사용할 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      public string Error { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Session recovery 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentSessionRecoveryResponse
      (
         bool success,
         DocumentSessionRecoveryKind kind,
         IDocumentSession session,
         string error
      ) : this()
      {
         Success = success;
         Kind    = kind;
         Session = session;
         Error   = error ?? "";
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentSessionRecoveryResponse Succeed
      (
         IDocumentSession session,
         DocumentSessionRecoveryKind kind
      )
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

            if
            (
                kind != DocumentSessionRecoveryKind.Recovered &&
                kind != DocumentSessionRecoveryKind.AlreadyOpen
            )
         {
            throw new ArgumentException("Session recovery 성공 종류가 올바르지 않습니다.", nameof(kind));
         }

         return new DocumentSessionRecoveryResponse(true, kind, session, "");
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentSessionRecoveryResponse Fail(string error)
      {
         return new DocumentSessionRecoveryResponse(false, DocumentSessionRecoveryKind.Failed, null, error);
      }

   #endregion

   }
}
