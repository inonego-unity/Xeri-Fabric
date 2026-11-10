# IO

Xeri Fabric IO는 저장 위치와 데이터 접근을 serializer나 domain 모델에서 분리하기 위한 Runtime입니다.

## 언제 사용하는가

- 파일, 메모리, 프로젝트 자산 등 저장 위치를 교체 가능하게 만들 때
- serializer가 실제 저장 매체를 직접 소유하지 않게 할 때
- Document나 Data Runtime에 일관된 IO 경계를 제공할 때

## 책임

- 데이터 접근과 저장 위치 추상화
- IO 실행 결과와 오류 경계
- 기존 `MappedDataReader` / `AsyncMappedDataReader` 및 신규 `MappedDataWriter` / `AsyncMappedDataWriter`를 통한 **범용 값 변환**
- `IO/File/FileCommitWriter<TValue>`의 **단일 파일 임시 기록·교체** (다중 파일 원자성·전원 장애 내구성은 범위 밖)
- 상위 Data/Workspace/Persistence Runtime에서 재사용 가능한 저장 기반

직렬화 포맷 자체는 base Xeri의 Serializable 계층 또는 각 도메인의 handler가 소유합니다. **범용 Mapping은 IO에 유지**하며, 타입화된 영속 기록의 검증과 읽기 `ReleaseHandle` 회수는 [Persistence](../persistence.md)가 담당합니다.

## Fabric IO 계약·수명 및 검증 (2026-11-09)

`ReadResponse<T>.ReleaseHandle`와 `IReleaseHandle`은 **Fabric IO의 공개 수명 계약**이다. `CallbackReleaseHandle`은 Release/Dispose를 일회 수행하며 실패한 읽기 응답이라도 ReleaseHandle이 있으면 호출자가 반드시 정리해야 한다. Core `ISerializer`는 별도 패키지의 포맷 변환 계약으로 사용하며 이 Stage에서 수정하지 않는다.

- `TEST_FileCommitWriter`: **7개**. 임시 파일 교체·실패 시 기존 대상 보존·기존 백업 세대 보존
- `TEST_MappedDataWriter`: **10개**. 동기·비동기 변환, 원본 예외·실패 응답, 사전·변환 중 취소
- `TEST_CallbackReleaseHandle`: **2개**. Release/Dispose 일회 호출·해제 예외
- `TEST_MappedDataReader`: **13개**. 동기·비동기 원본 예외, 성공·변환 실패 분리, 취소 및 ReleaseHandle 전달·회수
- `TEST_AddressablesAssetReader`: **1개**. 사전 취소 시 실패 응답 및 ReleaseHandle 없음

**2026-11-09 정렬 이후 실제 Unity 6000.7.0a6 Fabric EditMode 256/256 통과, 그중 IO 33/33**(Core와 Fabric을 함께 넣은 분리 프로젝트). 결과 XML: `C:\Users\hmud0\AppData\Local\Temp\XeriFabricS01Align_20261109\S01_Alignment_FabricEditMode_FullIO.xml`. 이 결과는 2026년 7월의 구 Core 작업 트리에서 발생했던 컴파일 오류와 다르며, 최신 실제 연결 Core의 **고정 검증 사본**을 사용한 결과다. 실제 Addressables 부하·Player/IL2CPP·파일 강제 종료 내구성까지 입증하지 않는다.

[시스템 Stage 01 검토·수정 및 검증](../../plans/stage-01-fabric-io.md)에 정확한 범위와 미검증 항목을 기록한다.

## 관련 문서

- [Runtime IO README](../../../Runtime/IO/README.md)
- [구조와 의존 방향](../../concepts/architecture.md)
