/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : LocalizedString.cs
수정일 : 2026-05-09

# 설명
다국어 지원 문자열 — 언어 코드 → 텍스트 매핑을 보관하는 직렬화 가능 딕셔너리.
xeri 의 XDictionary_VV<string, string> 위에 빌드되어 인스펙터 드로어를 그대로 활용한다.

# 특이사항
인덱서는 Dictionary 기본 동작 (KeyNotFoundException) 대신 미등록 키 시 빈 문자열을 반환한다.
캘러는 try/catch 없이 안전하게 조회 가능하다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.Localization
{
    using inonego.Xeri.Serializable;

    // ============================================================
    /// <summary>
    /// 다국어 지원 문자열 — 언어 코드 → 텍스트 매핑.
    /// </summary>
    // ============================================================
    [Serializable]
    public class LocalizedString : XDictionary_VV<string, string>, ILocalizedString
    {

    #region 인덱서

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 언어 코드로 텍스트 조회.
        /// <br/> 미등록 키는 빈 문자열 반환 (Dictionary 기본 throw 동작 대체).
        /// </summary>
        // ------------------------------------------------------------
        public new string this[string code]
        {
            get => code != null && TryGetValue(code, out var value) ? value : string.Empty;
            set => base[code] = value;
        }

    #endregion

    }
}
