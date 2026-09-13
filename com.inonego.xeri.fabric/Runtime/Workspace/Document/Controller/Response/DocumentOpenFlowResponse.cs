/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentOpenFlowResponse.cs
수정일 : 2026-06-22

# 설명
사용자-facing 문서 열기 흐름 결과와 작업 대상 session, 실패 메시지를 담는 응답 값을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 사용자-facing 문서 열기 흐름 결과.
   /// </summary>
   // ============================================================
   public enum DocumentOpenFlowKind
   {
      Failed      = 0,
      NewSession  = 1,
      AlreadyOpen = 2,
   }

   // ============================================================
   /// <summary>
   /// 사용자-facing 문서 열기 흐름 응답.
   /// </summary>
   // ============================================================
   public readonly struct DocumentOpenFlowResponse
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 사용자-facing 문서 열기 흐름 응답 종류.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentOpenFlowKind Kind { get; }

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
      /// 문서 열기 흐름이 성공했는지 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Succeeded => Kind == DocumentOpenFlowKind.NewSession ||
                               Kind == DocumentOpenFlowKind.AlreadyOpen;

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 열기 흐름이 실패했는지 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Failed => Kind == DocumentOpenFlowKind.Failed;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 열기 흐름 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentOpenFlowResponse
      (
         DocumentOpenFlowKind kind,
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
      /// 새 session 열기 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentOpenFlowResponse NewSession(IDocumentSession session)
      {
         return Succeed(DocumentOpenFlowKind.NewSession, session);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 이미 열린 session 반환 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentOpenFlowResponse AlreadyOpen(IDocumentSession session)
      {
         return Succeed(DocumentOpenFlowKind.AlreadyOpen, session);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentOpenFlowResponse Fail(string error)
      {
         return new DocumentOpenFlowResponse(DocumentOpenFlowKind.Failed, null, error);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private static DocumentOpenFlowResponse Succeed
      (
         DocumentOpenFlowKind kind,
         IDocumentSession session
      )
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return new DocumentOpenFlowResponse(kind, session, "");
      }

   #endregion

   }
}
