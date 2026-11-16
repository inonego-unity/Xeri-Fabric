/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RevisionStoreKind.cs
수정일 : 2026-11-15

# 설명
동형 및 이기종 Revision의 동일 Scope 혼용을 차단하는 내부 영속 식별.
File Scope의 잠금 아래 종류 표식을 읽고 검증·예약한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;

using inonego;
using inonego.Xeri;
using inonego.Xeri.IO;
using inonego.Xeri.Persistence;

namespace inonego.Xeri.Save
{
    internal static class RevisionStoreKind
    {
        private const string homogeneous = "homogeneous-v1";
        private const string mixed = "mixed-v1";

        // FileRevisionSave가 보유한 Scope 잠금 안에서 호출해야 한다.
        internal static void EnsureFile
        (
            string scopeDirectory,
            bool isMixed,
            bool reserve,
            IDataReader<string, string> reader,
            IDataWriter<string, string> writer
        )
        {
            var marker = Path.Combine(scopeDirectory, "revision.kind");
            var expected = isMixed ? mixed : homogeneous;
            var persistence = new RecordPersistence<string, string>
            (
                reader, new FileCommitWriter<string>(writer)
            );

            if (File.Exists(marker))
            {
                // 원본 Reader가 제공한 ReleaseHandle도 Persistence에서 종료한다.
                var read = persistence.Load(marker);
                if (!read.Success)
                {
                    throw read.Exception ?? new IOException(read.Error);
                }

                if (!string.Equals(read.Value, expected, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("저장소 Revision 종류가 일치하지 않습니다.");
                }

                return;
            }

            if (HasUnmarkedFileData(scopeDirectory))
            {
                throw new InvalidOperationException
                (
                    "종류 표식이 없는 Revision Scope에 기록이 남아 있습니다."
                );
            }

            if (!reserve)
            {
                return;
            }

            var write = persistence.Save(marker, expected);
            if (!write.Success)
            {
                throw write.Exception ?? new IOException(write.Error);
            }
        }

        private static bool HasUnmarkedFileData(string directory)
        {
            if (File.Exists(Path.Combine(directory, "current.txt")) ||
                File.Exists(Path.Combine(directory, "current.txt.bak")))
            {
                return true;
            }

            foreach (var name in new[]
            {
                "revisions",
                "records"
            })
            {
                var folder = Path.Combine(directory, name);
                if (Directory.Exists(folder))
                {
                    using var iterator = Directory.EnumerateFileSystemEntries(folder).GetEnumerator();
                    if (iterator.MoveNext())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

    }
}
