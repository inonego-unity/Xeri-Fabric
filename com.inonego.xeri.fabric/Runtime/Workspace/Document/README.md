# Xeri Workspace Document

## 개요


`Runtime/Document`는 Unity Editor와 Runtime 양쪽에서 사용할 수 있는 문서 작업 기반입니다.
문서의 정체성, 본문, 열린 작업 상태, 위치, 저장 흐름을 분리해서 여러 editor tool이 같은 core 흐름을 재사용할 수 있게 합니다.

이 문서는 Document를 사용하는 데 필요한 핵심 개념과 공개 계약을 설명합니다.
내부 구현과 검증 기준은 별도 유지보수 문서로 분리하고, 여기서는 사용 흐름과 책임 경계를 중심으로 설명합니다.

## 왜 필요한가

문서 편집 기능을 파일 IO 중심으로 만들면 “열려 있는 문서”, dirty 상태, 현재 위치, SaveAs 후 기준 위치 변경, 중복 Open, Recovery 같은 작업 상태가 UI 코드에 흩어집니다. Document Workspace는 **문서 내용과 작업 세션**, **저장 위치**, **포맷 변환**, **사용자 흐름 해석**을 분리해 같은 문서 모델을 서로 다른 Host에서 재사용하게 합니다.

## 언제 사용하는가

- 여러 문서를 동시에 열고 수정·저장·닫아야 할 때
- 새 문서와 기존 문서가 같은 Session 모델을 사용해야 할 때
- Save / SaveAs / SaveTo 의미를 명확히 구분해야 할 때
- UI가 파일 위치 선택이나 저장 확인을 담당하되 Core 저장 규칙은 공유해야 할 때
- domain reload나 Host 재생성 후 열린 Session을 복구해야 할 때

단일 파일을 로드해 즉시 처리하고 열린 작업 상태를 유지하지 않는 기능에는 Document Workspace가 필요하지 않습니다.

## 가장 빠른 시작

1. 문서 Body와 `TypeID`를 정합니다.
2. 저장 구조에 맞는 `IDocumentHandler` 또는 built-in Handler를 구성합니다.
3. `DocumentWorkspace`, `DocumentWorkspaceService`, 필요하면 `DocumentWorkspaceController`를 조립합니다.
4. Body를 수정한 쪽이 `SetDirty()`를 호출합니다.
5. 사용자 UI는 Controller의 `NeedLoc`, `PendingUser` 같은 결과를 해석합니다.

아래 `기본 흐름`, `Service와 Controller`, `기본 Handler` 섹션에 실제 코드가 이어집니다.

## 핵심 모델

```text
Document   = TypeID, Version, Name 같은 문서 metadata
Body       = 사용자가 편집하고 저장할 문서 본문
Location   = 문서를 열거나 저장할 외부 위치 또는 참조
Session    = 열린 Document + Body + Location + Dirty 상태
Handler    = 특정 TypeID의 create/open/save/recovery 규칙
Workspace  = 열린 Session 목록
Service    = create/open/save/close/recovery 저수준 실행
Controller = 사용자-facing open/save/close 흐름 해석
Recovery   = host lifecycle boundary 이후 Session을 다시 만들기 위한 기록
```

`Body`는 runtime 결과물이 아니라 원본 authoring document입니다.
문서로 유지해야 하는 editor memo, annotation, export setting 같은 정보는 Body에 들어갈 수 있습니다.
selection, scroll, focused tab 같은 UI 상태는 core document body가 아니라 view/editor state로 분리합니다.

## 저장 구조

Xeri document system은 하나의 파일 구조를 강제하지 않습니다.
같은 `DocumentWorkspaceService`를 쓰더라도 저장 구조는 handler가 결정합니다.

| Handler | 저장 root | metadata 저장 위치 | 주 용도 |
|---|---|---|---|
| `RawTextHandler` | raw string | handler + location | `.txt`, `.cs`, `.lua`, `.py` 같은 텍스트 문서 |
| `BodySerializedHandler<TBody, TLocation>` | `TBody` | handler + location | 기존 JSON/XML body root를 유지해야 하는 문서 |
| `EnvelopeSerializedHandler<TBody, TLocation>` | `DocumentEnvelope<TBody>` | 파일 내부 envelope | Xeri native metadata + body 문서 |

`DocumentEnvelope<TBody>`는 Xeri native serialized document 형식입니다.
모든 document가 envelope를 가져야 한다는 뜻이 아닙니다.

## 기본 흐름

문서 열기와 저장은 세 단계로 나뉩니다.

```text
Location
  ↕ IDataReader / IDataWriter
serialized string
  ↕ ISerializer or raw string policy
Body or DocumentEnvelope<TBody>
  ↕ IDocumentHandler
DocumentSession<TBody>
```

`IDataReader`와 `IDataWriter`는 파일, Resources, Addressables 같은 외부 저장소에서 문자열을 읽고 씁니다.
`ISerializer`는 객체와 문자열 사이의 변환을 담당합니다.
`IDocumentHandler`는 이 둘을 조합해서 `IDocumentSession`을 만들거나 저장합니다.

Handler는 JSON/XML 문자열을 직접 조립하지 않습니다.
pretty print, XML declaration, escaping, field naming 같은 출력 정책은 serializer 구현이 담당합니다.

## Session

`IDocumentSession`은 열린 문서 하나입니다.
typed body가 필요하면 `IDocumentSession<TBody>`를 사용합니다.

```csharp
var document = new Document("sample.document", "1", "Untitled");
var body = new SampleDocumentBody();
var session = new DocumentSession<SampleDocumentBody>(document, body, null);
```

body 변경은 자동 감지하지 않습니다.
편집한 쪽이 명시적으로 dirty를 표시해야 합니다.

```csharp
if (session is IDocumentSession<SampleDocumentBody> typedSession)
{
   typedSession.Body.Title = "Changed";
   typedSession.SetDirty();
}
```

`SetDocument`, `SetLocation`, `SetDirty`, `ClearDirty`는 session 상태 변경의 단일 진입점입니다.
같은 값으로 다시 설정하면 change event를 발생시키지 않습니다.

## Service와 Controller

`DocumentWorkspaceService`는 저수준 실행 API입니다.
입력이 부족하면 실패를 반환하고, 사용자에게 무엇을 물어볼지는 결정하지 않습니다.

```csharp
var workspace = new DocumentWorkspace();
var service = new DocumentWorkspaceService(workspace, handlers);

var create = service.Create("sample.document", "Untitled");
if (!create.Success) return;

var session = create.Session;
```

`DocumentWorkspaceController`는 사용자 흐름을 해석합니다.
예를 들어 location이 없는 새 문서에서 Save를 호출하면 service는 저장할 수 없지만, controller는 UI가 파일 위치를 요청할 수 있도록 `NeedLoc` 결과를 반환합니다.

```csharp
var controller = new DocumentWorkspaceController(service);

var save = controller.Save(session);
if (save.NeedLoc)
{
   save = controller.SaveAs(session, new FileDocumentLocation("C:/Temp/sample.json"));
}
```

Controller는 파일 패널이나 확인 창을 직접 띄우지 않습니다.
`NeedLoc`, `PendingUser`, `AlreadyOpen` 같은 결과를 보고 실제 UI를 처리하는 책임은 editor/view 쪽에 있습니다.

## 저장 의미

| Flow | 의미 | session location | dirty |
|---|---|---|---|
| `Save` | 현재 기준 location에 저장 | 유지 | 성공 시 해제 |
| `SaveAs` | 새 기준 location으로 저장 | 새 location으로 변경 | 성공 시 해제 |
| `SaveTo` | 다른 location에 복사 저장 | 유지 | 유지 |

`SaveTo`는 native save 계약을 사용한 복사 저장입니다.
다른 포맷으로 변환하는 import/export, runtime build, export pipeline은 별도 흐름입니다.

## Handler 계약

문서 타입 하나는 하나의 `IDocumentHandler`가 담당합니다.
Handler는 TypeID별 처리 규칙이며, 개별 session 상태를 내부에 저장하지 않습니다.

Handler의 기본 계약:

- `TypeID`는 비어 있으면 안 됩니다.
- `Create` 성공 session은 `Document`와 `Body`를 가져야 합니다.
- `Open` 성공 session은 `Document`, `Body`, 요청 `Location`을 가져야 합니다.
- 반환 session의 `Document.TypeID`는 handler의 `TypeID`와 같아야 합니다.
- `Save`는 session의 `Location`이나 `IsDirty`를 직접 바꾸지 않습니다.
- save 이후의 `Location` 변경과 dirty 해제는 `DocumentWorkspaceService`가 처리합니다.

## 기본 Handler

### Raw Text

`RawTextHandler`는 serializer를 사용하지 않고 문자열을 그대로 열고 저장합니다.
body 타입은 `RawTextDocumentBody`입니다.

```csharp
var handler = RawTextHandler.CreateForFile
(
   "sample.raw_text",
   "1"
);
```

이 handler는 파일 내부에 metadata를 쓰지 않습니다.
open 시 `Document`는 handler의 `TypeID`, `Version`, location 이름으로 구성됩니다.

### Body Serialized

`BodySerializedHandler<TBody, TLocation>`는 body 자체를 serializer root로 사용합니다.
기존 JSON/XML 구조를 유지해야 할 때 사용합니다.

```csharp
var handler = BodySerializedHandler.CreateForFile<SampleDocumentBody>
(
   "sample.body",
   "1",
   UnityJsonSerializer.Pretty,
   name => new SampleDocumentBody()
);
```

저장 결과는 `TBody` root입니다.
파일 내부에 Xeri metadata가 들어가지 않으므로, open 시 `Document`는 handler의 `TypeID`, `Version`, location 이름으로 구성됩니다.

### Envelope Serialized

`EnvelopeSerializedHandler<TBody, TLocation>`는 `DocumentEnvelope<TBody>`를 serializer root로 사용합니다.
Xeri metadata와 body를 같은 파일에 보관해야 할 때 사용합니다.

```csharp
var handler = EnvelopeSerializedHandler.CreateForFile<SampleDocumentBody>
(
   "sample.envelope",
   "1",
   UnityJsonSerializer.Pretty,
   name => new SampleDocumentBody()
);
```

Xeri native JSON 예시:

```json
{
  "Metadata": {
    "TypeID": "sample.envelope",
    "Version": "1",
    "Name": "Sample"
  },
  "Body": {
  }
}
```

Xeri native XML 예시:

```xml
<Document>
  <Metadata TypeID="sample.envelope" Version="1" Name="Sample" />
  <Body>
  </Body>
</Document>
```

파일 포맷 이름은 `Document`, `Metadata`, `Body`, `TypeID`, `Version`, `Name`을 사용합니다.
`_TypeID` 같은 C# 내부 필드명은 파일 포맷에 노출하지 않습니다.

## 생성 정책

새 문서 생성이 필요한 handler는 `DocumentBodyCreator<TBody>`를 주입받습니다.

```csharp
name => new SampleDocumentBody()
```

body 생성 정책을 넘기지 않은 serialized handler는 `Create`를 실패로 반환할 수 있습니다.
이는 open/save/recovery 전용 handler를 허용하기 위한 계약입니다.
`TBody : new()` 제약은 기본 계약이 아니며, 필요할 때 convenience factory에서만 선택합니다.

## Location

`IDocumentLocation`은 문서를 열거나 저장할 외부 위치입니다.
core 기본 구현에는 file, memory, object reference 계열 location이 있습니다.

file preset handler의 `CreateForFile`은 `FileDocumentLocation`을 `TextFileIO.Default`가 사용하는 파일 경로 문자열로 mapping합니다.
파일이 아닌 저장소를 쓰려면 handler 생성자에 reader, writer, location mapper를 직접 주입합니다.

같은 location으로 이미 열린 session을 다시 열려고 하면 controller/service 흐름에서 기존 session을 반환할 수 있습니다.
이는 일반 editor UX에서 같은 파일을 중복 tab으로 열지 않는 동작을 지원하기 위한 것입니다.

## Recovery

Recovery는 Unity domain reload나 editor window 재생성 뒤에 열린 session을 다시 만들기 위한 기능입니다.
문서 저장 포맷이 아니며, `Open`/`Save`를 대체하지 않습니다.

외부 API는 문자열 기반입니다.

```csharp
var record = service.RecordRecovery();
if (record.Success)
{
   EditorPrefs.SetString("SampleWorkspace", record.Record);
}

var recover = service.Recover(EditorPrefs.GetString("SampleWorkspace"));
```

core는 workspace recovery record DTO를 Unity JSON 문자열로 변환해서 반환합니다.
host/editor adapter는 그 문자열을 `EditorPrefs`, `SessionState`, 임시 파일 등 domain reload 밖에 보관하면 됩니다.

Recovery 책임 분리는 다음과 같습니다.

- Workspace recovery: session record 목록을 조립하고 복구합니다.
- Session recovery: dirty 여부, document, body record, location record를 조합합니다.
- Handler recovery: handler가 담당하는 body를 record로 만들고 body session을 복구합니다.
- Location recovery: location 자체가 복구 가능한 record를 제공합니다.

`FileDocumentLocation`은 recovery를 지원합니다.
memory location과 object reference location은 일반적으로 domain reload 이후 같은 값을 보장할 수 없으므로 기본 recovery 대상으로 보지 않습니다.
복구 record를 만들 수 없는 session이 있으면 `RecordRecovery()`는 실패 응답을 반환하고, 가능한 정보는 응답에 남깁니다.

## Workspace 책임

`DocumentWorkspace`는 열린 session 목록과 add/remove 이벤트만 관리합니다.

Workspace가 직접 하지 않는 것:

- active session 관리
- selected/focused session 관리
- save/open 의미 해석
- dirty session 목록 캐싱
- view state 저장
- file dialog나 사용자 확인 UI

이 값들은 editor/view/controller layer에서 관리합니다.

## 확장 지점

새 문서 타입은 먼저 `IDocumentHandler`로 추가합니다.
저장 구조가 raw text, body root, envelope 중 하나와 맞으면 built-in handler를 조합해서 시작합니다.
맞지 않는 경우에만 custom handler를 작성합니다.

새 저장 위치는 `IDocumentLocation`과 reader/writer 조합으로 추가합니다.
handler가 지원하는 location을 IO location으로 mapping할 수 있으면 기존 handler를 재사용할 수 있습니다.

다음 기능은 document core에 미리 넣지 않습니다.

- import/export
- runtime build/export pipeline
- undo/redo edit layer
- view/editor state
- resolver 기반 자동 문서 타입 판별
- LSP, syntax highlight, inspector UI 같은 view 기능

## 관련 문서

- [Document Workspace 구성하기](../../../Documentation~/guides/workspace/build-document-workspace.md)
- [Workspace](../../../Documentation~/modules/workspace/workspace.md)
- [Xeri Fabric IO](../../IO/README.md)
- [Workspace Document 유지보수 지침](../../../Documentation~/maintainers/workspace-document.md)
