# 프로젝트 Locale 정책 연결하기

Xeri Fabric Localization은 현재 언어 코드의 저장·변경과 Localized UI 갱신을 제공하지만, 프로젝트가 어떤 언어를 지원하는지와 기본 언어가 무엇인지는 결정하지 않습니다.

## 목적

지원 언어 목록, 기본 locale, 언어 선택 UX는 프로젝트 정책으로 유지하면서 Xeri에는 현재 locale 저장·변경과 UI reload만 맡기는 구조를 만듭니다.

## 역할 분리

```text
Project Locale Policy
├─ 지원 코드 목록
├─ 기본 locale
└─ 언어 선택 UX
        ↓
Xeri Localization
├─ CurrentLangCode
├─ Storage
└─ UI reload
```

지원 언어 정책을 Xeri의 `Localization` 클래스에 추가하지 않고 프로젝트의 별도 정책으로 둡니다.

## 1. 지원 Locale 정의

```csharp
using System;
using System.Collections.Generic;
using XeriLocalization = inonego.Xeri.Localization.Localization;

public static class ProjectLocale
{
    public const string English = "en-US";
    public const string Korean = "ko-KR";
    public const string Default = English;

    private static readonly string[] supported = { English, Korean };
    public static IReadOnlyList<string> Supported => supported;

    public static bool IsSupported(string code)
    {
        return Array.IndexOf(supported, code) >= 0;
    }

    public static void EnsureCurrent()
    {
        if (!IsSupported(XeriLocalization.CurrentLangCode))
        {
            XeriLocalization.CurrentLangCode = Default;
        }
    }
}
```

## 2. 시작 시 저장값 정규화

Xeri는 기본적으로 저장된 locale 값을 불러옵니다. 프로젝트 지원 목록에서 빠진 오래된 값이나 잘못된 값이 있을 수 있으므로 애플리케이션 준비 단계에서 한 번 정규화할 수 있습니다.

```csharp
ProjectLocale.EnsureCurrent();
```

## 3. 언어 선택 UI

사용자가 언어를 선택하면 프로젝트 UI는 지원 목록을 기준으로 선택지를 만들고 최종적으로 Xeri 현재 언어만 변경합니다.

```csharp
if (ProjectLocale.IsSupported(selectedCode))
{
    XeriLocalization.CurrentLangCode = selectedCode;
}
```

변경 시 Xeri는 Storage에 저장하고 `OnLangCodeChange`를 발행한 뒤 현재 `ILocalizedUI` 구현을 다시 로드합니다.
## 4. 문자열 데이터 사용

`LocalizedString`이나 `ILocalizedString`은 언어 코드별 값을 보관하고 현재 locale로 안전하게 해석할 수 있습니다.

```csharp
string text = localizedString.ToLocalized();
```

Fallback chain, locale alias, 번역 리소스 로딩 방식은 프로젝트 정책입니다. 필요하면 Xeri `ILocaleStorage`나 상위 Data/IO 시스템과 조합합니다.

## 관련 문서

- [Localization](../../modules/localization/localization.md)
- [DataPackage](../../modules/data/data-package.md)
- [Xeri 첫 설정](https://github.com/inonego-unity/Xeri/blob/main/com.inonego.xeri/Documentation~/getting-started/first-setup.md)