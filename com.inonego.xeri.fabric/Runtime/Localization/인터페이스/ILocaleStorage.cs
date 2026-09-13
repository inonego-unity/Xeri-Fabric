/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ILocaleStorage.cs
수정일 : 2026-05-09

# 설명
사용자 선택 언어 코드의 영속화 추상화.
기본 구현은 PlayerPrefsLocaleStorage (PlayerPrefs 기반).
서버 동기화 / 인메모리 / 파일 등 다른 영속화 전략으로 교체 가능.
========================================================================= BLOCK_HEADER_END */

using UnityEngine;

namespace inonego.Xeri.Localization
{
    // ============================================================
    /// <summary>
    /// 언어 코드 영속화 인터페이스.
    /// </summary>
    // ============================================================
    public interface ILocaleStorage
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 언어 코드를 로드한다. 없으면 null 또는 기본값을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        string Load();

        // ------------------------------------------------------------
        /// <summary>
        /// 언어 코드를 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        void Save(string langCode);
    }

    // ============================================================
    /// <summary>
    /// PlayerPrefs 기반 기본 ILocaleStorage 구현.
    /// </summary>
    // ============================================================
    public class PlayerPrefsLocaleStorage : ILocaleStorage
    {

    #region 필드

        private const string DEFAULT_KEY = "Xeri.Localization.LangCode";

        private readonly string prefsKey;

    #endregion

    #region 생성자

        public PlayerPrefsLocaleStorage(string prefsKey = DEFAULT_KEY)
        {
            this.prefsKey = prefsKey;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// PlayerPrefs 에서 언어 코드를 로드한다. 없으면 null.
        /// </summary>
        // ------------------------------------------------------------
        public string Load()
        {
            return PlayerPrefs.HasKey(prefsKey) ? PlayerPrefs.GetString(prefsKey) : null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// PlayerPrefs 에 언어 코드를 저장하고 즉시 flush 한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Save(string langCode)
        {
            PlayerPrefs.SetString(prefsKey, langCode ?? string.Empty);
            PlayerPrefs.Save();
        }

    #endregion

    }

    // ============================================================
    /// <summary>
    /// 인메모리 ILocaleStorage 구현 (테스트 / 일시 저장용).
    /// </summary>
    // ============================================================
    public class InMemoryLocaleStorage : ILocaleStorage
    {

    #region 필드

        private string stored;

    #endregion

    #region 생성자

        public InMemoryLocaleStorage(string initial = null)
        {
            stored = initial;
        }

    #endregion

    #region 메서드

        public string Load() => stored;

        public void Save(string langCode) => stored = langCode;

    #endregion

    }
}
