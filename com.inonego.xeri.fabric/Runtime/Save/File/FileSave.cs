/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FileSave.cs
수정일 : 2026-11-16

# 설명
논리 SaveKey로 게임 Snapshot을 단일 파일에 저장·복원·삭제한다.
IO Mapping·FileCommitWriter·RecordPersistence를 조합하며 Save Metadata와 파일명 정책만 소유한다.

# 주의
Entry/Revision/다중 파일 Transaction, 전원 손실 내구성·동시 저장의 일관성은 보장하지 않는다.
호출자는 저장 도중 변경되지 않는 독립 Snapshot DTO를 제공해야 한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Persistence;
using inonego.Xeri.Serializable;

namespace inonego.Xeri.Save
{
    // ============================================================
    /// <summary>
    /// SaveKey별로 Snapshot 파일을 기록하는 기본 Save 진입점.
    /// </summary>
    /// <typeparam name="TBody">게임 Snapshot 데이터 타입.</typeparam>
    // ============================================================
    public sealed class FileSave<TBody>
    where TBody : class
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 파일 Snapshot 형식 버전.
        /// </summary>
        // ------------------------------------------------------------
        private const string formatVersion = "1";

        // ------------------------------------------------------------
        /// <summary>
        /// SaveKey를 생략한 단일 Snapshot의 기본 식별자.
        /// </summary>
        // ------------------------------------------------------------
        public const string DefaultSaveKey = "default";

        private readonly string directory = string.Empty;
        private readonly string scopeID = string.Empty;
        private readonly RecordType schema = null;
        private readonly ISerializer serializer = null;
        private readonly IDataReader<string, string> sourceReader = null;
        private readonly IDataWriter<string, SaveRecord<TBody>> recordWriter = null;
        private readonly RecordPersistence<string, SaveRecord<TBody>> persistence = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 스키마와 직렬화 방식으로 파일 Snapshot Save를 구성한다.
        /// </summary>
        /// <param name="directory">Save 파일 디렉터리.</param>
        /// <param name="schema">기록할 게임 데이터의 SchemaID/Version 계약.</param>
        /// <param name="serializer">사용할 Serializer.</param>
        /// <param name="reader">읽기 IO. null이면 기본 TextFileIO를 사용한다.</param>
        /// <param name="writer">임시 파일에 쓰는 IO. null이면 기본 TextFileIO를 사용한다.</param>
        // ------------------------------------------------------------
        public FileSave
        (
            string directory,
            RecordType schema,
            ISerializer serializer,
            IDataReader<string, string> reader = null,
            IDataWriter<string, string> writer = null
        ) : this(directory, schema, serializer, reader, writer, string.Empty)
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Scope를 고정하는 내부 FileSave 구성.
        /// </summary>
        // ------------------------------------------------------------
        internal FileSave
        (
            string directory,
            RecordType schema,
            ISerializer serializer,
            IDataReader<string, string> reader,
            IDataWriter<string, string> writer,
            string scopeID
        )
        {
            if (!string.IsNullOrEmpty(scopeID) && !SaveIdentifier.IsValid(scopeID))
            {
                throw new ArgumentException("ScopeID가 올바르지 않습니다.", nameof(scopeID));
            }

            this.scopeID = scopeID ?? string.Empty;

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Save 디렉터리가 비어 있습니다.", nameof(directory));
            }

            this.directory = Path.GetFullPath(directory);
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            sourceReader = reader ?? TextFileIO.Default;
            var sourceWriter = writer ?? TextFileIO.Default;

            recordWriter = new MappedDataWriter<string, SaveRecord<TBody>, string>
            (
                new FileCommitWriter<string>(sourceWriter), this.serializer.Serialize
            );
            persistence = CreatePersistence(this.serializer.Deserialize<SaveRecord<TBody>>);
        }

    #endregion

    #region 저장

        // ----------------------------------------------------------------------
        /// <summary>
        /// Snapshot을 지정 SaveKey에 기록한다. 기존 파일은 완성된 새 파일로 교체한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public WriteResponse Save(string saveKey, TBody body)
        {
            try
            {
                var path = GetPath(saveKey);
                var metadata = new SaveMetadata
                {
                    FormatVersion = formatVersion,
                    SchemaID = schema.TypeID,
                    SchemaVersion = schema.Version,
                    SaveKey = saveKey,
                    ScopeID = scopeID,
                    SavedAtUTC = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };

                return persistence.Save(path, new SaveRecord<TBody>(metadata, body));
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// SaveKey 없이 기본 Snapshot을 기록한다.
        /// </summary>
        // ------------------------------------------------------------
        public WriteResponse Save(TBody body)
        {
            return Save(DefaultSaveKey, body);
        }

    #endregion

    #region 복원

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 SaveKey의 기록 전체를 로드한다. Metadata 단독 조회는 아니다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<SaveRecord<TBody>> LoadRecord(string saveKey)
        {
            return LoadRecord(saveKey, persistence);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 파일 검증이 완료된 원문 Reader로 기록을 복원한다.
        /// <br/> 일반 FileSave와 동일한 Mapping·Persistence 검증을 수행한다.
        /// </summary>
        // ----------------------------------------------------------------------
        internal ReadResponse<SaveRecord<TBody>> LoadRecord
        (
            string saveKey, IDataReader<string, string> verifiedReader
        )
        {
            if (verifiedReader == null)
            {
                throw new ArgumentNullException(nameof(verifiedReader));
            }

            var loader = CreatePersistence
            (
                serializer.Deserialize<SaveRecord<TBody>>, verifiedReader
            );
            return LoadRecord(saveKey, loader);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 동일한 Persistence 계약으로 기록과 SaveKey를 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private ReadResponse<SaveRecord<TBody>> LoadRecord
        (
            string saveKey, RecordPersistence<string, SaveRecord<TBody>> loader
        )
        {
            try
            {
                var path = GetPath(saveKey);
                var response = loader.Load(path);

                if (!response.Success)
                {
                    return ReadResponse<SaveRecord<TBody>>.Fail(response.Error, response.Exception);
                }

                if (!string.Equals(response.Value.Metadata.SaveKey, saveKey, StringComparison.Ordinal))
                {
                    return ReadResponse<SaveRecord<TBody>>.Fail("기록된 SaveKey가 요청한 SaveKey와 일치하지 않습니다.");
                }

                return response;
            }
            catch (Exception exception)
            {
                return ReadResponse<SaveRecord<TBody>>.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 SaveKey의 Snapshot Body만 로드한다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<TBody> Load(string saveKey)
        {
            var response = LoadRecord(saveKey);

            return response.Success
               ? ReadResponse<TBody>.Succeed(response.Value.Body)
               : ReadResponse<TBody>.Fail(response.Error, response.Exception);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 SaveKey의 Snapshot Body를 로드한다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<TBody> Load()
        {
            return Load(DefaultSaveKey);
        }

    #endregion

    #region 관리

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 현재 디렉터리에서 유효한 SaveKey를 가진 파일 목록을 반환한다.
        /// <br/> 파일 내용의 정상 여부는 검사하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<string[]> ListKeys()
        {
            try
            {
                if (!System.IO.Directory.Exists(directory))
                {
                    return ReadResponse<string[]>.Succeed(Array.Empty<string>());
                }

                var keys = new List<string>();
                const string suffix = ".save.json";

                foreach (var path in System.IO.Directory.EnumerateFiles
                (
                    directory, "*" + suffix, SearchOption.TopDirectoryOnly
                ))
                {
                    var name = Path.GetFileName(path);

                    if (!name.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var saveKey = name.Substring(0, name.Length - suffix.Length);

                    try
                    {
                        ValidateSaveKey(saveKey);
                        keys.Add(saveKey);
                    }
                    catch (ArgumentException)
                    {
                        // 이 FileSave가 소유하지 않는 파일명은 목록에서 제외한다.
                        // NONE
                    }
                }

                keys.Sort(StringComparer.Ordinal);

                return ReadResponse<string[]>.Succeed(keys.ToArray());
            }
            catch (Exception exception)
            {
                return ReadResponse<string[]>.Fail(exception.Message, exception);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지정 SaveKey의 파일 존재 여부를 확인한다. 잘못된 SaveKey는 false를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public bool Exists(string saveKey)
        {
            try
            {
                return File.Exists(GetPath(saveKey));
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 SaveKey 파일의 존재 여부를 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public bool Exists()
        {
            return Exists(DefaultSaveKey);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 지정 SaveKey의 파일을 삭제한다. 존재하지 않는 파일은 이미 삭제된 것으로 처리한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public WriteResponse Delete(string saveKey)
        {
            try
            {
                var path = GetPath(saveKey);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return WriteResponse.Succeed();
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 SaveKey의 파일을 삭제한다.
        /// </summary>
        // ------------------------------------------------------------
        public WriteResponse Delete()
        {
            return Delete(DefaultSaveKey);
        }

    #endregion

    #region Persistence 구성

        // ------------------------------------------------------------
        /// <summary>
        /// Reader 경계를 선택하되 동일한 기록 검증과 Writer를 재사용한다.
        /// </summary>
        // ------------------------------------------------------------
        private RecordPersistence<string, SaveRecord<TBody>> CreatePersistence
        (
            Func<string, SaveRecord<TBody>> deserialize,
            IDataReader<string, string> reader = null
        )
        {
            return new RecordPersistence<string, SaveRecord<TBody>>
            (
                new MappedDataReader<string, string, SaveRecord<TBody>>
                (
                    reader ?? sourceReader, deserialize
                ),
                recordWriter,
                ValidateRecord
            );
        }

    #endregion

    #region 기록·식별자 검증

        // ------------------------------------------------------------
        /// <summary>
        /// 읽기·쓰기 모두에서 Save의 형식·스키마 및 필수 내용을 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        private void ValidateRecord(SaveRecord<TBody> record)
        {
            // Document와 동일한 공통 Record 구조 검증을 사용한다.
            schema.Validate(record);

            if (!record.HasBody)
            {
                throw new InvalidOperationException("Save Body가 직렬화 기록에 존재하지 않습니다.");
            }

            var metadata = record.Metadata;

            if (!string.Equals(metadata.FormatVersion, formatVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("지원하지 않는 Save FormatVersion입니다.");
            }

            ValidateSaveKey(metadata.SaveKey);

            if (!string.Equals(metadata.ScopeID ?? string.Empty, scopeID, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("기록된 ScopeID가 현재 Scope와 일치하지 않습니다.");
            }

            if (string.IsNullOrWhiteSpace(metadata.SavedAtUTC) ||
                !metadata.SavedAtUTC.EndsWith("Z", StringComparison.Ordinal) ||
                !DateTime.TryParseExact
                (
                    metadata.SavedAtUTC,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _
                ))
            {
                throw new InvalidOperationException("Save SavedAtUTC가 올바르지 않습니다.");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 논리 SaveKey에서 안전한 파일 경로를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        private string GetPath(string saveKey)
        {
            ValidateSaveKey(saveKey);
            return Path.Combine(directory, saveKey + ".save.json");
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 경로 이탈과 플랫폼별 대소문자 충돌이 발생하지 않는 SaveKey만 허용한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void ValidateSaveKey(string saveKey)
        {
            if (!SaveIdentifier.IsValid(saveKey))
            {
                throw new ArgumentException
                (
                    "SaveKey는 1~64자의 소문자 영문, 숫자, '_' 및 '-'만 사용할 수 있습니다.",
                    nameof(saveKey)
                );
            }
        }

    #endregion

    }
}
