# Workspace 유지보수 지침

Workspace 영역을 수정하거나 확장할 때는 먼저 대상 기능이 실제 작업 상태인지 확인합니다.

- 작업 상태면 기존 workspace 모듈로 표현 가능한지 먼저 봅니다.
- 외부 데이터 접근이면 `Runtime/IO` 쪽을 우선 검토합니다.
- 표시와 입력이면 UI 또는 Editor 계층에 둡니다.
- serializer 포맷 분기는 Workspace가 아니라 serializer/handler 쪽에 둡니다.
- active view, focused tab, scroll 같은 view 상태를 Workspace의 전역 정책으로 고정하지 않습니다.

잘못된 방향:

```text
Workspace가 EditorWindow를 직접 참조
Workspace가 파일 다이얼로그를 직접 띄움
Workspace가 serializer 포맷을 직접 분기
Workspace가 active view 상태를 하나로 고정
```
