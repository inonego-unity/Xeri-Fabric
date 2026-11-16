/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ScopedFileSave.cs
수정일 : 2026-10-29

# 설명
Scope별 독립 Entry를 파일에 저장·복원·삭제하는 Save 진입점.
기존 FileSave를 조합하여 같은 Metadata/Schema·IO/Persistence/Commit 처리를 재사용한다.

# 주의
Entry 하나의 파일 교체만 지원한다. 여러 Entry의 공동 Revision Commit, 동시 쓰기,
예상 Revision 검사 및 전원 손실 내구성은 보장하지 않는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Persistence;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.Save
{
    // ======================================================================
    /// <summary>
    /// Scope 아래의 독립 Entry를 동일한 Snapshot 구조로 저장하는 Save 구성요소.
    /// </summary>
    /// <typeparam name="TBody">한 Entry의 직렬화 가능한 Body 타입.</typeparam>
    // ======================================================================
    public sealed class ScopedFileSave<TBody>
    where TBody : class
    {

    #region 필드

        private readonly string directory = string.Empty;
        private readonly RecordType schema = null;
        private readonly ISerializer serializer = null;
        private readonly IDataReader<string, string> reader = null;
        private readonly IDataWriter<string, string> writer = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 FileSave와 동일한 Schema/Serializer/IO 구성을 사용한다.
        /// </summary>
        // ------------------------------------------------------------
        public ScopedFileSave
        (
            string directory,
            RecordType schema,
            ISerializer serializer,
            IDataReader<string, string> reader = null,
            IDataWriter<string, string> writer = null
        )
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Save 디렉터리가 비어 있습니다.", nameof(directory));
            }

            this.directory = Path.GetFullPath(directory);
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.reader = reader;
            this.writer = writer;
        }

    #endregion

    #region Entry 기록·삭제

        // ------------------------------------------------------------
        /// <summary>
        /// 하나의 Scope 내 지정 Entry를 독립 파일로 저장한다.
        /// </summary>
        // ------------------------------------------------------------
        public WriteResponse WriteEntry(string scopeID, string entryID, TBody body)
        {
            try
            {
                return CreateFileSave(scopeID).Save(entryID, body);
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Scope에서 하나의 Entry만 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        public WriteResponse DeleteEntry(string scopeID, string entryID)
        {
            try
            {
                return CreateFileSave(scopeID).Delete(entryID);
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

    #endregion

    #region Entry 조회

        // ------------------------------------------------------------
        /// <summary>
        /// 하나의 Scope에서 필요한 Entry만 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<TBody> ReadEntry(string scopeID, string entryID)
        {
            try
            {
                return CreateFileSave(scopeID).Load(entryID);
            }
            catch (Exception exception)
            {
                return ReadResponse<TBody>.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Entry의 전체 기록과 Metadata를 복원한다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<SaveRecord<TBody>> ReadEntryRecord(string scopeID, string entryID)
        {
            try
            {
                return CreateFileSave(scopeID).LoadRecord(entryID);
            }
            catch (Exception exception)
            {
                return ReadResponse<SaveRecord<TBody>>.Fail(exception.Message, exception);
            }
        }

    #endregion

    #region Entry 목록·존재

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지정 Scope의 Entry 파일명을 정렬해 반환한다. 파일 내용은 검사하지 않는다.
        /// </summary>
        // ----------------------------------------------------------------------
        public ReadResponse<string[]> ListEntries(string scopeID)
        {
            try
            {
                return CreateFileSave(scopeID).ListKeys();
            }
            catch (Exception exception)
            {
                return ReadResponse<string[]>.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 Entry 파일의 존재 여부만 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool HasEntry(string scopeID, string entryID)
        {
            try
            {
                return CreateFileSave(scopeID).Exists(entryID);
            }
            catch
            {
                return false;
            }
        }

    #endregion

    #region 내부 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// Scope 경로를 검증하고 동일한 FileSave 구성요소를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private FileSave<TBody> CreateFileSave(string scopeID)
        {
            if (!SaveIdentifier.IsValid(scopeID))
            {
                throw new ArgumentException
                (
                    "ScopeID는 1~64자의 소문자 영문, 숫자, '_' 및 '-'만 사용할 수 있습니다.",
                    nameof(scopeID)
                );
            }

            var scopeDirectory = Path.Combine(directory, scopeID);

            return new FileSave<TBody>
            (
                scopeDirectory,
                schema,
                serializer,
                reader,
                writer,
                scopeID
            );
        }

    #endregion

    }
}
