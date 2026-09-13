/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TextFileIO.cs
수정일 : 2026-06-21

# 설명
파일 경로를 기준으로 문자열 값을 읽고 쓰는 IO 구현체를 정의한다.
JSON, XML, YAML처럼 문자열 기반 serializer와 조합해 사용할 수 있다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;
using System.Text;

namespace inonego.Xeri.IO
{
   // ============================================================
   /// <summary>
   /// 파일 경로에서 문자열 값을 읽고 쓰는 IO 구현체.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class TextFileIO : IDataReader<string, string>, IDataWriter<string, string>
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 UTF-8 인코딩을 사용하는 TextFileIO 인스턴스.
      /// </summary>
      // ------------------------------------------------------------
      public static TextFileIO Default { get; } = new();

      // ------------------------------------------------------------
      /// <summary>
      /// 파일 읽기와 쓰기에 사용할 문자열 인코딩.
      /// </summary>
      // ------------------------------------------------------------
      public Encoding Encoding => encoding;

      private readonly Encoding encoding = null;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// UTF-8 인코딩을 사용하는 TextFileIO를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public TextFileIO() : this(new UTF8Encoding(false)) {}

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 인코딩을 사용하는 TextFileIO를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public TextFileIO(Encoding encoding) : base()
      {
         this.encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 파일 경로에서 문자열 값을 읽는다.
      /// </summary>
      // ------------------------------------------------------------
      public ReadResponse<string> Read(string location)
      {
         try
         {
            ValidateLocation(location);

            return ReadResponse<string>.Succeed(File.ReadAllText(location, encoding));
         }
         catch (Exception exception)
         {
            return ReadResponse<string>.Fail(exception.Message, exception);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 파일 경로에 문자열 값을 쓴다.
      /// </summary>
      // ------------------------------------------------------------
      public WriteResponse Write(string location, string value)
      {
         try
         {
            ValidateLocation(location);

            if (value == null)
            {
               throw new ArgumentNullException(nameof(value));
            }

            File.WriteAllText(location, value, encoding);

            return WriteResponse.Succeed();
         }
         catch (Exception exception)
         {
            return WriteResponse.Fail(exception.Message, exception);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 파일 경로 입력값을 검증한다.
      /// </summary>
      // ------------------------------------------------------------
      private static void ValidateLocation(string location)
      {
         if (string.IsNullOrEmpty(location))
         {
            throw new ArgumentException("파일 경로가 비어 있습니다.", nameof(location));
         }
      }

   #endregion

   }
}
