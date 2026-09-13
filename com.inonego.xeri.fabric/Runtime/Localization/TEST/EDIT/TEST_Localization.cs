/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_Localization.cs
수정일 : 2026-05-09

# 설명
Localization Singleton + 정적 facade + InMemoryLocaleStorage 주입 단위 테스트.

# 테스트 구성
 R: LangCode 변경 → OnLangCodeChange 발생
 E: 초기값 / Storage 동기화

# 특이사항
[SetUp] 에서 Singleton<Localization>.Clear() + InMemoryLocaleStorage 주입으로 격리.
========================================================================= BLOCK_HEADER_END */

using System;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.TEST._Localization
{
    using inonego.Xeri.Localization;

    // ============================================================
    /// <summary>
    /// Localization 단위 테스트 — InMemory 저장소 주입으로 격리.
    /// </summary>
    // ============================================================
    public class TEST_Localization
    {

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 각 테스트 전 Singleton 슬롯을 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            Singleton<Localization>.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Singleton<Localization>.Clear();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// InMemory 저장소 + 초기 코드를 가진 Localization 을 등록한다.
        /// </summary>
        // ------------------------------------------------------------
        private Localization RegisterWith(string initial = null)
        {
            var loc = new Localization(new InMemoryLocaleStorage(initial));
            Singleton<Localization>.Register(loc);
            return loc;
        }

    #endregion

    #region E-1: 초기 LangCode = Storage.Load 결과

        [Test]
        public void TEST_Localization_초기_LangCode는_Storage_Load_반환()
        {
            var loc = RegisterWith("ko");

            Assert.AreEqual("ko", loc.LangCode);
            Assert.AreEqual("ko", Localization.CurrentLangCode);
        }

    #endregion

    #region E-2: Storage 미설정 시 LangCode null

        [Test]
        public void TEST_Localization_Storage_미설정시_LangCode_null()
        {
            var loc = RegisterWith(null);

            Assert.IsNull(loc.LangCode);
        }

    #endregion

    #region R-1: LangCode 변경 → OnLangCodeChange 발생

        [Test]
        public void TEST_Localization_LangCode_변경시_이벤트_발생()
        {
            var loc = RegisterWith("ko");

            string captured = null;
            loc.OnLangCodeChange += code => captured = code;

            loc.LangCode = "en";

            Assert.AreEqual("en", captured);
            Assert.AreEqual("en", loc.LangCode);
        }

    #endregion

    #region R-2: 동일 LangCode 재설정 시 이벤트 미발생

        [Test]
        public void TEST_Localization_동일_LangCode_재설정시_이벤트_미발생()
        {
            var loc = RegisterWith("ko");

            int callCount = 0;
            loc.OnLangCodeChange += _ => callCount++;

            loc.LangCode = "ko";

            Assert.AreEqual(0, callCount, "동일 코드 재설정은 이벤트가 발생하지 않아야 합니다");
        }

    #endregion

    #region R-3: LangCode 변경 → Storage.Save 호출

        [Test]
        public void TEST_Localization_LangCode_변경시_Storage_Save_호출()
        {
            var storage = new InMemoryLocaleStorage("ko");
            var loc     = new Localization(storage);
            Singleton<Localization>.Register(loc);

            loc.LangCode = "en";

            Assert.AreEqual("en", storage.Load(), "Storage 에 새 코드가 저장되어야 합니다");
        }

    #endregion

    #region X-1: LangCode null 설정 시 ArgumentNullException

        [Test]
        public void TEST_Localization_LangCode_null_설정시_예외()
        {
            var loc = RegisterWith("ko");

            Assert.Throws<ArgumentNullException>(() => loc.LangCode = null);
        }

    #endregion

    #region E-3: 정적 facade 양방향 동작

        [Test]
        public void TEST_Localization_정적_facade_양방향()
        {
            RegisterWith("ko");

            Localization.CurrentLangCode = "en";

            Assert.AreEqual("en", Localization.Current.LangCode);
            Assert.AreEqual("en", Localization.CurrentLangCode);
        }

    #endregion

    }
}
