/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ILocalizedUI.cs
수정일 : 2026-09-02

# 설명
다국어 지원 UI의 갱신 계약과 일괄 reload 진입점을 제공한다.
LangCode 변경 시 현재 로드된 MonoBehaviour ILocalizedUI와 UIDocument VisualElement ILocalizedUI를 순회한다.

# 특이사항
전체 순회는 LangCode 변경처럼 드문 사용자 트리거에서만 실행한다.
전역 탐색은 일반 Scene과 DontDestroyOnLoad를 모두 포함하고 FindObjectsInactive로 비활성 대상 포함 여부를 결정한다.
UGUI와 UITK는 같은 ILocalizedUI 계약을 사용하고 backend별 hierarchy 탐색 방식만 다르게 적용한다.
========================================================================= BLOCK_HEADER_END */

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UIElements;

namespace inonego.Xeri.Localization
{
    // ============================================================
    /// <summary>
    /// 다국어 지원 UI 의 갱신 계약.
    /// </summary>
    // ============================================================
    public interface ILocalizedUI
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 현재 언어로 UI 텍스트를 갱신한다.
        /// </summary>
        // ------------------------------------------------------------
        void ReloadLocalizedUI();

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 현재 로드된 GameObject hierarchy와 UIDocument Visual Tree를 순회해
        /// <br/> ILocalizedUI 구현체에 ReloadLocalizedUI를 호출한다.
        /// <br/> root가 null이면 일반 Scene과 DontDestroyOnLoad의 모든 Runtime MonoBehaviour를 대상으로 한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        public static void ReloadLocalizedUIAll
        (
            GameObject root = null,
            FindObjectsInactive findObjectsInactive = FindObjectsInactive.Include
        )
        {
            var visitedUITKElements = new HashSet<VisualElement>();

            if (root == null)
            {
                var monoBehaviours = Object.FindObjectsByType<MonoBehaviour>
                (
                    findObjectsInactive
                );

                ReloadLocalizedUIAll(monoBehaviours, visitedUITKElements);
                return;
            }

            var includeInactive = findObjectsInactive == FindObjectsInactive.Include;
            var scopedMonoBehaviours = root.GetComponentsInChildren<MonoBehaviour>(includeInactive);
            ReloadLocalizedUIAll(scopedMonoBehaviours, visitedUITKElements);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// MonoBehaviour ILocalizedUI와 UIDocument Visual Tree를 중복 없이 순회한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private static void ReloadLocalizedUIAll
        (
            MonoBehaviour[] monoBehaviours,
            HashSet<VisualElement> visitedUITKElements
        )
        {
            foreach (var monoBehaviour in monoBehaviours)
            {
                if (monoBehaviour == null) continue;

                if (monoBehaviour is ILocalizedUI localizedUI)
                {
                    localizedUI.ReloadLocalizedUI();
                }

                if (monoBehaviour is UIDocument document)
                {
                    ReloadLocalizedUIAll(document.rootVisualElement, visitedUITKElements);
                }
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// VisualElement hierarchy를 순회하며 ILocalizedUI 구현체를 한 번씩 갱신한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ReloadLocalizedUIAll
        (
            VisualElement element,
            HashSet<VisualElement> visitedUITKElements
        )
        {
            if (element == null || !visitedUITKElements.Add(element)) return;

            if (element is ILocalizedUI localizedUI)
            {
                localizedUI.ReloadLocalizedUI();
            }

            for (var index = 0; index < element.childCount; index++)
            {
                ReloadLocalizedUIAll(element[index], visitedUITKElements);
            }
        }
    }
}
