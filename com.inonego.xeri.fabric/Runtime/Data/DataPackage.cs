/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DataPackage.cs
수정일 : 2026-08-28

# 설명
데이터 패키지 중앙 관리자.
InstanceRegistry<DataPackage>를 통한 슬롯·Scope 시스템으로 여러 DataPackage를 이름별로 관리한다.
Source별 원본 Table을 보존하고 ValueType별 Lookup으로 여러 Source를 하나의 logical Table처럼 조회한다.

# 슬롯 시스템
- Register(pkg)           : DEFAULT_SLOT에 등록
- Register(slot, pkg)     : 지정 슬롯에 등록
- Unregister(slot)        : 슬롯 해제
- Current                 : 현재 컨텍스트 슬롯의 DataPackage (IReadOnlyDataPackage)
- Named[slot]             : 슬롯 이름으로 직접 접근
- Scope(slot)             : using 블록 안에서 일시적으로 슬롯 전환, Dispose 시 복원
- OpenScope(slot)         : 슬롯 전환 (테스트 SetUp/TearDown 용. 프로덕션은 Scope() 사용)
- CloseScope()            : 가장 최근에 열린 스코프를 닫고 이전 슬롯으로 복원

# Source 구성
- AddSource               : Source와 Source-owned Table을 등록하고 Lookup에 반영
- RemoveSource            : Source 제거 후 영향받은 runtime Lookup 재구성
- ReplaceSource           : 기존 Source를 검증 후 교체
- HasSource               : Source 등록 여부 조회
- Source 등록 이후 Table/row는 runtime read-only로 취급

# 이벤트
- OnChange: Register / Unregister / Clear 시 발생. Scope 전환 시에는 발생하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.Data
{
    using inonego.Xeri.Serializable;

    // ============================================================
    /// <summary>
    /// 데이터 패키지 읽기 전용 인터페이스.
    /// </summary>
    // ============================================================
    public interface IReadOnlyDataPackage
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 현재 패키지의 logical Table 목록을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IEnumerable<IReadOnlyTable> Tables { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 키로 데이터를 읽는다. 없으면 예외.
        /// </summary>
        // ------------------------------------------------------------
        public TTableValue Read<TTableValue>(string key)
        where TTableValue : class, ITableValue;

        // ------------------------------------------------------------
        /// <summary>
        /// 키로 데이터를 읽는다. 테이블이 없거나 키가 없으면 null 반환.
        /// </summary>
        // ------------------------------------------------------------
        public TTableValue TryRead<TTableValue>(string key)
        where TTableValue : class, ITableValue;

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 타입의 테이블을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyTable<TTableValue> Table<TTableValue>()
        where TTableValue : class, ITableValue;

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 타입의 테이블을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyTable Table(Type valueType);
    }

    // ================================================================================
    /// <summary>
    /// <br/> 데이터 패키지. 슬롯 기반으로 여러 인스턴스를 관리한다.
    /// <br/> Source별 원본 Table을 보존하고 ValueType별 Lookup으로 논리 조회를 제공한다.
    /// </summary>
    // ================================================================================
    [Serializable]
    public class DataPackage : IReadOnlyDataPackage
    {

    #region 내부 타입

        // ============================================================
        /// <summary>
        /// DataPackage를 구성하는 외부 데이터 공급 단위의 identity.
        /// </summary>
        // ============================================================
        [Serializable]
        public readonly struct Source : IEquatable<Source>
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Source를 제공하는 IO 계층 식별자.
            /// </summary>
            // ------------------------------------------------------------
            public string Provider { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Provider 내부의 원본 위치 식별자.
            /// </summary>
            // ------------------------------------------------------------
            public string Location { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// Provider와 Location이 모두 지정됐는지 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool HasValue
            {
                get
                {
                    return
                        !string.IsNullOrWhiteSpace(Provider) &&
                        !string.IsNullOrWhiteSpace(Location);
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Provider와 Location으로 Source identity를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public Source(string provider, string location) : this()
            {
                // Source identity는 두 구성 요소가 모두 있어야 이후 Dictionary key로 안정적으로 사용할 수 있다.
                if (string.IsNullOrWhiteSpace(provider))
                {
                    throw new ArgumentException("Source Provider가 비어 있습니다.", nameof(provider));
                }

                if (string.IsNullOrWhiteSpace(location))
                {
                    throw new ArgumentException("Source Location이 비어 있습니다.", nameof(location));
                }

                // 검증이 끝난 identity만 외부에 공개한다.
                Provider = provider;
                Location = location;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 다른 Source와 identity가 같은지 비교한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool Equals(Source other)
            {
                return
                    string.Equals(Provider, other.Provider, StringComparison.Ordinal) &&
                    string.Equals(Location, other.Location, StringComparison.Ordinal);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 다른 객체와 Source identity가 같은지 비교한다.
            /// </summary>
            // ------------------------------------------------------------
            public override bool Equals(object obj)
            {
                return obj is Source other && Equals(other);
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Provider와 Location에 대한 해시 코드를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Provider ?? "");
                    hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Location ?? "");
                    return hash;
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Source identity를 문자열로 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public override string ToString()
            {
                return HasValue ? $"{Provider}:{Location}" : "";
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 두 Source identity가 같은지 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public static bool operator ==(Source left, Source right) => left.Equals(right);

            // ------------------------------------------------------------
            /// <summary>
            /// 두 Source identity가 다른지 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public static bool operator !=(Source left, Source right) => !left.Equals(right);
        }

        // ============================================================
        /// <summary>
        /// Source lookup을 IReadOnlyTable로 노출하는 비제네릭 view.
        /// </summary>
        // ============================================================
        private sealed class ReadOnlyTableView : IReadOnlyTable
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 저장된 항목 수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count => lookup.Count;

            // ------------------------------------------------------------
            /// <summary>
            /// 저장된 Key 목록.
            /// </summary>
            // ------------------------------------------------------------
            public IEnumerable<string> Keys => lookup.Keys;

            // ------------------------------------------------------------
            /// <summary>
            /// 저장된 값 목록.
            /// </summary>
            // ------------------------------------------------------------
            public IEnumerable<ITableValue> Values => lookup.Values;

            // ------------------------------------------------------------
            /// <summary>
            /// lookup의 ValueType.
            /// </summary>
            // ------------------------------------------------------------
            public Type ValueType => valueType;

            private readonly Type valueType;
            private readonly IReadOnlyDictionary<string, ITableValue> lookup;

            // ------------------------------------------------------------
            /// <summary>
            /// ValueType과 lookup으로 읽기 전용 view를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadOnlyTableView
            (
                Type valueType,
                IReadOnlyDictionary<string, ITableValue> lookup
            )
            {
                this.valueType = valueType;
                this.lookup = lookup;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Key로 값을 조회하고 없으면 null을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public ITableValue this[string key]
            {
                get
                {
                    lookup.TryGetValue(key, out var value);
                    return value;
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Key가 존재하는지 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool Has(string key) => lookup.ContainsKey(key);

            // ------------------------------------------------------------
            /// <summary>
            /// Key와 값의 열거자를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public IEnumerator<KeyValuePair<string, ITableValue>> GetEnumerator() => lookup.GetEnumerator();
        }

        // ================================================================================
        /// <summary>
        /// <br/> Source lookup을 IReadOnlyTable<T>로 노출하는 제네릭 view.
        /// <br/> 별도 row 복사 없이 원본 lookup을 타입 안전하게 읽는다.
        /// </summary>
        // ================================================================================
        private sealed class ReadOnlyTableView<TTableValue> :
            IReadOnlyTable<TTableValue>,
            IReadOnlyDictionary<string, TTableValue>
        where TTableValue : class, ITableValue
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 타입 안전한 읽기 전용 Dictionary view.
            /// </summary>
            // ------------------------------------------------------------
            public IReadOnlyDictionary<string, TTableValue> Dictionary => this;

            // ------------------------------------------------------------
            /// <summary>
            /// 저장된 항목 수.
            /// </summary>
            // ------------------------------------------------------------
            public int Count => lookup.Count;

            // ------------------------------------------------------------
            /// <summary>
            /// 저장된 Key 목록.
            /// </summary>
            // ------------------------------------------------------------
            public IEnumerable<string> Keys => lookup.Keys;

            // ------------------------------------------------------------
            /// <summary>
            /// 저장된 값을 타입 안전하게 열거한다.
            /// </summary>
            // ------------------------------------------------------------
            public IEnumerable<TTableValue> Values
            {
                get
                {
                    foreach (var value in lookup.Values)
                    {
                        yield return (TTableValue)value;
                    }
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// lookup의 ValueType.
            /// </summary>
            // ------------------------------------------------------------
            public Type ValueType => typeof(TTableValue);

            private readonly IReadOnlyDictionary<string, ITableValue> lookup;

            // ------------------------------------------------------------
            /// <summary>
            /// Source lookup으로 타입 안전한 읽기 전용 view를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadOnlyTableView(IReadOnlyDictionary<string, ITableValue> lookup)
            {
                this.lookup = lookup;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Key로 값을 조회하고 없으면 null을 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public TTableValue this[string key]
            {
                get
                {
                    lookup.TryGetValue(key, out var value);
                    return (TTableValue)value;
                }
            }

            IEnumerable<ITableValue> IReadOnlyTable.Values => lookup.Values;
            ITableValue IReadOnlyTable.this[string key] => this[key];

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Key가 존재하는지 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool Has(string key) => lookup.ContainsKey(key);

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Key가 존재하는지 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool ContainsKey(string key) => lookup.ContainsKey(key);

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 Key의 값을 타입 안전하게 조회한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool TryGetValue(string key, out TTableValue value)
            {
                if (lookup.TryGetValue(key, out var lValue))
                {
                    value = (TTableValue)lValue;
                    return true;
                }

                value = null;
                return false;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 타입 안전한 Key와 값의 열거자를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public IEnumerator<KeyValuePair<string, TTableValue>> GetEnumerator()
            {
                foreach (var (key, value) in lookup)
                {
                    yield return new KeyValuePair<string, TTableValue>(key, (TTableValue)value);
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 비제네릭 Table 계약의 Key와 값 열거자를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            IEnumerator<KeyValuePair<string, ITableValue>> IReadOnlyTable.GetEnumerator() => lookup.GetEnumerator();

            // ------------------------------------------------------------
            /// <summary>
            /// 비제네릭 열거자를 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        // ================================================================================
        /// <summary>
        /// <br/> Named 슬롯 접근자.
        /// <br/> InstanceRegistry의 NamedAccessor를 상속해 IReadOnlyDataPackage로 노출한다.
        /// </summary>
        // ================================================================================
        public class NamedAccessor : InstanceRegistry<DataPackage>.NamedAccessor
        {
            internal NamedAccessor(InstanceRegistry<DataPackage> owner) : base(owner) {}

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 슬롯의 DataPackage를 IReadOnlyDataPackage로 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public new IReadOnlyDataPackage this[string slot] => base[slot];
        }

    #endregion

    #region 정적 필드

        private static readonly InstanceRegistry<DataPackage> registry = new();

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 컨텍스트의 DataPackage. 미등록 슬롯 접근 시 예외.
        /// </summary>
        // ------------------------------------------------------------
        public static IReadOnlyDataPackage Current => registry.Current;

        // ------------------------------------------------------------
        /// <summary>
        /// 슬롯 이름으로 DataPackage에 직접 접근한다.
        /// </summary>
        // ------------------------------------------------------------
        public static readonly NamedAccessor Named = new(registry);

        // ------------------------------------------------------------
        /// <summary>
        /// Register / Unregister / Clear 시 발생하는 이벤트.
        /// </summary>
        // ------------------------------------------------------------
        public static event Action OnChange;

    #endregion

    #region 정적 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// DEFAULT_SLOT에 DataPackage를 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Register(DataPackage package)
        {
            registry.Register(package);
            OnChange?.Invoke();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 슬롯에 DataPackage를 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Register(string slot, DataPackage package)
        {
            registry.Register(slot, package);
            OnChange?.Invoke();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 슬롯의 등록을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Unregister(string slot)
        {
            registry.Unregister(slot);
            OnChange?.Invoke();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 인스턴스가 등록된 모든 슬롯을 해제한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Unregister(DataPackage package)
        {
            registry.Unregister(package);
            OnChange?.Invoke();
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 지정 슬롯을 현재 컨텍스트로 전환하는 스코프를 반환한다.
        /// <br/> Dispose 시 이전 슬롯으로 자동 복원된다. OnChange를 발생시키지 않는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public static IDisposable Scope(string slot) => registry.Scope(slot);

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 지정 슬롯을 현재 컨텍스트로 설정한다.
        /// <br/> CloseScope()로 명시적으로 닫을 때까지 유지된다.
        /// <br/> 프로덕션 코드에서는 Scope() + using을 사용할 것.
        /// </summary>
        // ------------------------------------------------------------
        public static void OpenScope(string slot) => registry.OpenScope(slot);

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 가장 최근에 열린 스코프를 닫고 이전 슬롯으로 복원한다.
        /// <br/> 열린 스코프가 없을 시 InvalidOperationException이 발생한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static void CloseScope() => registry.CloseScope();

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 슬롯을 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Clear()
        {
            registry.Clear();
            OnChange?.Invoke();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 슬롯의 DataPackage를 안전하게 가져온다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool TryCurrent(out IReadOnlyDataPackage package)
        {
            var result = registry.TryCurrent(out var pkg);
            package = pkg;
            return result;
        }

    #endregion

    #region 인스턴스 데이터

        // ------------------------------------------------------------
        /// <summary>
        /// AddTable로 직접 등록한 ValueType별 Table 딕셔너리.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyDictionary<XType, ITable> Dictionary => dictionary;

        [SerializeField]
        private XDictionary_VR<XType, ITable> dictionary = new();

        // ------------------------------------------------------------
        /// <summary>
        /// Source별 원본 Table 보관소.
        /// </summary>
        // ------------------------------------------------------------
        private Dictionary<Source, Dictionary<Type, ITable>> Sources
        {
            get
            {
                return sources ??= new();
            }
        }

        [NonSerialized]
        private Dictionary<Source, Dictionary<Type, ITable>> sources = new();

        // ------------------------------------------------------------
        /// <summary>
        /// Source-backed ValueType별 runtime lookup.
        /// </summary>
        // ------------------------------------------------------------
        private Dictionary<Type, Dictionary<string, ITableValue>> SourceLookups
        {
            get
            {
                return sourceLookups ??= new();
            }
        }

        [NonSerialized]
        private Dictionary<Type, Dictionary<string, ITableValue>> sourceLookups = new();

    #endregion

    #region 데이터 조회

        // ------------------------------------------------------------
        /// <summary>
        /// direct Table과 Source-backed logical Table을 함께 열거한다.
        /// </summary>
        // ------------------------------------------------------------
        public IEnumerable<IReadOnlyTable> Tables
        {
            get
            {
                foreach (var (_, lTable) in dictionary)
                {
                    yield return lTable;
                }

                foreach (var (valueType, lLookup) in SourceLookups)
                {
                    yield return new ReadOnlyTableView(valueType, lLookup);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 키로 데이터를 읽는다. 없으면 예외.
        /// </summary>
        // ------------------------------------------------------------
        public TTableValue Read<TTableValue>(string key)
        where TTableValue : class, ITableValue
        {
            var valueType = typeof(TTableValue);

            // Source-backed 타입은 runtime lookup에서 바로 조회해 Table view 생성을 피한다.
            if (SourceLookups.TryGetValue(valueType, out var lLookup))
            {
                lLookup.TryGetValue(key, out var value);
                return value as TTableValue;
            }

            return Table<TTableValue>()[key];
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테이블이 없거나 키가 없으면 null을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public TTableValue TryRead<TTableValue>(string key)
        where TTableValue : class, ITableValue
        {
            var valueType = typeof(TTableValue);

            // Source-backed 타입은 runtime lookup에서 바로 조회한다.
            if (SourceLookups.TryGetValue(valueType, out var lLookup))
            {
                lLookup.TryGetValue(key, out var value);
                return value as TTableValue;
            }

            if (!dictionary.TryGetValue(valueType, out ITable lTable))
            {
                return null;
            }

            return (lTable as IReadOnlyTable<TTableValue>)?[key];
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 타입의 테이블을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyTable<TTableValue> Table<TTableValue>()
        where TTableValue : class, ITableValue
        {
            var valueType = typeof(TTableValue);

            if (SourceLookups.TryGetValue(valueType, out var lLookup))
            {
                return new ReadOnlyTableView<TTableValue>(lLookup);
            }

            if (dictionary.TryGetValue(valueType, out ITable lTable))
            {
                return lTable as IReadOnlyTable<TTableValue>;
            }

            throw new InvalidOperationException($"데이터 패키지에 {valueType.Name} 타입의 테이블이 존재하지 않습니다.");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 타입의 테이블을 반환한다. 없으면 예외.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyTable Table(Type valueType)
        {
            if (SourceLookups.TryGetValue(valueType, out var lLookup))
            {
                return new ReadOnlyTableView(valueType, lLookup);
            }

            if (dictionary.TryGetValue(valueType, out ITable lTable))
            {
                return lTable;
            }

            throw new InvalidOperationException($"데이터 패키지에 {valueType.Name} 타입의 테이블이 존재하지 않습니다.");
        }

    #endregion

    #region 직접 Table 관리

        // ------------------------------------------------------------
        /// <summary>
        /// 테이블을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddTable<TTable, TTableValue>(TTable lTable)
        where TTable : Table<TTableValue>
        where TTableValue : class, ITableValue
        {
            AddTable(typeof(TTableValue), lTable);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테이블을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddTable(Type valueType, ITable lTable)
        {
            if (dictionary.ContainsKey(valueType) || SourceLookups.ContainsKey(valueType))
            {
                throw new InvalidOperationException($"데이터 패키지에 {valueType.Name} 타입의 테이블이 이미 존재합니다.");
            }

            dictionary.Add(valueType, lTable);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Source를 사용하지 않고 직접 추가한 Table을 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void RemoveTable<TTableValue>()
        where TTableValue : class, ITableValue
        {
            var valueType = typeof(TTableValue);

            // Source-backed 조회 상태는 Source 단위 수명 계약을 우회해 제거하지 않는다.
            if (SourceLookups.ContainsKey(valueType))
            {
                throw new InvalidOperationException
                (
                    $"{valueType.Name} 타입은 Source로 구성되어 있습니다. RemoveSource를 사용해야 합니다."
                );
            }

            if (!dictionary.ContainsKey(valueType))
            {
                throw new InvalidOperationException($"데이터 패키지에 {valueType.Name} 타입의 테이블이 존재하지 않습니다.");
            }

            dictionary.Remove(valueType);
        }

    #endregion

    #region Source 구성

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Source가 현재 Package에 등록되어 있는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool HasSource(Source source) => source.HasValue && Sources.ContainsKey(source);

        // ------------------------------------------------------------
        /// <summary>
        /// 하나의 Table을 Source 소유 데이터로 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddSource(Source source, ITable lTable)
        {
            AddSource(source, new[] { lTable });
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 하나 이상의 Table을 Source 소유 데이터로 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddSource(Source source, IEnumerable<ITable> lTables)
        {
            // 입력 검증: 같은 Source 재등록과 현재 Package 상태와의 충돌을 commit 전에 차단한다.
            ValidateSource(source);

            if (Sources.ContainsKey(source))
            {
                throw new InvalidOperationException($"Source '{source}'가 이미 등록되어 있습니다.");
            }

            var lTablesByType = CreateTableMap(lTables);
            ValidateNewSource(source, lTablesByType);

            // 여러 ValueType의 다음 lookup을 모두 구성한 뒤 한 번에 공개한다.
            var lLookups = new Dictionary<Type, Dictionary<string, ITableValue>>();

            foreach (var valueType in lTablesByType.Keys)
            {
                lLookups.Add(valueType, BuildLookup(valueType, source, lTablesByType));
            }

            foreach (var (valueType, lLookup) in lLookups)
            {
                SourceLookups[valueType] = lLookup;
            }

            Sources.Add(source, lTablesByType);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Source가 소유한 모든 Table 값을 Package에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void RemoveSource(Source source)
        {
            // 입력 검증: 제거 대상 Source가 실제 현재 상태에 존재해야 한다.
            ValidateSource(source);

            if (!Sources.TryGetValue(source, out var lTablesByType))
            {
                throw new InvalidOperationException($"Source '{source}'가 등록되어 있지 않습니다.");
            }

            // 제거 후 남는 Source만으로 영향받은 ValueType lookup을 먼저 구성한다.
            var lLookups = new Dictionary<Type, Dictionary<string, ITableValue>>();

            foreach (var valueType in lTablesByType.Keys)
            {
                var lLookup = BuildLookup(valueType, source, null);

                if (lLookup != null)
                {
                    lLookups.Add(valueType, lLookup);
                }
            }

            // 다음 lookup이 모두 준비된 뒤 현재 lookup과 Source 보관 상태를 교체한다.
            foreach (var valueType in lTablesByType.Keys)
            {
                if (lLookups.TryGetValue(valueType, out var lLookup))
                {
                    SourceLookups[valueType] = lLookup;
                    continue;
                }

                SourceLookups.Remove(valueType);
            }

            Sources.Remove(source);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Source의 Table을 검증 후 교체한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ReplaceSource(Source source, ITable lTable)
        {
            ReplaceSource(source, new[] { lTable });
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Source의 Table 집합을 검증 후 교체한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ReplaceSource(Source source, IEnumerable<ITable> lTables)
        {
            // 입력 검증: 기존 Source가 있어야 reload/replace 의미가 성립한다.
            ValidateSource(source);

            if (!Sources.TryGetValue(source, out var lCurrentTables))
            {
                throw new InvalidOperationException($"Source '{source}'가 등록되어 있지 않습니다.");
            }

            var lTablesByType = CreateTableMap(lTables);
            ValidateNewSource(source, lTablesByType);

            // 기존/신규 ValueType의 다음 lookup을 중복 없이 준비한다.
            var lLookups = new Dictionary<Type, Dictionary<string, ITableValue>>();

            foreach (var valueType in lCurrentTables.Keys)
            {
                var lLookup = BuildLookup(valueType, source, lTablesByType);

                if (lLookup != null)
                {
                    lLookups.Add(valueType, lLookup);
                }
            }

            foreach (var valueType in lTablesByType.Keys)
            {
                if (lCurrentTables.ContainsKey(valueType))
                {
                    continue;
                }

                lLookups.Add(valueType, BuildLookup(valueType, source, lTablesByType));
            }

            // 모든 lookup이 준비된 뒤 기존 Source와 파생 조회 상태를 한 번에 교체한다.
            foreach (var valueType in lCurrentTables.Keys)
            {
                if (lLookups.TryGetValue(valueType, out var lLookup))
                {
                    SourceLookups[valueType] = lLookup;
                    continue;
                }

                SourceLookups.Remove(valueType);
            }

            foreach (var valueType in lTablesByType.Keys)
            {
                if (!lCurrentTables.ContainsKey(valueType))
                {
                    SourceLookups[valueType] = lLookups[valueType];
                }
            }

            Sources[source] = lTablesByType;
        }

    #endregion

    #region Source 내부 처리

        // ------------------------------------------------------------
        /// <summary>
        /// Source identity가 유효한지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ValidateSource(Source source)
        {
            if (!source.HasValue)
            {
                throw new ArgumentException("유효하지 않은 DataPackage.Source입니다.", nameof(source));
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 입력 Table 목록을 ValueType별 맵으로 정규화한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Dictionary<Type, ITable> CreateTableMap(IEnumerable<ITable> lTables)
        {
            if (lTables == null)
            {
                throw new ArgumentNullException(nameof(lTables));
            }

            var result = new Dictionary<Type, ITable>();

            // Source 내부에서는 ValueType 하나당 하나의 Table만 소유하도록 맵으로 정규화한다.
            foreach (var lTable in lTables)
            {
                if (lTable == null)
                {
                    throw new ArgumentException("Source Table 목록에 null이 포함되어 있습니다.", nameof(lTables));
                }

                result.Add(lTable.ValueType, lTable);
            }

            if (result.Count == 0)
            {
                throw new ArgumentException("Source에는 하나 이상의 Table이 필요합니다.", nameof(lTables));
            }

            return result;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 새 Source Table이 direct Table 또는 다른 Source Key와 충돌하지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private void ValidateNewSource
        (
            Source source,
            IReadOnlyDictionary<Type, ITable> lTablesByType
        )
        {
            foreach (var (valueType, lTable) in lTablesByType)
            {
                // direct Table과 Source-backed lookup은 같은 ValueType을 동시에 소유하지 않는다.
                if (dictionary.ContainsKey(valueType))
                {
                    throw new InvalidOperationException
                    (
                        $"{valueType.Name} 타입은 AddTable로 직접 등록되어 있어 Source와 함께 구성할 수 없습니다."
                    );
                }

                // ReplaceSource에서는 자기 기존 Table을 제외한 다른 Source만 충돌 대상으로 본다.
                foreach (var (otherSource, lOtherTables) in Sources)
                {
                    if (otherSource == source || !lOtherTables.TryGetValue(valueType, out var lOtherTable))
                    {
                        continue;
                    }

                    foreach (var key in lTable.Keys)
                    {
                        if (lOtherTable.Has(key))
                        {
                            throw new InvalidOperationException
                            (
                                $"{valueType.Name} 타입 Key '{key}'가 다른 Source에서 이미 제공되고 있습니다."
                            );
                        }
                    }
                }
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 지정 Source를 제외하고 새 Table 집합을 반영한 ValueType runtime lookup을 구성한다.
        /// <br/> lookup에는 Source-owned Table의 원본 row reference를 그대로 넣는다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private Dictionary<string, ITableValue> BuildLookup
        (
            Type valueType,
            Source source,
            IReadOnlyDictionary<Type, ITable> lTablesByType
        )
        {
            var lLookup = new Dictionary<string, ITableValue>();
            var hasTable = false;

            // 현재 Source 중 교체/제거 대상만 제외하고 같은 ValueType의 Table을 반영한다.
            foreach (var (otherSource, lOtherTables) in Sources)
            {
                if (otherSource == source || !lOtherTables.TryGetValue(valueType, out var lTable))
                {
                    continue;
                }

                hasTable = true;

                foreach (var key in lTable.Keys)
                {
                    lLookup.Add(key, lTable[key]);
                }
            }

            // Add/Replace에서는 새 Source Table을 마지막에 반영하고 Remove에서는 생략한다.
            if (lTablesByType != null && lTablesByType.TryGetValue(valueType, out var lNewTable))
            {
                hasTable = true;

                foreach (var key in lNewTable.Keys)
                {
                    lLookup.Add(key, lNewTable[key]);
                }
            }

            return hasTable ? lLookup : null;
        }

    #endregion

    }
}
