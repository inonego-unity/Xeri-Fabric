# Xeri IO

## 개요


`Runtime/IO`는 값을 어디서 읽고 어디에 쓸지를 추상화하는 작은 계층입니다.
파일, 메모리, Resources, Addressables 같은 입력원 차이를 상위 시스템이 직접 알지 않도록 합니다.

## 왜 필요한가

상위 시스템이 `File.ReadAllText`, `Resources.Load`, Addressables handle을 직접 섞어 쓰면 저장 위치가 바뀔 때 도메인 코드와 수명 처리까지 함께 바뀝니다. Xeri IO는 **어디서 읽는가**와 **무엇을 읽는가**를 `TLocation`/`TValue` 계약으로 분리하고, 외부 자원 수명이 있으면 `Lease`로 함께 전달합니다.

## 언제 사용하는가

- 같은 소비 코드에서 파일·메모리·Resources·Addressables 입력원을 교체할 때
- 테스트에서 실제 파일 대신 Memory IO를 주입하고 싶을 때
- 읽은 값이 외부 asset handle 수명에 묶이는 경우를 공통 응답으로 처리할 때
- serializer와 저장 위치 접근을 별도 계층으로 유지하고 싶을 때

프로젝트 코드가 단일 고정 파일 하나만 읽고 다른 저장 경계가 전혀 없다면 직접 IO가 더 단순할 수 있습니다.

```text
TLocation -> IDataReader<TLocation, TValue> -> ReadResponse<TValue>
TLocation + TValue -> IDataWriter<TLocation, TValue> -> WriteResponse
```

이 계층은 serializer, domain model, editor UI를 직접 책임지지 않습니다.
JSON/XML/YAML 같은 포맷 변환은 serializer가 맡고, IO는 이미 정해진 값 타입을 읽고 쓰는 역할만 합니다.

## 핵심 개념

| 개념 | 의미 |
|---|---|
| `TLocation` | 값을 찾거나 저장할 위치 타입. 예: file path `string`, `MemoryLocation<T>`, Addressables address |
| `TValue` | IO가 읽거나 쓰는 값 타입. 예: `string`, `byte[]`, `TextAsset`, domain object |
| `IDataReader<TLocation, TValue>` | location에서 value를 읽는 동기 계약 |
| `IDataWriter<TLocation, TValue>` | location에 value를 쓰는 동기 계약 |
| `IAsyncDataReader<TLocation, TValue>` | 비동기로 value를 읽는 계약 |
| `IAsyncDataWriter<TLocation, TValue>` | 비동기로 value를 쓰는 계약 |
| `ReadResponse<TValue>` | read 성공/실패, value, optional `Lease`를 담는 응답 |
| `WriteResponse` | write 성공/실패를 담는 응답 |
| `Lease` | 읽은 value의 수명을 유지하기 위해 함께 전달하는 일회 종료 책임 |

## 제공 구현

```text
Runtime/IO/
├── IDataReader.cs
├── IDataWriter.cs
├── Response/
│   ├── ReadResponse.cs
│   └── WriteResponse.cs
├── File/
│   ├── TextFileIO.cs
│   └── BinaryFileIO.cs
├── Memory/
│   ├── MemoryLocation.cs
│   └── MemoryIO.cs
├── Mapping/
│   ├── MappedDataReader.cs
│   └── AsyncMappedDataReader.cs
└── Unity/
    ├── ResourcesAssetReader.cs
    └── AddressablesAssetReader.cs
```

| 타입 | 계약 | 용도 |
|---|---|---|
| `TextFileIO` | `IDataReader<string, string>`, `IDataWriter<string, string>` | 파일 경로에서 UTF-8 문자열 읽기/쓰기 |
| `BinaryFileIO` | `IDataReader<string, byte[]>`, `IDataWriter<string, byte[]>` | 파일 경로에서 byte 배열 읽기/쓰기 |
| `MemoryLocation<T>` | location container | 메모리 안의 값을 IO location처럼 전달 |
| `MemoryIO<T>` | `IDataReader<MemoryLocation<T>, T>`, `IDataWriter<MemoryLocation<T>, T>` | 테스트, 임시 저장, 런타임 memory 저장 |
| `ResourcesAssetReader<TAsset>` | `IDataReader<string, TAsset>` | Unity Resources에서 asset 읽기 |
| `AddressablesAssetReader<TAsset>` | sync/async reader | Addressables에서 asset을 읽고 release Lease를 response로 전달 |
| `MappedDataReader` 계열 | reader adapter | 원본 reader의 response value를 필요한 타입으로 변환 |

## 기본 사용

### 문자열 파일 IO

```csharp
var io = TextFileIO.Default;
var path = "C:/Temp/sample.json";

var write = io.Write(path, "{\"name\":\"sample\"}");
if (!write.Success)
{
   return;
}

var read = io.Read(path);
if (!read.Success)
{
   return;
}

var text = read.Value;
```

파일 IO는 외부 수명이 없으므로 `read.Lease`는 `null`입니다.

### 메모리 IO

```csharp
var loc = new MemoryLocation<string>();
var io = MemoryIO<string>.Default;

var write = io.Write(loc, "serialized text");
if (!write.Success)
{
   return;
}

var read = io.Read(loc);
if (!read.Success)
{
   return;
}

var text = read.Value;
```

테스트, 캐시, 임시 편집 데이터처럼 파일을 만들 필요가 없는 흐름에서는 이 조합을 사용할 수 있습니다.

### Resources TextAsset을 string으로 읽기

```csharp
var assetReader = ResourcesAssetReader<TextAsset>.Default;
var textReader = new MappedDataReader<string, TextAsset, string>
(
   assetReader,
   asset => asset.text
);

var read = textReader.Read("Config/sample");
if (!read.Success)
{
   return;
}

var text = read.Value;
```

`ResourcesAssetReader<TextAsset>`는 `TextAsset`을 반환합니다.
호출자가 `string`을 원한다면 reader 구현을 새로 만들기보다 mapping adapter로 변환합니다.

### Addressables asset 읽기

```csharp
var reader = AddressablesAssetReader<TextAsset>.Default;
var read = await reader.ReadAsync("config/sample");

if (!read.Success)
{
   read.Lease?.Dispose();
   return;
}

var asset = read.Value;
var lease = read.Lease;
```

Addressables는 asset을 읽은 뒤 operation handle release가 필요합니다.
`AddressablesAssetReader<TAsset>`는 raw asset을 `Value`로 반환하고, release 책임은 `Lease`로 함께 전달합니다.

단기 사용이면 사용이 끝난 뒤 직접 release합니다.

```csharp
try
{
   var text = read.Value.text;
}
finally
{
   read.Lease?.Dispose();
}
```

장기 보관이면 `Value`를 보관하는 session/model/cache가 `Lease`도 함께 보관하고, 자신이 닫힐 때 Dispose합니다.

`Lease`는 `Success`와 독립적인 수명 책임입니다.
실패 응답이라도 Lease가 있으면 호출자가 즉시 Dispose하거나, 값을 소유할 객체가 함께 보관해야 합니다.

## Mapping과 Lease

`MappedDataReader`는 source response의 `Value`만 변환합니다.
source response에 `Lease`가 있으면 mapped response에도 같은 Lease를 유지합니다.

```text
ReadResponse<TSource>
-> map(source.Value)
-> ReadResponse<TValue>
   - Value = mapped value
   - Lease = source.Lease
```

따라서 `TextAsset.GetData<T>()`, `Texture2D.GetRawTextureData<T>()`처럼 asset 내부 buffer view를 반환하는 mapping도 Lease가 끊기지 않습니다.

독립 값으로 확실히 복사한 경우에도 기본 mapper는 Lease를 유지합니다.
조기 release 최적화는 기본 계약에 넣지 않습니다.

## 상위 시스템과 조합

IO는 보통 상위 시스템의 입력/출력 경계에서 조합됩니다.
상위 시스템은 필요한 값을 정하고, IO는 그 값이 어디서 오고 어디로 가는지만 담당합니다.

```text
external location
-> TLocation
-> IDataReader<TLocation, TValue>
-> ReadResponse<TValue>
-> caller/domain/serializer/UI
```

저장 흐름은 반대입니다.

```text
caller/domain/serializer/UI
-> TValue
-> IDataWriter<TLocation, TValue>
-> WriteResponse
```

예를 들어 문자열 기반 저장 흐름은 다음처럼 나눌 수 있습니다.

```text
domain object -> serializer -> string -> TextFileIO
TextFileIO -> string -> serializer -> domain object
```

IO는 `domain object`가 무엇인지, 문자열이 JSON인지 XML인지 알지 않습니다.
반대로 serializer는 문자열이 파일에서 왔는지 메모리에서 왔는지 알 필요가 없습니다.

## 확장 지점

- IO 구현은 `Runtime/IO` 안에서 특정 domain, editor UI, feature service를 참조하지 않습니다.
- serializer 포맷을 IO 타입 이름에 섞지 않습니다. 예: `JsonFileIO`보다 `TextFileIO` + `ISerializer` 조합을 우선합니다.
- location 타입은 "어디서 읽는가"를 표현하고, value 타입은 "무엇을 읽는가"를 표현합니다.
- reader와 writer는 필요할 때만 둘 다 구현합니다. 읽기 전용 입력원은 reader만 둡니다.
- Addressables처럼 release가 필요한 입력원은 `ReadResponse<TValue>.Lease`로 수명 책임을 전달합니다.
- 변환이 목적이면 새 reader를 만들기 전에 `MappedDataReader` 또는 `AsyncMappedDataReader` 조합을 검토합니다.
- async가 실제 입력원 계약이면 `IAsyncDataReader`/`IAsyncDataWriter`를 함께 구현합니다. sync 구현이 자연스럽지 않으면 억지로 sync를 만들지 않습니다.

## 관련 문서

- [소유권과 수명](../../Documentation~/concepts/ownership-and-lifetime.md)
- [IO 유지보수 지침](../../Documentation~/maintainers/io.md)
