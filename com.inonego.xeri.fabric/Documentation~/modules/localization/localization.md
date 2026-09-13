# Localization

Xeri Fabric Localization은 현재 언어 코드, 언어 코드 저장소, localized string 조회와 UI reload를 연결하는 Runtime 시스템입니다.

## 왜 필요한가

언어 선택 상태와 저장 위치, 문자열 조회, UI 갱신을 각 화면이 따로 처리하면 locale 변경 시점과 저장 정책이 분산됩니다. Xeri Localization은 현재 locale과 reload 신호를 한 경계로 모으고, 프로젝트가 지원 언어 목록·기본 언어·fallback 정책을 별도로 소유하게 합니다.

## 언제 사용하는가

- 여러 UGUI/UITK 화면이 같은 현재 언어 상태를 공유할 때
- locale 변경을 저장하고 UI를 일괄 갱신해야 할 때
- 테스트나 도구에서 locale storage를 교체해야 할 때

문자열 몇 개를 고정 번역하는 정도라면 전역 Localization Runtime이 필요하지 않을 수 있습니다.

## 기본 사용

```csharp
Localization.CurrentLangCode = "en-US";
string text = localizedString.ToLocalized();
```

프로젝트가 지원하는 locale 목록과 기본값은 별도 정책으로 두는 것을 권장합니다. 자세한 예는 [프로젝트 Locale 정책 연결하기](../../guides/localization/project-locale-policy.md)를 참고합니다.

## 핵심 구조

```text
ILocaleStorage
    ↓ Load / Save
Localization
    ├─ LangCode
    └─ OnLangCodeChange
          ↓
ILocalizedUI.ReloadLocalizedUIAll()
```

기본 `Localization` 인스턴스는 Unity `SubsystemRegistration` 시점에 `PlayerPrefsLocaleStorage`를 사용해 자동 등록됩니다.

## 언어 코드

`LangCode`는 자유 문자열입니다. `ko-KR`, `en-US` 같은 BCP 47 형태를 권장하지만 코드 정규화와 fallback chain은 Xeri가 강제하지 않습니다.

언어 변경 시 순서는 다음과 같습니다.

```text
LangCode 변경
→ Storage.Save
→ OnLangCodeChange
→ 모든 ILocalizedUI reload
```

이벤트 처리 중 다시 `LangCode`를 변경하려는 재진입은 무시되고 경고가 기록됩니다.
## LocalizedString

`LocalizedString`은 언어 코드에서 문자열로 매핑되는 직렬화 가능한 dictionary입니다. 등록되지 않은 언어 코드를 조회하면 예외 대신 빈 문자열을 반환합니다.

`ToLocalized()` 확장은 현재 `Localization` 슬롯이 없거나 입력이 null인 경우에도 빈 문자열을 반환합니다.

## Storage 교체

`ILocaleStorage`를 구현하면 언어 코드 저장 위치를 교체할 수 있습니다. 테스트나 임시 Runtime에서는 `InMemoryLocaleStorage`, 기본 Player 환경에서는 `PlayerPrefsLocaleStorage`를 사용할 수 있습니다.

## 책임 범위

- 현재 언어 코드와 영속화
- 언어 변경 이벤트
- `ILocalizedString`의 현재 언어 조회
- 등록된 localized UI의 reload 경계

다음은 상위 프로젝트 정책입니다.

- fallback 언어 선택
- locale code 정규화
- 번역 asset 배포와 원격 갱신
- 복수형·문법·formatting 정책

## 관련 문서

- [Localization 모듈](../../../Runtime/Localization/README.md)
- [Xeri 구조](https://github.com/inonego-unity/Xeri/blob/main/com.inonego.xeri/Documentation~/concepts/architecture.md)
