# Xeri Data

## 개요

Xeri Data는 table 기반 데이터와 여러 table을 묶는 `DataPackage`, 참조 표현인 `REF`를 제공합니다.

## 왜 필요한가

프로젝트의 정적 데이터가 여러 Table/Source로 나뉘면 소비 코드가 파일 위치나 Asset 종류를 직접 알기 쉽습니다. Data 모듈은 Key 기반 Table과 현재 데이터 Context를 제공해 데이터의 물리적 공급 방식과 조회 코드를 분리합니다.

## 언제 사용하는가

- 여러 Table을 하나의 `DataPackage`에서 타입별로 조회할 때
- Source 단위 add/remove/replace가 필요한 데이터 집합을 구성할 때
- 저장된 문자열 Key를 `REF<T>`로 늦게 해석할 때

외부 Source를 실제로 읽는 책임은 IO/Loader가 소유합니다. 전체 구성 흐름은 [DataPackage 구성하기](../../Documentation~/guides/data/build-data-package.md)를 참고합니다.

## 어디서 시작하는가

Table/Source/Scope/`REF<T>`의 역할은 [DataPackage 상세](../../Documentation~/modules/data/data-package.md), 외부 Source를 모두 준비한 뒤 안전하게 현재 Context에 공개하는 절차는 [DataPackage 구성하기](../../Documentation~/guides/data/build-data-package.md)에서 시작합니다.

## 핵심 개념

| 개념 | 설명 |
|---|---|
| `Table` | 메모리에 있는 key/value 데이터의 기본 Table |
| `DataPackage` | 여러 Source의 Table과 타입별 Key 조회를 구성 |
| `REF<T>` | 저장된 Key를 현재 DataPackage에서 해석하는 참조 |
| `ITable` / `IReadOnlyTable` | 구현 방식과 무관한 Table 계약 |
| `TableQuery` | 외부 Query Reader가 사용하는 조회 조건 계약 |

외부 로딩·포맷·Unity 자산은 [Data Adapters](../Adapters/Data/README.md)로 분리되어 있습니다. `TableAsset`, `XMLTable`, `DbTableLoader`, `HttpTableLoader`, `MemoryTableQueryReader`는 `inonego.Xeri.Fabric.Data.Adapters` 어셈블리에서 제공합니다. SQLite의 행 조회·편집·Commit 연계는 별도의 `Runtime/SQLite` 모듈에 있습니다.

## Source 소유권 조회

`DataPackage`는 Source 등록·교체 시 외부 Table을 잠금 밖에서 읽어 Key·Row 매핑을 복사하고, 등록 수명·교체 세대·재로드 상태와 함께 내부 `SourceState`에 보관합니다. 타입별 `TypeLookup`은 `Values: Key → ITableValue`와 `Sources: Key → Source`를 함께 구성하며, Source 변경 시 영향받는 타입의 완성된 Lookup으로 교체합니다. 값과 소유 Source는 각각 메모리 인덱스로 조회하므로 전체 Source를 순회하지 않습니다.

```csharp
if (package.TryFindSource<ItemData>("sword", out var source))
{
    // source.Provider 및 source.Location으로 등록 출처 확인
}

REF<ItemData> reference = new("sword");
if (reference.TryFindSource(out var currentSource))
{
    // REF는 현재 DataPackage의 등록 출처를 다시 조회
}
```

`AddTable`로 직접 등록한 Row, `REF<T>`의 직접 객체 참조, 존재하지 않는 Key에는 등록 Source가 없으므로 `false`를 반환합니다. 조회는 파일이나 DB를 열지 않습니다. `NeedsSourceReload(source)`는 별도의 최신성 판단이며, Source 역조회 성공이 데이터의 최신성을 보증하지는 않습니다.

Source 추가·교체·제거 시 Row와 소유 출처 인덱스를 같은 변경 게이트에서 재구성합니다. **원본 `ITable`의 Key·Row 매핑은 등록·교체 시 복사하므로**, 이후 입력 Table의 행 추가·삭제·교체는 Package에 반영되지 않습니다. 등록할 때 Dictionary Key와 Row의 `Key`가 일치해야 합니다. `ITableValue` 행 객체 자체는 기존 참조를 유지하므로, **등록 이후 Row의 Key와 내부 값은 호출자가 변경하지 않아야 합니다.** 이를 강제해야 하는 데이터 타입은 불변 Row로 설계하고, 편집은 별도 Body·복제본에서 수행해야 합니다. `Read<T>(key)`는 해당 타입이 없거나 Key가 없을 때 예외를, `TryRead<T>(key)`는 `null`을 반환합니다. 재로드 필요 상태인 Source를 일반 `ReplaceSource()`로 교체하더라도 최신으로 표시하지 않으며, 등록·세대가 일치하는 `TryReplaceSource()` 결과가 성공해야 재로드 필요 상태가 해제됩니다. 외부 공급자에게 별도 Source Manager를 맡기지 않습니다.

**Table 조회는 Key·Row 중심입니다.** 내부 `TypeLookup.Values`를 `IReadOnlyTable`의 `Dictionary`·`Values`·`GetEnumerator()`로 노출하며, Source 소유권은 `TryFindSource<T>()`로 별도 조회합니다.

## 책임 범위

Data 모듈은 데이터 모델과 table 계약을 제공합니다. 파일에서 문자열을 읽는 책임은 `IO`, 객체를 특정 포맷으로 변환하는 책임은 serializer와 구분합니다.

## 제약과 주의사항

프로젝트 도메인별 repository나 저장 workflow를 `DataPackage` 자체에 추가하지 않습니다. 상위 서비스가 필요한 table과 참조를 조합합니다.

## 관련 문서

- [DataPackage](../../Documentation~/modules/data/data-package.md)
- [IO](../IO/README.md)
- [Xeri Serializer](https://inonego-unity.github.io/Xeri/docs/modules/serialization/serializer.html)
