/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : AddressablesAssetReader.cs
수정일 : 2026-07-30

# 설명
Unity Addressables 주소 또는 AssetReferenceT<TAsset>에서 asset을 읽는 IO reader를 정의한다.
읽은 asset의 Addressables handle release 책임은 ReadResponse의 Lease로 전달한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace inonego.Xeri.IO
{
   // ============================================================
   /// <summary>
   /// Unity Addressables에서 asset을 읽는 reader.
   /// </summary>
   /// <typeparam name="TAsset">읽어올 Unity asset 타입.</typeparam>
   // ============================================================
   [Serializable]
   public sealed class AddressablesAssetReader<TAsset> :
      IDataReader<string, TAsset>,
      IAsyncDataReader<string, TAsset>,
      IDataReader<AssetReferenceT<TAsset>, TAsset>,
      IAsyncDataReader<AssetReferenceT<TAsset>, TAsset>
   where TAsset : UnityEngine.Object
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 AddressablesAssetReader 인스턴스.
      /// </summary>
      // ------------------------------------------------------------
      public static AddressablesAssetReader<TAsset> Default { get; } = new();

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// AddressablesAssetReader를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public AddressablesAssetReader() : base() {}

   #endregion

   #region 문자열 주소 읽기

      // ------------------------------------------------------------
      /// <summary>
      /// Addressables 주소에서 asset을 읽는다.
      /// </summary>
      // ------------------------------------------------------------
      public ReadResponse<TAsset> Read(string location)
      {
         try
         {
            ValidateLocation(location);

            var handle = Addressables.LoadAssetAsync<TAsset>(location);

            return ReadFromHandle(handle, location);
         }
         catch (Exception exception)
         {
            return ReadResponse<TAsset>.Fail(exception.Message, exception);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Addressables 주소에서 asset을 비동기로 읽는다.
      /// </summary>
      // ------------------------------------------------------------
      public async Task<ReadResponse<TAsset>> ReadAsync
      (
         string location,
         CancellationToken cancellationToken = default
      )
      {
         try
         {
            ValidateLocation(location);

            var handle = Addressables.LoadAssetAsync<TAsset>(location);

            return await ReadFromHandleAsync(handle, location, cancellationToken);
         }
         catch (Exception exception)
         {
            return ReadResponse<TAsset>.Fail(exception.Message, exception);
         }
      }

   #endregion

   #region AssetReference 읽기

      // ------------------------------------------------------------
      /// <summary>
      /// Addressables AssetReference에서 asset을 읽는다.
      /// </summary>
      // ------------------------------------------------------------
      public ReadResponse<TAsset> Read(AssetReferenceT<TAsset> location)
      {
         try
         {
            ValidateLocation(location);

            var handle = Addressables.LoadAssetAsync<TAsset>(location);

            return ReadFromHandle(handle, GetLocationText(location));
         }
         catch (Exception exception)
         {
            return ReadResponse<TAsset>.Fail(exception.Message, exception);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Addressables AssetReference에서 asset을 비동기로 읽는다.
      /// </summary>
      // ------------------------------------------------------------
      public async Task<ReadResponse<TAsset>> ReadAsync
      (
         AssetReferenceT<TAsset> location,
         CancellationToken cancellationToken = default
      )
      {
         try
         {
            ValidateLocation(location);

            var handle = Addressables.LoadAssetAsync<TAsset>(location);

            return await ReadFromHandleAsync
            (
               handle,
               GetLocationText(location),
               cancellationToken
            );
         }
         catch (Exception exception)
         {
            return ReadResponse<TAsset>.Fail(exception.Message, exception);
         }
      }

   #endregion

   #region Handle 처리

      // ------------------------------------------------------------
      /// <summary>
      /// Addressables 로드 handle에서 asset read response를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private static ReadResponse<TAsset> ReadFromHandle
      (
         AsyncOperationHandle<TAsset> handle,
         string location
      )
      {
         try
         {
            var asset = handle.WaitForCompletion();

            if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
            {
               throw new FileNotFoundException($"Addressables asset을 로드할 수 없습니다. Type: {typeof(TAsset).Name}", location);
            }

            return ReadResponse<TAsset>.Succeed
            (
               asset,
               new Lease(() => ReleaseAddressablesHandle(handle))
            );
         }
         catch (Exception exception)
         {
            ReleaseAddressablesHandle(handle);
            return ReadResponse<TAsset>.Fail(exception.Message, exception);
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Addressables 로드 handle에서 asset read response를 비동기로 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      private static async Task<ReadResponse<TAsset>> ReadFromHandleAsync
      (
         AsyncOperationHandle<TAsset> handle,
         string location,
         CancellationToken cancellationToken
      )
      {
         try
         {
            var asset = await WaitForAssetAsync(handle, cancellationToken);

            if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
            {
               throw new FileNotFoundException($"Addressables asset을 로드할 수 없습니다. Type: {typeof(TAsset).Name}", location);
            }

            return ReadResponse<TAsset>.Succeed
            (
               asset,
               new Lease(() => ReleaseAddressablesHandle(handle))
            );
         }
         catch (Exception exception)
         {
            ReleaseAddressablesHandle(handle);
            return ReadResponse<TAsset>.Fail(exception.Message, exception);
         }
      }

      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> Addressables handle 완료 또는 cancellation 중 먼저 발생한 상태를 기다린다.
      /// <br/> Cancellation이 먼저 발생하면 정리 소유자인 상위 실패 경계로 취소를 전파한다.
      /// </summary>
      // ----------------------------------------------------------------------
      private static async Task<TAsset> WaitForAssetAsync
      (
         AsyncOperationHandle<TAsset> handle,
         CancellationToken cancellationToken
      )
      {
         if (!cancellationToken.CanBeCanceled)
         {
            return await handle.Task;
         }

         var loadTask = handle.Task;
         var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
         var completedTask = await Task.WhenAny(loadTask, cancellationTask);

         if (completedTask == cancellationTask)
         {
            cancellationToken.ThrowIfCancellationRequested();
         }

         return await loadTask;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 실패한 Addressables handle을 release한다.
      /// </summary>
      // ------------------------------------------------------------
      private static void ReleaseAddressablesHandle(AsyncOperationHandle<TAsset> handle)
      {
         if (handle.IsValid())
         {
            Addressables.Release(handle);
         }
      }

   #endregion

   #region 입력 검증

      // ------------------------------------------------------------
      /// <summary>
      /// Addressables 주소 입력값을 검증한다.
      /// </summary>
      // ------------------------------------------------------------
      private static void ValidateLocation(string location)
      {
         if (string.IsNullOrEmpty(location))
         {
            throw new ArgumentException("Addressables 주소가 비어 있습니다.", nameof(location));
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Addressables AssetReference 입력값을 검증한다.
      /// </summary>
      // ------------------------------------------------------------
      private static void ValidateLocation(AssetReferenceT<TAsset> location)
      {
         if (location == null)
         {
            throw new ArgumentNullException(nameof(location));
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 오류 메시지에 사용할 AssetReference 위치 문자열을 가져온다.
      /// </summary>
      // ------------------------------------------------------------
      private static string GetLocationText(AssetReferenceT<TAsset> location)
      {
         return location.RuntimeKey?.ToString() ?? location.ToString();
      }

   #endregion

   }
}
