# 구조와 의존 방향

Xeri Fabric은 base Xeri 위에서 데이터 접근과 저장, 데이터 패키지, localization, document workspace를 제공하는 확장 패키지입니다.

```text
com.inonego.xeri.fabric
        ↓
com.inonego.xeri
```

base Xeri는 Fabric assembly를 참조하지 않습니다.

## 도메인

- **IO**: 저장 위치와 데이터 접근 경계
- **Data**: DataPackage와 데이터 조회/참조
- **Localization**: locale 상태와 localized value/UI
- **Workspace**: Document create/open/save/close/recovery

각 도메인은 같은 `inonego.Xeri.Fabric` assembly에 들어가지만 기존 public namespace를 유지합니다.

## 원칙

- serializer와 저장 위치를 분리합니다.
- document 상태와 표시 UI 상태를 분리합니다.
- Workspace는 EditorWindow나 파일 선택 UI를 직접 소유하지 않습니다.
- base Xeri의 범용 계약을 재사용하되 Fabric 도메인을 base Xeri로 역침투시키지 않습니다.
