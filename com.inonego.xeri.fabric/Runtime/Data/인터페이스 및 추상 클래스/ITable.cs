/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ITable.cs
수정일 : 2026-05-01

# 설명
읽기/쓰기 데이터 테이블 인터페이스.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.Data
{
    // ============================================================
    /// <summary>
    /// 읽기/쓰기 테이블 인터페이스.
    /// </summary>
    // ============================================================
    public interface ITable : IReadOnlyTable
    {
        public void Reload();

        public void Add(object value);
        public void Merge(ITable other);
    }

    // ============================================================
    /// <summary>
    /// 읽기/쓰기 테이블 인터페이스 (제네릭).
    /// </summary>
    // ============================================================
    public interface ITable<TTableValue> : ITable, IReadOnlyTable<TTableValue>
    where TTableValue : class, ITableValue
    {
        public new Dictionary<string, TTableValue> Dictionary { get; }

        public void Add(TTableValue value);
        public void Merge(ITable<TTableValue> other);
    }
}
