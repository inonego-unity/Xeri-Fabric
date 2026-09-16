# Workspace Document 유지보수 지침

이 영역을 수정하거나 확장할 때는 다음 기준을 우선합니다.

- 변경하려는 값이 `Document` metadata인지, `Body` 데이터인지, `Session` 상태인지, `Location`인지 먼저 구분합니다.
- 저장 실행은 service, 사용자 분기는 controller에 둡니다.
- 새 추상화를 만들기 전에 기존 `IDocumentHandler`, `IDocumentLocation`, `IDocumentSession` 조합으로 가능한지 확인합니다.
- Xeri metadata가 파일 안에 필요하면 envelope handler를 사용합니다.
- 기존 JSON/XML root를 유지해야 하면 body serialized handler를 사용합니다.
- raw text/code file은 raw text handler를 사용합니다.
- domain reload 대응은 `RecordRecovery()`/`Recover(string)`을 사용하고, record 문자열 보관은 host adapter에서 처리합니다.
- body 변경은 자동 감지하지 않습니다. 변경한 쪽이 `SetDirty()`를 호출합니다.

피해야 할 방향:

```text
Document가 body 데이터를 직접 소유
Body가 session이나 workspace를 참조
Handler.Save가 session.SetLocation 또는 ClearDirty 호출
Workspace가 active session을 강제로 하나만 관리
SaveTo가 SaveAs처럼 session location을 바꿈
DocumentWorkspaceService가 IO, serializer, envelope 구성 책임을 가짐
Handler가 JSON/XML 문자열을 직접 조립함
모든 문서에 DocumentEnvelope를 강제함
Recovery record DTO를 host adapter의 public 저장 계약으로 노출함
```
