/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : LocalizationBinding.cs
수정일 : 2026-05-09

# 설명
UI Toolkit VisualElement 에 LocalizedString 을 바인딩하는 정적 확장 메서드.
Localization.OnLangCodeChange 이벤트를 자동 구독하고 panel detach 시 자동 해제한다.

# 특이사항
Label / Button / TextField 자동 분기. 그 외 VisualElement 는 텍스트 갱신 무시.
push 모델이라 ILocalizedUI 씬 순회와 독립적으로 동작 (둘 다 LangCode 변경 시 트리거됨).
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.Localization
{
    // ============================================================
    /// <summary>
    /// UI Toolkit 측 LocalizedString 바인딩 정적 확장.
    /// </summary>
    // ============================================================
    public static class LocalizationBinding
    {

    #region 메서드

        // ----------------------------------------------------------------------------------
        /// <summary>
        /// <br/> VisualElement 의 텍스트를 LocalizedString 에 바인딩한다.
        /// <br/> Localization.OnLangCodeChange 자동 구독, DetachFromPanelEvent 시 자동 해제.
        /// <br/> 주의: element 가 panel 에 attach 된 적이 없으면 자동 해제 콜백이 발생하지 않아
        /// <br/> 정적 이벤트가 클로저를 강참조해 누수 가능. 호출자는 UIDocument rootVisualElement
        /// <br/> 트리에 element 가 포함됨을 보장할 것.
        /// </summary>
        // ----------------------------------------------------------------------------------
        public static void BindLocalized(this VisualElement element, ILocalizedString localized)
        {
            if (element == null) return;

            void Refresh(string _) => SetText(element, localized);

            // 즉시 1회 갱신 후 이벤트 구독.
            Refresh(Localization.CurrentLangCode);

            Localization.OnCurrentLangCodeChange += Refresh;

            // panel 이탈 시 자동 구독 해제 — 메모리 누수 방지.
            element.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Localization.OnCurrentLangCodeChange -= Refresh;
            });
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// VisualElement 타입별 분기로 텍스트를 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void SetText(VisualElement element, ILocalizedString localized)
        {
            var text = localized?.ToLocalized() ?? string.Empty;

            switch (element)
            {
                case Label label:        label.text  = text; break;
                case Button button:      button.text = text; break;
                case TextField field:    field.value = text; break;
                // 그 외 — 텍스트 속성이 없는 element 는 무시.
            }
        }

    #endregion

    }
}
