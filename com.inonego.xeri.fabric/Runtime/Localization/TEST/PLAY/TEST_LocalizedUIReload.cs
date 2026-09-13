/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_LocalizedUIReload.cs
수정일 : 2026-09-02

# 설명
LangCode 변경 시 ILocalizedUI 구현 MonoBehaviour와 UIDocument VisualElement의 ReloadLocalizedUI 자동 호출 검증.

# 테스트 구성
 R: Runtime 전체 reload — 일반 Scene과 DontDestroyOnLoad의 ILocalizedUI 구현체가 모두 호출되는지

# 특이사항
PlayMode 필요 — Runtime Object 탐색과 UIDocument Visual Tree가 실제 생성된 상태에서 검증한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.TEST._LocalizedUI
{
    using inonego.Xeri.Localization;

    // ============================================================
    /// <summary>
    /// ILocalizedUI 씬 순회 reload PlayMode 테스트.
    /// </summary>
    // ============================================================
    public class TEST_LocalizedUIReload
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트용 ILocalizedUI 구현 — Reload 호출 횟수와 마지막 코드를 기록.
        /// </summary>
        // ============================================================
        private class TestLocalizedUI : MonoBehaviour, ILocalizedUI
        {
            public int    ReloadCount      { get; private set; }
            public string LastCodeAtReload { get; private set; }

            public void ReloadLocalizedUI()
            {
                ReloadCount++;
                LastCodeAtReload = Localization.CurrentLangCode;
            }
        }

        // ============================================================
        /// <summary>
        /// 테스트용 UITK ILocalizedUI 구현.
        /// </summary>
        // ============================================================
        private class TestLocalizedElement : VisualElement, ILocalizedUI
        {
            public int    ReloadCount      { get; private set; }
            public string LastCodeAtReload { get; private set; }

            public void ReloadLocalizedUI()
            {
                ReloadCount++;
                LastCodeAtReload = Localization.CurrentLangCode;
            }
        }

    #endregion

    #region 픽스처

        private readonly List<GameObject> spawned = new();
        private readonly List<PanelSettings> panelSettings = new();

        [SetUp]
        public void SetUp()
        {
            Singleton<Localization>.Clear();

            var loc = new Localization(new InMemoryLocaleStorage("ko"));
            Singleton<Localization>.Register(loc);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            spawned.Clear();

            foreach (var settings in panelSettings)
            {
                if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
            }
            panelSettings.Clear();

            Singleton<Localization>.Clear();
        }

        private TestLocalizedUI CreateTarget(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go.AddComponent<TestLocalizedUI>();
        }

        private TestLocalizedElement CreateUITKTarget
        (
            string name,
            bool dontDestroyOnLoad = false
        )
        {
            var go = new GameObject(name);
            go.SetActive(false);
            spawned.Add(go);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.Add(settings);

            var document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            go.SetActive(true);

            var element = new TestLocalizedElement();
            document.rootVisualElement.Add(element);

            if (dontDestroyOnLoad)
            {
                UnityEngine.Object.DontDestroyOnLoad(go);
            }

            return element;
        }

    #endregion

    #region R-1: LangCode 변경 → 모든 ILocalizedUI Reload 호출

        [UnityTest]
        public IEnumerator TEST_LocalizedUI_LangCode_변경시_모든_구현체_Reload()
        {
            var a = CreateTarget("LocA");
            var b = CreateTarget("LocB");

            // 한 프레임 흘려 OnEnable 등 라이프사이클 진행.
            yield return null;

            Localization.CurrentLangCode = "en";

            yield return null;

            Assert.GreaterOrEqual(a.ReloadCount, 1, "A 가 최소 1회 Reload 받아야 합니다");
            Assert.GreaterOrEqual(b.ReloadCount, 1, "B 가 최소 1회 Reload 받아야 합니다");
            Assert.AreEqual("en", a.LastCodeAtReload);
            Assert.AreEqual("en", b.LastCodeAtReload);
        }

    #endregion

    #region R-2: LangCode 변경 → UIDocument VisualElement ILocalizedUI Reload 호출

        [UnityTest]
        public IEnumerator TEST_LocalizedUI_LangCode_변경시_UITK_구현체_Reload()
        {
            var ui = CreateUITKTarget("UITKLoc");

            yield return null;

            Localization.CurrentLangCode = "en";

            yield return null;

            Assert.GreaterOrEqual(ui.ReloadCount, 1, "UITK ILocalizedUI가 최소 1회 Reload 받아야 합니다");
            Assert.AreEqual("en", ui.LastCodeAtReload);
        }

    #endregion

    #region R-3: LangCode 변경 → DontDestroyOnLoad ILocalizedUI Reload 호출

        [UnityTest]
        public IEnumerator TEST_LocalizedUI_LangCode_변경시_DontDestroyOnLoad_구현체_Reload()
        {
            var mono = CreateTarget("DDOLLoc");
            UnityEngine.Object.DontDestroyOnLoad(mono.gameObject);
            var ui = CreateUITKTarget("DDOLUITKLoc", true);

            yield return null;

            Assert.AreEqual("DontDestroyOnLoad", mono.gameObject.scene.name);

            Localization.CurrentLangCode = "en";

            yield return null;

            Assert.GreaterOrEqual(mono.ReloadCount, 1, "DontDestroyOnLoad MonoBehaviour가 Reload 받아야 합니다");
            Assert.GreaterOrEqual(ui.ReloadCount, 1, "DontDestroyOnLoad UIDocument VisualElement가 Reload 받아야 합니다");
            Assert.AreEqual("en", mono.LastCodeAtReload);
            Assert.AreEqual("en", ui.LastCodeAtReload);
        }

    #endregion

    #region R-4: 동일 LangCode 재설정 시 Reload 호출 안 됨

        [UnityTest]
        public IEnumerator TEST_LocalizedUI_동일_LangCode시_Reload_미호출()
        {
            var ui = CreateTarget("Loc");

            yield return null;

            int initial = ui.ReloadCount;

            Localization.CurrentLangCode = "ko"; // 동일

            yield return null;

            Assert.AreEqual(initial, ui.ReloadCount, "동일 LangCode 는 Reload 호출 안 됨");
        }

    #endregion

    }
}
