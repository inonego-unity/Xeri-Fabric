/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RawTextHandler.cs
수정일 : 2026-11-14

# 설명
문자열 파일을 serializer 없이 그대로 열고 저장하는 document handler를 정의한다.
IO Mapping과 RecordPersistence로 읽기 수명·기록 검증 흐름을 공유하되, 텍스트 파일에 Envelope를 강제하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Persistence;

namespace inonego.Xeri.Workspace.Document
{
    // ============================================================
    /// <summary>
    /// Raw text document handler preset factory.
    /// </summary>
    // ============================================================
    public static class RawTextHandler
    {

    #region 생성

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> FileDocumentLocation을 읽고, 임시 파일 교체 방식으로 저장하는 raw
        /// <br/> text handler를 생성한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static RawTextHandler<string> CreateForFile
        (
            string _typeID,
            string version
        )
        {
            return new RawTextHandler<string>
            (
                _typeID,
                version,
                TextFileIO.Default,
                new FileCommitWriter<string>(TextFileIO.Default),
                TryMapFileLocation
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

    // ============================================================
    /// <summary>
    /// Raw text document body.
    /// </summary>
    // ============================================================
    [Serializable]
    public sealed class RawTextDocumentBody
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 파일에 그대로 저장할 텍스트.
        /// </summary>
        // ------------------------------------------------------------
        public string Text = string.Empty;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 raw text body를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public RawTextDocumentBody() : this("")
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 텍스트로 raw text body를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public RawTextDocumentBody(string text) : base()
        {
            Text = text ?? "";
        }

    #endregion

    }

    // ======================================================================
    /// <summary>
    /// serializer 없이 문자열 body를 그대로 읽고 쓰는 document handler.
    /// </summary>
    /// <typeparam name="TLocation">reader/writer가 실제로 사용하는 IO location 타입.</typeparam>
    // ======================================================================
    [Serializable]
    public sealed class RawTextHandler<TLocation> : IDocumentHandler
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


        private readonly RecordPersistence<TLocation, RawTextDocumentBody> persistence = null;
        private readonly DocumentLocationMapper<TLocation> mapLocation = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// RawTextHandler를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public RawTextHandler
        (
            string _typeID,
            string version,
            IDataReader<TLocation, string> reader,
            IDataWriter<TLocation, string> writer,
            DocumentLocationMapper<TLocation> mapLocation
        ) : base()
        {
            // 문서 종류와 버전 입력은 공통 RecordType에서 검증한다.
            this.recordType = new RecordType(_typeID, version);
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            this.mapLocation = mapLocation ?? throw new ArgumentNullException(nameof(mapLocation));

            // RawText 역시 별도의 serializer 없이 범용 IO Mapping과 기록 수명 경계를 사용한다.
            persistence = new RecordPersistence<TLocation, RawTextDocumentBody>
            (
                new MappedDataReader<TLocation, string, RawTextDocumentBody>
                (
                    reader,
                    text => new RawTextDocumentBody(text)
                ),
                new MappedDataWriter<TLocation, RawTextDocumentBody, string>
                (
                    writer,
                    body => body.Text ?? string.Empty
                )
            );
        }

    #endregion

    #region 생성

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 이름으로 새 raw text document session을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public DocumentCreateResponse Create(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return DocumentCreateResponse.Fail("문서 이름이 비어 있습니다.");
            }

            var document = new Document(TypeID, Version, name);
            var body = new RawTextDocumentBody();
            var session = new DocumentSession<RawTextDocumentBody>(document, body, null);

            return DocumentCreateResponse.Succeed(session);
        }

    #endregion

    #region 열기

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 location을 raw text document session으로 연다.
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

            // RecordPersistence가 읽은 텍스트의 수명을 종료하고 독립된 Body를 반환한다.
            var response = persistence.Load(ioLoc);

            if (!response.Success)
            {
                return DocumentOpenResponse.Fail(response.Error);
            }

            try
            {
                var document = new Document(TypeID, Version, loc.Name);
                var session = new DocumentSession<RawTextDocumentBody>(document, response.Value, loc);

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
        /// 지정한 raw text document session을 location에 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        public DocumentSaveResponse Save(IDocumentSession session, IDocumentLocation loc)
        {
            if (!TryGetBody(session, out var body))
            {
                return DocumentSaveResponse.Fail("session을 raw text로 저장할 수 없습니다.");
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
        /// 지정한 raw text session body를 recovery record로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        public DocumentBodyRecoveryRecord RecordSessionBody(IDocumentSession session)
        {
            return TryGetBody(session, out var body) ? new DocumentBodyRecoveryRecord(body.Text ?? "") : null;
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 지정한 document, body record, location에서 raw text session을 복구한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public DocumentOpenResponse RecoverSession
        (
            IDocument document,
            DocumentBodyRecoveryRecord bodyRecord,
            IDocumentLocation loc
        )
        {
            if (!DocumentMetadataValidation.IsMatch(document, recordType) || bodyRecord == null)
            {
                return DocumentOpenResponse.Fail("raw text recovery record를 복구할 수 없습니다.");
            }

            var body = new RawTextDocumentBody(bodyRecord.Record);
            var session = new DocumentSession<RawTextDocumentBody>(document, body, loc);

            return DocumentOpenResponse.Succeed(session);
        }

    #endregion

    #region Body

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 session에서 raw text body를 꺼낸다.
        /// </summary>
        // ------------------------------------------------------------
        private bool TryGetBody
        (
            IDocumentSession session,
            out RawTextDocumentBody body
        )
        {
            body = null;

            if (!DocumentMetadataValidation.IsMatch(session?.Document, recordType))
            {
                return false;
            }

            if (session is not IDocumentSession<RawTextDocumentBody> typedSession)
            {
                return false;
            }

            body = typedSession.Body;

            return body != null;
        }

    #endregion

    }
}
