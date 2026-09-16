# Xeri Fabric

`com.inonego.xeri.fabric`는 Xeri 기반의 IO, Data, Localization, Workspace Runtime을 제공하는 Unity Package Manager 패키지입니다.

이 패키지는 `com.inonego.xeri`에 의존하고 Addressables를 사용하며, base Xeri가 Fabric을 역참조하지 않는 단방향 경계를 유지합니다.

## 구성

- `Runtime/IO`: 외부/로컬 데이터 접근과 저장 위치 추상화
- `Runtime/Data`: DataPackage와 데이터 조회/참조 Runtime
- `Runtime/Localization`: locale 상태와 localized value/UI Runtime
- `Runtime/Workspace`: Document create/open/save/close/recovery Runtime
- `Tests`: Edit/Play Test assembly
- `Documentation~`: 사용자·유지보수 문서

## 로컬 패키지 연결

```json
"com.inonego.xeri.fabric": "file:../../Xeri-Fabric/com.inonego.xeri.fabric"
```

자세한 사용법은 [`Documentation~/index.md`](Documentation~/index.md)를 참고합니다.
