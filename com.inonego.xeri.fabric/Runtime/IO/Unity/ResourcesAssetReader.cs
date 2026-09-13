/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ResourcesAssetReader.cs
수정일 : 2026-06-21

# 설명
Unity Resources 경로에서 UnityEngine.Object 기반 asset을 로드하는 IO reader를 정의한다.
Resources는 런타임에서 읽기 전용 입력원으로 다룬다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;

using UnityEngine;

namespace inonego.Xeri.IO
{
   // ============================================================
   /// <summary>
   /// Unity Resources 경로에서 asset을 읽는 reader.
   /// </summary>
   /// <typeparam name="TAsset">읽어올 Unity asset 타입.</typeparam>
   // ============================================================
   [Serializable]
   public sealed class ResourcesAssetReader<TAsset> : IDataReader<string, TAsset>
   where TAsset : UnityEngine.Object
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 ResourcesAssetReader 인스턴스.
      /// </summary>
      // ------------------------------------------------------------
      public static ResourcesAssetReader<TAsset> Default { get; } = new();

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// ResourcesAssetReader를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public ResourcesAssetReader() : base() {}

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// Resources 경로에서 asset을 읽는다.
      /// </summary>
      // ------------------------------------------------------------
      public ReadResponse<TAsset> Read(string location)
      {
         try
         {
            ValidateLocation(location);

            var asset = Resources.Load<TAsset>(location);

            if (asset == null)
            {
               throw new FileNotFoundException($"Resources asset을 로드할 수 없습니다. Type: {typeof(TAsset).Name}", location);
            }

            return ReadResponse<TAsset>.Succeed(asset);
         }
         catch (Exception exception)
         {
            return ReadResponse<TAsset>.Fail(exception.Message, exception);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Resources 경로 입력값을 검증한다.
      /// </summary>
      // ------------------------------------------------------------
      private static void ValidateLocation(string location)
      {
         if (string.IsNullOrEmpty(location))
         {
            throw new ArgumentException("Resources 경로가 비어 있습니다.", nameof(location));
         }
      }

   #endregion

   }
}
