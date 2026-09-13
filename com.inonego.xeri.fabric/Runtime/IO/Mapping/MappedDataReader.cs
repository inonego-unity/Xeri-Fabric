/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : MappedDataReader.cs
수정일 : 2026-07-30

# 설명
기존 reader가 읽은 response value를 다른 값으로 변환해 반환하는 동기 IO reader adapter를 정의한다.
입력원과 값 변환을 분리해 handler가 최종으로 필요한 타입만 의존하도록 한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.IO
{
   // ========================================================================================
   /// <summary>
   /// source reader가 읽은 response value를 지정한 변환 함수로 변환해 반환하는 reader adapter.
   /// </summary>
   /// <typeparam name="TLocation">읽기 위치 타입.</typeparam>
   /// <typeparam name="TSource">source reader가 읽는 원본 값 타입.</typeparam>
   /// <typeparam name="TValue">adapter가 반환하는 변환 값 타입.</typeparam>
   // ========================================================================================
   [Serializable]
   public sealed class MappedDataReader<TLocation, TSource, TValue> : IDataReader<TLocation, TValue>
   {

   #region 필드

      private readonly IDataReader<TLocation, TSource> sourceReader = null;
      private readonly Func<TSource, TValue> map = null;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// MappedDataReader를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public MappedDataReader
      (
         IDataReader<TLocation, TSource> sourceReader,
         Func<TSource, TValue> map
      ) : base()
      {
         this.sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
         this.map = map ?? throw new ArgumentNullException(nameof(map));
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// source reader에서 read response를 받은 뒤 value를 변환해 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      public ReadResponse<TValue> Read(TLocation location)
      {
         var sourceResponse = sourceReader.Read(location);
         if (!sourceResponse.Success)
         {
            return ReadResponse<TValue>.Fail
            (
               sourceResponse.Error,
               sourceResponse.Exception,
               sourceResponse.Lease
            );
         }

         try
         {
            var value = map(sourceResponse.Value);

            return ReadResponse<TValue>.Succeed(value, sourceResponse.Lease);
         }
         catch (Exception exception)
         {
            return ReadResponse<TValue>.Fail
            (
               exception.Message,
               exception,
               sourceResponse.Lease
            );
         }
      }

   #endregion

   }
}
