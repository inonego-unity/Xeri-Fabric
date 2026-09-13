/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : LocalizedStringBinder.cs
수정일 : 2026-05-09

# 설명
TextMeshProUGUI 한 개에 LocalizedString 을 바인딩하는 단순 컴포넌트.
ILocalizedUI 를 구현해 LangCode 변경 시 자동으로 ReloadLocalizedUI 가 호출된다.

# 특이사항
TMP 패키지 부재 환경에서도 컴파일 통과하도록 #if TMP_PRESENT 가드.
복수 텍스트를 다루는 복잡 UI 는 직접 ILocalizedUI 를 구현하는 쪽을 권장.
========================================================================= BLOCK_HEADER_END */

#if TMP_PRESENT

using UnityEngine;

using TMPro;

namespace inonego.Xeri.Localization
{
    // ============================================================
    /// <summary>
    /// 단일 TextMeshProUGUI 와 LocalizedString 을 연결하는 바인더.
    /// </summary>
    // ============================================================
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedStringBinder : MonoBehaviour, ILocalizedUI
    {

    #region 필드

        [Header("로컬라이징 설정")]

        // ------------------------------------------------------------
        /// <summary>
        /// 인스펙터 직렬화는 구체 타입 LocalizedString 사용 (XDictionary_VV 드로어 호환).
        /// </summary>
        // ------------------------------------------------------------
        [SerializeField]
        private LocalizedString localizedString = new();

        // ------------------------------------------------------------
        /// <summary>
        /// 로컬라이즈 결과가 비어 있을 때 표시할 폴백 문자열.
        /// </summary>
        // ------------------------------------------------------------
        [TextArea(2, 4)]
        public string Fallback;

        public ILocalizedString LocalizedString
        {
            get => localizedString;
            set
            {
                // 이 setter 는 외부에서 새 LocalizedString 인스턴스를 주입할 때만 사용한다.
                if (value is LocalizedString concrete)
                {
                    localizedString = concrete;
                }
                else if (value == null)
                {
                    localizedString = null;
                }
                else
                {
                    // 외부 ILocalizedString 구현은 키 열거 API 가 보장되지 않아 LocalizedString 으로 안전 복사 불가.
                    // 직렬화 호환 깨짐 방지를 위해 setter 는 LocalizedString 또는 null 만 허용한다.
                    Debug.LogWarning("[LocalizedStringBinder] LocalizedString 외 구현 주입은 무시됩니다. 직렬화 호환을 위해 LocalizedString 을 사용하세요.", this);
                    return;
                }

                ReloadLocalizedUI();
            }
        }

        private TextMeshProUGUI targetText;

    #endregion

    #region 유니티 이벤트

        private void Awake()
        {
            targetText = GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            ReloadLocalizedUI();
        }

    #endregion

    #region ILocalizedUI 구현

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 언어로 텍스트 갱신. 비어 있으면 Fallback 사용.
        /// </summary>
        // ------------------------------------------------------------
        public void ReloadLocalizedUI()
        {
            if (targetText == null) return;

            if (localizedString == null)
            {
                targetText.text = Fallback;
                return;
            }

            var text = localizedString.ToLocalized();

            targetText.text = string.IsNullOrEmpty(text) ? Fallback : text;
        }

    #endregion

    }
}

#endif
