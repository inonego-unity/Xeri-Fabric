/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : BodySerializedHandler.cs
수정일 : 2026-11-14

# 설명
DocumentEnvelope 없이 body 자체를 serializer root로 열고 저장하는 document handler를 정의한다.
기존 IO Mapping과 RecordPersistence의 검증·ReleaseHandle 수명 처리 기능을 조합한다.
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
    /// Body-root serialized handler preset factory.
    /// </summary>
    // ============================================================
    public static class BodySerializedHandler
    {

    #region 생성

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> FileDocumentLocation을 읽고, 임시 파일 교체 방식으로 저장하는
        /// <br/> body-root serialized handler를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static BodySerializedHandler<TBody, string> CreateForFile<TBody>
        (
            string _typeID,
            string version,
            ISerializer serializer,
            DocumentBodyCreator<TBody> createBody = null
        )
        where TBody : class
        {
            return new BodySerializedHandler<TBody, string>
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
    /// <br/> Body 자체를 serialized document root로 사용하는 document handler.
    /// <br/> Document metadata는 파일 내부에 없으므로 handler의 TypeID/Version과
    /// <br/> location 이름으로 session을 구성한다.
    /// </summary>
    /// <typeparam name="TBody">문서 body 타입.</typeparam>
    /// <typeparam name="TLocation">reader/writer가 실제로 사용하는 IO location 타입.</typeparam>
    // ================================================================================
    [Serializable]
    public sealed class BodySerializedHandler<TBody, TLocation> : IDocumentHandler
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


        private readonly RecordPersistence<TLocation, TBody> persistence = null;
        private readonly SerializedDocumentBodyRecovery<TBody> recovery = null;
        private readonly DocumentLocationMapper<TLocation> mapLocation = null;
        private readonly DocumentBodyCreator<TBody> createBody = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// BodySerializedHandler를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public BodySerializedHandler
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

            // 변환은 기존 IO Mapping, 기록 검증과 수명 종료는 Persistence가 담당한다.
            persistence = new RecordPersistence<TLocation, TBody>
            (
                new MappedDataReader<TLocation, string, TBody>
                (
                    reader,
                    serializer.Deserialize<TBody>
                ),
                new MappedDataWriter<TLocation, TBody, string>
                (
                    writer,
                    serializer.Serialize
                )
            );
        }

    #endregion

    #region 생성

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 이름으로 새 body-root document session을 생성한다.
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

                var document = new Document(TypeID, Version, name);
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
        /// 지정한 location을 body-root document session으로 연다.
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

            // IO Mapping의 변환 결과를 Persistence에서 검증하고 ReleaseHandle을 종료한다.
            var response = persistence.Load(ioLoc);

            if (!response.Success)
            {
                return DocumentOpenResponse.Fail(response.Error);
            }

            try
            {
                var document = new Document(TypeID, Version, loc.Name);
                var session = new DocumentSession<TBody>(document, response.Value, loc);

                return DocumentOpenResponse.Succeed(session);
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
        /// 지정한 body-root document session을 location에 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        public DocumentSaveResponse Save(IDocumentSession session, IDocumentLocation loc)
        {
            if (!TryGetBody(session, out var body))
            {
                return DocumentSaveResponse.Fail("session을 body-root document로 저장할 수 없습니다.");
            }

            if (!mapLocation(loc, out var ioLoc))
            {
                return DocumentSaveResponse.Fail("location을 IO location으로 변환할 수 없습니다.");
            }

            var response = persistence.Save(ioLoc, body);
            return response.Success ? DocumentSaveResponse.Succeed() : DocumentSaveResponse.Fail(response.Error);
        }

    #endregion

    #region Recovery

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 body-root session body를 recovery record로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        public DocumentBodyRecoveryRecord RecordSessionBody(IDocumentSession session)
        {
            if (!TryGetBody(session, out var body))
            {
                return null;
            }

            return recovery.Record(body);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 지정한 document, body record, location에서 body-root session을 복구한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public DocumentOpenResponse RecoverSession
        (
            IDocument document,
            DocumentBodyRecoveryRecord bodyRecord,
            IDocumentLocation loc
        )
        {
            if (!HasRecoverableRecord(document, bodyRecord))
            {
                return DocumentOpenResponse.Fail("body-root recovery record를 복구할 수 없습니다.");
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

    #endregion

    #region Body

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

    #endregion

    }
}
