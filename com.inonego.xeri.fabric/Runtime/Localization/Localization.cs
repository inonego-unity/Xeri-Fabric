/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : Localization.cs
수정일 : 2026-05-09

# 설명
다국어 시스템의 게이트웨이.
xeri 슬롯 기반 Singleton<T> 위에 빌드되며, 정적 facade 로 호출 측 단축을 제공한다.
LangCode 변경 시 OnLangCodeChange 이벤트 + ILocalizedUI.ReloadLocalizedUIAll 자동 호출.

# 특이사항
부트스트랩: [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] 로 PlayerPrefsLocaleStorage 기반 default 인스턴스를 자동 Register 한다.
테스트는 Singleton<Localization>.Clear() + Register(new Localization(InMemoryLocaleStorage)) 로 격리.

# 가이드
LangCode 형식은 자유 문자열이지만 ISO 639 / BCP 47 (예: "ko-KR", "en-US") 권장.
폴백 체인 / 코드 정규화는 본 시스템 범위 외 — 사용자가 정책 결정.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.Localization
{
    public delegate void LangCodeChangeEventHandler(string code);

    // ============================================================
    /// <summary>
    /// 다국어 시스템의 슬롯 기반 매니저.
    /// </summary>
    // ============================================================
    public class Localization : Singleton<Localization>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 저장소 (영속화 전략).
        /// </summary>
        // ------------------------------------------------------------
        public ILocaleStorage Storage { get; }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 언어 코드.
        /// </summary>
        // ------------------------------------------------------------
        public string LangCode
        {
            get => langCode;
            set => SetLangCode(value);
        }

        private string langCode;

        // 핸들러 안에서 LangCode 재설정 시 재진입 방어 플래그.
        private bool isSettingLangCode;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// LangCode 변경 시 발생.
        /// </summary>
        // ------------------------------------------------------------
        public event LangCodeChangeEventHandler OnLangCodeChange;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 저장소를 주입받아 초기 LangCode 를 로드한다.
        /// </summary>
        // ------------------------------------------------------------
        public Localization(ILocaleStorage storage)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));

            langCode = Storage.Load();
        }

    #endregion

    #region 메서드

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> LangCode 를 변경한다. 변경 시 영속화 + OnLangCodeChange 발생 +
        /// <br/> ILocalizedUI.ReloadLocalizedUIAll() 자동 호출.
        /// <br/> 핸들러 안에서 LangCode 재설정은 재진입 가드로 무시되며 경고 로그를 남긴다.
        /// </summary>
        // ----------------------------------------------------------------------
        private void SetLangCode(string code)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));

            if (langCode == code) return;

            // 핸들러 안에서 LangCode 재설정 시도 → 무시 (이벤트 폭주 / 무한 재귀 방지).
            if (isSettingLangCode)
            {
                Debug.LogWarning($"[Localization] 핸들러 안에서 LangCode 재설정 무시: '{langCode}' → '{code}'");
                return;
            }

            isSettingLangCode = true;
            try
            {
                langCode = code;
                Storage.Save(code);

                OnLangCodeChange?.Invoke(code);

                // UGUI와 UITK의 현재 ILocalizedUI 구현체를 일괄 갱신한다.
                ILocalizedUI.ReloadLocalizedUIAll();
            }
            finally
            {
                isSettingLangCode = false;
            }
        }

    #endregion

    #region 정적 facade

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 슬롯의 LangCode 단축 접근.
        /// </summary>
        // ------------------------------------------------------------
        public static string CurrentLangCode
        {
            get => Current.LangCode;
            set => Current.LangCode = value;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 슬롯의 OnLangCodeChange 이벤트 단축 구독.
        /// </summary>
        // ------------------------------------------------------------
        public static event LangCodeChangeEventHandler OnCurrentLangCodeChange
        {
            add    => Current.OnLangCodeChange += value;
            remove => Current.OnLangCodeChange -= value;
        }

    #endregion

    #region 자동 부트스트랩

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Unity 첫 부트 단계에 default 슬롯 미등록 시 PlayerPrefs 기반 인스턴스를 자동 등록.
        /// <br/> 테스트 환경에서는 Clear() + Register() 로 덮어써 격리할 수 있다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Bootstrap()
        {
            if (Named.Has(DEFAULT_SLOT)) return;

            Register(new Localization(new PlayerPrefsLocaleStorage()));
        }

    #endregion

    }
}
