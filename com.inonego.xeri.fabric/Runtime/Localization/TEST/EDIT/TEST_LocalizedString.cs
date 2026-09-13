/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_LocalizedString.cs
수정일 : 2026-05-09

# 설명
LocalizedString 데이터 구조 단위 테스트.

# 테스트 구성
 E: 키 조회 / 인덱서 / ToLocalized
 X: null safe / 미등록 키
========================================================================= BLOCK_HEADER_END */

using System;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.TEST._LocalizedString
{
    using inonego.Xeri.Localization;

    // ============================================================
    /// <summary>
    /// LocalizedString 단위 테스트 클래스.
    /// </summary>
    // ============================================================
    public class TEST_LocalizedString
    {

    #region E-1: 등록된 키 조회

        [Test]
        public void TEST_LocalizedString_등록된_키_조회()
        {
            var loc = new LocalizedString
            {
                ["ko"] = "안녕",
                ["en"] = "Hello",
            };

            Assert.AreEqual("안녕",  loc["ko"]);
            Assert.AreEqual("Hello", loc["en"]);
        }

    #endregion

    #region E-2: 미등록 키 조회 → 빈 문자열

        [Test]
        public void TEST_LocalizedString_미등록_키_빈_문자열_반환()
        {
            var loc = new LocalizedString
            {
                ["ko"] = "안녕",
            };

            Assert.AreEqual(string.Empty, loc["en"], "미등록 키는 빈 문자열을 반환해야 합니다");
            Assert.AreEqual(string.Empty, loc["unknown"]);
        }

    #endregion

    #region E-3: ILocalizedString 인터페이스 인덱서

        [Test]
        public void TEST_LocalizedString_ILocalizedString_인덱서_동작()
        {
            ILocalizedString loc = new LocalizedString
            {
                ["ko"] = "안녕",
            };

            Assert.AreEqual("안녕",       loc["ko"]);
            Assert.AreEqual(string.Empty, loc["missing"]);
        }

    #endregion

    #region X-1: ToLocalized null safe

        [Test]
        public void TEST_LocalizedString_ToLocalized_null_safe()
        {
            ILocalizedString nullLoc = null;

            Assert.AreEqual(string.Empty, nullLoc.ToLocalized("ko"));
            Assert.AreEqual(string.Empty, nullLoc.ToLocalized());
        }

    #endregion

    #region X-2: ToLocalized null code

        [Test]
        public void TEST_LocalizedString_ToLocalized_null_code()
        {
            var loc = new LocalizedString { ["ko"] = "안녕" };

            Assert.AreEqual(string.Empty, loc.ToLocalized(null));
        }

    #endregion

    }
}
