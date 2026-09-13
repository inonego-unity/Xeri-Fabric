/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentSaveFlowResponse.cs
수정일 : 2026-06-23

# 설명
사용자-facing 문서 저장 흐름 결과와 작업 대상 session, 실패 메시지를 담는 응답 값을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 사용자-facing 문서 저장 흐름 결과.
   /// </summary>
   // ============================================================
   public enum DocumentSaveFlowKind
   {
      Failed  = 0,
      Saved   = 1,
      NeedLoc = 2,
   }

   // ============================================================
   /// <summary>
   /// 사용자-facing 문서 저장 흐름 응답.
   /// </summary>
   // ============================================================
   public readonly struct DocumentSaveFlowResponse
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 사용자-facing 문서 저장 흐름 응답 종류.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSaveFlowKind Kind { get; }

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
      /// 문서 저장 흐름이 저장 완료 상태인지 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Saved => Kind == DocumentSaveFlowKind.Saved;

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 저장 흐름이 실패했는지 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Failed => Kind == DocumentSaveFlowKind.Failed;

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 저장 흐름을 계속하려면 location 입력이 필요한지 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool NeedLoc => Kind == DocumentSaveFlowKind.NeedLoc;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 저장 흐름 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentSaveFlowResponse
      (
         DocumentSaveFlowKind kind,
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
      /// 저장 완료 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentSaveFlowResponse Succeed(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return new DocumentSaveFlowResponse(DocumentSaveFlowKind.Saved, session, "");
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentSaveFlowResponse Fail(string error)
      {
         return new DocumentSaveFlowResponse(DocumentSaveFlowKind.Failed, null, error);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 대상 session을 유지하는 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentSaveFlowResponse Fail(IDocumentSession session, string error)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return new DocumentSaveFlowResponse(DocumentSaveFlowKind.Failed, session, error);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Location 입력이 필요한 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentSaveFlowResponse RequireLoc(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return new DocumentSaveFlowResponse(DocumentSaveFlowKind.NeedLoc, session, "");
      }

   #endregion

   }
}
