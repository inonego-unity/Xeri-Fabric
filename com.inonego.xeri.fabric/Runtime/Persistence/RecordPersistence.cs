/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RecordPersistence.cs
수정일 : 2026-10-28

# 설명
타입이 지정된 영속 기록의 읽기/쓰기 경계에서 도메인 검증과 원본 IO ReleaseHandle 회수를 수행한다.
역직렬화와 타입 변환은 주입받은 기존 IO reader/writer의 Mapping에 맡긴다.
Document·Save의 Metadata, Session, Slot과 파일 위치 정책은 이 클래스가 소유하지 않는다.

# 주의
복원된 기록은 원본 Reader ReleaseHandle에 의존하지 않는 독립 데이터여야 한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;

namespace inonego.Xeri.Persistence
{
    // ======================================================================
    /// <summary>
    /// 타입이 지정된 한 영속 기록의 검증, 읽기 수명 종료 및 쓰기 흐름을 조정한다.
    /// </summary>
    /// <typeparam name="TLocation">IO 위치 타입.</typeparam>
    /// <typeparam name="TRecord">도메인이 정의한 기록 단위 타입.</typeparam>
    // ======================================================================
    public sealed class RecordPersistence<TLocation, TRecord>
    {

    #region 필드

        private readonly IDataReader<TLocation, TRecord> reader = null;
        private readonly IDataWriter<TLocation, TRecord> writer = null;
        private readonly Action<TRecord> validateRecord = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기존 IO reader/writer와 기록 검증 정책을 조합한다.
        /// </summary>
        // ------------------------------------------------------------
        public RecordPersistence
        (
            IDataReader<TLocation, TRecord> reader,
            IDataWriter<TLocation, TRecord> writer,
            Action<TRecord> validateRecord = null
        )
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.validateRecord = validateRecord;
        }

    #endregion

    #region 읽기

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 기록을 읽고 검증한 뒤 원본 IO ReleaseHandle을 종료한다.
        /// <br/> 반환 Record는 원본 리소스의 수명에 의존하지 않아야 한다.
        /// </summary>
        // ------------------------------------------------------------
        public ReadResponse<TRecord> Load(TLocation location)
        {
            ReadResponse<TRecord> response;

            try
            {
                response = reader.Read(location);
            }
            catch (Exception exception)
            {
                return ReadResponse<TRecord>.Fail(exception.Message, exception);
            }

            ReadResponse<TRecord> result;

            try
            {
                if (!response.Success)
                {
                    result = ReadResponse<TRecord>.Fail(response.Error, response.Exception);
                }
                else
                {
                    Validate(response.Value);
                    result = ReadResponse<TRecord>.Succeed(response.Value);
                }
            }
            catch (Exception exception)
            {
                result = ReadResponse<TRecord>.Fail(exception.Message, exception);
            }

            try
            {
                // 기록을 완성한 후 원본 IO 수명의 소유권을 종료한다.
                response.ReleaseHandle?.Dispose();
            }
            catch (Exception exception)
            {
                if (!result.Success)
                {
                    // 읽기 오류와 ReleaseHandle 정리 오류를 모두 호출자에게 전달한다.
                    var readError = result.Exception ??
                                    new InvalidOperationException(result.Error);

                    var combined = new AggregateException
                    (
                        "기록 읽기와 IO ReleaseHandle 정리에 모두 실패했습니다.",
                        readError,
                        exception
                    );

                    return ReadResponse<TRecord>.Fail(combined.Message, combined);
                }

                return ReadResponse<TRecord>.Fail(exception.Message, exception);
            }

            return result;
        }

    #endregion

    #region 쓰기

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 기록을 검증한 다음 주입받은 writer에 기록을 위임한다.
        /// <br/> 실패한 검증은 파일·DB 기록을 시작하지 않는다.
        /// </summary>
        // ------------------------------------------------------------
        public WriteResponse Save(TLocation location, TRecord record)
        {
            try
            {
                Validate(record);
                return writer.Write(location, record);
            }
            catch (Exception exception)
            {
                return WriteResponse.Fail(exception.Message, exception);
            }
        }

    #endregion

    #region 검증

        // ------------------------------------------------------------
        /// <summary>
        /// null 기록을 거부하고 도메인에서 전달한 검증 규칙을 실행한다.
        /// </summary>
        // ------------------------------------------------------------
        private void Validate(TRecord record)
        {
            if (record is null)
            {
                throw new InvalidOperationException("영속 기록이 null입니다.");
            }

            validateRecord?.Invoke(record);
        }

    #endregion

    }
}
