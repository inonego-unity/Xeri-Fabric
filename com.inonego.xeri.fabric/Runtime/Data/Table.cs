/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : Table.cs
수정일 : 2026-11-13

# 설명
데이터 테이블 구현.
- Table_V<T>: 값 형태([SerializeField]) 직렬화
- Table_R<T>: 참조 형태([SerializeReference]) 직렬화 (다형성 지원)
- Merge는 concrete Table 구현이 아니라 ITable<T> 계약을 기준으로 호환한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.Data
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.Serializable;

    // ============================================================
    /// <summary>
    /// 테이블 구현 (값 형태 직렬화).
    /// </summary>
    // ============================================================
    [Serializable]
    public class Table_V<TTableValue> : Table<TTableValue>
    where TTableValue : class, ITableValue
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 값 직렬화 테이블의 Key별 항목을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public override Dictionary<string, TTableValue> Dictionary => dictionary;

        [SerializeField]
        private XDictionary_VV<string, TTableValue> dictionary = new();
    }

    // ============================================================
    /// <summary>
    /// 테이블 구현 (참조 형태 직렬화, 다형성 지원).
    /// </summary>
    // ============================================================
    [Serializable]
    public class Table_R<TTableValue> : Table<TTableValue>
    where TTableValue : class, ITableValue
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 참조 직렬화 테이블의 Key별 항목을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public override Dictionary<string, TTableValue> Dictionary => dictionary;

        [SerializeField]
        private XDictionary_VR<string, TTableValue> dictionary = new();
    }

    // ============================================================
    /// <summary>
    /// 데이터 테이블 추상 기반 클래스.
    /// </summary>
    // ============================================================
    [Serializable]
    public abstract class Table<TTableValue> : ITable<TTableValue>
    where TTableValue : class, ITableValue
    {

    #region Table 저장 구조

        // ------------------------------------------------------------
        /// <summary>
        /// 테이블의 키별 저장 구조를 제공합니다.
        /// </summary>
        // ------------------------------------------------------------
        public abstract Dictionary<string, TTableValue> Dictionary { get; }

    #endregion

    #region Table 조회 및 열거

        IReadOnlyDictionary<string, TTableValue> IReadOnlyTable<TTableValue>.Dictionary => Dictionary;
        Dictionary<string, TTableValue>          ITable<TTableValue>.Dictionary         => Dictionary;

        int                              IReadOnlyTable.Count  => Dictionary.Count;
        IEnumerable<string>              IReadOnlyTable.Keys   => Dictionary.Keys;
        IEnumerable<ITableValue>         IReadOnlyTable.Values => Dictionary.Values;
        IEnumerable<TTableValue> IReadOnlyTable<TTableValue>.Values => Dictionary.Values;

        ITableValue IReadOnlyTable.this[string key]
        {
            get
            {
                Dictionary.TryGetValue(key, out var value);
                return value;
            }
        }

        TTableValue IReadOnlyTable<TTableValue>.this[string key]
        {
            get
            {
                Dictionary.TryGetValue(key, out var value);
                return value;
            }
        }

        bool IReadOnlyTable.Has(string key) => Dictionary.ContainsKey(key);

        Type IReadOnlyTable.ValueType => typeof(TTableValue);

        IEnumerator<KeyValuePair<string, ITableValue>> IReadOnlyTable.GetEnumerator()
        {
            foreach (var (key, value) in Dictionary)
            {
                yield return new KeyValuePair<string, ITableValue>(key, value);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테이블 데이터를 순회할 열거자를 반환합니다.
        /// </summary>
        // ------------------------------------------------------------
        public IEnumerator GetEnumerator() => Dictionary.GetEnumerator();

    #endregion

    #region Table 재로드 및 변경

        // ------------------------------------------------------------
        /// <summary>
        /// 데이터를 다시 로드한다. 기본 구현은 비어있다.
        /// </summary>
        // ------------------------------------------------------------
        public virtual void Reload()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 데이터를 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Add(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value is not TTableValue tableValue)
            {
                throw new InvalidOperationException($"추가하려는 데이터가 {typeof(TTableValue).Name} 타입이 아닙니다.");
            }

            Add(tableValue);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 데이터를 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Add(TTableValue value)
        {
            Dictionary.Add(value.Key, value);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// ITable 인터페이스 구현: 다른 테이블과 병합한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Merge(ITable other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (other is not ITable<TTableValue> otherTable)
            {
                throw new InvalidOperationException($"병합하려는 테이블이 {typeof(TTableValue).Name} 타입이 아닙니다.");
            }

            Merge(otherTable);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 다른 테이블과 병합한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Merge(ITable<TTableValue> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            foreach (var (key, item) in other.Dictionary)
            {
                if (item.HasKey)
                {
                    Dictionary.Add(key, item);
                }
            }
        }

    #endregion

    }
}