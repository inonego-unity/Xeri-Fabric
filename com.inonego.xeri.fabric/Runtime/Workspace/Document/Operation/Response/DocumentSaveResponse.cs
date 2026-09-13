/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentSaveResponse.cs
수정일 : 2026-06-19

# 설명
문서 저장 요청의 성공 여부와 실패 메시지를 담는 응답 값을 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 문서 저장 요청 결과.
   /// </summary>
   // ============================================================
   public readonly struct DocumentSaveResponse
   {
      
   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 저장 성공 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Success { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 저장에 실패했을 때 사용할 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      public string Error { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 저장 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentSaveResponse(bool success, string error) : this()
      {
         Success = success;
         Error   = error ?? "";
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentSaveResponse Succeed() => new DocumentSaveResponse(true, "");

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static DocumentSaveResponse Fail(string error) => new DocumentSaveResponse(false, error);

   #endregion

   }
}
