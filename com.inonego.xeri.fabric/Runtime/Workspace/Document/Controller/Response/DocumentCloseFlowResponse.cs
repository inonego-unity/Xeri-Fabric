/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentCloseFlowResponse.cs
수정일 : 2026-06-23

# 설명
사용자-facing 문서 닫기 흐름 결과와 작업 대상 session, 실패 메시지를 담는 응답 값을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 사용자-facing 문서 닫기 흐름 결과.
   /// </summary>
   // ============================================================
   public enum DocumentCloseFlowKind
   {
      Failed      = 0,
      Closed      = 1,
      PendingUser = 2,
   }

   // ============================================================
   /// <summary>
   /// 사용자-facing 문서 닫기 흐름 응답.
   /// </summary>
   // ============================================================
   public readonly struct DocumentCloseFlowResponse
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 사용자-facing 문서 닫기 흐름 응답 종류.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentCloseFlowKind Kind { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 흐름 결과와 연결된 작업 대상 document session.
      /// </summary>
      // ------------------------------------------------------------
      public IDocumentSession Session { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패했을 때 사용할 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      public string Error { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 닫기 흐름이 닫기 완료 상태인지 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Closed => Kind == DocumentCloseFlowKind.Closed;

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 닫기 흐름이 실패했는지 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Failed => Kind == DocumentCloseFlowKind.Failed;

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 닫기 흐름을 계속하려면 사용자 결정이 필요한지 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool PendingUser => Kind == DocumentCloseFlowKind.PendingUser;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 닫기 흐름 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentCloseFlowResponse
      (
         DocumentCloseFlowKind kind,
         IDocumentSession session,
         string error
      ) : this()
      {
         Kind    = kind;
         Session = session;
         Error   = error ?? "";
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 닫기 완료 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentCloseFlowResponse Succeed(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return new DocumentCloseFlowResponse(DocumentCloseFlowKind.Closed, session, "");
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentCloseFlowResponse Fail(string error)
      {
         return new DocumentCloseFlowResponse(DocumentCloseFlowKind.Failed, null, error);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 대상 session을 유지하는 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentCloseFlowResponse Fail(IDocumentSession session, string error)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return new DocumentCloseFlowResponse(DocumentCloseFlowKind.Failed, session, error);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 사용자 결정이 필요한 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentCloseFlowResponse RequireUser(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return new DocumentCloseFlowResponse(DocumentCloseFlowKind.PendingUser, session, "");
      }

   #endregion

   }
}
