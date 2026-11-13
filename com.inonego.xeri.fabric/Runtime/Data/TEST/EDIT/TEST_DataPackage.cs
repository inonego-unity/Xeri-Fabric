/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_DataPackage.cs
수정일 : 2026-11-13

# 설명
DataPackage 직접 Table, Source 구성, 슬롯/Scope와 REF 소비 흐름 테스트.

# 테스트 범위
- 직접 Table 등록과 충돌·제거
- Source 등록·교체·제거와 조회·소유권 정합성
- 슬롯·Scope·Registry와 REF 소비 흐름
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using inonego;
using inonego.Xeri;
using inonego.Xeri.Data;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.Data.TEST._DataPackage
{

    // ======================================================================
    /// <summary>
    /// DataPackage 직접 Table, Source 구성, 슬롯 및 REF 핵심 기능 테스트 클래스.
    /// </summary>
    // ======================================================================
    public class TEST_DataPackage
    {

    #region 테스트 데이터 및 헬퍼

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

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 데이터 A의 수치 값이다.
            /// </summary>
            // ------------------------------------------------------------
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

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 데이터 B의 문자열 값이다.
            /// </summary>
            // ------------------------------------------------------------
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

    #region 테스트 수명

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

    #region 직접 Table 등록 및 제거

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
            var valueA1 = new TestDataA
            {
                Key = "A1",
                Value = 100
            };
            var valueB1 = new TestDataB
            {
                Key = "B1",
                Value = "B1"
            };

            package.AddTable<TestTableA, TestDataA>(CreateTableA(valueA1));
            package.AddTable<TestTableB, TestDataB>(CreateTableB(valueB1));

            // 두 타입이 함께 공개된 상태에서 기존 row reference를 그대로 조회해야 한다.
            Assert.AreSame(valueA1, package.Read<TestDataA>("A1"));
            Assert.AreSame(valueB1, package.Read<TestDataB>("B1"));
            Assert.Throws<KeyNotFoundException>(() => package.Read<TestDataA>("missing"));
            Assert.IsNull(package.TryRead<TestDataA>("missing"));

            // 동일 타입 재등록 실패가 이미 공개된 두 Table 상태를 훼손하면 안 된다.
            Assert.Throws<InvalidOperationException>
            (
                () => package.AddTable<TestTableA, TestDataA>
                (
                    CreateTableA
                    (
                        new TestDataA
                        {
                            Key = "A2",
                            Value = 999
                        }
                    )
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

    #region Source 등록 및 변경

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 서로 다른 Source의 같은 타입 데이터를 함께 조회하고 REF로 소비한 뒤,
        /// <br/> Source 제거에 따라 해당 데이터만 순차적으로 사라지는 흐름을 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_추가_REF_조회_제거_흐름()
        {
            var package = new DataPackage();
            var sourceA = CreateSource("a.xml");
            var sourceB = CreateSource("b.xml");

            var valueA1 = new TestDataA
            {
                Key = "A1",
                Value = 100
            };
            var valueA2 = new TestDataA
            {
                Key = "A2",
                Value = 200
            };
            var valueB1 = new TestDataA
            {
                Key = "B1",
                Value = 300
            };

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
            Assert.Throws<KeyNotFoundException>(() => package.Read<TestDataA>("missing"));
            Assert.IsNull(package.TryRead<TestDataA>("missing"));

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

            var valueA1Old = new TestDataA
            {
                Key = "A1",
                Value = 100
            };
            var valueA2 = new TestDataA
            {
                Key = "A2",
                Value = 200
            };
            var valueB1 = new TestDataA
            {
                Key = "B1",
                Value = 300
            };

            package.AddSource(sourceA, CreateTableA(valueA1Old, valueA2));
            package.AddSource(sourceB, CreateTableA(valueB1));
            DataPackage.Register(package);

            var refA1 = new REF<TestDataA>("A1");
            var refA2 = new REF<TestDataA>("A2");
            var refB1 = new REF<TestDataA>("B1");
            var refTypeB1 = new REF<TestDataB>("A1");

            var valueA1New = new TestDataA
            {
                Key = "A1",
                Value = 500
            };
            var valueA3 = new TestDataA
            {
                Key = "A3",
                Value = 400
            };
            var valueTypeB1 = new TestDataB
            {
                Key = "A1",
                Value = "B1"
            };

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

            var valueTypeB2 = new TestDataB
            {
                Key = "A1",
                Value = "B2"
            };

            // Source A가 더 이상 A 타입을 제공하지 않아도 Source B의 A lookup은 유지되어야 한다.
            package.ReplaceSource(sourceA, CreateTableB(valueTypeB2));

            Assert.IsNull(refA1.ToValue());
            Assert.AreSame(valueB1, refB1.ToValue());
            Assert.AreSame(valueTypeB2, refTypeB1.ToValue());
            Assert.AreEqual(1, package.Table<TestDataA>().Count);
        }

    #endregion

    #region Source 충돌 및 상태 보존

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 동일 Source 재추가와 다른 Source Key 충돌이 실패했을 때,
        /// <br/> 이미 공개된 lookup과 REF 결과가 기존 상태를 유지하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_재구성_실패_기존_상태_유지()
        {
            var package = new DataPackage();
            var sourceA = CreateSource("a.xml");
            var sourceB = CreateSource("b.xml");

            var valueA1 = new TestDataA
            {
                Key = "A1",
                Value = 100
            };
            var valueB1 = new TestDataA
            {
                Key = "B1",
                Value = 200
            };

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

                    CreateTableA
                    (
                        new TestDataA
                        {
                            Key = "A2",
                            Value = 999
                        }
                    )
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
                        new TestDataA
                        {
                            Key = "A2",
                            Value = 300
                        },

                        new TestDataA
                        {
                            Key = "B1",
                            Value = 400
                        }
                    )
                )
            );

            Assert.AreSame(valueA1, refA1.ToValue());
            Assert.AreSame(valueB1, refB1.ToValue());
            Assert.IsNull(package.TryRead<TestDataA>("A2"));
            Assert.AreEqual(2, package.Table<TestDataA>().Count);
        }

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

            var valueA1 = new TestDataA
            {
                Key = "A1",
                Value = 10
            };
            var valueA2 = new TestDataA
            {
                Key = "A2",
                Value = 20
            };

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

    #region Source 조회 및 Table View

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Type/Key별 Source 역조회와 기존 제네릭/비제네릭 Table View가
        /// <br/> 동일한 Row 참조를 노출하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_소유자_조회_및_Table_View_호환()
        {
            var package = new DataPackage();
            var sourceA = CreateSource("a.xml");
            var sourceB = CreateSource("b.xml");
            var valueA = new TestDataA
            {
                Key = "shared",
                Value = 100
            };
            var valueB = new TestDataB
            {
                Key = "shared",
                Value = "B"
            };
            var other = new TestDataA
            {
                Key = "other",
                Value = 200
            };

            package.AddSource
            (
                sourceA,
                new ITable[]
                {
                    CreateTableA(valueA),
                    CreateTableB(valueB)
                }
            );
            package.AddSource(sourceB, CreateTableA(other));

            Assert.IsTrue(package.TryFindSource<TestDataA>("shared", out var ownerA));
            Assert.AreEqual(sourceA, ownerA);
            Assert.IsTrue(package.TryFindSource<TestDataB>("shared", out var ownerB));
            Assert.AreEqual(sourceA, ownerB);
            Assert.IsTrue(package.TryFindSource<TestDataA>("other", out var otherOwner));
            Assert.AreEqual(sourceB, otherOwner);
            Assert.IsFalse(package.TryFindSource<TestDataB>("other", out var missingOwner));
            Assert.AreEqual(default(DataPackage.Source), missingOwner);

            var typed = package.Table<TestDataA>();
            var nonGeneric = package.Table(typeof(TestDataA));

            Assert.AreSame(valueA, package.Read<TestDataA>("shared"));
            Assert.AreSame(valueA, typed.Dictionary["shared"]);
            Assert.AreSame(other, typed.Dictionary["other"]);
            Assert.IsTrue(typed.Dictionary.TryGetValue("shared", out var found));
            Assert.AreSame(valueA, found);
            Assert.AreSame(valueA, nonGeneric["shared"]);
            Assert.AreEqual(2, typed.Count);
            Assert.AreEqual(2, nonGeneric.Count);

            var typedValues = 0;
            foreach (var value in typed.Values)
            {
                Assert.AreSame(package.Read<TestDataA>(value.Key), value);
                typedValues++;
            }

            Assert.AreEqual(2, typedValues);

            var values = 0;
            foreach (var value in nonGeneric.Values)
            {
                Assert.AreSame(package.Read<TestDataA>(value.Key), value);
                values++;
            }

            Assert.AreEqual(2, values);

            var seen = 0;
            foreach (var pair in nonGeneric)
            {
                Assert.AreSame(package.Read<TestDataA>(pair.Key), pair.Value);
                seen++;
            }

            Assert.AreEqual(2, seen);
        }

    #endregion

    #region Source 교체 및 등록본 정합성

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 실패한 교체는 기존 Row/Source를 유지하고, 성공한 교체와 제거는
        /// <br/> 이전 Key의 소유권을 남기지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_교체_제거_충돌_출처_원자성()
        {
            var package = new DataPackage();
            var sourceA = CreateSource("a.xml");
            var sourceB = CreateSource("b.xml");
            var original = new TestDataA
            {
                Key = "A1",
                Value = 10
            };
            var other = new TestDataA
            {
                Key = "B1",
                Value = 20
            };

            package.AddSource(sourceA, CreateTableA(original));
            package.AddSource(sourceB, CreateTableA(other));
            var previousTable = package.Table<TestDataA>();

            Assert.Throws<InvalidOperationException>
            (
                () => package.ReplaceSource
                (
                    sourceA,
                    CreateTableA
                    (
                        new TestDataA
                        {
                            Key = "B1",
                            Value = 999
                        }
                    )
                )
            );

            Assert.AreSame(original, package.Read<TestDataA>("A1"));
            Assert.IsTrue(package.TryFindSource<TestDataA>("A1", out var owner));
            Assert.AreEqual(sourceA, owner);
            Assert.IsTrue(package.TryFindSource<TestDataA>("B1", out owner));
            Assert.AreEqual(sourceB, owner);

            var replacement = new TestDataA
            {
                Key = "A2",
                Value = 30
            };
            package.ReplaceSource(sourceA, CreateTableA(replacement));

            // 이전 View는 교체 전 Value Dictionary를 계속 참조한다.
            Assert.AreSame(original, previousTable["A1"]);
            Assert.IsNull(previousTable["A2"]);
            Assert.AreEqual(2, previousTable.Count);
            Assert.AreSame(replacement, package.Table<TestDataA>()["A2"]);

            Assert.IsNull(package.TryRead<TestDataA>("A1"));
            Assert.IsFalse(package.TryFindSource<TestDataA>("A1", out owner));
            Assert.AreEqual(default(DataPackage.Source), owner);
            Assert.AreSame(replacement, package.Read<TestDataA>("A2"));
            Assert.IsTrue(package.TryFindSource<TestDataA>("A2", out owner));
            Assert.AreEqual(sourceA, owner);

            package.RemoveSource(sourceB);
            Assert.IsFalse(package.TryFindSource<TestDataA>("B1", out _));

            var returned = new TestDataA
            {
                Key = "A1",
                Value = 40
            };

            package.AddSource(sourceB, CreateTableA(returned));
            Assert.IsTrue(package.TryFindSource<TestDataA>("A1", out owner));
            Assert.AreEqual(sourceB, owner);

            package.RemoveSource(sourceA);
            Assert.IsFalse(package.TryFindSource<TestDataA>("A2", out _));
            Assert.IsTrue(package.TryFindSource<TestDataA>("A1", out owner));
            Assert.AreEqual(sourceB, owner);
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// <br/> Source 등록 및 교체 뒤 원본 Table의 Key·Row 매핑을 변경해도
        /// <br/> 다른 Source의 추가·제거 시 등록된 Source 조회 상태가 유지되는지 검증한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_등록후_원본_Table_변경_격리()
        {
            var package = new DataPackage();
            var sourceA = CreateSource("a.xml");
            var sourceB = CreateSource("b.xml");
            var sourceC = CreateSource("c.xml");
            var originalA = new TestDataA
            {
                Key = "A",
                Value = 10
            };
            var originalB = new TestDataA
            {
                Key = "B",
                Value = 20
            };
            var tableA = CreateTableA(originalA, originalB);

            package.AddSource(sourceA, tableA);

            // 등록된 Source의 Key·Value 매핑은 입력 Table의 이후 변경과 분리된다.
            tableA.Dictionary["A"] = new TestDataA
            {
                Key = "A",
                Value = 999
            };
            tableA.Dictionary.Remove("B");
            var added = new TestDataA
            {
                Key = "C",
                Value = 30
            };
            var other = new TestDataA
            {
                Key = "D",
                Value = 40
            };

            tableA.Add(added);
            package.AddSource(sourceB, CreateTableA(other));

            Assert.AreSame(originalA, package.Read<TestDataA>("A"));
            Assert.AreSame(originalB, package.Read<TestDataA>("B"));
            Assert.IsNull(package.TryRead<TestDataA>("C"));
            Assert.IsTrue(package.TryFindSource<TestDataA>("B", out var owner));
            Assert.AreEqual(sourceA, owner);

            // 원본에서 삭제한 Key도 Source 등록에는 남으므로 새 Source가 소유할 수 없다.
            var conflicting = new TestDataA
            {
                Key = "B",
                Value = 50
            };
            Assert.Throws<InvalidOperationException>
            (
                () => package.AddSource(sourceC, CreateTableA(conflicting))
            );

            var replacement = new TestDataA
            {
                Key = "E",
                Value = 60
            };
            var replacementTable = CreateTableA(replacement);

            package.ReplaceSource(sourceA, replacementTable);
            replacementTable.Dictionary.Clear();
            package.RemoveSource(sourceB);

            Assert.IsNull(package.TryRead<TestDataA>("A"));
            Assert.IsNull(package.TryRead<TestDataA>("B"));
            Assert.AreSame(replacement, package.Read<TestDataA>("E"));
            Assert.IsTrue(package.TryFindSource<TestDataA>("E", out owner));
            Assert.AreEqual(sourceA, owner);

            // 등록 당시 Key와 Row 식별자가 다른 입력은 상태 변경 전에 거부한다.
            var invalidValue = new TestDataA
            {
                Key = "valid",
                Value = 70
            };
            var invalidTable = CreateTableA(invalidValue);
            invalidValue.Key = "changed";
            Assert.Throws<InvalidOperationException>
            (
                () => package.AddSource(sourceC, invalidTable)
            );
            Assert.IsFalse(package.HasSource(sourceC));
        }

    #endregion

    #region Source 재로드 수명 및 세대

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 만료된 Source 재로드는 외부 Table을 열거하지 않고 거부하며,
        /// <br/> 현재 Source 조회 결과를 유지하는지 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_만료된_재로드_입력_열거_생략()
        {
            var package = new DataPackage();
            var source = CreateSource("reload.xml");
            var original = new TestDataA
            {
                Key = "A",
                Value = 10
            };
            package.AddSource(source, CreateTableA(original));

            var registration = package.SourceRegistration(source);
            var staleGeneration = package.SourceGeneration(source);
            var latest = new TestDataA
            {
                Key = "A",
                Value = 20
            };
            package.ReplaceSource(source, CreateTableA(latest));

            var enumerationCount = 0;
            IEnumerable<ITable> StaleTables()
            {
                enumerationCount++;
                var stale = new TestDataA
                {
                    Key = "A",
                    Value = 99
                };
                yield return CreateTableA(stale);
            }

            Assert.IsFalse(package.TryReplaceSource(source, registration, staleGeneration, StaleTables()));
            Assert.IsFalse(package.TryReplaceSource(source, registration, staleGeneration, null));
            Assert.AreEqual(0, enumerationCount);
            Assert.AreEqual(20, package.Read<TestDataA>("A").Value);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 재로드 입력을 준비하는 동안 다른 교체가 발생하면 이전 결과를 거부하고,
        /// <br/> 마지막으로 공개된 Source 상태를 보존하는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Source_재로드_열거중_세대_변경_거부()
        {
            var package = new DataPackage();
            var source = CreateSource("reload.xml");
            var original = new TestDataA
            {
                Key = "A",
                Value = 10
            };
            package.AddSource(source, CreateTableA(original));

            var registration = package.SourceRegistration(source);
            var generation = package.SourceGeneration(source);
            IEnumerable<ITable> SupersededTables()
            {
                var latest = new TestDataA
                {
                    Key = "A",
                    Value = 30
                };
                package.ReplaceSource(source, CreateTableA(latest));
                var stale = new TestDataA
                {
                    Key = "A",
                    Value = 99
                };
                yield return CreateTableA(stale);
            }

            Assert.IsFalse(package.TryReplaceSource(source, registration, generation, SupersededTables()));
            Assert.AreEqual(30, package.Read<TestDataA>("A").Value);
        }

    #endregion

    #region REF 및 슬롯 Registry

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> REF는 Source를 보관하지 않고 현재 Scope의 Package에서 찾으며,
        /// <br/> 직접 참조와 직접 등록 Table에는 출처가 없는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_REF_Source_현재_Scope_및_Direct_구분()
        {
            var packageA = new DataPackage();
            var packageB = new DataPackage();
            var directPackage = new DataPackage();
            var sourceA = CreateSource("a.xml");
            var sourceB = CreateSource("b.xml");
            var directRow = new TestDataA
            {
                Key = "A1",
                Value = 1
            };
            var reference = new REF<TestDataA>("A1");
            var directReference = new REF<TestDataA>(directRow);
            var emptyReference = default(REF<TestDataA>);

            Assert.IsFalse(reference.TryFindSource(out _));
            Assert.IsFalse(emptyReference.TryFindSource(out _));

            var sourceValueA = new TestDataA
            {
                Key = "A1",
                Value = 10
            };
            var sourceValueB = new TestDataA
            {
                Key = "A1",
                Value = 20
            };

            packageA.AddSource(sourceA, CreateTableA(sourceValueA));
            packageB.AddSource(sourceB, CreateTableA(sourceValueB));
            directPackage.AddTable<TestTableA, TestDataA>(CreateTableA(directRow));

            DataPackage.Register("A", packageA);
            DataPackage.Register("B", packageB);
            DataPackage.Register("DIRECT", directPackage);

            using (DataPackage.Scope("A"))
            {
                Assert.IsTrue(reference.TryFindSource(out var owner));
                Assert.AreEqual(sourceA, owner);
                Assert.IsFalse(directReference.TryFindSource(out _));

                using (DataPackage.Scope("B"))
                {
                    Assert.IsTrue(reference.TryFindSource(out owner));
                    Assert.AreEqual(sourceB, owner);
                }

                Assert.IsTrue(reference.TryFindSource(out owner));
                Assert.AreEqual(sourceA, owner);
            }

            using (DataPackage.Scope("DIRECT"))
            {
                Assert.AreSame(directRow, reference.ToValue());
                Assert.IsFalse(directPackage.TryFindSource<TestDataA>("A1", out _));
                Assert.IsFalse(reference.TryFindSource(out _));
                Assert.IsFalse(directReference.TryFindSource(out _));
            }

            Assert.IsFalse(reference.TryFindSource(out _));
        }

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
                CreateTableA
                (
                    new TestDataA
                    {
                        Key = "A1",
                        Value = 100
                    }
                )
            );
            packageB.AddTable<TestTableA, TestDataA>
            (
                CreateTableA
                (
                    new TestDataA
                    {
                        Key = "A1",
                        Value = 200
                    }
                )
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

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> Named 인덱서는 지정 슬롯을 읽기 전용 DataPackage로 반환하고,
        /// <br/> 현재 Scope를 바꾸지 않는지 검증한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void TEST_DataPackage_Named_슬롯_조회_계약()
        {
            var package = new DataPackage();
            var other = new DataPackage();
            DataPackage.Register("GAME", package);
            DataPackage.Register("OTHER", other);

            using (DataPackage.Scope("OTHER"))
            {
                IReadOnlyDataPackage namedPackage = DataPackage.Named["GAME"];
                Assert.AreSame(package, namedPackage);
                Assert.AreSame(other, DataPackage.Current);
            }
        }

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