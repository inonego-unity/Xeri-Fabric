/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentSessionEventArgs.cs
수정일 : 2026-06-22

# 설명
DocumentWorkspace의 session collection 변경 이벤트에서 전달할 event args를 정의한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// Document session 이벤트 인자.
   /// </summary>
   // ============================================================
   public sealed class DocumentSessionEventArgs : EventArgs
   {
   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 이벤트 대상 document session.
      /// </summary>
      // ------------------------------------------------------------
      public IDocumentSession Session { get; }

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Document session 이벤트 인자를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSessionEventArgs(IDocumentSession session) : base()
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         Session = session;
      }

   #endregion

   }
}
