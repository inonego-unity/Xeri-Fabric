/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentWorkspace.cs
수정일 : 2026-06-22

# 설명
열린 document session 목록을 보관하는 Workspace container를 정의한다.
Open, Save, ActiveSession 관리는 별도 service 또는 view/controller 계층의 책임으로 둔다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 열린 document session 목록을 관리하는 Workspace container.
   /// </summary>
   // ============================================================
   public sealed class DocumentWorkspace
   {
      
   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 현재 Workspace에 열린 document session 목록.
      /// </summary>
      // ------------------------------------------------------------
      public IReadOnlyList<IDocumentSession> Sessions => sessions;

      private readonly List<IDocumentSession> sessions = new List<IDocumentSession>();

   #endregion

   #region 이벤트

      // ------------------------------------------------------------
      /// <summary>
      /// Document session이 Workspace에 추가될 때 호출된다.
      /// </summary>
      // ------------------------------------------------------------
      public event EventHandler<DocumentSessionEventArgs> OnSessionAdd = null;

      // ------------------------------------------------------------
      /// <summary>
      /// Document session이 Workspace에서 제거될 때 호출된다.
      /// </summary>
      // ------------------------------------------------------------
      public event EventHandler<DocumentSessionEventArgs> OnSessionRemove = null;

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace에 document session이 포함되어 있는지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool HasSession(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         return sessions.Contains(session);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> 현재 열린 session 목록에서 같은 문서 종류와 location을 가진 session을 찾는다.
      /// <br/> Location은 session identity가 아니라 기본 Open 흐름의 중복 방지 lookup 기준이다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      public bool TryFindOpenSession
      (
         string _TypeID,
         IDocumentLocation location,
         out IDocumentSession session
      )
      {
         if (string.IsNullOrEmpty(_TypeID))
         {
            throw new ArgumentException("문서 종류 식별자가 비어 있습니다.", nameof(_TypeID));
         }

         if (location == null)
         {
            throw new ArgumentNullException(nameof(location));
         }

         foreach (var candidate in sessions)
         {
            if (!IsOpenSessionMatch(candidate, _TypeID, location)) continue;

            session = candidate;
            return true;
         }

         session = null;
         return false;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace에 document session을 추가한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool AddSession(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         if (sessions.Contains(session))
         {
            return false;
         }

         sessions.Add(session);
         OnSessionAdd?.Invoke(this, new DocumentSessionEventArgs(session));

         return true;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace에서 document session을 제거한다.
      /// </summary>
      // ------------------------------------------------------------
      public bool RemoveSession(IDocumentSession session)
      {
         if (session == null)
         {
            throw new ArgumentNullException(nameof(session));
         }

         if (!sessions.Remove(session))
         {
            return false;
         }

         OnSessionRemove?.Invoke(this, new DocumentSessionEventArgs(session));

         return true;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 session이 document type과 location 조건에 맞는지 확인한다.
      /// </summary>
      // ------------------------------------------------------------
      private static bool IsOpenSessionMatch
      (
         IDocumentSession session,
         string _TypeID,
         IDocumentLocation location
      )
      {
         if (session?.Document == null || session.Location == null)
         {
            return false;
         }

         if (session.Document.TypeID != _TypeID)
         {
            return false;
         }

         return session.Location.Equals(location);
      }

   #endregion

   }
}
