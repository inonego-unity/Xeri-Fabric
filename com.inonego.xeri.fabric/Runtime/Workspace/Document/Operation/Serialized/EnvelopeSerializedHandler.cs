/* BLOCK_HEADER_BEGIN ===============================================================================================
파일명 : EnvelopeSerializedHandler.cs
수정일 : 2026-07-30

# 설명
DocumentEnvelope 기반 Xeri native serialized document를 생성, 열기, 저장, 복구하는 concrete handler를 정의한다.
Storage structure와 serializer 정책을 중심으로 두고, IO transport와 location mapping은 주입받아 조합한다.
================================================================================================= BLOCK_HEADER_END */

using System;

using inonego.Xeri.IO;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.Workspace.Document
{
   // ==========================================================================================
   /// <summary>
   /// Document location을 IO location으로 변환하는 함수 계약.
   /// </summary>
   /// <typeparam name="TLocation">reader/writer가 실제로 사용하는 IO location 타입.</typeparam>
   // ==========================================================================================
   public delegate bool DocumentLocationMapper<TLocation>(IDocumentLocation loc, out TLocation ioLoc);

   // ==========================================================================================
   /// <summary>
   /// 새 문서 생성 시 사용할 body를 만드는 함수 계약.
   /// </summary>
   /// <typeparam name="TBody">문서 body 타입.</typeparam>
   // ==========================================================================================
   public delegate TBody DocumentBodyCreator<out TBody>(string name);

   // ============================================================
   /// <summary>
   /// Envelope serialized handler preset factory.
   /// </summary>
   // ============================================================
   public static class EnvelopeSerializedHandler
   {
   #region 생성

      // ------------------------------------------------------------------------------------------------
      /// <summary>
      /// FileDocumentLocation과 TextFileIO 기본 인스턴스를 사용하는 envelope serialized handler를 생성한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------------
      public static EnvelopeSerializedHandler<TBody, string> CreateForFile<TBody>
      (
         string _TypeID,
         string version,
         ISerializer serializer,
         DocumentBodyCreator<TBody> createBody = null
      )
      where TBody : class
      {
         return new EnvelopeSerializedHandler<TBody, string>
         (
            _TypeID,
            version,
            serializer,
            TextFileIO.Default,
            TextFileIO.Default,
            TryMapFileLocation,
            createBody
         );
      }

      // --------------------------------------------------------------------------------
      /// <summary>
      /// FileDocumentLocation을 파일 경로 문자열로 변환한다.
      /// </summary>
      // --------------------------------------------------------------------------------
      private static bool TryMapFileLocation(IDocumentLocation loc, out string ioLoc)
      {
         if (loc is FileDocumentLocation fileLoc)
         {
            ioLoc = fileLoc.Path;
            return true;
         }

         ioLoc = null;
         return false;
      }

   #endregion

   }

   // ========================================================================================================
   /// <summary>
   /// <br/> DocumentEnvelope 기반 Xeri native serialized document handler.
   /// <br/> IO transport, serializer, location mapping, create policy를 생성자에서 주입받아 동작한다.
   /// </summary>
   /// <typeparam name="TBody">문서 body 타입.</typeparam>
   /// <typeparam name="TLocation">reader/writer가 실제로 사용하는 IO location 타입.</typeparam>
   // ========================================================================================================
   [Serializable]
   public class EnvelopeSerializedHandler<TBody, TLocation> : IDocumentHandler
   where TBody : class
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 담당하는 문서 종류 식별자.
      /// </summary>
      // ------------------------------------------------------------
      public string TypeID => _TypeID;

      private readonly string _TypeID = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 생성하는 문서 버전.
      /// </summary>
      // ------------------------------------------------------------
      public string Version => version;

      private readonly string version = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// Document envelope와 문자열 사이를 변환하는 serializer.
      /// </summary>
      // ------------------------------------------------------------
      public ISerializer Serializer => serializer;

      private readonly ISerializer serializer = null;

      // ------------------------------------------------------------
      /// <summary>
      /// IO location에서 serialized text를 읽는 reader.
      /// </summary>
      // ------------------------------------------------------------
      public IDataReader<TLocation, string> Reader => reader;

      private readonly IDataReader<TLocation, string> reader = null;

      // ------------------------------------------------------------
      /// <summary>
      /// IO location에 serialized text를 쓰는 writer.
      /// </summary>
      // ------------------------------------------------------------
      public IDataWriter<TLocation, string> Writer => writer;

      private readonly IDataWriter<TLocation, string> writer = null;

      private readonly DocumentLocationMapper<TLocation> mapLocation = null;
      private readonly DocumentBodyCreator<TBody> createBody = null;

   #endregion

   #region 생성자

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// EnvelopeSerializedHandler를 생성한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      public EnvelopeSerializedHandler
      (
         string _TypeID,
         string version,
         ISerializer serializer,
         IDataReader<TLocation, string> reader,
         IDataWriter<TLocation, string> writer,
         DocumentLocationMapper<TLocation> mapLocation,
         DocumentBodyCreator<TBody> createBody = null
      ) : base()
      {
         if (string.IsNullOrEmpty(_TypeID))
         {
            throw new ArgumentException("문서 종류 식별자가 비어 있습니다.", nameof(_TypeID));
         }

         if (string.IsNullOrEmpty(version))
         {
            throw new ArgumentException("문서 버전이 비어 있습니다.", nameof(version));
         }

         this._TypeID         = _TypeID;
         this.version         = version;
         this.serializer      = serializer ?? throw new ArgumentNullException(nameof(serializer));
         this.reader          = reader ?? throw new ArgumentNullException(nameof(reader));
         this.writer          = writer ?? throw new ArgumentNullException(nameof(writer));
         this.mapLocation     = mapLocation ?? throw new ArgumentNullException(nameof(mapLocation));
         this.createBody      = createBody;
      }

   #endregion

   #region 생성

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 이름으로 새 문서 세션을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentCreateResponse Create(string name)
      {
         if (createBody == null)
         {
            return DocumentCreateResponse.Fail("문서 생성을 지원하지 않습니다.");
         }

         if (string.IsNullOrEmpty(name))
         {
            return DocumentCreateResponse.Fail("문서 이름이 비어 있습니다.");
         }

         try
         {
            var body = createBody(name);
            if (body == null)
            {
               return DocumentCreateResponse.Fail("생성된 문서 body가 없습니다.");
            }

            var document = CreateDocument(name);
            var session = new DocumentSession<TBody>(document, body, null);

            return DocumentCreateResponse.Succeed(session);
         }
         catch (Exception exception)
         {
            return DocumentCreateResponse.Fail(exception.Message);
         }
      }

   #endregion

   #region 열기

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 location을 문서 세션으로 연다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentOpenResponse Open(IDocumentLocation loc)
      {
         if (loc == null)
         {
            return DocumentOpenResponse.Fail("열 location이 없습니다.");
         }

         if (!mapLocation(loc, out var ioLoc))
         {
            return DocumentOpenResponse.Fail("location을 IO location으로 변환할 수 없습니다.");
         }

         Lease lease = null;

         try
         {
            var readResponse = reader.Read(ioLoc);
            lease = readResponse.Lease;

            if (!readResponse.Success)
            {
               return DocumentOpenResponse.Fail(readResponse.Error);
            }

            var envelope = serializer.Deserialize<DocumentEnvelope<TBody>>(readResponse.Value);
            if (envelope == null || envelope.Metadata == null || envelope.Body == null)
            {
               return DocumentOpenResponse.Fail("역직렬화된 document envelope가 올바르지 않습니다.");
            }

            return CreateOpenResponse(envelope, loc);
         }
         catch (Exception exception)
         {
            return DocumentOpenResponse.Fail(exception.Message);
         }
         finally
         {
            // Envelope handler는 serialized text를 body로 복사한 뒤 원본 IO 수명을 보관하지 않는다.
            lease?.Dispose();
         }
      }

   #endregion

   #region 저장

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 문서 세션을 location에 저장한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSaveResponse Save(IDocumentSession session, IDocumentLocation loc)
      {
         if (!TryCreateEnvelope(session, out var envelope))
         {
            return DocumentSaveResponse.Fail("session을 지정 location에 저장할 수 없습니다.");
         }

         if (!mapLocation(loc, out var ioLoc))
         {
            return DocumentSaveResponse.Fail("location을 IO location으로 변환할 수 없습니다.");
         }

         try
         {
            var documentText = serializer.Serialize(envelope);
            var writeResponse = writer.Write(ioLoc, documentText);

            if (!writeResponse.Success)
            {
               return DocumentSaveResponse.Fail(writeResponse.Error);
            }

            return DocumentSaveResponse.Succeed();
         }
         catch (Exception exception)
         {
            return DocumentSaveResponse.Fail(exception.Message);
         }
      }

   #endregion

   #region Recovery

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 문서 세션의 body를 recovery record로 만든다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentBodyRecoveryRecord RecordSessionBody(IDocumentSession session)
      {
         if (!TryGetBody(session, out var body))
         {
            return null;
         }

         if (session?.Document == null || session.Document.TypeID != TypeID)
         {
            return null;
         }

         try
         {
            return new DocumentBodyRecoveryRecord(serializer.Serialize(body));
         }
         catch
         {
            return null;
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 document, body record, location에서 문서 세션을 복구한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentOpenResponse RecoverSession
      (
         IDocument document,
         DocumentBodyRecoveryRecord bodyRecord,
         IDocumentLocation loc
      )
      {
         if (!HasRecoverableRecord(document, bodyRecord))
         {
            return DocumentOpenResponse.Fail("session recovery body record를 복구할 수 없습니다.");
         }

         try
         {
            var body = serializer.Deserialize<TBody>(bodyRecord.Record);
            if (body == null)
            {
               return DocumentOpenResponse.Fail("recovery body record가 비어 있습니다.");
            }

            var session = new DocumentSession<TBody>(document, body, loc);

            return DocumentOpenResponse.Succeed(session);
         }
         catch (Exception exception)
         {
            return DocumentOpenResponse.Fail(exception.Message);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 session을 document envelope로 변환한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool TryCreateEnvelope(IDocumentSession session, out DocumentEnvelope<TBody> envelope)
      {
         envelope = null;

         if (session?.Document == null || session.Document.TypeID != TypeID)
         {
            return false;
         }

         if (session is not IDocumentSession<TBody> typedSession)
         {
            return false;
         }

         if (typedSession.Body == null)
         {
            return false;
         }

         envelope = new DocumentEnvelope<TBody>(session.Document, typedSession.Body);

         return true;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 session에서 typed body를 꺼낸다.
      /// </summary>
      // ------------------------------------------------------------
      private bool TryGetBody
      (
         IDocumentSession session,
         out TBody body
      )
      {
         body = null;

         if (session?.Document == null || session.Document.TypeID != TypeID)
         {
            return false;
         }

         if (session is not IDocumentSession<TBody> typedSession)
         {
            return false;
         }

         body = typedSession.Body;

         return body != null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Recovery record가 이 handler의 최소 입력 계약을 만족하는지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      private bool HasRecoverableRecord
      (
         IDocument document,
         DocumentBodyRecoveryRecord bodyRecord
      )
      {
         if (document == null || bodyRecord == null)
         {
            return false;
         }

         if (document.TypeID != TypeID)
         {
            return false;
         }

         return string.IsNullOrEmpty(bodyRecord.Record) == false;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Document envelope와 location에서 문서 열기 응답을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private DocumentOpenResponse CreateOpenResponse
      (
         DocumentEnvelope<TBody> envelope,
         IDocumentLocation loc
      )
      {
         if (!TryCreateSession(envelope, loc, out var session))
         {
            return DocumentOpenResponse.Fail("document envelope를 session으로 변환할 수 없습니다.");
         }

         return DocumentOpenResponse.Succeed(session);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Document envelope와 location에서 문서 세션을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private bool TryCreateSession
      (
         DocumentEnvelope<TBody> envelope,
         IDocumentLocation loc,
         out DocumentSession<TBody> session
      )
      {
         session = null;

         if (envelope == null || envelope.Metadata == null || envelope.Body == null)
         {
            return false;
         }

         var document = envelope.Metadata.ToDocument();
         if (document == null || document.TypeID != TypeID)
         {
            return false;
         }

         session = new DocumentSession<TBody>(document, envelope.Body, loc);

         return true;
      }

   #endregion

   #region Document 생성

      // ------------------------------------------------------------
      /// <summary>
      /// 새 문서 생성 시 사용할 document 설명 정보를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private IDocument CreateDocument(string name)
      {
         return new Document(TypeID, Version, name);
      }

   #endregion

   }
}
