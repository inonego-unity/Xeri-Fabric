/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SaveIdentifier.cs
수정일 : 2026-11-17

# 설명
File·SQL Save에서 공유하는 논리 식별자의 안전한 문자·길이 규칙.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.Save
{
    // ============================================================
    /// <summary>
    /// 저장소 유형과 무관한 Save 식별자 규칙.
    /// </summary>
    // ============================================================
    internal static class SaveIdentifier
    {

    #region 검증

        // ------------------------------------------------------------
        /// <summary>
        /// 경로·테이블 키에 안전한 1~64자 식별자인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        internal static bool IsValid(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64)
            {
                return false;
            }

            foreach (var ch in id)
            {
                var allowed = (ch >= 'a' && ch <= 'z') ||
                              (ch >= '0' && ch <= '9') ||
                              ch == '_' ||
                              ch == '-';

                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

    #endregion

    }
}