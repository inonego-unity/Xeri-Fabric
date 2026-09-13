/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentWorkspaceRecoveryRecord.cs
수정일 : 2026-07-01

# 설명
Workspace document session을 host lifecycle boundary 이후 복구하기 위한 record 값을 정의한다.
Unity Editor API에 의존하지 않는 순수 데이터 계약으로 유지한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections.Generic;

using UnityEngine;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// Workspace 전체 recovery record.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class DocumentWorkspaceRecoveryRecord
   {
   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 복구 대상 session record 목록.
      /// </summary>
      // ------------------------------------------------------------
      public List<DocumentSessionRecoveryRecord> Sessions = new List<DocumentSessionRecoveryRecord>();

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 workspace recovery record를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentWorkspaceRecoveryRecord() : base() {}

      // ------------------------------------------------------------
      /// <summary>
      /// Workspace recovery record를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentWorkspaceRecoveryRecord
      (
         IEnumerable<DocumentSessionRecoveryRecord> sessions
      ) : this()
      {
         if (sessions == null) return;

         foreach (var session in sessions)
         {
            if (session == null) continue;

            Sessions.Add(session);
         }
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// 하나의 document session을 복구하기 위한 record.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class DocumentSessionRecoveryRecord
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 복구 대상 문서 종류 식별자.
      /// </summary>
      // ------------------------------------------------------------
      public string TypeID = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// 복구 대상 문서 버전.
      /// </summary>
      // ------------------------------------------------------------
      public string Version = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// 복구 대상 문서 표시 이름.
      /// </summary>
      // ------------------------------------------------------------
      public string Name = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// Session location 복구 정보.
      /// </summary>
      // ------------------------------------------------------------
      [SerializeReference]
      public IDocumentLocationRecord Location = null;

      // ------------------------------------------------------------
      /// <summary>
      /// Session body 복구 정보.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentBodyRecoveryRecord Body = null;

      // ------------------------------------------------------------
      /// <summary>
      /// Session dirty 상태.
      /// </summary>
      // ------------------------------------------------------------
      public bool IsDirty = false;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 session recovery record를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSessionRecoveryRecord() : base() {}

      // ------------------------------------------------------------
      /// <summary>
      /// Document session recovery record를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSessionRecoveryRecord
      (
         string _TypeID,
         string version,
         string name,
         IDocumentLocationRecord location,
         DocumentBodyRecoveryRecord body,
         bool isDirty
      ) : base()
      {
         TypeID   = _TypeID ?? "";
         Version  = version ?? "";
         Name     = name ?? "";
         Location = location;
         Body     = body;
         IsDirty  = isDirty;
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// Document location recovery record 계약.
   /// </summary>
   // ============================================================
   public interface IDocumentLocationRecord
   {
      // ------------------------------------------------------------
      /// <summary>
      /// Recovery record에서 document location을 복구한다. 복구할 수 없으면 null을 반환한다.
      /// </summary>
      // ------------------------------------------------------------
      IDocumentLocation Recover();
   }

   // ============================================================
   /// <summary>
   /// FileDocumentLocation 복구 record.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class FileDocumentLocationRecord : IDocumentLocationRecord
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// 사용자에게 표시하거나 진단에 사용할 location 이름.
      /// </summary>
      // ------------------------------------------------------------
      public string Name => name;

      [SerializeField]
      private string name = string.Empty;

      // ------------------------------------------------------------
      /// <summary>
      /// 문서 파일 경로.
      /// </summary>
      // ------------------------------------------------------------
      public string Path => path;

      [SerializeField]
      private string path = string.Empty;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 file document location record를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public FileDocumentLocationRecord() : base() {}

      // ------------------------------------------------------------
      /// <summary>
      /// File document location record를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public FileDocumentLocationRecord
      (
         string name,
         string path
      ) : this()
      {
         this.name = name ?? "";
         this.path = path ?? "";
      }

   #endregion

   #region 메서드

      // ------------------------------------------------------------
      /// <summary>
      /// Record 값에서 file document location을 복구한다.
      /// </summary>
      // ------------------------------------------------------------
      public IDocumentLocation Recover()
      {
         if (string.IsNullOrEmpty(path))
         {
            return null;
         }

         var locName = string.IsNullOrEmpty(name) ? path : name;

         return new FileDocumentLocation(path, locName);
      }

   #endregion

   }

   // ============================================================
   /// <summary>
   /// Handler가 담당하는 body 복구 payload를 담는 record.
   /// </summary>
   // ============================================================
   [Serializable]
   public sealed class DocumentBodyRecoveryRecord
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// Handler가 해석할 body record 문자열.
      /// </summary>
      // ------------------------------------------------------------
      public string Record = string.Empty;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// 기본 document body recovery record를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentBodyRecoveryRecord() : base() {}

      // ------------------------------------------------------------
      /// <summary>
      /// Document body recovery record를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentBodyRecoveryRecord(string record) : this()
      {
         Record = record ?? "";
      }

   #endregion

   }
}
