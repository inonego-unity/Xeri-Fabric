/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentSaveResponse.cs
수정일 : 2026-11-14

# 설명
문서 저장 요청의 성공 여부와 실패 메시지를 담는 응답 값을 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.Workspace.Document
{
    // ============================================================
    /// <summary>
    /// 문서 저장 요청의 성공 여부와 실패 메시지를 담는 응답.
    /// </summary>
    // ============================================================
    public readonly struct DocumentSaveResponse
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 처리 성공 여부를 나타냅니다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Success { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패 원인을 설명하는 메시지입니다.
        /// </summary>
        // ------------------------------------------------------------
        public string Error { get; }

    #endregion

    #region 생성자

        private DocumentSaveResponse(bool success, string error)
        {
            Success = success;
            Error = error ?? "";
        }

    #endregion

    #region 응답

        // ------------------------------------------------------------
        /// <summary>
        /// 성공 결과를 생성합니다.
        /// </summary>
        // ------------------------------------------------------------
        public static DocumentSaveResponse Succeed()
        {
            return new DocumentSaveResponse(true, "");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실패 결과를 생성합니다.
        /// </summary>
        // ------------------------------------------------------------
        public static DocumentSaveResponse Fail(string error)
        {
            return new DocumentSaveResponse(false, error);
        }

    #endregion

    }
}
