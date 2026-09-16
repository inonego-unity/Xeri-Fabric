# 설치

Xeri Fabric은 `com.inonego.xeri.fabric` UPM 패키지이며 `com.inonego.xeri`와 Unity Addressables에 의존합니다.

## 요구 사항

- Unity 6000.0 이상
- `com.inonego.xeri` 0.0.1 이상
- Unity Addressables 2.8.0 이상

실제 최소 버전은 패키지 루트의 `package.json`을 기준으로 확인합니다.

## Git / UPM

```json
"com.inonego.xeri": "https://github.com/inonego-unity/Xeri.git?path=/com.inonego.xeri#main",
"com.inonego.xeri.fabric": "https://github.com/inonego-unity/Xeri-Fabric.git?path=/com.inonego.xeri.fabric#main"
```

안정화 뒤에는 `#main` 대신 호환 버전 태그를 고정하는 방식을 권장합니다.
## Local checkout

KnackH처럼 Xeri-Fabric과 Unity 프로젝트가 같은 상위 디렉터리에 있다면 다음처럼 연결할 수 있습니다.

```json
"com.inonego.xeri.fabric": "file:../../Xeri-Fabric/com.inonego.xeri.fabric"
```

## 확인

설치 후 Package Manager에서 `Xeri Fabric`이 보이고 `inonego.Xeri.Fabric` assembly가 해석되는지 확인합니다.
