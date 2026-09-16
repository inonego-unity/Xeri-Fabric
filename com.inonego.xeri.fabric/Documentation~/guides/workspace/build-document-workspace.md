# Document Workspace 구성하기

이 가이드는 직렬화 가능한 문서 Body를 만들고 Xeri Fabric Workspace에서 Create, Edit, Save, Close하는 최소 흐름을 설명합니다.

## 목적

파일 IO, 문서 Session, 사용자 Save/Close 흐름과 UI를 분리해서 같은 문서 작업 규칙을 EditorWindow, Runtime Tool, 테스트에서 재사용합니다.

## 1. 문서 Body 정의

```csharp
using System;

[Serializable]
public sealed class SampleDocumentBody
{
    public string Title;
    public string Content;
}
```

selection, scroll, focused tab 같은 View 상태는 Body에 넣지 않는 편이 좋습니다.

## 2. Handler 구성

```csharp
using inonego.Xeri.Serializable;
using inonego.Xeri.Workspace.Document;

var handler = BodySerializedHandler.CreateForFile<SampleDocumentBody>
(
    "sample.document",
    "1",
    UnityJsonSerializer.Pretty,
    name => new SampleDocumentBody { Title = name }
);
```

파일 내부에 Xeri metadata까지 저장해야 한다면 `EnvelopeSerializedHandler`를 선택합니다.

## 3. Workspace와 Controller 조립

```csharp
var workspace = new DocumentWorkspace();
var service = new DocumentWorkspaceService
(
    workspace,
    new IDocumentHandler[] { handler }
);
var controller = new DocumentWorkspaceController(service);
```

`Service`는 저수준 실행을, `Controller`는 사용자-facing Save/Close 의미를 해석합니다.

## 4. 문서 생성과 편집
```csharp
var create = controller.Create("sample.document", "Untitled");
if (!create.Success) return;

var session = (IDocumentSession<SampleDocumentBody>)create.Session;
session.Body.Content = "Edited";
session.SetDirty();
```

Body 변경은 자동 dirty 감지 대상이 아닙니다. 편집을 수행한 계층이 명시적으로 `SetDirty()`를 호출합니다.

## 5. 저장

```csharp
var save = controller.Save(session);
if (save.NeedLoc)
{
    var location = new FileDocumentLocation("C:/Temp/sample.json");
    save = controller.SaveAs(session, location);
}
```

`SaveAs`는 성공 뒤 Session의 기준 Location을 바꾸고 dirty를 해제합니다. `SaveTo`는 다른 위치에 복사하지만 기준 Location과 dirty 상태를 유지합니다.

## 6. 닫기
```csharp
var close = controller.Close(session);
if (close.PendingUser)
{
    // 프로젝트 UI가 저장/폐기/취소를 사용자에게 묻는다.
}
```

Dirty Session은 Controller가 임의로 닫지 않습니다. 변경 폐기가 확정되면 `CloseDiscardingChanges(session)`을 호출합니다.

## 7. Recovery

```csharp
var record = service.RecordRecovery();
if (record.Success)
{
    string recoveryText = record.Record;
    // EditorPrefs, SessionState, 임시 파일 등 Host가 보관한다.
}
```

복구 시 같은 Service의 `Recover(recoveryText)`를 사용합니다.

## 관련 문서

- [Workspace](../../modules/workspace/workspace.md)
- [IO](../../modules/io/io.md)
- [Runtime Document README](../../../Runtime/Workspace/Document/README.md)
