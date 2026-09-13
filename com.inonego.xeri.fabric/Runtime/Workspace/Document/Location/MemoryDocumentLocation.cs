/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : MemoryDocumentLocation.cs
수정일 : 2026-07-01

# 설명
Workspace 안에서만 식별되는 임시 문서 location 구현체를 정의한다.
외부 파일이나 Unity Object에 연결되지 않은 새 문서, 테스트 문서, 런타임 임시 문서에 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// Workspace 내부 메모리 슬롯을 나타내는 문서 location.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class MemoryDocumentLocation : IDocumentLocation
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 사용자에게 표시하거나 진단에 사용할 location 이름.
      /// </summary>
      // ------------------------------------------------------------
      public string Name => name;

      [SerializeField]
      private string name = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace 안에서 memory location을 구분하는 키.
      /// </summary>
      // ------------------------------------------------------------
      public string Key => key;

      [SerializeField]
      private string key = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// Memory location에 연결된 임시 값.
      /// </summary>
      // ------------------------------------------------------------
      public object Value => value;

      [SerializeReference]
      private object value = null;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 새 key를 가진 memory location을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public MemoryDocumentLocation(string name) : this(name, Guid.NewGuid().ToString("N"), null) {}

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 key를 가진 memory location을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public MemoryDocumentLocation(string name, string key) : this(name, key, null) {}

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 key와 값을 가진 memory location을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public MemoryDocumentLocation(string name, string key, object value) : base()
      {
         if (string.IsNullOrEmpty(name))
         {
            throw new ArgumentException("Memory location 이름이 비어 있습니다.", nameof(name));
         }

         if (string.IsNullOrEmpty(key))
         {
            throw new ArgumentException("Memory location 키가 비어 있습니다.", nameof(key));
         }

         this.name = name;
         this.key = key;
         this.value = value;
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// Memory location은 in-memory value를 복구할 수 없으므로 기본 recovery record를 제공하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      public IDocumentLocationRecord Record()
      {
         return null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Memory location에 연결된 임시 값을 설정한다.
      /// </summary>
      // ------------------------------------------------------------
      public void SetValue(object value)
      {
         this.value = value;
      }

   #endregion

   #region Equality / Hash

      // ------------------------------------------------------------
      /// <summary>
      /// 같은 memory key를 가진 document location인지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool Equals(IDocumentLocation other)
      {
         return other is MemoryDocumentLocation loc &&
                string.Equals(key, loc.key, StringComparison.Ordinal);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 같은 memory key를 가진 document location인지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      public override bool Equals(object obj)
      {
         return obj is IDocumentLocation loc && Equals(loc);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Memory key 기준 hash code를 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      public override int GetHashCode()
      {
         return StringComparer.Ordinal.GetHashCode(key);
      }

   #endregion

   }
}
