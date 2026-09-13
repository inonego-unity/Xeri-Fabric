/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentOpenResponse.cs
수정일 : 2026-06-22

# 설명
문서 열기 요청의 성공 여부, session 확보 방식, 생성된 문서 세션 또는 실패 메시지를 담는 응답 값을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 문서 열기 성공 결과의 세부 종류.
   /// </summary>
   // ============================================================
   public enum DocumentOpenKind
   {
      None        = 0,
      NewSession  = 1,
      AlreadyOpen = 2,
   }

   // ============================================================
   /// <summary>
   /// 문서 열기 요청 결과.
   /// </summary>
   // ============================================================
   public readonly struct DocumentOpenResponse
   {
      
   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 열기 성공 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Success { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 열기 성공 시 session이 확보된 방식.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentOpenKind Kind { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 열기에 성공했을 때 생성된 문서 세션.
      /// </summary>
      // ------------------------------------------------------------
      public IDocumentSession Session { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 열기에 실패했을 때 사용할 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      public string Error { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 열기 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentOpenResponse
      (
         bool success,
         DocumentOpenKind kind,
         IDocumentSession session,
         string error
      ) : this()
      {
         Success  = success;
         Kind     = kind;
         Session  = session;
         Error    = error ?? "";
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentOpenResponse Succeed(IDocumentSession session)
      {
         return Succeed(session, DocumentOpenKind.NewSession);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentOpenResponse Succeed
      (
         IDocumentSession session,
         DocumentOpenKind kind
      )
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         if (kind == DocumentOpenKind.None)
         {
            throw new ArgumentException("문서 열기 성공 종류가 지정되지 않았습니다.", nameof(kind));
         }

         return new DocumentOpenResponse(true, kind, session, "");
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentOpenResponse Fail(string error)
      {
         return new DocumentOpenResponse(false, DocumentOpenKind.None, null, error);
      }

   #endregion

   }
}
