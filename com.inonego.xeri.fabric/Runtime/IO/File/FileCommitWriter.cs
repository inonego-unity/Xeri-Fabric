/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FileCommitWriter.cs
수정일 : 2026-11-09

# 설명
파일 경로를 대상으로 하는 IO writer의 출력을 동일 디렉터리 임시 파일에 기록한 뒤 교체한다.
선택적으로 기존 대상의 이전 내용을 .bak 파일에 보존하는 단일 파일 IO 어댑터다.

# 특이사항
백업은 기존 파일 교체가 성공할 때 갱신한다. 다중 파일 Transaction, 전원 손실 내구성,
동시 쓰기 제어는 보장하지 않는다.
주입된 writer는 전달받은 문자열 경로에 실제 파일을 작성해야 한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;

namespace inonego.Xeri.IO
{
    // ============================================================
    /// <summary>
    /// 임시 파일 작성에 성공한 경우에만 대상 파일을 교체하는 파일 writer.
    /// </summary>
    // ============================================================
    public sealed class FileCommitWriter<TValue> : IDataWriter<string, TValue>
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 임시 파일 경로에 실제 값을 기록할 IO writer.
        /// </summary>
        // ------------------------------------------------------------
        public IDataWriter<string, TValue> Writer => writer;

        private readonly IDataWriter<string, TValue> writer = null;
        private readonly bool preservePrevious = false;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 파일 writer를 사용해 단일 파일 Commit writer를 구성한다.
        /// </summary>
        // ------------------------------------------------------------
        public FileCommitWriter
        (
            IDataWriter<string, TValue> writer,
            bool preservePrevious = false
        )
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.preservePrevious = preservePrevious;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 동일 디렉터리의 임시 파일을 완성한 뒤 대상 파일을 교체한다.
        /// <br/> 실패하면 기존 대상은 삭제하지 않고 임시 파일만 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public WriteResponse Write(string location, TValue value)
        {
            string tempPath = null;

            try
            {
                if (string.IsNullOrWhiteSpace(location))
                {
                    throw new ArgumentException("파일 경로가 비어 있습니다.", nameof(location));
                }

                var path = Path.GetFullPath(location);
                var directory = Path.GetDirectoryName(path);

                if (string.IsNullOrEmpty(directory))
                {
                    throw new InvalidOperationException("파일 디렉터리를 결정할 수 없습니다.");
                }

                Directory.CreateDirectory(directory);

                // 대상 파일과 동일한 디렉터리에서 작성해 볼륨 간 이동을 피한다.
                tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

                var response = writer.Write(tempPath, value);

                if (!response.Success)
                {
                    throw response.Exception ?? new IOException(response.Error);
                }

                if (!File.Exists(tempPath))
                {
                    throw new IOException("Writer가 임시 파일을 생성하지 않았습니다.");
                }

                if (File.Exists(path))
                {
                    // 이전 내용은 대상 파일의 교체에 성공할 때만 백업 세대로 보존한다.
                    File.Replace(tempPath, path, preservePrevious ? path + ".bak" : null);
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return WriteResponse.Succeed();
            }
            catch (Exception exception)
            {
                if (tempPath != null)
                {
                    try
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                    catch (Exception cleanupException)
                    {
                        var combined = new AggregateException
                        (
                            "파일 작성 및 임시 파일 정리에 모두 실패했습니다.",
                            exception,
                            cleanupException
                        );

                        return WriteResponse.Fail(combined.Message, combined);
                    }
                }

                return WriteResponse.Fail(exception.Message, exception);
            }
        }

    #endregion

    }
}
