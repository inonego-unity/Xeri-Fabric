/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_RecordType.cs
수정일 : 2026-10-29

# 설명
Persistence RecordType의 기록 종류와 지원 버전의 동일성 검사 계약을 검증한다.
========================================================================= BLOCK_HEADER_END */

using System;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.Persistence.TEST
{
    // ============================================================
    /// <summary>
    /// RecordType 종류·버전 검증 테스트.
    /// </summary>
    // ============================================================
    public sealed class TEST_RecordType
    {

    #region 테스트 자료형

        // ============================================================
        /// <summary>
        /// 특정 Save 구현 없이 공통 Metadata 매핑을 검증한다.
        /// </summary>
        // ============================================================
        private sealed class TestMetadata : IRecordMetadata
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Metadata의 스키마 식별자.
            /// </summary>
            // ------------------------------------------------------------
            public string SchemaID { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 Metadata의 스키마 버전.
            /// </summary>
            // ------------------------------------------------------------
            public string SchemaVersion { get; set; }

            string IRecordMetadata.TypeID => SchemaID;
            string IRecordMetadata.Version => SchemaVersion;
        }

        // ============================================================
        /// <summary>
        /// Metadata와 Body 구조 계약을 확인하는 테스트 기록.
        /// </summary>
        // ============================================================
        private sealed class TestRecord : IRecord<object>
        {

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 기록의 Metadata.
            /// </summary>
            // ------------------------------------------------------------
            public TestMetadata Metadata { get; set; }

            // ------------------------------------------------------------
            /// <summary>
            /// 테스트 기록의 Body.
            /// </summary>
            // ------------------------------------------------------------
            public object Body { get; set; }

            IRecordMetadata IRecord<object>.Metadata => Metadata;

            // ------------------------------------------------------------
            /// <summary>
            /// 빈 테스트 기록을 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestRecord()
            {
                // NONE
            }

            // ------------------------------------------------------------
            /// <summary>
            /// Metadata와 Body를 포함하는 테스트 기록을 생성한다.
            /// </summary>
            // ------------------------------------------------------------
            public TestRecord(TestMetadata metadata, object body)
            {
                Metadata = metadata;
                Body = body;
            }
        }

    #endregion

    #region 종류와 버전

        // ------------------------------------------------------------
        /// <summary>
        /// 동일한 기록 종류·버전은 일치하며 다른 종류나 버전은 일치하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void RecordType_종류와_버전_정확_일치()
        {
            var recordType = new RecordType("document", "1.0");

            Assert.IsTrue(recordType.IsMatch("document", "1.0"));
            Assert.IsFalse(recordType.IsMatch("Document", "1.0"));
            Assert.IsFalse(recordType.IsMatch("document", "1.0.0"));
            Assert.IsFalse(recordType.IsMatch("document", null));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지원하지 않는 기록 종류 또는 버전을 명확히 실패시킨다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void RecordType_불일치_검증_실패()
        {
            var recordType = new RecordType("document", "1.0");

            Assert.Throws<InvalidOperationException>(() => recordType.Validate("save", "1.0"));
            Assert.Throws<InvalidOperationException>(() => recordType.Validate("document", "2.0"));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 공백 종류·버전으로는 기록 계약을 생성하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void RecordType_빈_계약_거절()
        {
            Assert.Throws<ArgumentException>(() => new RecordType("", "1.0"));
            Assert.Throws<ArgumentException>(() => new RecordType("document", " "));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 유효한 계약은 Validation 예외 없이 사용될 수 있다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void RecordType_유효한_기록_검증()
        {
            var recordType = new RecordType("save.snapshot", "2");

            recordType.Validate("save.snapshot", "2");

            Assert.AreEqual("save.snapshot", recordType.TypeID);
            Assert.AreEqual("2", recordType.Version);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// Save Metadata/Record가 공통 종류·버전 구조 계약을 구현하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        [Test]
        public void RecordType_공통_Record_Metadata_검증()
        {
            var recordType = new RecordType("save.snapshot", "2");
            var metadata = new TestMetadata
            {
                SchemaID = "save.snapshot",
                SchemaVersion = "2"
            };
            var record = new TestRecord(metadata, new object());

            Assert.IsTrue(recordType.IsMatch((IRecordMetadata)metadata));
            recordType.Validate((IRecordMetadata)metadata);
            recordType.Validate(record);

            metadata.SchemaVersion = "3";
            Assert.IsFalse(recordType.IsMatch((IRecordMetadata)metadata));
            Assert.Throws<InvalidOperationException>(() => recordType.Validate(record));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Metadata/Body가 없는 Record는 공통 검증에서 거절한다.
        /// </summary>
        // ------------------------------------------------------------
        [Test]
        public void RecordType_누락_Record_거절()
        {
            var recordType = new RecordType("save.snapshot", "2");
            var metadata = new TestMetadata
            {
                SchemaID = "save.snapshot",
                SchemaVersion = "2"
            };
            var record = new TestRecord(metadata, null);

            Assert.Throws<InvalidOperationException>(() => recordType.Validate(record));

            record.Body = new object();
            record.Metadata = null;
            Assert.Throws<InvalidOperationException>(() => recordType.Validate(record));
        }

    #endregion

    }
}
