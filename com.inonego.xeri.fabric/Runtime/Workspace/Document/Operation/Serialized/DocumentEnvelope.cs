/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentEnvelope.cs
수정일 : 2026-06-28

# 설명
Serialized document 저장 단위인 metadata/body envelope를 정의한다.
Handler는 이 객체를 만들고, 실제 JSON/XML 문자열 구성과 formatting 정책은 주입된 serializer가 담당한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Xml.Serialization;

using UnityEngine;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// Serialized document 전체 저장 단위.
   /// </summary>
   /// <typeparam name="TBody">문서 body 타입.</typeparam>
   // ============================================================
   [Serializable]
   [XmlRoot("Document")]
   public sealed class DocumentEnvelope<TBody>
   where TBody : class
   {
   #region 내부 데이터

      // ============================================================
      /// <summary>
      /// Serialized document metadata 영역.
      /// </summary>
      // ============================================================
      [Serializable]
      public sealed class _Metadata
      {
         // ------------------------------------------------------------
         /// <summary>
         /// 문서 종류 식별자.
         /// </summary>
         // ------------------------------------------------------------
         [XmlAttribute]
         public string TypeID = string.Empty;

         // ------------------------------------------------------------
         /// <summary>
         /// 문서 버전.
         /// </summary>
         // ------------------------------------------------------------
         [XmlAttribute]
         public string Version = string.Empty;

         // ------------------------------------------------------------
         /// <summary>
         /// 문서 표시 이름.
         /// </summary>
         // ------------------------------------------------------------
         [XmlAttribute]
         public string Name = string.Empty;

         // ------------------------------------------------------------
         /// <summary>
         /// 기본 metadata를 생성한다.
         /// </summary>
         // ------------------------------------------------------------
         public _Metadata() : base() {}

         // ------------------------------------------------------------
         /// <summary>
         /// Document 계약에서 envelope metadata를 생성한다.
         /// </summary>
         // ------------------------------------------------------------
         public _Metadata(IDocument document) : this()
         {
            TypeID   = document?.TypeID ?? "";
            Version  = document?.Version ?? "";
            Name     = document?.Name ?? "";
         }

         // ------------------------------------------------------------
         /// <summary>
         /// Metadata를 IDocument 기본 구현으로 변환한다.
         /// </summary>
         // ------------------------------------------------------------
         public IDocument ToDocument()
         {
            return new Document(TypeID, Version, Name);
         }
      }

   #endregion

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 metadata.
      /// </summary>
      // ------------------------------------------------------------
      [XmlElement]
      public _Metadata Metadata = new _Metadata();

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 body.
      /// </summary>
      // ------------------------------------------------------------
      [XmlElement]
      public TBody Body;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 document envelope를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentEnvelope() : base() {}

      // ------------------------------------------------------------
      /// <summary>
      /// Document metadata와 body를 묶은 envelope를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentEnvelope(IDocument document, TBody body) : this()
      {
         Metadata = new _Metadata(document);
         Body = body;
      }

   #endregion
   }
}
