/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentSession.cs
수정일 : 2026-06-28

# 설명
Workspace에서 열린 문서의 문서 정보, body, location, dirty 상태를 보관하는 기본 세션 구현체.

# 특이사항
저장, 열기, 다른 이름 저장 정책은 포함하지 않고 세션 상태 전환만 담당한다.
========================================================================= BLOCK_HEADER_END */

using System;

using inonego.Xeri;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// Workspace에서 열린 문서의 기본 세션 구현체.
   /// </summary>
   // ============================================================
   [Serializable]
   public class DocumentSession<TBody> : IDocumentSession<TBody>
   where TBody : class
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 세션이 다루는 문서 정보.
      /// </summary>
      // ------------------------------------------------------------
      public IDocument Document { get; private set; }

      // ------------------------------------------------------------
      /// <summary>
      /// 세션에서 편집 중인 문서 body.
      /// </summary>
      // ------------------------------------------------------------
      public TBody Body { get; }

      object IDocumentSession.Body => Body;

      // ------------------------------------------------------------
      /// <summary>
      /// 세션이 현재 연결된 문서 location.
      /// </summary>
      // ------------------------------------------------------------
      public IDocumentLocation Location { get; private set; }

      // ------------------------------------------------------------
      /// <summary>
      /// 세션의 편집 내용이 저장 대상과 달라졌는지 여부.
      /// </summary>
      // ------------------------------------------------------------
      public bool IsDirty => dirtyFlag.IsDirty;

      private readonly DirtyFlag dirtyFlag = new DirtyFlag();

   #endregion

   #region 이벤트

      // ------------------------------------------------------------
      /// <summary>
      /// 세션의 문서 정보가 변경될 때 발생한다.
      /// </summary>
      // ------------------------------------------------------------
      public event ValueChangeEventHandler<IDocument> OnDocumentChange = null;

      // ------------------------------------------------------------
      /// <summary>
      /// 세션의 문서 location이 변경될 때 발생한다.
      /// </summary>
      // ------------------------------------------------------------
      public event ValueChangeEventHandler<IDocumentLocation> OnLocationChange = null;

      // ------------------------------------------------------------
      /// <summary>
      /// 세션의 dirty 상태가 변경될 때 발생한다.
      /// </summary>
      // ------------------------------------------------------------
      public event ValueChangeEventHandler<bool> OnDirtyChange = null;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 세션을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSession
      (
         IDocument document,
         TBody body,
         IDocumentLocation location
      ) : base()
      {
         Document = document ?? throw new ArgumentNullException(nameof(document));
         Body = body ?? throw new ArgumentNullException(nameof(body));
         Location = location;
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// 세션의 문서 정보를 설정한다.
      /// </summary>
      // ------------------------------------------------------------
      public void SetDocument(IDocument document)
      {
         if (document == null)
         {
            throw new ArgumentNullException(nameof(document));
         }

         if (Equals(Document, document)) return;

         var previous = Document;

         Document = document;
         OnDocumentChange?.Invoke(this, new ValueChangeEventArgs<IDocument>(previous, Document));
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 세션의 문서 location을 설정한다.
      /// </summary>
      // ------------------------------------------------------------
      public void SetLocation(IDocumentLocation location)
      {
         if (Equals(Location, location)) return;

         var previous = Location;

         Location = location;
         OnLocationChange?.Invoke(this, new ValueChangeEventArgs<IDocumentLocation>(previous, Location));
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 세션을 변경됨 상태로 표시한다.
      /// </summary>
      // ------------------------------------------------------------
      public void SetDirty()
      {
         if (IsDirty) return;

         var previous = IsDirty;

         dirtyFlag.SetDirty();
         OnDirtyChange?.Invoke(this, new ValueChangeEventArgs<bool>(previous, IsDirty));
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 세션의 변경됨 상태를 해제한다.
      /// </summary>
      // ------------------------------------------------------------
      public void ClearDirty()
      {
         if (!IsDirty) return;

         var previous = IsDirty;

         dirtyFlag.Clear();
         OnDirtyChange?.Invoke(this, new ValueChangeEventArgs<bool>(previous, IsDirty));
      }

   #endregion

   }
}
