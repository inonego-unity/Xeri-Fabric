# DataPackage

`DataPackage`는 여러 `Table`과 외부 Source를 하나의 논리 데이터 조회 계층으로 묶고, 슬롯별로 현재 패키지를 전환할 수 있게 하는 Runtime 데이터 시스템입니다.

## 왜 필요한가

게임 데이터가 여러 파일·Asset·DLC Source에 나뉘어 있으면 소비 코드가 원본 위치와 병합 순서를 직접 알게 되기 쉽습니다. `DataPackage`는 Source 구성과 실제 조회를 분리해서 소비 코드가 "현재 데이터 Context에서 이 Key를 읽는다"는 계약만 사용하게 합니다.

## 언제 사용하는가

- 여러 Table을 하나의 읽기 Context로 묶어야 할 때
- Source 단위 reload/remove가 필요한 정적 데이터 집합을 구성할 때
- 테스트, 미리보기, 다른 데이터 세트를 슬롯으로 전환해야 할 때
- `REF<T>`처럼 Key만 저장하고 현재 Context에서 값을 늦게 해석하고 싶을 때

단일 Dictionary 하나로 충분하고 Source/Scope 개념이 필요 없다면 `DataPackage`까지 사용할 필요는 없습니다.

## 기본 사용

```csharp
var package = new DataPackage();
package.AddSource
(
    new DataPackage.Source("project", "items/main"),
    itemTable
);

DataPackage.Register(package);
ItemData item = DataPackage.Current.TryRead<ItemData>("item.sword");
```

여러 외부 Source를 로드하는 경우에는 필요한 구성이 모두 성공한 뒤 `Register`해서 부분 상태가 외부에 보이지 않게 합니다. 전체 절차는 [DataPackage를 구성하고 공개하기](../../guides/data/build-data-package.md)를 참고합니다.

## 핵심 구조

```text
Source A ─┐
Source B ─┼─→ ValueType별 Runtime Lookup ─→ DataPackage
Direct Table ──────────────────────────────→ DataPackage
                                             ↓
                                      Read / TryRead / Table
```

Source별 원본 Table은 보존되고, 같은 ValueType의 여러 Source는 하나의 logical Table처럼 조회됩니다.

## 슬롯과 Scope

`DataPackage`는 `InstanceRegistry<DataPackage>`를 사용합니다.

- `Current`: 현재 컨텍스트 슬롯의 읽기 전용 패키지
- `Named[slot]`: 이름으로 직접 접근
- `Scope(slot)`: `using` 범위 동안 현재 슬롯 전환 후 자동 복원
- `OpenScope` / `CloseScope`: 수동 Scope 관리

프로덕션에서는 `Scope()` 사용을 우선합니다.

## Table

`Table<T>`은 `ITableValue.Key`를 기준으로 값을 보관합니다. 직렬화 방식에 따라 `Table_V<T>`와 `Table_R<T>`가 있습니다.

- `Table_V`: `SerializeField` 값 직렬화
- `Table_R`: `SerializeReference` 다형성 직렬화

`Merge()`는 구체 Table 클래스가 아니라 `ITable<T>` 계약이 호환되면 병합할 수 있습니다.
## Source 수명

`DataPackage.Source`는 `(Provider, Location)`으로 외부 데이터 공급 단위를 식별합니다.

`AddSource`, `RemoveSource`, `ReplaceSource`는 영향받는 ValueType lookup을 먼저 완성한 뒤 현재 상태를 교체합니다. 중간 검증 실패로 부분 lookup이 공개되지 않도록 구성되어 있습니다.

Source-backed table은 `RemoveTable()`로 직접 제거할 수 없고 Source 단위 API로 제거해야 합니다.

## REF

`REF<T>`는 string key를 보관하고 `ToValue()` 시 현재 `DataPackage` 슬롯에서 값을 조회합니다.

```text
REF<T>.Key
   ↓
DataPackage.Current
   ↓
TryRead<T>(Key)
```

직접 value로 생성한 `REF<T>`는 DataPackage 조회를 하지 않습니다. direct value는 직렬화 대상이 아닙니다.

현재 DataPackage 슬롯이 없거나 key가 없으면 `ToValue()`는 null을 반환합니다.

## 책임 범위

- Table과 row의 논리 조회
- Source 단위 등록·교체·제거
- 슬롯 기반 현재 DataPackage 선택
- key 기반 `REF<T>` 해석

다음은 상위 시스템 책임입니다.

- 실제 파일/Addressables 읽기
- Source를 언제 reload할지 결정하는 정책
- 데이터 버전 마이그레이션
- 프로젝트별 validation과 patch 우선순위

## 관련 문서

- [Data 모듈](../../../Runtime/Data/README.md)
- [Xeri Singleton과 슬롯](https://github.com/inonego-unity/Xeri/blob/main/com.inonego.xeri/Documentation~/modules/core/singleton.md)
- [IO](../../../Runtime/IO/README.md)
