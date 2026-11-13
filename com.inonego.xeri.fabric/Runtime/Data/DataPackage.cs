/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DataPackage.cs
수정일 : 2026-11-13

# 설명
데이터 패키지 중앙 관리자.
InstanceRegistry<DataPackage>를 통한 슬롯·Scope 시스템으로 여러 DataPackage를 이름별로 관리한다.
Source별 등록 시점의 Key·Row 매핑과 등록 수명·교체 세대·재로드 상태를 보관한다.
Type별 Value/Source 조회 Dictionary는 별도 TypeLookup으로 관리한다.

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
- AddSource               : Source의 Key→Row 매핑을 확정해 Lookup에 반영
- RemoveSource            : Source 제거 후 영향받은 runtime Lookup 재구성
- ReplaceSource           : 기존 Source를 검증 후 교체
- HasSource               : Source 등록 여부 조회
- TryFindSource<T>        : Type/Key로 Row를 등록한 Source 조회 (direct Table 제외)
- Source 등록·교체 시 Table의 Key→Row 매핑을 확정하며 이후 원본 Table 구조 변경은 반영하지 않는다.
- Row 객체는 참조를 유지하며 객체 자체의 불변성은 ITableValue 구현 계약을 따른다.

# 이벤트
- OnChange: Register / Unregister / Clear 시 발생. Scope 전환 시에는 발생하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;

namespace inonego.Xeri.Data
{
    using inonego;
    using inonego.Xeri;
    using inonego.Xeri.Serializable;

    using RowMap = IReadOnlyDictionary<string, ITableValue>;

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
    /// <br/> Source별 등록 Row 매핑을 보관하고 Type별 Lookup으로 논리 조회를 제공한다.
    /// </summary>
    // ================================================================================
    [Serializable]
    public class DataPackage : IReadOnlyDataPackage
    {

    #region Source 식별 및 상태 모델

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

        // ======================================================================
        /// <summary>
        /// Source의 등록 시점 Row 매핑과 등록·교체·재로드 상태를 함께 소유한다.
        /// </summary>
        // ======================================================================
        private sealed class SourceState
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Source 등록 때 확정한 타입별 Key·Row 매핑.
            /// </summary>
            // ------------------------------------------------------------
            public Dictionary<Type, RowMap> RowsByType { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 제거·재등록을 구별하는 등록 수명 번호.
            /// </summary>
            // ------------------------------------------------------------
            public long Registration { get; }

            // ------------------------------------------------------------
            /// <summary>
            /// 같은 등록 안에서 Source 교체를 구별하는 세대 번호.
            /// </summary>
            // ------------------------------------------------------------
            public long Generation { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 원본의 변경을 아직 메모리에 반영하지 못한 상태.
            /// </summary>
            // ------------------------------------------------------------
            public bool NeedsReload { get; private set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 하나의 Source 등록 상태를 구성한다.
            /// </summary>
            // ------------------------------------------------------------
            public SourceState(Dictionary<Type, RowMap> rowsByType, long registration, long generation)
            {
                RowsByType = rowsByType;
                Registration = registration;
                Generation = generation;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 같은 등록의 Row 매핑과 세대를 교체한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Replace
            (
                Dictionary<Type, RowMap> rowsByType,
                long generation,
                bool clearReload
            )
            {
                RowsByType = rowsByType;
                Generation = generation;
                if (clearReload)
                {
                    NeedsReload = false;
                }
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 현재 등록의 원본 재로드가 필요함을 표시한다.
            /// </summary>
            // ------------------------------------------------------------
            public void MarkForReload()
            {
                NeedsReload = true;
            }
        }

    #endregion

    #region 조회 인덱스 및 Table View

        // ============================================================
        /// <summary>
        /// 타입별 값과 소유 Source 조회를 하나의 갱신 단위로 묶는다.
        /// </summary>
        // ============================================================
        private sealed class TypeLookup
        {
            // ------------------------------------------------------------
            /// <summary>
            /// Key별 원본 Row 참조.
            /// </summary>
            // ------------------------------------------------------------
            public Dictionary<string, ITableValue> Values { get; } = new();

            // ------------------------------------------------------------
            /// <summary>
            /// Key별 등록 Source.
            /// </summary>
            // ------------------------------------------------------------
            public Dictionary<string, Source> Sources { get; } = new();

            // ------------------------------------------------------------
            /// <summary>
            /// 같은 Key의 값과 소유 Source를 함께 추가한다.
            /// </summary>
            // ------------------------------------------------------------
            public void Add(string key, ITableValue value, Source source)
            {
                Values.Add(key, value);
                Sources.Add(key, source);
            }
        }

        // ============================================================
        /// <summary>
        /// Row Dictionary를 IReadOnlyTable로 노출한다.
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
            private readonly RowMap lookup;

            // ------------------------------------------------------------
            /// <summary>
            /// ValueType과 lookup으로 읽기 전용 view를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadOnlyTableView
            (
                Type valueType,
                RowMap lookup
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
                    return lookup.TryGetValue(key, out var value) ? value : null;
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

        // ======================================================================
        /// <summary>
        /// <br/> TypeLookup의 Row 값을 IReadOnlyTable<T>로 노출하는 제네릭 View.
        /// <br/> 별도 Row 복사 없이 조회 Dictionary를 타입 안전하게 읽는다.
        /// </summary>
        // ======================================================================
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

            private readonly RowMap lookup;

            // ------------------------------------------------------------
            /// <summary>
            /// Source lookup으로 타입 안전한 읽기 전용 view를 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public ReadOnlyTableView(RowMap lookup)
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
                    return lookup.TryGetValue(key, out var value) ? (TTableValue)value : null;
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
                if (lookup.TryGetValue(key, out var row))
                {
                    value = (TTableValue)row;
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

    #endregion

    #region 슬롯 및 Scope 관리

        // ============================================================
        /// <summary>
        /// <br/> Named 슬롯 접근자.
        /// <br/> Core InstanceRegistry의 Named 접근자를 조합한다.
        /// <br/> 공개 반환값은 IReadOnlyDataPackage로 제한한다.
        /// </summary>
        // ============================================================
        public class NamedAccessor
        {
            private readonly InstanceRegistry<DataPackage>.NamedAccessor accessor;

            internal NamedAccessor(InstanceRegistry<DataPackage> owner)
            {
                accessor = owner.Named;
            }

            // ------------------------------------------------------------
            /// <summary>
            /// 지정 슬롯의 DataPackage를 IReadOnlyDataPackage로 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public IReadOnlyDataPackage this[string slot] => accessor[slot];
        }

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

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 지정 슬롯을 현재 컨텍스트로 전환하는 스코프를 반환한다.
        /// <br/> Dispose 시 이전 슬롯으로 자동 복원된다. OnChange를 발생시키지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
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

    #endregion

    #region Table 및 Source 상태

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
        /// Source별 등록 매핑과 수명·재로드 상태를 보관한다.
        /// </summary>
        // ------------------------------------------------------------
        private Dictionary<Source, SourceState> Sources
        {
            get
            {
                return sources ??= new();
            }
        }

        [NonSerialized]
        private Dictionary<Source, SourceState> sources = new();

        // ----------------------------------------------------------------------
        /// <summary>
        /// Source-backed Type별 Value/Source 조회를 묶은 runtime lookup.
        /// </summary>
        // ----------------------------------------------------------------------
        private Dictionary<Type, TypeLookup> Lookups
        {
            get
            {
                return lookups ??= new();
            }
        }

        [NonSerialized]
        private Dictionary<Type, TypeLookup> lookups = new();

        // 제거·재등록과 Source 교체의 버전은 서로 독립적으로 증가한다.
        [NonSerialized]
        private long nextSourceRegistration;

        [NonSerialized]
        private long nextSourceGeneration;

        // Source 상태와 조회 인덱스를 동일한 잠금으로 보호한다.
        private object SourceLock
        {
            get
            {
                if (sourceLock == null)
                {
                    Interlocked.CompareExchange(ref sourceLock, new object(), null);
                }

                return sourceLock;
            }
        }

        [NonSerialized]
        private object sourceLock = new();

    #endregion

    #region Row 및 Table 조회

        // ------------------------------------------------------------
        /// <summary>
        /// direct Table과 Source-backed logical Table을 함께 열거한다.
        /// </summary>
        // ------------------------------------------------------------
        public IEnumerable<IReadOnlyTable> Tables
        {
            get
            {
                // 열거 중 Source가 교체돼도 컬렉션 무효화가 발생하지 않는 목록을 반환한다.
                lock (SourceLock)
                {
                    var tables = new List<IReadOnlyTable>();

                    foreach (var (_, lTable) in dictionary)
                    {
                        tables.Add(lTable);
                    }

                    foreach (var (valueType, lLookup) in Lookups)
                    {
                        tables.Add(new ReadOnlyTableView(valueType, lLookup.Values));
                    }

                    return tables;
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 타입의 테이블을 반환한다. 없으면 예외.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyTable Table(Type valueType)
        {
            lock (SourceLock)
            {
                return TableCore(valueType);
            }
        }

        private IReadOnlyTable TableCore(Type valueType)
        {
            if (Lookups.TryGetValue(valueType, out var lLookup))
            {
                return new ReadOnlyTableView(valueType, lLookup.Values);
            }

            if (dictionary.TryGetValue(valueType, out ITable lTable))
            {
                return lTable;
            }

            throw new InvalidOperationException($"데이터 패키지에 {valueType.Name} 타입의 테이블이 존재하지 않습니다.");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 키로 데이터를 읽는다. 없으면 예외.
        /// </summary>
        // ------------------------------------------------------------
        public TTableValue Read<TTableValue>(string key)
        where TTableValue : class, ITableValue
        {
            lock (SourceLock)
            {
                return ReadCore<TTableValue>(key);
            }
        }

        private TTableValue ReadCore<TTableValue>(string key)
        where TTableValue : class, ITableValue
        {
            var valueType = typeof(TTableValue);

            // Source와 direct Table 모두 Key 누락은 실패로 취급한다.
            if (Lookups.TryGetValue(valueType, out var lLookup))
            {
                if (lLookup.Values.TryGetValue(key, out var value))
                {
                    return (TTableValue)value;
                }
            }
            else
            {
                var table = TableCore<TTableValue>();
                var value = table[key];
                if (value != null)
                {
                    return value;
                }
            }

            throw new KeyNotFoundException($"{valueType.Name} 타입에 Key '{key}'가 존재하지 않습니다.");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 테이블이 없거나 키가 없으면 null을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public TTableValue TryRead<TTableValue>(string key)
        where TTableValue : class, ITableValue
        {
            lock (SourceLock)
            {
                return TryReadCore<TTableValue>(key);
            }
        }

        private TTableValue TryReadCore<TTableValue>(string key)
        where TTableValue : class, ITableValue
        {
            var valueType = typeof(TTableValue);

            // Source-backed 타입은 runtime lookup에서 바로 조회한다.
            if (Lookups.TryGetValue(valueType, out var lLookup))
            {
                lLookup.Values.TryGetValue(key, out var value);
                return value as TTableValue;
            }

            if (!dictionary.TryGetValue(valueType, out ITable lTable))
            {
                return null;
            }

            return (lTable as IReadOnlyTable<TTableValue>)?[key];
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 지정 타입과 Key를 등록한 Source를 조회한다.
        /// <br/> 직접 추가한 Table 또는 없는 Key는 Source를 반환하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public bool TryFindSource<TTableValue>(string key, out Source source)
        where TTableValue : class, ITableValue
        {
            lock (SourceLock)
            {
                if (Lookups.TryGetValue(typeof(TTableValue), out var lLookup))
                {
                    return lLookup.Sources.TryGetValue(key, out source);
                }

                source = default;
                return false;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 타입의 테이블을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public IReadOnlyTable<TTableValue> Table<TTableValue>()
        where TTableValue : class, ITableValue
        {
            lock (SourceLock)
            {
                return TableCore<TTableValue>();
            }
        }

        private IReadOnlyTable<TTableValue> TableCore<TTableValue>()
        where TTableValue : class, ITableValue
        {
            var valueType = typeof(TTableValue);

            if (Lookups.TryGetValue(valueType, out var lLookup))
            {
                return new ReadOnlyTableView<TTableValue>(lLookup.Values);
            }

            if (dictionary.TryGetValue(valueType, out ITable lTable))
            {
                return lTable as IReadOnlyTable<TTableValue>;
            }

            throw new InvalidOperationException($"데이터 패키지에 {valueType.Name} 타입의 테이블이 존재하지 않습니다.");
        }

    #endregion

    #region 직접 Table 등록 및 제거

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
            lock (SourceLock)
            {
                AddTableCore(valueType, lTable);
            }
        }

        private void AddTableCore(Type valueType, ITable lTable)
        {
            if (dictionary.ContainsKey(valueType) || Lookups.ContainsKey(valueType))
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
            lock (SourceLock)
            {
                RemoveTableCore<TTableValue>();
            }
        }

        private void RemoveTableCore<TTableValue>()
        where TTableValue : class, ITableValue
        {
            var valueType = typeof(TTableValue);

            // Source-backed 조회 상태는 Source 단위 수명 계약을 우회해 제거하지 않는다.
            if (Lookups.ContainsKey(valueType))
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

    #region Source 등록 및 변경

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Source가 현재 Package에 등록되어 있는지 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool HasSource(Source source)
        {
            lock (SourceLock)
            {
                return source.HasValue && Sources.ContainsKey(source);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 하나의 Table을 Source 소유 데이터로 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddSource(Source source, ITable lTable)
        {
            AddSource
            (
                source,
                new[]
                {
                    lTable
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 하나 이상의 Table을 Source 소유 데이터로 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        public void AddSource(Source source, IEnumerable<ITable> lTables)
        {
            // 외부 Table을 잠금 밖에서 한 번 읽고, 이후에는 Package 소유 매핑만 사용한다.
            var rowsByType = CreateTableMap(lTables);
            lock (SourceLock)
            {
                AddSourceCore(source, rowsByType);
            }
        }

        private void AddSourceCore
        (
            Source source,
            Dictionary<Type, RowMap> rowsByType
        )
        {
            // 입력 검증: 같은 Source 재등록과 현재 Package 상태와의 충돌을 commit 전에 차단한다.
            ValidateSource(source);

            if (Sources.ContainsKey(source))
            {
                throw new InvalidOperationException($"Source '{source}'가 이미 등록되어 있습니다.");
            }

            ValidateSourceRows(source, rowsByType);
            ApplySource(source, null, rowsByType);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Source가 소유한 모든 Table 값을 Package에서 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public void RemoveSource(Source source)
        {
            lock (SourceLock)
            {
                RemoveSourceCore(source);
            }
        }

        private void RemoveSourceCore(Source source)
        {
            // 입력 검증: 제거 대상 Source가 실제 현재 상태에 존재해야 한다.
            ValidateSource(source);

            if (!Sources.TryGetValue(source, out var state))
            {
                throw new InvalidOperationException($"Source '{source}'가 등록되어 있지 않습니다.");
            }

            ApplySource(source, state, null);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Source의 Table을 검증 후 교체한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ReplaceSource(Source source, ITable lTable)
        {
            ReplaceSource
            (
                source,
                new[]
                {
                    lTable
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Source의 Table 집합을 검증 후 교체한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ReplaceSource(Source source, IEnumerable<ITable> lTables)
        {
            var rowsByType = CreateTableMap(lTables);
            lock (SourceLock)
            {
                ReplaceSourceCore(source, rowsByType, false);
            }
        }

        private void ReplaceSourceCore
        (
            Source source,
            Dictionary<Type, RowMap> rowsByType,
            bool clearReload
        )
        {
            // 입력 검증: 기존 Source가 있어야 reload/replace 의미가 성립한다.
            ValidateSource(source);

            if (!Sources.TryGetValue(source, out var state))
            {
                throw new InvalidOperationException($"Source '{source}'가 등록되어 있지 않습니다.");
            }

            ValidateSourceRows(source, rowsByType);
            ApplySource(source, state, rowsByType, clearReload);
        }

    #endregion

    #region Source 재로드 상태

        // ----------------------------------------------------------------------
        /// <summary>
        /// 아직 원본 변경을 반영하지 못해 마지막 유효 상태를 제공하는 Source인지 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public bool NeedsSourceReload(Source source)
        {
            lock (SourceLock)
            {
                return source.HasValue && Sources.TryGetValue(source, out var state) && state.NeedsReload;
            }
        }

        // 현재 Source의 등록 수명을 조회한다. 제거·재등록 시 이전 수명과 구별된다.
        internal long SourceRegistration(Source source)
        {
            lock (SourceLock)
            {
                return Sources.TryGetValue(source, out var state) ? state.Registration : 0L;
            }
        }

        // 같은 등록 안에서 교체된 Source의 세대를 조회한다.
        internal long SourceGeneration(Source source)
        {
            lock (SourceLock)
            {
                return Sources.TryGetValue(source, out var state) ? state.Generation : 0L;
            }
        }

        // 현재 등록을 확인하면서 Source를 재로드 필요 상태로 표시한다.
        internal long MarkSourceForReload(Source source, long registration)
        {
            lock (SourceLock)
            {
                if (registration == 0L || !Sources.TryGetValue(source, out var state) ||
                    state.Registration != registration)
                {
                    return 0L;
                }

                state.MarkForReload();
                return state.Generation;
            }
        }

        // 늦게 끝난 Source 로드는 현재 등록과 버전이 모두 같을 때만 공개한다.
        internal bool TryReplaceSource
        (
            Source source,
            long registration,
            long generation,
            IEnumerable<ITable> lTables
        )
        {
            bool IsCurrentSource()
            {
                return registration != 0L && generation != 0L &&
                    Sources.TryGetValue(source, out var state) &&
                    state.Registration == registration && state.Generation == generation;
            }

            // 이미 무효화된 재로드는 외부 Table을 열거하거나 검증하지 않고 버린다.
            lock (SourceLock)
            {
                if (!IsCurrentSource())
                {
                    return false;
                }
            }

            var rowsByType = CreateTableMap(lTables);
            lock (SourceLock)
            {
                // Table 준비 중 Source가 변경될 수 있으므로 공개 직전에 다시 확인한다.
                if (!IsCurrentSource())
                {
                    return false;
                }

                // 검증된 재로드 결과만 Source의 미반영 상태를 해제할 수 있다.
                ReplaceSourceCore(source, rowsByType, true);
                return true;
            }
        }

    #endregion

    #region Source 매핑 검증 및 적용

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
        /// 외부 Table의 현재 Key·Row 매핑을 Package 소유 상태로 고정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Dictionary<Type, RowMap> CreateTableMap(IEnumerable<ITable> lTables)
        {
            if (lTables == null)
            {
                throw new ArgumentNullException(nameof(lTables));
            }

            var result = new Dictionary<Type, RowMap>();

            // Source 내부에서는 ValueType 하나당 하나의 Table만 소유하도록 맵으로 정규화한다.
            foreach (var lTable in lTables)
            {
                if (lTable == null)
                {
                    throw new ArgumentException("Source Table 목록에 null이 포함되어 있습니다.", nameof(lTables));
                }

                // 등록된 Key와 Row 식별자가 다르면 이후 조회 인덱스의 의미가 불명확해진다.
                var values = new Dictionary<string, ITableValue>();
                foreach (var key in lTable.Keys)
                {
                    var value = lTable[key];
                    if (value == null || value.Key != key)
                    {
                        throw new InvalidOperationException
                        (
                            $"Source Table의 Key '{key}'와 Row 식별자가 일치하지 않습니다."
                        );
                    }

                    values.Add(key, value);
                }

                result.Add(lTable.ValueType, values);
            }

            if (result.Count == 0)
            {
                throw new ArgumentException("Source에는 하나 이상의 Table이 필요합니다.", nameof(lTables));
            }

            return result;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Source의 Row 매핑이 직접 Table이나 다른 Source와 충돌하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ValidateSourceRows
        (
            Source source,
            IReadOnlyDictionary<Type, RowMap> rowsByType
        )
        {
            foreach (var (valueType, rows) in rowsByType)
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
                foreach (var (otherSource, otherState) in Sources)
                {
                    if (otherSource == source || !otherState.RowsByType.TryGetValue(valueType, out var existingRows))
                    {
                        continue;
                    }

                    foreach (var key in rows.Keys)
                    {
                        if (existingRows.ContainsKey(key))
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 대상 Source를 제외하고 다음 Type별 조회 인덱스를 구성한다.
        /// <br/> 같은 Type의 Value/Source Dictionary를 한 번의 순회로 채운다.
        /// </summary>
        // ----------------------------------------------------------------------
        private TypeLookup BuildLookup
        (
            Type valueType,
            Source source,
            IReadOnlyDictionary<Type, RowMap> rowsByType
        )
        {
            var lLookup = new TypeLookup();
            var hasTable = false;

            // 현재 Source 중 교체/제거 대상만 제외하고 같은 ValueType의 Table을 반영한다.
            foreach (var (otherSource, otherState) in Sources)
            {
                if (otherSource == source || !otherState.RowsByType.TryGetValue(valueType, out var existingRows))
                {
                    continue;
                }

                hasTable = true;

                foreach (var (key, value) in existingRows)
                {
                    lLookup.Add(key, value, otherSource);
                }
            }

            // Add/Replace에서는 새 Source Table을 마지막에 반영하고 Remove에서는 생략한다.
            if (rowsByType != null && rowsByType.TryGetValue(valueType, out var newRows))
            {
                hasTable = true;

                foreach (var (key, value) in newRows)
                {
                    lLookup.Add(key, value, source);
                }
            }

            return hasTable ? lLookup : null;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 변경 대상 Type의 Lookup을 모두 준비한 후 등록 상태와 함께 공개한다.
        /// <br/> 새 Lookup 구성에 실패하면 기존 Source와 조회 상태는 유지된다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void ApplySource
        (
            Source source,
            SourceState current,
            Dictionary<Type, RowMap> nextRowsByType,
            bool clearReload = false
        )
        {
            var affectedTypes = new HashSet<Type>();

            if (current != null)
            {
                affectedTypes.UnionWith(current.RowsByType.Keys);
            }

            if (nextRowsByType != null)
            {
                affectedTypes.UnionWith(nextRowsByType.Keys);
            }

            // 같은 Type의 Value와 Source 인덱스를 한 번에 구성해 검증한다.
            var nextLookups = new Dictionary<Type, TypeLookup>();

            foreach (var valueType in affectedTypes)
            {
                nextLookups.Add(valueType, BuildLookup(valueType, source, nextRowsByType));
            }

            // 모든 Lookup이 준비된 뒤 Source 등록 상태를 전환한다.
            if (nextRowsByType == null)
            {
                Sources.Remove(source);
            }
            else if (current == null)
            {
                Sources.Add
                (
                    source,
                    new SourceState(nextRowsByType, ++nextSourceRegistration, ++nextSourceGeneration)
                );
            }
            else
            {
                current.Replace(nextRowsByType, ++nextSourceGeneration, clearReload);
            }

            // 같은 잠금에서 완성된 Type Lookup만 공개하며, 이전 View의 원본은 보존한다.
            foreach (var (valueType, lookup) in nextLookups)
            {
                if (lookup == null)
                {
                    Lookups.Remove(valueType);
                    continue;
                }

                Lookups[valueType] = lookup;
            }
        }

    #endregion

    }
}
