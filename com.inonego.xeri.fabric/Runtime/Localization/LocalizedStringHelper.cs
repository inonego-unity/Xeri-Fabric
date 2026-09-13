/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : LocalizedStringHelper.cs
수정일 : 2026-05-09

# 설명
ILocalizedString 의 현재 언어 변환 확장 메서드.
Localization 슬롯이 미등록된 환경(테스트, 부트스트랩 이전)에서도 빈 문자열을 반환해 안전하다.
========================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.Localization
{
    // ============================================================
    /// <summary>
    /// ILocalizedString 확장 — 현재 언어로 변환.
    /// </summary>
    // ============================================================
    public static class LocalizedStringHelper
    {
        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 현재 LangCode 로 로컬라이즈한다. null safe.
        /// <br/> Localization 슬롯이 등록되지 않은 환경(테스트 등)에서도 빈 문자열을 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static string ToLocalized(this ILocalizedString data)
        {
            if (data == null) return string.Empty;

            if (!Localization.TryCurrent(out var current)) return string.Empty;

            return ToLocalized(data, current.LangCode);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 명시한 코드로 로컬라이즈한다. null safe, 미등록 키 시 빈 문자열.
        /// </summary>
        // ------------------------------------------------------------
        public static string ToLocalized(this ILocalizedString data, string code)
        {
            if (data == null || code == null) return string.Empty;

            return data[code];
        }
    }
}
