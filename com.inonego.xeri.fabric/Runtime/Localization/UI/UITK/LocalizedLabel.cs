/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : LocalizedLabel.cs
수정일 : 2026-09-02

# 설명
LocalizedStringRow Key를 선언적으로 소비하는 UI Toolkit Label을 제공한다.
TextKey 변경은 현재 element만 갱신하고 LangCode 전체 변경은 ILocalizedUI traversal로 갱신한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego.Xeri.Data;

using UnityEngine.UIElements;

namespace inonego.Xeri.Localization
{
    // ============================================================
    /// <summary>
    /// LocalizedStringRow Key를 현재 locale 문자열로 표시하는 UI Toolkit Label.
    /// </summary>
    // ============================================================
    [UxmlElement]
    public partial class LocalizedLabel : Label, ILocalizedUI
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 표시할 LocalizedStringRow의 안정 Key.
        /// </summary>
        // ------------------------------------------------------------
        [UxmlAttribute("text-key")]
        public string TextKey
        {
            get => textKey;
            set
            {
                if (string.Equals(textKey, value, StringComparison.Ordinal)) return;

                textKey = value ?? string.Empty;
                ReloadLocalizedUI();
            }
        }

        private string textKey = string.Empty;

    #endregion

    #region ILocalizedUI

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 TextKey를 현재 locale 문자열로 다시 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        public void ReloadLocalizedUI()
        {
            if (string.IsNullOrWhiteSpace(textKey))
            {
                text = string.Empty;
                return;
            }

            var row = new REF<LocalizedStringRow>(textKey).ToValue();
            text = row?.Value.ToLocalized() ?? string.Empty;
        }

    #endregion

    }
}