# IO 유지보수 지침

이 영역을 수정하거나 확장할 때는 다음 순서로 판단합니다.

1. 필요한 것이 IO인지 serializer인지 domain operation인지 먼저 분리합니다.
2. `TLocation`과 `TValue`를 한 문장으로 정의합니다.
3. 기존 구현 또는 mapping adapter 조합으로 해결 가능한지 확인합니다.
4. 새 구현이 필요하면 읽기 전용인지 읽기/쓰기 모두 필요한지 정합니다.
5. Unity asset 수명 관리가 있으면 `Lease`가 필요한지 검토합니다.
6. 테스트가 필요하면 파일 시스템 의존이 핵심이 아닌 한 `MemoryIO<T>`를 우선 사용합니다.

잘못된 방향의 예:

```text
JsonTextFileIO         // 포맷 책임과 파일 IO 책임이 섞임
ProjectDataReader      // 특정 domain 책임이 IO로 내려옴
AddressablesTextReader // TextAsset -> string 변환만 위해 전용 reader를 계속 늘림
```

권장 방향:

```text
TextFileIO + UnityJsonSerializer
Runtime service + MemoryIO<T>
ResourcesAssetReader<TextAsset> + MappedDataReader<string, TextAsset, string>
AddressablesAssetReader<TextAsset> + MappedDataReader<string, TextAsset, string>
```
