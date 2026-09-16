# IO

Xeri Fabric IO는 저장 위치와 데이터 접근을 serializer나 domain 모델에서 분리하기 위한 Runtime입니다.

## 언제 사용하는가

- 파일, 메모리, 프로젝트 자산 등 저장 위치를 교체 가능하게 만들 때
- serializer가 실제 저장 매체를 직접 소유하지 않게 할 때
- Document나 Data Runtime에 일관된 IO 경계를 제공할 때

## 책임

- 데이터 접근과 저장 위치 추상화
- IO 실행 결과와 오류 경계
- 상위 Data/Workspace Runtime에서 재사용 가능한 저장 기반

직렬화 포맷 자체는 base Xeri의 Serializable 계층 또는 각 handler가 소유합니다.

## 관련 문서

- [Runtime IO README](../../../Runtime/IO/README.md)
- [구조와 의존 방향](../../concepts/architecture.md)
