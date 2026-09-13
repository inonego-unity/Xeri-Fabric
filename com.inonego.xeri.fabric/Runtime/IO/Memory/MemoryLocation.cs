/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : MemoryLocation.cs
수정일 : 2026-06-21

# 설명
메모리 안의 값을 IO location처럼 다루기 위한 범용 location 컨테이너를 정의한다.
Workspace, File, Unity Resource 같은 상위 도메인에 의존하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.IO
{
   // ============================================================
   /// <summary>
   /// 메모리 값을 IO location처럼 다루기 위한 범용 컨테이너.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class MemoryLocation<TValue>
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 현재 메모리 location이 보관하는 값.
      /// </summary>
      // ------------------------------------------------------------
      public TValue Value
      {
         get => value;
         set => this.value = value;
      }

      private TValue value = default;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 기본값을 보관하는 MemoryLocation을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public MemoryLocation() : base() {}

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 초기값을 보관하는 MemoryLocation을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public MemoryLocation(TValue value) : this()
      {
         this.value = value;
      }

   #endregion

   }
}
