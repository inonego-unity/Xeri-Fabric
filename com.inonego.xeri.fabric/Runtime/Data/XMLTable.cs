/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : XMLTable.cs
수정일 : 2026-08-28

# 설명
XML 직렬화 목록과 Table Dictionary를 동기화하는 데이터 테이블 기반 구현을 정의한다.
XML 테이블 식별 이름을 선언하는 XMLTableNameAttribute를 함께 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.Data
{
    // ============================================================
    /// <summary>
    /// XML 테이블 식별 이름을 지정하는 Attribute.
    /// </summary>
    // ============================================================
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class XMLTableNameAttribute : Attribute
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// XML 테이블 식별 이름.
        /// </summary>
        // ------------------------------------------------------------
        public string Name { get; }

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// XML 테이블 식별 이름 Attribute를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public XMLTableNameAttribute(string name) : base()
        {
            Name = name;
        }

    #endregion

    }

    // ========================================================================
    /// <summary>
    /// XML 직렬화 목록과 Dictionary를 동기화하는 데이터 테이블 기반 클래스.
    /// </summary>
    /// <typeparam name="TTableValue">테이블 값 타입.</typeparam>
    // ========================================================================
    [Serializable]
    public abstract class XMLTable<TTableValue> : Table<TTableValue>, ISerializationCallbackReceiver
    where TTableValue : class, ITableValue
    {

    #region 직렬화 데이터

        // ------------------------------------------------------------
        /// <summary>
        /// XML 직렬화에 사용하는 테이블 값 목록.
        /// </summary>
        // ------------------------------------------------------------
        protected abstract List<TTableValue> Serialized { get; }

    #endregion

    #region Unity 직렬화 콜백

        // ------------------------------------------------------------
        /// <summary>
        /// Unity 직렬화 전에 Dictionary 값을 XML 직렬화 목록에 반영한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnBeforeSerialize()
        {
            Serialized.Clear();

            foreach (var (_, item) in Dictionary)
            {
                if (item.HasKey)
                {
                    Serialized.Add(item);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Unity 역직렬화 후 Dictionary를 다시 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        public void OnAfterDeserialize()
        {
            Reload();
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// XML 직렬화 목록에서 Dictionary를 다시 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        public override void Reload()
        {
            Dictionary.Clear();

            if (Serialized == null)
            {
                return;
            }

            foreach (var item in Serialized)
            {
                if (item.HasKey)
                {
                    Dictionary.Add(item.Key, item);
                }
            }
        }

    #endregion

    }
}
