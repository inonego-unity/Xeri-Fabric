/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ObjectDocumentLocation.cs
수정일 : 2026-07-01

# 설명
이미 존재하는 객체 인스턴스를 문서 location으로 다루는 구현체를 정의한다.
ScriptableObject, MonoBehaviour, 일반 C# 객체, 런타임 데이터 객체를 문서처럼 열 때 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 이미 존재하는 객체 참조를 나타내는 문서 location.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class ObjectDocumentLocation : IDocumentLocation
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
      /// 문서 location으로 사용할 객체 참조.
      /// </summary>
      // ------------------------------------------------------------
      public object Value => value;

      [SerializeReference]
      private object value = null;

      // ------------------------------------------------------------
      /// <summary>
      /// 객체 참조의 런타임 타입.
      /// </summary>
      // ------------------------------------------------------------
      public Type ObjectType => value?.GetType();

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 객체 이름 또는 타입 이름을 표시 이름으로 사용해 location을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public ObjectDocumentLocation(object value) : this(GetDefaultName(value), value) {}

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 표시 이름과 객체 참조로 location을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public ObjectDocumentLocation(string name, object value) : base()
      {
         if (string.IsNullOrEmpty(name))
         {
            throw new ArgumentException("Object location 이름이 비어 있습니다.", nameof(name));
         }

         this.value = value ?? throw new ArgumentNullException(nameof(value));
         this.name = name;
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// Object reference location은 object reference를 복구할 수 없으므로 기본 recovery record를 제공하지 않는다.
      /// </summary>
      // ------------------------------------------------------------
      public IDocumentLocationRecord Record()
      {
         return null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 객체에서 기본 표시 이름을 계산한다.
      /// </summary>
      // ------------------------------------------------------------
      private static string GetDefaultName(object value)
      {
         if (value == null)
         {
            throw new ArgumentNullException(nameof(value));
         }

         if (value is UnityEngine.Object unityObject && string.IsNullOrEmpty(unityObject.name) == false)
         {
            return unityObject.name;
         }

         return value.GetType().Name;
      }

   #endregion

   #region Equality / Hash

      // ------------------------------------------------------------
      /// <summary>
      /// 같은 객체 참조를 가진 document location인지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool Equals(IDocumentLocation other)
      {
         return other is ObjectDocumentLocation loc &&
                ReferenceEquals(value, loc.value);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 같은 객체 참조를 가진 document location인지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      public override bool Equals(object obj)
      {
         return obj is IDocumentLocation loc && Equals(loc);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 객체 참조 기준 hash code를 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      public override int GetHashCode()
      {
         return value.GetHashCode();
      }

   #endregion

   }
}
