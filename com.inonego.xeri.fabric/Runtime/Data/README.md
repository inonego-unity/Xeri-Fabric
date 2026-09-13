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
| `Table` | key/value 형태의 table 데이터 기반 |
| `TableAsset` | Unity asset으로 보관하는 table 표현 |
| `DataPackage` | 여러 table을 하나의 데이터 집합으로 구성 |
| `REF` | 데이터 항목을 참조하기 위한 표현 |
| `XMLTable` | XML 기반 table 표현 |

## 책임 범위

Data 모듈은 데이터 모델과 table 계약을 제공합니다. 파일에서 문자열을 읽는 책임은 `IO`, 객체를 특정 포맷으로 변환하는 책임은 serializer와 구분합니다.

## 제약과 주의사항

프로젝트 도메인별 repository나 저장 workflow를 `DataPackage` 자체에 추가하지 않습니다. 상위 서비스가 필요한 table과 참조를 조합합니다.

## 관련 문서

- [DataPackage](../../Documentation~/modules/data/data-package.md)
- [IO](../IO/README.md)
- [Serializable](../Serializable/README.md)
