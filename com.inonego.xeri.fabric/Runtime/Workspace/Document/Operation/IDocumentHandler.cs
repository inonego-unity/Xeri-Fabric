/* BLOCK_HEADER_BEGIN ===============================================================================================
파일명 : IDocumentHandler.cs
수정일 : 2026-07-01

# 설명
하나의 문서 타입에 대한 create, open, save, recovery 흐름을 일관되게 처리하는 handler 인터페이스를 정의한다.
================================================================================================= BLOCK_HEADER_END */

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 하나의 문서 타입에 대한 생성, 열기, 저장, 복구 흐름을 처리하는 인터페이스.
   /// </summary>
   // ============================================================
   public interface IDocumentHandler
   {
      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 담당하는 문서 종류 식별자.
      /// </summary>
      // ------------------------------------------------------------
      string TypeID { get; }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 이름으로 새 문서 세션을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      DocumentCreateResponse Create(string name);

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 location을 문서 세션으로 연다.
      /// </summary>
      // ------------------------------------------------------------
      DocumentOpenResponse Open(IDocumentLocation location);

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 문서 세션을 location에 저장한다.
      /// </summary>
      // ------------------------------------------------------------
      DocumentSaveResponse Save(IDocumentSession session, IDocumentLocation location);

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 문서 세션의 body를 recovery record로 만든다.
      /// </summary>
      // ------------------------------------------------------------
      DocumentBodyRecoveryRecord RecordSessionBody(IDocumentSession session);

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 document, body record, location에서 문서 세션을 복구한다.
      /// </summary>
      // ------------------------------------------------------------
      DocumentOpenResponse RecoverSession
      (
         IDocument document,
         DocumentBodyRecoveryRecord bodyRecord,
         IDocumentLocation location
      );
   }
}
