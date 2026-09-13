/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : LocalizedDataSource.cs
수정일 : 2026-05-09

# 설명
UXML data-source 기반 바인딩을 위한 INotifyBindablePropertyChanged 어댑터.
LocalizedString 을 감싸 Localization.OnLangCodeChange 발생 시 propertyChanged 이벤트로 UI 자동 갱신.

# 특이사항
Unity 6 이상 (#if UNITY_2023_3_OR_NEWER) 에서만 활성화.
UXML 사용 예: <Label binding-path="Text" /> + dataSource = LocalizedDataSource(...)
========================================================================= BLOCK_HEADER_END */

#if UNITY_2023_3_OR_NEWER

using System;

using UnityEngine;
using UnityEngine.UIElements;

using Unity;
using Unity.Properties;

namespace inonego.Xeri.Localization
{
    // ============================================================
    /// <summary>
    /// UXML DataBinding 용 LocalizedString 어댑터.
    /// </summary>
    // ============================================================
    public sealed class LocalizedDataSource : INotifyBindablePropertyChanged
    {

    #region 필드

        private readonly ILocalizedString source;

        // ------------------------------------------------------------
        /// <summary>
        /// UXML binding-path 로 노출되는 현재 언어 텍스트.
        /// </summary>
        // ------------------------------------------------------------
        [CreateProperty]
        public string Text => source?.ToLocalized() ?? string.Empty;

    #endregion

    #region 이벤트

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    #endregion

    #region 생성자

        public LocalizedDataSource(ILocalizedString source)
        {
            this.source = source;

            // LangCode 변경 → propertyChanged 발생 → UXML 바인딩 측 UI 자동 갱신.
            Localization.OnCurrentLangCodeChange += OnLangCodeChanged;
        }

    #endregion

    #region 이벤트 핸들러

        private void OnLangCodeChanged(string _)
        {
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Text)));
        }

    #endregion

    #region 해제

        // ----------------------------------------------------------------------
        /// <summary>
        /// 명시적 구독 해제. UXML dataSource 가 사라질 때 호출하면 메모리 누수 방지.
        /// </summary>
        // ----------------------------------------------------------------------
        public void Dispose()
        {
            Localization.OnCurrentLangCodeChange -= OnLangCodeChanged;
        }

    #endregion

    }
}

#endif
