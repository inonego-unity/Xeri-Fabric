/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TableAsset.cs
수정일 : 2026-08-30

# 설명
ITableValue row를 Unity ScriptableObject asset으로 저장하는 공통 Table 기반형을 제공한다.
TableAsset_V<T>는 값 직렬화, TableAsset_R<T>는 참조 직렬화 저장소를 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.Data
{
    using inonego.Xeri.Serializable;

    // ================================================================================
    /// <summary>
    /// ScriptableObject 기반 읽기/쓰기 Table asset의 공통 동작을 구현한다.
    /// </summary>
    // ================================================================================
    public abstract class TableAsset<TTableValue> : ScriptableObject, ITable<TTableValue>
    where TTableValue : class, ITableValue
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 row Dictionary를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract Dictionary<string, TTableValue> Dictionary { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 row 수를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public int Count => Dictionary.Count;

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 row Key 목록을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IEnumerable<string> Keys => Dictionary.Keys;

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 row 값 목록을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IEnumerable<TTableValue> Values => Dictionary.Values;

        // ------------------------------------------------------------
        /// <summary>
        /// 이 Table의 row 타입을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public Type ValueType => typeof(TTableValue);

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Key의 row를 반환하고 없으면 null을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public TTableValue this[string key]
        {
            get
            {
                Dictionary.TryGetValue(key, out var value);
                return value;
            }
        }

    #endregion

    #region 읽기 전용 조회

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Key가 현재 Table에 존재하는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Has(string key) => Dictionary.ContainsKey(key);

        // ------------------------------------------------------------
        /// <summary>
        /// 읽기 전용 제네릭 계약에 현재 Dictionary를 노출한다.
        /// </summary>
        // ------------------------------------------------------------
        IReadOnlyDictionary<string, TTableValue> IReadOnlyTable<TTableValue>.Dictionary => Dictionary;

        // ------------------------------------------------------------
        /// <summary>
        /// 읽기/쓰기 제네릭 계약에 현재 Dictionary를 노출한다.
        /// </summary>
        // ------------------------------------------------------------
        Dictionary<string, TTableValue> ITable<TTableValue>.Dictionary => Dictionary;

        // ------------------------------------------------------------
        /// <summary>
        /// 비제네릭 읽기 전용 계약에 현재 row 값 목록을 노출한다.
        /// </summary>
        // ------------------------------------------------------------
        IEnumerable<ITableValue> IReadOnlyTable.Values => Dictionary.Values;

        // ------------------------------------------------------------
        /// <summary>
        /// 비제네릭 읽기 전용 계약에서 지정 Key의 row를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        ITableValue IReadOnlyTable.this[string key] => this[key];

        // ----------------------------------------------------------------------
        /// <summary>
        /// 비제네릭 읽기 전용 계약의 Key와 row 열거자를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        IEnumerator<KeyValuePair<string, ITableValue>> IReadOnlyTable.GetEnumerator()
        {
            foreach (var (key, value) in Dictionary)
            {
                yield return new KeyValuePair<string, ITableValue>(key, value);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 Dictionary 열거자를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IEnumerator GetEnumerator() => Dictionary.GetEnumerator();

    #endregion

    #region 쓰기 처리

        // ------------------------------------------------------------
        /// <summary>
        /// 직렬화된 저장 상태를 다시 로드한다.
        /// </summary>
        // ------------------------------------------------------------
        public virtual void Reload()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// object 값을 타입 안전한 row로 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Add(object value)
        {
            // 입력 검증: null과 다른 row 타입이 Dictionary 변경 단계까지 들어오지 못하게 한다.
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value is not TTableValue tableValue)
            {
                throw new InvalidOperationException
                (
                    $"추가하려는 데이터가 {typeof(TTableValue).Name} 타입이 아닙니다."
                );
            }

            Add(tableValue);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 row를 자신의 Key로 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Add(TTableValue value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Dictionary.Add(value.Key, value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 row 타입의 Table을 현재 Table에 병합한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Merge(ITable other)
        {
            // 계약 검증: concrete 구현이 아니라 ITable<T> 호환 여부만 요구한다.
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (other is not ITable<TTableValue> typedTable)
            {
                throw new InvalidOperationException
                (
                    $"병합하려는 테이블이 {typeof(TTableValue).Name} 타입이 아닙니다."
                );
            }

            Merge(typedTable);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 같은 row 타입의 typed Table을 현재 Table에 병합한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Merge(ITable<TTableValue> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            foreach (var (key, value) in other.Dictionary)
            {
                if (!value.HasKey) continue;

                Dictionary.Add(key, value);
            }
        }

    #endregion

    }

    // ================================================================================
    /// <summary>
    /// SerializeField 값 직렬화를 사용하는 ScriptableObject Table asset 기반형.
    /// </summary>
    // ================================================================================
    public abstract class TableAsset_V<TTableValue> : TableAsset<TTableValue>
    where TTableValue : class, ITableValue
    {

    #region 필드

        [SerializeField]
        private XDictionary_VV<string, TTableValue> dictionary = new();

        // ------------------------------------------------------------
        /// <summary>
        /// 값 직렬화 Dictionary를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public override Dictionary<string, TTableValue> Dictionary => dictionary;

    #endregion

    }

    // ================================================================================
    /// <summary>
    /// SerializeReference 참조 직렬화를 사용하는 ScriptableObject Table asset 기반형.
    /// </summary>
    // ================================================================================
    public abstract class TableAsset_R<TTableValue> : TableAsset<TTableValue>
    where TTableValue : class, ITableValue
    {

    #region 필드

        [SerializeField]
        private XDictionary_VR<string, TTableValue> dictionary = new();

        // ------------------------------------------------------------
        /// <summary>
        /// 참조 직렬화 Dictionary를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public override Dictionary<string, TTableValue> Dictionary => dictionary;

    #endregion

    }
}
