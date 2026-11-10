/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : BodySerializedHandler.cs
수정일 : 2026-11-10

# 설명
DocumentEnvelope 없이 body 자체를 serializer root로 열고 저장하는 document handler를 정의한다.
기존 JSON/XML root 구조를 유지해야 하는 문서에 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
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
        /// <br/> FileDocumentLocation과 TextFileIO 기본 인스턴스를 사용하는 body
        /// <br/> serialized handler를 생성한다.
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
                TextFileIO.Default,
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
        public string TypeID => _typeID;

        private readonly string _typeID = string.Empty;

        // ------------------------------------------------------------
        /// <summary>
        /// Handler가 생성하는 문서 버전.
        /// </summary>
        // ------------------------------------------------------------
        public string Version => version;

        private readonly string version = string.Empty;

        private readonly ISerializer serializer = null;
        private readonly IDataReader<TLocation, string> reader = null;
        private readonly IDataWriter<TLocation, string> writer = null;
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
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.mapLocation = mapLocation ?? throw new ArgumentNullException(nameof(mapLocation));
            this.createBody = createBody;
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
                    var body = serializer.Deserialize<TBody>(response.Value);
                    if (body == null)
                    {
                        result = DocumentOpenResponse.Fail("역직렬화된 document body가 없습니다.");
                    }
                    else
                    {
                        var document = new Document(TypeID, Version, loc.Name);
                        var session = new DocumentSession<TBody>(document, body, loc);
                        result = DocumentOpenResponse.Succeed(session);
                    }
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

            try
            {
                var text = serializer.Serialize(body);
                var response = writer.Write(ioLoc, text);

                return response.Success ? DocumentSaveResponse.Succeed() : DocumentSaveResponse.Fail(response.Error);
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
        /// 지정한 body-root session body를 recovery record로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        public DocumentBodyRecoveryRecord RecordSessionBody(IDocumentSession session)
        {
            if (!TryGetBody(session, out var body))
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 지정한 document, body record, location에서 body-root session을
        /// <br/> 복구한다.
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
                return DocumentOpenResponse.Fail("body-root recovery record를 복구할 수 없습니다.");
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

    #endregion

    }
}
