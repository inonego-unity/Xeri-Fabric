/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FileDocumentLocation.cs
수정일 : 2026-07-01

# 설명
파일 시스템 경로를 문서 location으로 다루는 구현체를 정의한다.
문서 열기, 저장, 다른 이름 저장에서 파일 경로와 파일명, 확장자 정보를 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 파일 시스템 경로를 나타내는 문서 location.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class FileDocumentLocation : IDocumentLocation
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 사용자에게 표시하거나 진단에 사용할 location 이름.
      /// </summary>
      // ------------------------------------------------------------
      public string Name => name;

      [SerializeField]
      private string name = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 파일 경로.
      /// </summary>
      // ------------------------------------------------------------
      public string Path => path;

      [SerializeField]
      private string path = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// 파일 확장자.
      /// </summary>
      // ------------------------------------------------------------
      public string Extension => System.IO.Path.GetExtension(path);

      // ------------------------------------------------------------
      /// <summary>
      /// 파일 이름.
      /// </summary>
      // ------------------------------------------------------------
      public string FileName => System.IO.Path.GetFileName(path);

      // ------------------------------------------------------------
      /// <summary>
      /// 파일이 속한 디렉터리 경로.
      /// </summary>
      // ------------------------------------------------------------
      public string Directory => System.IO.Path.GetDirectoryName(path) ?? string.Empty;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 파일 경로에서 표시 이름을 계산해 location을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public FileDocumentLocation(string path) : this(path, GetDefaultName(path)) {}

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 파일 경로와 표시 이름으로 location을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public FileDocumentLocation(string path, string name) : base()
      {
         if (string.IsNullOrEmpty(path))
         {
            throw new ArgumentException("파일 경로가 비어 있습니다.", nameof(path));
         }

         if (string.IsNullOrEmpty(name))
         {
            throw new ArgumentException("File location 이름이 비어 있습니다.", nameof(name));
         }

         this.path = path;
         this.name = name;
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// File location을 path/name 기반 recovery record로 만든다.
      /// </summary>
      // ------------------------------------------------------------
      public IDocumentLocationRecord Record()
      {
         return new FileDocumentLocationRecord(name, path);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 파일 경로에서 기본 표시 이름을 계산한다.
      /// </summary>
      // ------------------------------------------------------------
      private static string GetDefaultName(string path)
      {
         if (string.IsNullOrEmpty(path))
         {
            throw new ArgumentException("파일 경로가 비어 있습니다.", nameof(path));
         }

         var fileName = System.IO.Path.GetFileName(path);

         if (string.IsNullOrEmpty(fileName) == false)
         {
            return fileName;
         }

         return path;
      }

   #endregion

   #region Equality / Hash

      // ------------------------------------------------------------
      /// <summary>
      /// 같은 파일 경로를 가진 document location인지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool Equals(IDocumentLocation other)
      {
         return other is FileDocumentLocation loc &&
                string.Equals
                (
                   NormalizePath(path),
                   NormalizePath(loc.path),
                   GetPathComparison()
                );
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 같은 파일 경로를 가진 document location인지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      public override bool Equals(object obj)
      {
         return obj is IDocumentLocation loc && Equals(loc);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 정규화된 파일 경로 기준 hash code를 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      public override int GetHashCode()
      {
         return GetPathComparer().GetHashCode(NormalizePath(path));
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 파일 경로 비교를 위한 정규화 값을 계산한다.
      /// </summary>
      // ------------------------------------------------------------
      private static string NormalizePath(string path)
      {
         if (string.IsNullOrEmpty(path))
         {
            return string.Empty;
         }

         try
         {
            var normalized = System.IO.Path.GetFullPath(path).Replace('\\', '/');

            return normalized.TrimEnd('/');
         }
         catch
         {
            return path.Replace('\\', '/').TrimEnd('/');
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 현재 플랫폼에 맞는 파일 경로 비교 방식을 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      private static StringComparison GetPathComparison()
      {
         return IsCaseInsensitivePlatform()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 현재 플랫폼에 맞는 파일 경로 hash comparer를 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      private static StringComparer GetPathComparer()
      {
         return IsCaseInsensitivePlatform()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 파일 시스템 경로 대소문자를 무시하는 플랫폼인지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      private static bool IsCaseInsensitivePlatform()
      {
         return SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows ||
                SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX;
      }

   #endregion

   }
}
