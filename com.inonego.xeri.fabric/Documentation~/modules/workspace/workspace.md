# Workspace

Xeri Fabric Workspace는 문서의 create/open/save/close/recovery lifecycle을 UI와 분리해서 관리합니다.

## 언제 사용하는가

- 편집 가능한 문서 Session을 여러 개 관리할 때
- 저장 위치 선택, dirty 상태, close 확인을 UI와 분리할 때
- runtime tool과 Editor tool에서 같은 document 규칙을 재사용할 때

## 핵심 구성

- `DocumentWorkspace`: 열린 Session 집합
- `DocumentWorkspaceService`: create/open/save/recovery 실행
- `DocumentWorkspaceController`: 사용자-facing save/close 흐름 해석
- `IDocumentHandler`: 문서 타입별 저장/복구 규칙
- `IDocumentLocation`: 저장 위치 계약

Workspace는 EditorWindow, 탭 UI, 파일 선택 창을 직접 소유하지 않습니다.

## 관련 문서

- [Document Runtime README](../../../Runtime/Workspace/Document/README.md)
- [Document Workspace 구성하기](../../guides/workspace/build-document-workspace.md)
- [Workspace 유지보수](../../maintainers/workspace.md)
