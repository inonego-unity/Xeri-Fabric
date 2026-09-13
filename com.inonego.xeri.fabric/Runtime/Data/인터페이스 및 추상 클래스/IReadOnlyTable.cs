/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IReadOnlyTable.cs
수정일 : 2026-05-01

# 설명
읽기 전용 데이터 테이블 인터페이스.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.Data
{
    // ============================================================
    /// <summary>
    /// 읽기 전용 테이블 인터페이스.
    /// </summary>
    // ============================================================
    public interface IReadOnlyTable
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 항목 수.
        /// </summary>
        // ------------------------------------------------------------
        public int Count { get; }

        public IEnumerable<string>      Keys   { get; }
        public IEnumerable<ITableValue> Values { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 키로 값을 조회한다. 없으면 null 반환.
        /// </summary>
        // ------------------------------------------------------------
        public ITableValue this[string key] { get; }

        public bool Has(string key);

        public Type ValueType { get; }

        public IEnumerator<KeyValuePair<string, ITableValue>> GetEnumerator();
    }

    // ============================================================
    /// <summary>
    /// <br/> 읽기 전용 테이블 인터페이스 (제네릭).
    /// <br/> 타입 안전한 접근을 제공한다.
    /// </summary>
    // ============================================================
    public interface IReadOnlyTable<TTableValue> : IReadOnlyTable
    where TTableValue : class, ITableValue
    {
        public IReadOnlyDictionary<string, TTableValue> Dictionary { get; }

        public new IEnumerable<TTableValue> Values { get; }

        public new TTableValue this[string key] { get; }
    }
}
