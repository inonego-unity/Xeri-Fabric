# DataPackage를 구성하고 공개하기

이 가이드는 하나 이상의 Table을 `DataPackage`로 조합하고, 완전한 상태가 된 뒤 전역 또는 named 슬롯에 공개하는 기본 흐름을 설명합니다.

## 목적

데이터 Source의 실제 로딩 방식과 소비 코드의 조회 방식을 분리하고, 부분 로드 상태가 `DataPackage.Current`에 노출되지 않도록 안전한 공개 순서를 구성합니다.

## 1. Table 값 정의

Table row는 `ITableValue`를 구현하고 안정적인 문자열 Key를 제공합니다.

```csharp
using System;
using UnityEngine;
using inonego.Xeri.Data;

[Serializable]
public sealed class ItemData : ITableValue
{
    [SerializeField] private string key;
    [SerializeField] private string displayName;

    public string Key => key;
    public string DisplayName => displayName;
}
```

## 2. Table 구성

직렬화 방식에 맞는 Table 구현을 선택합니다.

```csharp
var table = new Table_V<ItemData>();
table.Add(itemA);
table.Add(itemB);
```

다형 Row를 `[SerializeReference]`로 유지해야 한다면 `Table_R<T>`를 사용합니다.
## 3. Source 단위로 Package에 추가

외부 파일이나 Addressables처럼 원본 단위가 구분된다면 `DataPackage.Source`를 함께 사용합니다.

```csharp
var package = new DataPackage();
var source = new DataPackage.Source("project", "items/main");
package.AddSource(source, table);
```

Source identity는 reload/remove 경계가 되므로 실제 원본을 안정적으로 식별하는 값을 사용합니다.

## 4. 구성이 끝난 뒤 등록

여러 Source를 로드한다면 일부만 준비된 Package를 먼저 `Register`하지 않습니다.

```csharp
// 모든 Source 구성과 검증이 성공한 뒤 공개한다.
DataPackage.Register(package);
```

이후 일반 소비자는 `DataPackage.Current`, `TryRead<T>()`, `REF<T>`를 사용합니다.

```csharp
ItemData item = DataPackage.Current.TryRead<ItemData>("item.sword");

var reference = new REF<ItemData>("item.sword");
ItemData resolved = reference.ToValue();
```

## 5. Source 자원 수명 연결

Table이 Addressables Asset처럼 외부 handle의 수명에 의존한다면 Package를 사용하는 동안 해당 handle도 유지합니다. Loader가 `Lease`를 소유하고 Package 등록 해제와 함께 release하는 방식이 일반적입니다.
## 종료

Package 수명이 끝나면 등록을 해제한 뒤 Source 자원을 정리합니다.

```csharp
DataPackage.Unregister(package);
// 그 다음 Loader가 보관한 Lease/Addressables handle 등을 해제한다.
```

`DataPackage` 자체가 외부 asset handle을 자동으로 release하지는 않습니다. 외부 Source를 획득한 Loader가 그 수명을 소유합니다.

## 관련 문서

- [DataPackage](../../modules/data/data-package.md)
- [IO](../../../Runtime/IO/README.md)
- [Xeri 소유권과 수명](https://github.com/inonego-unity/Xeri/blob/main/com.inonego.xeri/Documentation~/concepts/ownership-and-lifetime.md)
- [Xeri 통합 패턴](https://github.com/inonego-unity/Xeri/blob/main/com.inonego.xeri/Documentation~/concepts/integration-patterns.md)