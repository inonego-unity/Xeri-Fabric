/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : MemoryIO.cs
수정일 : 2026-06-21

# 설명
MemoryLocation에 보관된 값을 읽고 쓰는 범용 IO 구현체를 정의한다.
테스트, 임시 데이터, 런타임 메모리 기반 저장 흐름에서 파일 시스템 없이 사용할 수 있다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.IO
{
   // ============================================================
   /// <summary>
   /// MemoryLocation에서 값을 읽고 쓰는 범용 IO 구현체.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class MemoryIO<TValue> :
      IDataReader<MemoryLocation<TValue>, TValue>,
      IDataWriter<MemoryLocation<TValue>, TValue>
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 MemoryIO 인스턴스.
      /// </summary>
      // ------------------------------------------------------------
      public static MemoryIO<TValue> Default { get; } = new();

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// MemoryIO를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public MemoryIO() : base() {}

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 메모리 location에서 값을 읽는다.
      /// </summary>
      // ------------------------------------------------------------
      public ReadResponse<TValue> Read(MemoryLocation<TValue> location)
      {
         try
         {
            ValidateLocation(location);

            return ReadResponse<TValue>.Succeed(location.Value);
         }
         catch (Exception exception)
         {
            return ReadResponse<TValue>.Fail(exception.Message, exception);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 메모리 location에 값을 쓴다.
      /// </summary>
      // ------------------------------------------------------------
      public WriteResponse Write(MemoryLocation<TValue> location, TValue value)
      {
         try
         {
            ValidateLocation(location);

            location.Value = value;

            return WriteResponse.Succeed();
         }
         catch (Exception exception)
         {
            return WriteResponse.Fail(exception.Message, exception);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 메모리 location 입력값을 검증한다.
      /// </summary>
      // ------------------------------------------------------------
      private static void ValidateLocation(MemoryLocation<TValue> location)
      {
         if (location == null)
         {
            throw new ArgumentNullException(nameof(location));
         }
      }

   #endregion

   }
}
