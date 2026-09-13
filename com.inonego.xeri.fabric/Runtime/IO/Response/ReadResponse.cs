/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ReadResponse.cs
수정일 : 2026-07-30

# 설명
IO read operation의 성공 여부, 읽은 값, 실패 정보, optional Lease를 담는 응답 값을 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.IO
{
   // =======================================================================
   /// <summary>
   /// IO read operation 결과.
   /// </summary>
   /// <typeparam name="TValue">읽기 성공 시 제공되는 값 타입.</typeparam>
   // =======================================================================
   public readonly struct ReadResponse<TValue>
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// read operation 성공 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool Success { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 읽기에 성공했을 때 제공되는 값.
      /// </summary>
      // ------------------------------------------------------------
      public TValue Value { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 읽기에 실패했을 때 사용할 실패 메시지.
      /// </summary>
      // ------------------------------------------------------------
      public string Error { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 읽기에 실패했을 때 보관할 원본 예외.
      /// </summary>
      // ------------------------------------------------------------
      public Exception Exception { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 읽은 값의 수명을 유지하기 위해 소유자가 함께 보관할 Lease.
      /// </summary>
      // ------------------------------------------------------------
      public Lease Lease { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// read response를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private ReadResponse
      (
         bool success,
         TValue value,
         string error,
         Exception exception,
         Lease lease
      ) : this()
      {
         Success   = success;
         Value     = value;
         Error     = error ?? "";
         Exception = exception;
         Lease     = lease;
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 성공 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static ReadResponse<TValue> Succeed
      (
         TValue value,
         Lease lease = null
      )
      {
         return new ReadResponse<TValue>(true, value, "", null, lease);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public static ReadResponse<TValue> Fail
      (
         string error,
         Exception exception = null,
         Lease lease = null
      )
      {
         return new ReadResponse<TValue>(false, default, error, exception, lease);
      }

   #endregion

   }
}
