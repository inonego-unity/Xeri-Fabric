# Xeri Fabric 문서

Xeri Fabric은 Xeri 기반의 IO, Data, Localization, Workspace Runtime을 하나의 UPM 패키지로 제공합니다.

## 처음이라면

1. [설치](getting-started/installation.md)
2. [구조와 의존 방향](concepts/architecture.md)
3. 필요한 도메인 문서
4. 실제 조립이 필요하면 각 가이드를 확인합니다.

## 모듈

- [IO](modules/io/io.md): 저장 위치와 데이터 접근 경계
- [DataPackage](modules/data/data-package.md): 데이터 패키지와 조회 Runtime
- [Localization](modules/localization/localization.md): locale와 localized value/UI
- [Workspace](modules/workspace/workspace.md): Document lifecycle과 recovery

## 가이드

- [DataPackage 구성하기](guides/data/build-data-package.md)
- [Locale 정책 연결하기](guides/localization/project-locale-policy.md)
- [Document Workspace 구성하기](guides/workspace/build-document-workspace.md)

## 유지보수

- [IO 유지보수 지침](maintainers/io.md)
- [Workspace 유지보수 지침](maintainers/workspace.md)
- [Workspace Document 유지보수 지침](maintainers/workspace-document.md)
