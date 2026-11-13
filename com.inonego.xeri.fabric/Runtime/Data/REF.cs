/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : REF.cs
수정일 : 2026-11-13

# 설명
DataPackage에서 값을 참조하는 구조체.
string 키를 저장하고 ToValue() 호출 시 현재 슬롯의 DataPackage에서 값을 조회한다.
TryFindSource()는 직접 참조를 제외한 Key의 현재 등록 Source를 조회한다.

# 특이사항
IXmlSerializable 직접 구현으로 Key를 읽기 전용으로 유지한다.
directValue는 직렬화 대상에서 제외한다 — XML/Unity 모두 무시.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using UnityEngine;

namespace inonego.Xeri.Data
{
    // ============================================================
    /// <summary>
    /// <br/> DataPackage에서 값을 참조하는 구조체.
    /// <br/> ToValue()로 현재 슬롯의 DataPackage에서 값을 조회한다.
    /// </summary>
    // ============================================================
    [Serializable]
    public struct REF<T> : IKeyable<string>, IXmlSerializable
    where T : class, ITableValue
    {

    #region 참조 상태

        // ------------------------------------------------------------
        /// <summary>
        /// 참조할 데이터의 키.
        /// </summary>
        // ------------------------------------------------------------
        public readonly string Key
        {
            get
            {
                return directValue != null ? directValue.Key : key;
            }
        }

        [SerializeField]
        private string key;

        // ------------------------------------------------------------
        /// <summary>
        /// 키가 설정되어 있는지 여부.
        /// </summary>
        // ------------------------------------------------------------
        [XmlIgnore]
        public readonly bool HasKey
        {
            get
            {
                bool hasKey = !string.IsNullOrEmpty(key);

                return directValue != null ? directValue.HasKey : hasKey;
            }
        }

        // 직렬화 제외 — XML/Unity 모두 무시
        // directValue 필드 — DataPackage 조회 없이 값을 직접 반환한다.
        [XmlIgnore]
        private readonly T directValue;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 키로 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        public REF(string key)
        {
            this.key = key;
            this.directValue = null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 값을 직접 감싼다. DataPackage 조회 없이 값을 그대로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public REF(T directValue)
        {
            this.key = null;
            this.directValue = directValue ?? throw new ArgumentNullException(nameof(directValue));
        }

    #endregion

    #region 참조 해석 및 출처 조회

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 값을 반환한다.
        /// <br/> directValue가 있으면 그대로 반환, 없으면 현재 DataPackage에서 조회한다.
        /// <br/> 키가 없거나 DataPackage가 미등록이면 null을 반환한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public readonly T ToValue()
        {
            if (directValue != null) return directValue;
            if (!HasKey) return null;
            if (!DataPackage.TryCurrent(out var package)) return null;
            return package.TryRead<T>(key);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 현재 DataPackage에서 Key를 소유한 Source를 조회한다.
        /// <br/> 직접 참조 또는 Source 없는 Key는 false를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public readonly bool TryFindSource(out DataPackage.Source source)
        {
            source = default;

            if (directValue != null || !HasKey)
            {
                return false;
            }

            if (!DataPackage.TryCurrent(out var package))
            {
                return false;
            }

            return package is DataPackage dataPackage &&
                dataPackage.TryFindSource<T>(key, out source);
        }

    #endregion

    #region XML 직렬화

        // ------------------------------------------------------------
        /// <summary>
        /// 스키마 없음.
        /// </summary>
        // ------------------------------------------------------------
        public readonly XmlSchema GetSchema() => null;

        // ------------------------------------------------------------
        /// <summary>
        /// Key 속성을 XML에 쓴다.
        /// </summary>
        // ------------------------------------------------------------
        public readonly void WriteXml(XmlWriter writer)
        {
            if (!string.IsNullOrEmpty(key))
            {
                writer.WriteAttributeString("Key", key);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// XML에서 Key 속성을 읽는다.
        /// </summary>
        // ------------------------------------------------------------
        public void ReadXml(XmlReader reader)
        {
            key = reader.GetAttribute("Key");
            reader.Read();
        }

    #endregion

    }
}
