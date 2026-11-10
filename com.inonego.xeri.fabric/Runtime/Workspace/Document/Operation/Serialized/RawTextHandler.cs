/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RawTextHandler.cs
수정일 : 2026-11-10

# 설명
문자열 파일을 serializer 없이 그대로 열고 저장하는 document handler를 정의한다.
텍스트 파일, 스크립트 파일처럼 document metadata envelope가 없는 문서에 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;

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

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> FileDocumentLocation과 TextFileIO 기본 인스턴스를 사용하는 raw text
        /// <br/> handler를 생성한다.
        /// </summary>
        // --------------------------------------------------------------------------------
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
                TextFileIO.Default,
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
        public RawTextDocumentBody(string text)
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
        public string TypeID => _typeID;

        private readonly string _typeID = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Handler가 생성하는 문서 버전.
        /// </summary>
        // ------------------------------------------------------------
        public string Version => version;

        private readonly string version = string.Empty;

        private readonly IDataReader<TLocation, string> reader = null;
        private readonly IDataWriter<TLocation, string> writer = null;
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
        )
        {
            if (string.IsNullOrEmpty(_typeID))
            {
                throw new ArgumentException("문서 종류 식별자가 비어 있습니다.", nameof(_typeID));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException("문서 버전이 비어 있습니다.", nameof(version));
            }

            this._typeID = _typeID;
            this.version = version;
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.mapLocation = mapLocation ?? throw new ArgumentNullException(nameof(mapLocation));
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

            IReleaseHandle releaseHandle = null;
            DocumentOpenResponse result;

            try
            {
                var response = reader.Read(ioLoc);
                releaseHandle = response.ReleaseHandle;

                if (!response.Success)
                {
                    result = DocumentOpenResponse.Fail(response.Error);
                }
                else
                {
                    var document = new Document(TypeID, Version, loc.Name);
                    var body = new RawTextDocumentBody(response.Value);
                    var session = new DocumentSession<RawTextDocumentBody>(document, body, loc);

                    result = DocumentOpenResponse.Succeed(session);
                }
            }
            catch (Exception exception)
            {
                result = DocumentOpenResponse.Fail(exception.Message);
            }

            try
            {
                // 결과를 확정하기 전에 IO 수명 종료를 완료한다.
                releaseHandle?.Dispose();
            }
            catch (Exception exception)
            {
                var releaseError = "IO ReleaseHandle 정리에 실패했습니다: " + exception.Message;
                return DocumentOpenResponse.Fail
                (
                    result.Success ? releaseError : result.Error + "; " + releaseError
                );
            }

            return result;
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

            var response = writer.Write(ioLoc, body.Text ?? "");
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
            if (document == null || document.TypeID != TypeID || bodyRecord == null)
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

            if (session?.Document == null || session.Document.TypeID != TypeID)
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
