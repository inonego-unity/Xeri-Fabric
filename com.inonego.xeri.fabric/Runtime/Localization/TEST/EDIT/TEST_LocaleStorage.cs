/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_LocaleStorage.cs
수정일 : 2026-05-09

# 설명
ILocaleStorage / InMemoryLocaleStorage 단위 테스트.

# 테스트 구성
 S: Save/Load 왕복
========================================================================= BLOCK_HEADER_END */

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.TEST._LocaleStorage
{
    using inonego.Xeri.Localization;

    // ============================================================
    /// <summary>
    /// LocaleStorage 단위 테스트.
    /// </summary>
    // ============================================================
    public class TEST_LocaleStorage
    {

    #region S-1: InMemory 초기값 + Save/Load 왕복

        [Test]
        public void TEST_InMemoryLocaleStorage_초기값_없음()
        {
            var storage = new InMemoryLocaleStorage();

            Assert.IsNull(storage.Load(), "초기값 없으면 null 반환");
        }

        [Test]
        public void TEST_InMemoryLocaleStorage_초기값_지정()
        {
            var storage = new InMemoryLocaleStorage("ko");

            Assert.AreEqual("ko", storage.Load());
        }

        [Test]
        public void TEST_InMemoryLocaleStorage_Save_Load_왕복()
        {
            var storage = new InMemoryLocaleStorage();

            storage.Save("en");
            Assert.AreEqual("en", storage.Load());

            storage.Save("jp");
            Assert.AreEqual("jp", storage.Load());
        }

    #endregion

    }
}
