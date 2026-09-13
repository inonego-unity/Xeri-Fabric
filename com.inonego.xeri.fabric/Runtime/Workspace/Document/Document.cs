/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : Document.cs
수정일 : 2026-06-20

# 설명
IDocument의 기본 immutable 구현체를 정의한다.
문서 종류, 버전, 표시 이름만 보관하고 실제 편집 데이터는 포함하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 문서의 공통 설명 정보를 보관하는 기본 구현체.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class Document : IDocument
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 종류 식별자.
      /// </summary>
      // ------------------------------------------------------------
      public string TypeID => _TypeID;

      [SerializeField]
      private string _TypeID = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 스키마 또는 형식 버전.
      /// </summary>
      // ------------------------------------------------------------
      public string Version => version;

      [SerializeField]
      private string version = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// 사용자에게 표시할 문서 이름.
      /// </summary>
      // ------------------------------------------------------------
      public string Name => name;

      [SerializeField]
      private string name = string.Empty;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 설명 정보를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public Document(string _TypeID, string version, string name) : base()
      {
         if (string.IsNullOrEmpty(_TypeID))
         {
            throw new ArgumentException("문서 종류 식별자가 비어 있습니다.", nameof(_TypeID));
         }

         if (string.IsNullOrEmpty(version))
         {
            throw new ArgumentException("문서 버전이 비어 있습니다.", nameof(version));
         }

         if (string.IsNullOrEmpty(name))
         {
            throw new ArgumentException("문서 이름이 비어 있습니다.", nameof(name));
         }

         this._TypeID = _TypeID;
         this.version = version;
         this.name = name;
      }

   #endregion

   }
}
