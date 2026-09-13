/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IDataReader.cs
수정일 : 2026-06-21

# 설명
지정한 위치에서 값을 읽고 read operation 응답을 반환하는 범용 IO 인터페이스를 정의한다.
Workspace, Resource, File 같은 상위 도메인에 의존하지 않는 최소 계약이다.
========================================================================= BLOCK_HEADER_END */

using System.Threading;
using System.Threading.Tasks;

namespace inonego.Xeri.IO
{
   // ============================================================
   /// <summary>
   /// 지정한 위치에서 값을 읽어오는 범용 인터페이스.
   /// </summary>
   // ============================================================
   public interface IDataReader<TLocation, TValue>
   {
      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 위치에서 값을 읽고 operation 응답을 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      ReadResponse<TValue> Read(TLocation location);
   }

   // ============================================================
   /// <summary>
   /// 지정한 위치에서 값을 비동기로 읽어오는 범용 인터페이스.
   /// </summary>
   // ============================================================
   public interface IAsyncDataReader<TLocation, TValue>
   {
      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 위치에서 값을 비동기로 읽고 operation 응답을 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      Task<ReadResponse<TValue>> ReadAsync(TLocation location, CancellationToken cancellationToken = default);
   }
}
