/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DocumentWorkspaceController.cs
수정일 : 2026-06-23

# 설명
DocumentWorkspaceService 위에서 사용자-facing create/open/save/close 흐름을 해석한다.

# 특이사항
저수준 실행 정책은 DocumentWorkspaceService에 유지하고, 이 controller는 사용자-facing 흐름을
작업별 flow response로 표현한다.
========================================================================= BLOCK_HEADER_END */

using System;

namespace inonego.Xeri.Workspace.Document
{
   // ============================================================
   /// <summary>
   /// 사용자-facing document workspace 흐름 controller.
   /// </summary>
   // ============================================================
   public sealed class DocumentWorkspaceController
   {

   #region 필드

      // ------------------------------------------------------------
      /// <summary>
      /// Controller가 호출하는 저수준 document workspace service.
      /// </summary>
      // ------------------------------------------------------------
      private readonly DocumentWorkspaceService service = null;

   #endregion

   #region 생성자

      // ------------------------------------------------------------
      /// <summary>
      /// Document workspace controller를 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentWorkspaceController(DocumentWorkspaceService service) : base()
      {
         this.service = service ?? throw new ArgumentNullException(nameof(service));
      }

   #endregion

   #region 생성과 열기

      // ------------------------------------------------------------
      /// <summary>
      /// 새 문서 session을 생성한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentCreateResponse Create(string _TypeID, string name)
      {
         if (string.IsNullOrEmpty(_TypeID))
         {
            return DocumentCreateResponse.Fail("문서 종류 식별자가 비어 있습니다.");
         }

         return service.Create(_TypeID, name);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 location에서 문서 session을 연다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentOpenFlowResponse Open(string _TypeID, IDocumentLocation loc)
      {
         if (string.IsNullOrEmpty(_TypeID))
         {
            return DocumentOpenFlowResponse.Fail("문서 종류 식별자가 비어 있습니다.");
         }

         if (loc == null)
         {
            return DocumentOpenFlowResponse.Fail("열 document location이 없습니다.");
         }

         var response = service.Open(_TypeID, loc);
         return ToOpenFlowResponse(response);
      }

   #endregion

   #region 저장

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> 현재 session의 기준 location에 저장한다.
      /// <br/> 기준 location이 없으면 실패가 아니라 location 입력 필요 상태로 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      public DocumentSaveFlowResponse Save(IDocumentSession session)
      {
         if (session == null)
         {
            return DocumentSaveFlowResponse.Fail("저장할 session이 없습니다.");
         }

         if (!service.HasSession(session))
         {
            return DocumentSaveFlowResponse.Fail(session, "workspace에 포함되지 않은 session입니다.");
         }

         if (session.Location == null)
         {
            return DocumentSaveFlowResponse.RequireLoc(session);
         }

         var response = service.Save(session);
         return ToSaveFlowResponse(response, session);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// 지정한 location에 저장하고 session의 기준 location을 변경한다.
      /// </summary>
      // ------------------------------------------------------------
      public DocumentSaveFlowResponse SaveAs(IDocumentSession session, IDocumentLocation loc)
      {
         if (session == null)
         {
            return DocumentSaveFlowResponse.Fail("저장할 session이 없습니다.");
         }

         if (!service.HasSession(session))
         {
            return DocumentSaveFlowResponse.Fail(session, "workspace에 포함되지 않은 session입니다.");
         }

         if (loc == null)
         {
            return DocumentSaveFlowResponse.RequireLoc(session);
         }

         var response = service.SaveAs(session, loc);
         return ToSaveFlowResponse(response, session);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> 지정한 location에 저장하되 session의 기준 location과 dirty 상태는 변경하지 않는다.
      /// <br/> 이 흐름은 export와 달리 현재 document handler의 저장 계약을 그대로 사용한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      public DocumentSaveFlowResponse SaveTo(IDocumentSession session, IDocumentLocation loc)
      {
         if (session == null)
         {
            return DocumentSaveFlowResponse.Fail("저장할 session이 없습니다.");
         }

         if (!service.HasSession(session))
         {
            return DocumentSaveFlowResponse.Fail(session, "workspace에 포함되지 않은 session입니다.");
         }

         if (loc == null)
         {
            return DocumentSaveFlowResponse.RequireLoc(session);
         }

         var response = service.SaveTo(session, loc);
         return ToSaveFlowResponse(response, session);
      }

   #endregion

   #region 닫기

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> 지정한 session을 닫는다.
      /// <br/> Dirty session은 즉시 닫지 않고 사용자 결정 대기 상태로 반환한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      public DocumentCloseFlowResponse Close(IDocumentSession session)
      {
         if (session == null)
         {
            return DocumentCloseFlowResponse.Fail("닫을 session이 없습니다.");
         }

         if (!service.HasSession(session))
         {
            return DocumentCloseFlowResponse.Fail(session, "workspace에 포함되지 않은 session입니다.");
         }

         if (session.IsDirty)
         {
            return DocumentCloseFlowResponse.RequireUser(session);
         }

         return ToCloseFlowResponse(service.Close(session), session);
      }

      // ------------------------------------------------------------------------------------------
      /// <summary>
      /// <br/> 저장되지 않은 변경을 폐기하고 지정한 session을 닫는다.
      /// <br/> 사용자 확인 UI는 호출자가 처리하고, 이 메서드는 확인 이후의 실행만 담당한다.
      /// </summary>
      // ------------------------------------------------------------------------------------------
      public DocumentCloseFlowResponse CloseDiscardingChanges(IDocumentSession session)
      {
         if (session == null)
         {
            return DocumentCloseFlowResponse.Fail("닫을 session이 없습니다.");
         }

         if (!service.HasSession(session))
         {
            return DocumentCloseFlowResponse.Fail(session, "workspace에 포함되지 않은 session입니다.");
         }

         return ToCloseFlowResponse(service.Close(session), session);
      }

   #endregion

   #region 응답 변환

      // ------------------------------------------------------------
      /// <summary>
      /// Service 열기 응답을 사용자-facing 열기 흐름 응답으로 변환한다.
      /// </summary>
      // ------------------------------------------------------------
      private static DocumentOpenFlowResponse ToOpenFlowResponse(DocumentOpenResponse response)
      {
         if (!response.Success)
         {
            return DocumentOpenFlowResponse.Fail(response.Error);
         }

         switch (response.Kind)
         {
            case DocumentOpenKind.NewSession:
               return DocumentOpenFlowResponse.NewSession(response.Session);

            case DocumentOpenKind.AlreadyOpen:
               return DocumentOpenFlowResponse.AlreadyOpen(response.Session);

            default:
               return DocumentOpenFlowResponse.Fail("알 수 없는 문서 열기 결과입니다.");
         }
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Service 저장 응답을 사용자-facing 저장 흐름 응답으로 변환한다.
      /// </summary>
      // ------------------------------------------------------------
      private static DocumentSaveFlowResponse ToSaveFlowResponse
      (
         DocumentSaveResponse response,
         IDocumentSession session
      )
      {
         return response.Success
            ? DocumentSaveFlowResponse.Succeed(session)
            : DocumentSaveFlowResponse.Fail(session, response.Error);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Service 닫기 결과를 사용자-facing 닫기 흐름 응답으로 변환한다.
      /// </summary>
      // ------------------------------------------------------------
      private static DocumentCloseFlowResponse ToCloseFlowResponse
      (
         bool removed,
         IDocumentSession session
      )
      {
         return removed
            ? DocumentCloseFlowResponse.Succeed(session)
            : DocumentCloseFlowResponse.Fail(session, "workspace에 포함되지 않은 session입니다.");
      }

   #endregion

   }
}
