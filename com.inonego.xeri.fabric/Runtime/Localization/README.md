# Xeri Localization

Xeri Localization은 locale 상태, localized string 조회와 UGUI/UI Toolkit 표시 연결을 제공합니다.

## 개요

현재 영역은 locale 저장소와 문자열 데이터, locale 변경에 반응하는 binding/UI adapter를 분리합니다. 표시 계층은 localization core의 상태를 소비하지만 locale 저장 방식 자체를 소유하지 않습니다.

## 왜 필요한가

지원 언어 선택, 현재 locale 저장, 문자열 조회와 UI 갱신을 각 화면이 따로 처리하면 언어 변경 경계가 분산됩니다. Localization은 현재 locale과 reload 신호를 공통화하고, 프로젝트 고유의 지원 언어 목록·기본값·fallback 정책은 상위 계층에 남깁니다.

## 언제 사용하는가

- 여러 화면이 같은 현재 locale 상태를 공유할 때
- locale 변경을 저장하고 UGUI/UITK 표시를 일괄 갱신할 때
- 테스트나 Tool에서 `ILocaleStorage`를 교체해야 할 때

프로젝트 정책을 연결하는 예는 [Locale 정책 연결하기](../../Documentation~/guides/localization/project-locale-policy.md)를 참고합니다.

## 어디서 시작하는가

현재 locale 저장·변경과 UI reload 구조는 [Localization 상세](../../Documentation~/modules/localization/localization.md), 프로젝트 지원 언어/default 정책을 붙이는 실제 절차는 [Locale 정책 연결하기](../../Documentation~/guides/localization/project-locale-policy.md)에서 시작합니다.

## 책임 범위

### 담당하는 것

- 현재 locale 저장과 변경
- `LocalizedString`과 문자열 row 표현
- `ILocaleStorage`, `ILocalizedString`, `ILocalizedUI` 계약
- UGUI와 UI Toolkit localized UI 연결

### 담당하지 않는 것

- 번역문 제작이나 외부 번역 서비스 연동
- 프로젝트 전체 언어 선택 UX
- 임의의 파일 포맷을 localization core에서 직접 읽는 것

## 핵심 개념

| 개념 | 설명 |
|---|---|
| Locale Storage | 현재 locale의 보관 위치 |
| Localized String | locale에 따라 선택되는 문자열 데이터 |
| Binding | locale 변경을 실제 표시 객체에 반영하는 연결 |

## 관련 문서

- [Localization 상세](../../Documentation~/modules/localization/localization.md)
- [IO](../IO/README.md)
- [Xeri 구조](../../Documentation~/concepts/architecture.md)
