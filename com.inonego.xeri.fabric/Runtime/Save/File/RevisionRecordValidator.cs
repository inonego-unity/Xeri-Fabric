/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RevisionRecordValidator.cs
수정일 : 2026-11-16

# 설명
불변 Revision Record의 바이트 크기와 SHA-256을 검증한다.
Record 원문은 검증한 같은 파일 스트림에서 BOM 없는 UTF-8로 복원한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;

namespace inonego.Xeri.Save
{
    // ============================================================
    /// <summary>
    /// Revision Record의 파일 크기와 해시를 검증한다.
    /// </summary>
    // ============================================================
    internal static class RevisionRecordValidator
    {

    #region 해시 계산·검증

        // ------------------------------------------------------------
        /// <summary>
        /// 실제 파일의 SHA-256과 바이트 크기를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        public static string Compute(string path, out long byteLength)
        {
            using var stream = new FileStream
            (
                path, FileMode.Open, FileAccess.Read, FileShare.Read
            );
            byteLength = stream.Length;
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", "").ToLowerInvariant();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 해시와 크기를 확인한 같은 파일 핸들에서 원문을 읽는다.
        /// <br/> 문자열 변환과 Record 복원은 이 검증이 끝난 뒤 실행한다.
        /// </summary>
        // ------------------------------------------------------------
        public static ReadResponse<string> ReadVerifiedText
        (
            string path, long byteLength, string contentHash
        )
        {
            try
            {
                using var stream = new FileStream
                (
                    path, FileMode.Open, FileAccess.Read, FileShare.Read
                );

                if (!VerifyStream(stream, byteLength, contentHash))
                {
                    return ReadResponse<string>.Fail("Revision Record의 파일 해시 또는 크기가 일치하지 않습니다.");
                }

                stream.Position = 0;
                if (stream.ReadByte() == 0xEF &&
                    stream.ReadByte() == 0xBB &&
                    stream.ReadByte() == 0xBF)
                {
                    return ReadResponse<string>.Fail("Revision Record는 UTF-8 BOM을 허용하지 않습니다.");
                }

                stream.Position = 0;
                using var reader = new StreamReader
                (
                    stream, new UTF8Encoding(false, true), false
                );
                return ReadResponse<string>.Succeed(reader.ReadToEnd());
            }
            catch (Exception exception)
            {
                return ReadResponse<string>.Fail(exception.Message, exception);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 보존할 Record의 물리 무결성만 검증한다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool Verify(string path, long byteLength, string contentHash)
        {
            using var stream = new FileStream
            (
                path, FileMode.Open, FileAccess.Read, FileShare.Read
            );
            return VerifyStream(stream, byteLength, contentHash);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 스트림의 크기와 SHA-256을 비교한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool VerifyStream
        (
            Stream stream, long byteLength, string contentHash
        )
        {
            if (stream.Length != byteLength)
            {
                return false;
            }

            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", "").ToLowerInvariant();
            return string.Equals(actual, contentHash, StringComparison.Ordinal);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 저장된 해시 형식이 유효한지 확인합니다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool IsValidHash(string hash)
        {
            if (hash == null || hash.Length != 64)
            {
                return false;
            }

            foreach (var ch in hash)
            {
                if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

    #endregion

    }
}
