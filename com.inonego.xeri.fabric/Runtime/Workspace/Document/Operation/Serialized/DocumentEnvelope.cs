/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentEnvelope.cs
수정일 : 2026-11-14

# 설명
Serialized document 저장 단위인 metadata/body envelope를 정의한다.
Handler는 현행 Body 존재 표시를 포함한 Envelope를 만들며, JSON/XML 문자열 구성은 주입된 serializer가 담당한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Xml;
using System.Xml.Serialization;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Persistence;

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
    public sealed class DocumentEnvelope<TBody> : IRecord<TBody>, ISerializationCallbackReceiver
    where TBody : class
    {

    #region 내부 데이터

        // ============================================================
        /// <summary>
        /// Serialized document metadata 영역.
        /// </summary>
        // ============================================================
        [Serializable]
        public sealed class _Metadata : IRecordMetadata
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

            // 직렬화된 필드 이름을 유지하면서 공통 Metadata 계약에 연결한다.
            string IRecordMetadata.TypeID => TypeID;
            string IRecordMetadata.Version => Version;

            // ------------------------------------------------------------
            /// <summary>
            /// 기본 metadata를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public _Metadata() : base()
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Document 계약에서 envelope metadata를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public _Metadata(IDocument document) : this()
            {
                TypeID = document?.TypeID ?? "";
                Version = document?.Version ?? "";
                Name = document?.Name ?? "";
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Unity JSON의 null Body 객체 치환을 감지하기 위한 본문 존재 플래그.
        /// <br/> 현행 XML/JSON Envelope에 필수 필드로 기록한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [XmlElement]
        public bool HasBody = false;

        // 공통 Record 인터페이스는 별도의 직렬화 필드 없이 Metadata/Body를 참조한다.
        IRecordMetadata IRecord<TBody>.Metadata => Metadata;
        TBody IRecord<TBody>.Body => Body;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 document envelope를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public DocumentEnvelope() : base()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Document metadata와 body를 묶은 envelope를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public DocumentEnvelope(IDocument document, TBody body) : this()
        {
            Metadata = new _Metadata(document);
            Body = body;
            HasBody = body != null;
        }

    #endregion

    #region 직렬화

        // ----------------------------------------------------------------------
        /// <summary>
        /// null Body를 Unity JSON이 기본 객체로 치환해도 원본 존재 여부를 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void OnBeforeSerialize()
        {
            HasBody = Body != null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 복원 후 RecordType과 문서 정책에 따라 Envelope를 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnAfterDeserialize()
        {
            // NONE
        }

    #endregion

    }
}
