/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : EnvelopeSerializedHandler.cs
수정일 : 2026-11-14

# 설명
DocumentEnvelope 기반 Xeri native serialized document를 생성, 열기, 저장, 복구하는 handler다.
문서 Metadata·Session 정책은 유지하고 IO Mapping 및 RecordPersistence로 공통 기록 검증·수명 처리를 수행한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Persistence;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.Workspace.Document
{
    // ============================================================
    /// <summary>
    /// Document location을 IO location으로 변환하는 함수 계약.
    /// </summary>
    /// <typeparam name="TLocation">reader/writer가 실제로 사용하는 IO location 타입.</typeparam>
    // ============================================================
    public delegate bool DocumentLocationMapper<TLocation>(IDocumentLocation loc, out TLocation ioLoc);

    // ============================================================
    /// <summary>
    /// 새 문서 생성 시 사용할 body를 만드는 함수 계약.
    /// </summary>
    /// <typeparam name="TBody">문서 body 타입.</typeparam>
    // ============================================================
    public delegate TBody DocumentBodyCreator<out TBody>(string name);

    // ============================================================
    /// <summary>
    /// Envelope serialized handler preset factory.
    /// </summary>
    // ============================================================
    public static class EnvelopeSerializedHandler
    {

    #region 생성

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> FileDocumentLocation을 읽고, 임시 파일 교체 방식으로 저장하는
        /// <br/> envelope serialized handler를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static EnvelopeSerializedHandler<TBody, string> CreateForFile<TBody>
        (
            string _typeID,
            string version,
            ISerializer serializer,
            DocumentBodyCreator<TBody> createBody = null
        )
        where TBody : class
        {
            return new EnvelopeSerializedHandler<TBody, string>
            (
                _typeID,
                version,
                serializer,
                TextFileIO.Default,
                new FileCommitWriter<string>(TextFileIO.Default),
                TryMapFileLocation,
                createBody
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// FileDocumentLocation을 파일 경로 문자열로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
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

    // ================================================================================
    /// <summary>
    /// <br/> DocumentEnvelope 기반 Xeri native serialized document handler.
    /// <br/> IO transport, serializer, location mapping, create policy를 생성자에서
    /// <br/> 주입받아 동작한다.
    /// </summary>
    /// <typeparam name="TBody">문서 body 타입.</typeparam>
    /// <typeparam name="TLocation">reader/writer가 실제로 사용하는 IO location 타입.</typeparam>
    // ================================================================================
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
        public string TypeID => recordType.TypeID;

        private readonly RecordType recordType = null;

        // ------------------------------------------------------------
        /// <summary>
        /// Handler가 생성하는 문서 버전.
        /// </summary>
        // ------------------------------------------------------------
        public string Version => recordType.Version;

        private readonly RecordPersistence<TLocation, DocumentEnvelope<TBody>> persistence = null;

        private readonly SerializedDocumentBodyRecovery<TBody> recovery = null;
        private readonly DocumentLocationMapper<TLocation> mapLocation = null;
        private readonly DocumentBodyCreator<TBody> createBody = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// EnvelopeSerializedHandler를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public EnvelopeSerializedHandler
        (
            string _typeID,
            string version,
            ISerializer serializer,
            IDataReader<TLocation, string> reader,
            IDataWriter<TLocation, string> writer,
            DocumentLocationMapper<TLocation> mapLocation,
            DocumentBodyCreator<TBody> createBody = null
        ) : base()
        {
            // 문서 종류와 버전 입력은 공통 RecordType에서 검증한다.
            this.recordType = new RecordType(_typeID, version);
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            this.mapLocation = mapLocation ?? throw new ArgumentNullException(nameof(mapLocation));
            this.createBody = createBody;
            this.recovery = new SerializedDocumentBodyRecovery<TBody>(serializer);

            // 범용 IO Mapping을 재사용하고 기록 유효성·IO ReleaseHandle 수명을 공통 Persistence에 위임한다.
            persistence = new RecordPersistence<TLocation, DocumentEnvelope<TBody>>
            (
                new MappedDataReader<TLocation, string, DocumentEnvelope<TBody>>
                (
                    reader,
                    serializer.Deserialize<DocumentEnvelope<TBody>>
                ),
                new MappedDataWriter<TLocation, DocumentEnvelope<TBody>, string>
                (
                    writer,
                    serializer.Serialize
                ),
                ValidateEnvelope
            );
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

            // 읽기/변환/검증 후 원본 IO ReleaseHandle 해제는 Persistence가 맡는다.
            var response = persistence.Load(ioLoc);

            if (!response.Success)
            {
                return DocumentOpenResponse.Fail(response.Error);
            }

            try
            {
                return CreateOpenResponse(response.Value, loc);
            }
            catch (Exception exception)
            {
                return DocumentOpenResponse.Fail(exception.Message);
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

            var response = persistence.Save(ioLoc, envelope);
            return response.Success ? DocumentSaveResponse.Succeed() : DocumentSaveResponse.Fail(response.Error);
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

            if (!DocumentMetadataValidation.IsMatch(session.Document, recordType))
            {
                return null;
            }

            return recovery.Record(body);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지정한 document, body record, location에서 문서 세션을 복구한다.
        /// </summary>
        // ----------------------------------------------------------------------
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
                var body = recovery.Recover(bodyRecord);

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

            if (!DocumentMetadataValidation.IsMatch(session?.Document, recordType))
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
        /// 문서 envelope의 필수 metadata와 body가 존재하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ValidateEnvelope(DocumentEnvelope<TBody> envelope)
        {
            // 공통 Record/Metadata 계약에서 Body와 종류·버전을 함께 검증한다.
            recordType.Validate(envelope);

            if (!envelope.HasBody)
            {
                throw new InvalidOperationException("Document Body가 직렬화 기록에 존재하지 않습니다.");
            }

            if (string.IsNullOrWhiteSpace(envelope.Metadata.Name))
            {
                throw new InvalidOperationException("document envelope의 Name이 비어 있습니다.");
            }
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

            if (!DocumentMetadataValidation.IsMatch(session?.Document, recordType))
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// Recovery record가 이 handler의 최소 입력 계약을 만족하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private bool HasRecoverableRecord
        (
            IDocument document,
            DocumentBodyRecoveryRecord bodyRecord
        )
        {
            return DocumentMetadataValidation.IsMatch(document, recordType) &&
                   bodyRecord != null &&
                   !string.IsNullOrEmpty(bodyRecord.Record);
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
            if (!DocumentMetadataValidation.IsMatch(document, recordType))
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
