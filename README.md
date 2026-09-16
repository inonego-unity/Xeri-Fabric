# Xeri Fabric

`Xeri-Fabric`는 Xeri 기반의 IO, Data, Localization, Workspace Runtime을 제공하는 독립 Unity Package 저장소입니다.

## Package

- `com.inonego.xeri.fabric`
- dependency: `com.inonego.xeri`
- additional dependency: Unity Addressables

## Git / UPM

프로젝트의 `Packages/manifest.json`에서 Xeri와 Fabric을 함께 연결합니다.

```json
"com.inonego.xeri": "https://github.com/inonego-unity/Xeri.git?path=/com.inonego.xeri#main",
"com.inonego.xeri.fabric": "https://github.com/inonego-unity/Xeri-Fabric.git?path=/com.inonego.xeri.fabric#main"
```

릴리스 태그를 사용하기 시작하면 `#main` 대신 동일한 호환 버전 태그를 고정합니다.
## Local checkout

```json
"com.inonego.xeri.fabric": "file:../../Xeri-Fabric/com.inonego.xeri.fabric"
```

## Documentation

- [사용자 문서](com.inonego.xeri.fabric/Documentation~/index.md)
- [설치](com.inonego.xeri.fabric/Documentation~/getting-started/installation.md)
- [구조와 의존 방향](com.inonego.xeri.fabric/Documentation~/concepts/architecture.md)

의존 방향은 `Xeri Fabric -> Xeri`만 허용하며 base Xeri는 Fabric assembly를 참조하지 않습니다.
