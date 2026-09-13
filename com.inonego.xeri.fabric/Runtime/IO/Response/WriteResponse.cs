/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : WriteResponse.cs
수정일 : 2026-06-21

# 설명
IO write operation의 성공 여부와 실패 정보를 담는 응답 값을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.IO
{
   // ============================================================
   /// <summary>
   /// IO write operation 결과.
   /// </summary>
   // ============================================================
   public readonly struct WriteResponse
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// write operation 성공 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Success { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 쓰기에 실패했을 때 사용할 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      public string Error { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 쓰기에 실패했을 때 보관할 원본 예외.
      /// </summary>
      // ------------------------------------------------------------
      public Exception Exception { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// write response를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private WriteResponse
      (
         bool success,
         string error,
         Exception exception
      ) : this()
      {
         Success   = success;
         Error     = error ?? "";
         Exception = exception;
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static WriteResponse Succeed() => new WriteResponse(true, "", null);

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static WriteResponse Fail
      (
         string error,
         Exception exception = null
      )
      {
         return new WriteResponse(false, error, exception);
      }

   #endregion

   }
}
