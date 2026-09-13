/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_DataPackage.cs
수정일 : 2026-08-28

# 설명
DataPackage 직접 Table, Source 구성, 슬롯/Scope와 REF 소비 흐름 테스트.

# 테스트 구성
 T: 직접 Table 수명 흐름 (다중 타입/충돌/제거)
 D: Source 구성 흐름 (다중 Source/REF/Replace/Rollback/직접 Table 경계)
 S: 슬롯/Registry 흐름 (Scope/REF/OnChange/Clear)
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using inonego.Xeri.Data;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.Data.TEST._DataPackage
{

    // ================================================================================
    /// <summary>
    /// DataPackage 직접 Table, Source 구성, 슬롯 및 REF 핵심 기능 테스트 클래스.
    /// </summary>
    // ================================================================================
    public class TEST_DataPackage
    {

    #region 헬퍼

        // ============================================================
        /// <summary>
        /// 테스트용 데이터 클래스 A.
        /// </summary>
        // ============================================================
        [Serializable]
        private class TestDataA : ITableValue
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 데이터 Key.
            /// </summary>
            // ------------------------------------------------------------
            public string Key { get => key; set => key = value; }

            [SerializeField]
            private string key = null;

            // ------------------------------------------------------------
            /// <summary>
            /// 유효한 Key를 가지고 있는지 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool HasKey
            {
                get
                {
                    return !string.IsNullOrEmpty(key);
                }
            }

            public int Value = 0;
        }

        // ============================================================
        /// <summary>
        /// 테스트용 데이터 클래스 B.
        /// </summary>
        // ============================================================
        [Serializable]
        private class TestDataB : ITableValue
        {
            // ------------------------------------------------------------
            /// <summary>
            /// 데이터 Key.
            /// </summary>
            // ------------------------------------------------------------
            public string Key { get => key; set => key = value; }

            [SerializeField]
            private string key = null;

            // ------------------------------------------------------------
            /// <summary>
            /// 유효한 Key를 가지고 있는지 반환한다.
            /// </summary>
            // ------------------------------------------------------------
            public bool HasKey
            {
                get
                {
                    return !string.IsNullOrEmpty(key);
                }
            }

            public string Value = null;
        }

        // ============================================================
        /// <summary>
        /// TestDataA 전용 테스트 Table.
        /// </summary>
        // ============================================================
        [Serializable]
        private class TestTableA : Table_V<TestDataA>
        {
            // NONE
        }

        // ============================================================
        /// <summary>
        /// TestDataB 전용 테스트 Table.
        /// </summary>
        // ============================================================
        [Serializable]
        private class TestTableB : Table_V<TestDataB>
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// TestDataA 값 목록으로 테스트용 Table을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static TestTableA CreateTableA(params TestDataA[] values)
        {
            var lTable = new TestTableA();

            // 테스트 시나리오에서 선언한 row reference를 그대로 Table 입력으로 구성한다.
            foreach (var value in values)
            {
                lTable.Add(value);
            }

            return lTable;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// TestDataB 값 목록으로 테스트용 Table을 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static TestTableB CreateTableB(params TestDataB[] values)
        {
            var lTable = new TestTableB();

            // 테스트 시나리오에서 선언한 row reference를 그대로 Table 입력으로 구성한다.
            foreach (var value in values)
            {
                lTable.Add(value);
            }

            return lTable;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 고정 test provider와 지정 location으로 Source를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private static DataPackage.Source CreateSource(string location)
        {
            return new DataPackage.Source("test", location);
        }

    #endregion

    #region 픽스처

        // ------------------------------------------------------------
        /// <summary>
        /// 각 테스트 전에 정적 슬롯 레지스트리를 초기화한다.
        /// </summary>
        // ------------------------------------------------------------
        [SetUp]
        public void SetUp()
        {
            DataPackage.Clear();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 각 테스트 후에 정적 슬롯 레지스트리를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        [TearDown]
        public void TearDown()
        {
            DataPackage.Clear();
        }

    #endregion

    #region T-1: 직접 Table 수명 흐름

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 서로 다른 타입의 Table을 함께 사용하다 동일 타입 재등록이 실패한 뒤에도
        /// <br/> 기존 조회 상태가 유지되고, 한 타입 제거가 다른 타입에 영향을 주지 않는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Direct_Table_추가_충돌_제거_흐름()
        {
            var package = new DataPackage();
            var valueA1 = new TestDataA { Key = "A1", Value = 100 };
            var valueB1 = new TestDataB { Key = "B1", Value = "B1" };

            package.AddTable<TestTableA, TestDataA>(CreateTableA(valueA1));
            package.AddTable<TestTableB, TestDataB>(CreateTableB(valueB1));

            // 두 타입이 함께 공개된 상태에서 기존 row reference를 그대로 조회해야 한다.
            Assert.AreSame(valueA1, package.Read<TestDataA>("A1"));
            Assert.AreSame(valueB1, package.Read<TestDataB>("B1"));

            // 동일 타입 재등록 실패가 이미 공개된 두 Table 상태를 훼손하면 안 된다.
            Assert.Throws<InvalidOperationException>
            (
                () => package.AddTable<TestTableA, TestDataA>
                (
                    CreateTableA(new TestDataA { Key = "A2", Value = 999 })
                )
            );

            Assert.AreSame(valueA1, package.Read<TestDataA>("A1"));
            Assert.AreSame(valueB1, package.Read<TestDataB>("B1"));

            // A Table 제거 후에도 B Table은 같은 상태로 계속 사용되어야 한다.
            package.RemoveTable<TestDataA>();

            Assert.IsNull(package.TryRead<TestDataA>("A1"));
            Assert.AreSame(valueB1, package.Read<TestDataB>("B1"));
            Assert.Throws<InvalidOperationException>(() => package.RemoveTable<TestDataA>());
        }

    #endregion

    #region D-1: 다중 Source 조회와 제거

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 서로 다른 Source의 같은 타입 데이터를 함께 조회하고 REF로 소비한 뒤,
        /// <br/> Source 제거에 따라 해당 데이터만 순차적으로 사라지는 흐름을 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_추가_REF_조회_제거_흐름()
        {
            var package = new DataPackage();
            var sourceA = CreateSource("a.xml");
            var sourceB = CreateSource("b.xml");

            var valueA1 = new TestDataA { Key = "A1", Value = 100 };
            var valueA2 = new TestDataA { Key = "A2", Value = 200 };
            var valueB1 = new TestDataA { Key = "B1", Value = 300 };

            package.AddSource(sourceA, CreateTableA(valueA1, valueA2));
            package.AddSource(sourceB, CreateTableA(valueB1));
            DataPackage.Register(package);

            var refA1 = new REF<TestDataA>("A1");
            var refB1 = new REF<TestDataA>("B1");

            // 두 Source가 generic/non-generic Table view와 REF 조회 경로에 함께 반영되어야 한다.
            Assert.AreEqual(3, package.Table<TestDataA>().Count);
            Assert.AreEqual(3, package.Table(typeof(TestDataA)).Count);
            Assert.AreSame(valueA2, package.Table<TestDataA>().Dictionary["A2"]);
            Assert.AreSame(valueA1, refA1.ToValue());
            Assert.AreSame(valueB1, refB1.ToValue());

            // Source A 제거 후 A의 값만 사라지고 Source B의 값은 계속 조회되어야 한다.
            package.RemoveSource(sourceA);

            Assert.IsNull(refA1.ToValue());
            Assert.AreSame(valueB1, refB1.ToValue());
            Assert.AreEqual(1, package.Table<TestDataA>().Count);

            // 마지막 Source까지 제거하면 해당 ValueType의 logical Table도 사라져야 한다.
            package.RemoveSource(sourceB);

            Assert.IsNull(refB1.ToValue());
            Assert.Throws<InvalidOperationException>(() => package.Table<TestDataA>());
        }

    #endregion

    #region D-2: Source 교체와 REF 재해석

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Source reload 뒤 기존 REF가 새 row를 해석하고 ValueType 구성도 갱신되며,
        /// <br/> 다른 Source의 데이터는 영향 없이 유지되는 흐름을 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_교체_REF_재해석_흐름()
        {
            var package = new DataPackage();
            var sourceA = CreateSource("a.xml");
            var sourceB = CreateSource("b.xml");

            var valueA1Old = new TestDataA { Key = "A1", Value = 100 };
            var valueA2    = new TestDataA { Key = "A2", Value = 200 };
            var valueB1    = new TestDataA { Key = "B1", Value = 300 };

            package.AddSource(sourceA, CreateTableA(valueA1Old, valueA2));
            package.AddSource(sourceB, CreateTableA(valueB1));
            DataPackage.Register(package);

            var refA1     = new REF<TestDataA>("A1");
            var refA2     = new REF<TestDataA>("A2");
            var refB1     = new REF<TestDataA>("B1");
            var refTypeB1 = new REF<TestDataB>("A1");

            var valueA1New = new TestDataA { Key = "A1", Value = 500 };
            var valueA3    = new TestDataA { Key = "A3", Value = 400 };
            var valueTypeB1 = new TestDataB { Key = "A1", Value = "B1" };

            package.ReplaceSource
            (
                sourceA,
                new ITable[]
                {
                    CreateTableA(valueA1New, valueA3),
                    CreateTableB(valueTypeB1),
                }
            );

            // A/B ValueType은 같은 "A1" Key를 독립적으로 해석하면서 reload 결과를 각각 반영해야 한다.
            Assert.AreSame(valueA1New, refA1.ToValue());
            Assert.IsNull(refA2.ToValue());
            Assert.AreSame(valueB1, refB1.ToValue());
            Assert.AreSame(valueTypeB1, refTypeB1.ToValue());
            Assert.AreSame(valueA3, package.Read<TestDataA>("A3"));

            var valueTypeB2 = new TestDataB { Key = "A1", Value = "B2" };

            // Source A가 더 이상 A 타입을 제공하지 않아도 Source B의 A lookup은 유지되어야 한다.
            package.ReplaceSource(sourceA, CreateTableB(valueTypeB2));

            Assert.IsNull(refA1.ToValue());
            Assert.AreSame(valueB1, refB1.ToValue());
            Assert.AreSame(valueTypeB2, refTypeB1.ToValue());
            Assert.AreEqual(1, package.Table<TestDataA>().Count);
        }

    #endregion

    #region D-3: Source 재구성 실패 롤백

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 동일 Source 재추가와 다른 Source Key 충돌이 실패했을 때,
        /// <br/> 이미 공개된 lookup과 REF 결과가 기존 상태를 유지하는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_재구성_실패_기존_상태_유지()
        {
            var package = new DataPackage();
            var sourceA = CreateSource("a.xml");
            var sourceB = CreateSource("b.xml");

            var valueA1 = new TestDataA { Key = "A1", Value = 100 };
            var valueB1 = new TestDataA { Key = "B1", Value = 200 };

            package.AddSource(sourceA, CreateTableA(valueA1));
            package.AddSource(sourceB, CreateTableA(valueB1));
            DataPackage.Register(package);

            var refA1 = new REF<TestDataA>("A1");
            var refB1 = new REF<TestDataA>("B1");

            // 동일 Source를 AddSource로 다시 넣는 호출은 reload로 해석하지 않고 기존 상태를 유지해야 한다.
            Assert.Throws<InvalidOperationException>
            (
                () => package.AddSource
                (
                    sourceA,
                    CreateTableA(new TestDataA { Key = "A2", Value = 999 })
                )
            );

            Assert.AreSame(valueA1, refA1.ToValue());
            Assert.AreSame(valueB1, refB1.ToValue());

            // ReplaceSource가 다른 Source의 Key와 충돌하면 incoming 값이 일부라도 노출되면 안 된다.
            Assert.Throws<InvalidOperationException>
            (
                () => package.ReplaceSource
                (
                    sourceA,
                    CreateTableA
                    (
                        new TestDataA { Key = "A2", Value = 300 },
                        new TestDataA { Key = "B1", Value = 400 }
                    )
                )
            );

            Assert.AreSame(valueA1, refA1.ToValue());
            Assert.AreSame(valueB1, refB1.ToValue());
            Assert.IsNull(package.TryRead<TestDataA>("A2"));
            Assert.AreEqual(2, package.Table<TestDataA>().Count);
        }

    #endregion

    #region D-4: 직접 Table과 Source 구성 경계

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 같은 ValueType을 direct AddTable과 Source 구성으로 섞으려는 흐름이 실패하고,
        /// <br/> 실패 전 기존 조회 상태가 그대로 유지되는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Direct_Table_Source_혼용_실패_상태_유지()
        {
            var directPackage = new DataPackage();
            var sourcePackage = new DataPackage();
            var source = CreateSource("a.xml");

            var valueA1 = new TestDataA { Key = "A1", Value = 10 };
            var valueA2 = new TestDataA { Key = "A2", Value = 20 };

            directPackage.AddTable<TestTableA, TestDataA>(CreateTableA(valueA1));

            // direct Table이 이미 공개된 타입에는 Source lookup을 추가하지 않는다.
            Assert.Throws<InvalidOperationException>
            (
                () => directPackage.AddSource(source, CreateTableA(valueA2))
            );

            Assert.AreSame(valueA1, directPackage.Read<TestDataA>("A1"));
            Assert.IsNull(directPackage.TryRead<TestDataA>("A2"));

            sourcePackage.AddSource(source, CreateTableA(valueA2));

            // 반대 방향도 같은 타입의 기존 Source 조회 상태를 덮어쓰지 않아야 한다.
            Assert.Throws<InvalidOperationException>
            (
                () => sourcePackage.AddTable<TestTableA, TestDataA>(CreateTableA(valueA1))
            );

            Assert.Throws<InvalidOperationException>(() => sourcePackage.RemoveTable<TestDataA>());
            Assert.AreSame(valueA2, sourcePackage.Read<TestDataA>("A2"));
        }

    #endregion

    #region S-1: Scope와 REF 컨텍스트 전환

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> 슬롯별 DataPackage를 중첩 Scope로 전환할 때 REF가 현재 슬롯 값을 해석하고,
        /// <br/> Scope 종료 순서에 따라 이전 컨텍스트로 정확히 복원되는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Scope_REF_중첩_전환_복원_흐름()
        {
            var packageA = new DataPackage();
            var packageB = new DataPackage();

            packageA.AddTable<TestTableA, TestDataA>
            (
                CreateTableA(new TestDataA { Key = "A1", Value = 100 })
            );
            packageB.AddTable<TestTableA, TestDataA>
            (
                CreateTableA(new TestDataA { Key = "A1", Value = 200 })
            );

            DataPackage.Register("A", packageA);
            DataPackage.Register("B", packageB);

            var refA1 = new REF<TestDataA>("A1");

            // 바깥 Scope의 조회 상태를 기준으로 안쪽 Scope가 일시적으로 컨텍스트를 교체한다.
            using (DataPackage.Scope("A"))
            {
                Assert.AreEqual(100, refA1.ToValue().Value);

                using (DataPackage.Scope("B"))
                {
                    Assert.AreEqual(200, refA1.ToValue().Value);
                }

                Assert.AreEqual(100, refA1.ToValue().Value);
            }

            // 모든 Scope가 끝나면 미등록 기본 슬롯로 복원되어 REF가 값을 해석하지 못해야 한다.
            Assert.IsNull(refA1.ToValue());
        }

    #endregion

    #region S-2: Registry 변경 이벤트 흐름

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Register·Unregister·Clear는 변경 이벤트를 발생시키고 Scope 전환은 제외되며,
        /// <br/> 최종 Clear 뒤 현재 슬롯 상태까지 함께 정리되는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Registry_변경_이벤트_및_Clear_흐름()
        {
            var changeCount = 0;
            Action onChange = () => changeCount++;

            DataPackage.OnChange += onChange;

            try
            {
                DataPackage.Register(new DataPackage());
                DataPackage.Register("SUB", new DataPackage());

                var countBeforeScope = changeCount;

                // Scope는 등록 상태를 바꾸지 않으므로 변경 이벤트가 추가로 발생하면 안 된다.
                using (DataPackage.Scope("SUB"))
                {
                    Assert.IsTrue(DataPackage.TryCurrent(out _));
                }

                Assert.AreEqual(countBeforeScope, changeCount);

                DataPackage.Unregister("SUB");
                DataPackage.Clear();

                Assert.AreEqual(4, changeCount);
                Assert.IsFalse(DataPackage.TryCurrent(out _));
            }
            finally
            {
                // 같은 delegate instance로 구독을 해제해 후속 테스트에 정적 이벤트가 누적되지 않게 한다.
                DataPackage.OnChange -= onChange;
                DataPackage.Clear();
            }
        }

    #endregion

    }

}
