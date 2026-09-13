/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : BinaryFileIO.cs
수정일 : 2026-06-21

# 설명
파일 경로를 기준으로 byte 배열 값을 읽고 쓰는 IO 구현체를 정의한다.
이미 직렬화된 바이너리 데이터나 텍스트가 아닌 파일 데이터와 조합해 사용할 수 있다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;

namespace inonego.Xeri.IO
{
   // ============================================================
   /// <summary>
   /// 파일 경로에서 byte 배열 값을 읽고 쓰는 IO 구현체.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class BinaryFileIO : IDataReader<string, byte[]>, IDataWriter<string, byte[]>
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 BinaryFileIO 인스턴스.
      /// </summary>
      // ------------------------------------------------------------
      public static BinaryFileIO Default { get; } = new();

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// BinaryFileIO를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public BinaryFileIO() : base() {}

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 파일 경로에서 byte 배열 값을 읽는다.
      /// </summary>
      // ------------------------------------------------------------
      public ReadResponse<byte[]> Read(string location)
      {
         try
         {
            ValidateLocation(location);

            return ReadResponse<byte[]>.Succeed(File.ReadAllBytes(location));
         }
         catch (Exception exception)
         {
            return ReadResponse<byte[]>.Fail(exception.Message, exception);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 파일 경로에 byte 배열 값을 쓴다.
      /// </summary>
      // ------------------------------------------------------------
      public WriteResponse Write(string location, byte[] value)
      {
         try
         {
            ValidateLocation(location);

            if (value == null)
            {
               throw new ArgumentNullException(nameof(value));
            }

            File.WriteAllBytes(location, value);

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
