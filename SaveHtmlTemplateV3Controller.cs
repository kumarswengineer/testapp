using EHR_Common_Functions_Utility;
using EHR_DB_Connect_Helper;
using EHR_EasyFormSaveV2_API.CommonFunctions;
using EHR_EF_NotesFormation.BusinessFacade;
using EHR_RestAPI_Calling;
using EHR_SendMail;
using AutoMapper;
using EHR_Shared_Utilities_New.SharedModels;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using EHR_EFFieldedSaving.BusinessFacade.EasyFormFieldedSaving;
using EHR_CentralizerMessage;
using EHR_EFFieldedSaving.BusinessFacade.EasyFormProperties;
using System.Globalization;
using EHR_EFNotesSaveGoogleCloudStorage.Models;
using EHR_EFNotesSaveGoogleCloudStorage;
using EHR_EFFieldedSaving.BusinessFacade.DBAcess;
using EHR_EasyFormSaveV2_API.SaveV3Classes;
using System.Diagnostics;

namespace EHR_EasyFormSaveV2_API.Controllers
{
    #region "   Controller "
    [RoutePrefix("EasyFormSaveV3")]
    public class SaveHtmlTemplateV3Controller : ApiController
    {

        #region "                   SAVE HTML TEMPLATES                 "

        /// <summary>
        /// *******PURPOSE              : THIS IS USED FOR SAVING EASY FORM INFORMAIOTN
        ///*******CREATED BY            : DURGA PRASAD
        ///*******CREATED DATE          : 05/23/2017
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="htmltemplateinputmodel"></param>
        /// <returns></returns>
        //WEB API Method Goes Here
        [Route("SaveHtmlTemplateV3")]
        [HttpPost]
        public async Task<IHttpActionResult> SaveHtmlTemplateV3_Main(HtmlTemplateSavingInputModel objSavingInputModel)
        {

            if (objSavingInputModel == null)
                return BadRequest("Provide Valid Inputs.");

            //here we are calling this method  for to save Documented Notes Saving input json Meta Data in temp 
            //return Ok(CallsaveapiInCaseofEmergency(objSavingInputModel));
            new SaveEasyFormDocNotesInputjsoninTempBF().ExecuteDocNotesInputJsonSavinginTemp(objSavingInputModel);
            EasyFormNotesSaveOrUpdateBF _ObjBusinessFacade = new EasyFormNotesSaveOrUpdateBF();
            return Ok(await Task.FromResult(_ObjBusinessFacade.SaveOrUpdateNotesWithStaticHCIs(objSavingInputModel)));

        }
        #endregion

        private EasyFormSaveResponseModel CallsaveapiInCaseofEmergency(HtmlTemplateSavingInputModel objSavingInputModel)
        {
            EasyFormSaveResponseModel returnModel = new EasyFormSaveResponseModel();
            ClsInputforPostServiceModel clsinputforpostservicemodel = new ClsInputforPostServiceModel();
            clsinputforpostservicemodel.requestURL = HttpContext.Current.Request.Url.Scheme + @"://" + HttpContext.Current.Request.Url.Host + "/EHR_EasyFormSave_API/EasyFormSave/SaveHtmlTemplate";
            clsinputforpostservicemodel.strJsonstring = JsonConvert.SerializeObject(objSavingInputModel);
            string responseJson = new Cls_RestApiCalling().CallPostServiceWithModelInput(clsinputforpostservicemodel);
            returnModel = JsonConvert.DeserializeObject<EasyFormSaveResponseModel>(responseJson);
            return returnModel;
        }
    }
    #endregion

    #region "   Business Facade"
    internal class EasyFormNotesSaveOrUpdateBF
    {
        #region  "      CONSTRUCTOR FUNCTION TO CREATE THE INSTANCE FOR THE DIFFERERENT CHILD CLASS USED IN THIS CLASS     "

        // declaring the class level varible to hold the child classes calss instance
        private CreatUdtForAutoForwardedUsersInfoBF _objUDTForAutoForwardUserBF;
        private GetReqInputsInfoFromDBOnSaveActionBF _objReqInputsGetBF;
        private GetNotesDOSInReqFormatBF _objDOSFormatBF;
        private SetPNFS_ActionsFlagsOnNotesSaveOrUpdateInputsBF _objSetPNFSActionFlagsBF;
        private NotesFormationBF _objNotesFormationBF;
        private SaveAndGetNotesOriginalAndFormattedBinaryInTempURLsBF _objBinarySavingInTempCls;

        public EasyFormNotesSaveOrUpdateBF()
        {
            _objUDTForAutoForwardUserBF = new CreatUdtForAutoForwardedUsersInfoBF();
            _objReqInputsGetBF = new GetReqInputsInfoFromDBOnSaveActionBF();
            _objDOSFormatBF = new GetNotesDOSInReqFormatBF();
            _objSetPNFSActionFlagsBF = new SetPNFS_ActionsFlagsOnNotesSaveOrUpdateInputsBF();
            _objNotesFormationBF = new NotesFormationBF();
            _objBinarySavingInTempCls = new SaveAndGetNotesOriginalAndFormattedBinaryInTempURLsBF();
        }

        #endregion

        #region     "       SAVE OR UPDATE EASYFORM NOTES ALONG WITH STATIC HCIS SAVING BEFORE EAYSFORM SAVE      "

        public EasyFormSaveResponseModel SaveOrUpdateNotesWithStaticHCIs(HtmlTemplateSavingInputModel objSavingInputModel)
        {
            EasyFormSaveResponseModel easyFormSavingResponse = null;
            ResponseModel model = null;  //Used to maintain the User Data.
            List<EFNotesStartTimeAndEndtimeValidationInfoModel> EFNotesStartTimeAndEndtimeValidationInfoList = null;
            List<EasyFormSaveActionWiseTimeInfo> EasyFormSaveActionWiseTimeInfoList = new List<EasyFormSaveActionWiseTimeInfo>();
            EasyFormSaveActionWiseTimeInfo eachAction = null;
            var timer = new Stopwatch();
            try
            {
                easyFormSavingResponse = new EasyFormSaveResponseModel();
                timer.Start();
                #region "         Step 1    -    UN ZIP EASY FORM               "
                // when not offline application then only doing lzstring unzipping
                if (!objSavingInputModel.easyformIsOfflineSync)
                {
                    new EasyFormsLZStringDeCompressBF().EasyFormsLZStringDeCompress(ref objSavingInputModel);
                }
                #endregion

                #region "         Step 2    -    STATIC HCI SAVE             "

                //ADDED BY MAHESH P ON 05/14/2016 FOR EMERGENCY WRITE MODE SAVING
                //when not emergency web only then only saving the static health care items based domain values// && !objSavingInputModel.easyformIsOfflineSync
                if (!objSavingInputModel.isEmergencyWeb && objSavingInputModel.IsEasyFormContainsStaticHciItemsLinkingExists == true)
                    model = new ValidateAssignSaveStaticHCIInfo().GetEasyFormsFIeldsAndValues(objSavingInputModel);
                //model = SaveStaticHCIValuesModuleWise(objSavingInputModel);


                if (objSavingInputModel.ApplicationType != 2 && new[] { 999, 618, 467 }.Contains(objSavingInputModel.PracticeID) &&
                    (objSavingInputModel.ButtonClickActionType == 11 || objSavingInputModel.IsSignedOff))
                {

                    model = new GetProgramServiceDOSCombinationValidationDA().GetProgramServiceDOSCombinationValidation(objSavingInputModel);
                    if (model?.RequestExecutionStatus == -2 && !string.IsNullOrEmpty(model?.ErrorMessage))
                    {
                        easyFormSavingResponse.RequestExecutionStatus = -2;
                        easyFormSavingResponse.ErrorMessage = model.ErrorMessage;
                        return easyFormSavingResponse;
                    }

                }

                if (new[] { 999, 480, 467, 633 }.Contains(objSavingInputModel.PracticeID))
                {

                    if (objSavingInputModel?.DoctorID > 0 && !string.IsNullOrWhiteSpace(objSavingInputModel?.DocumentedDate) && objSavingInputModel?.StartTime > 0 && objSavingInputModel?.EndTime > 0)
                    {
                        EFNotesStartTimeAndEndtimeValidationInfoList = new GetEasyFormsNotesStartEndTimeofallnotesdocumentedbySameProvideronSameDateValidationDA().GetEasyFormsNotesStartEndTimeofallnotesdocumentedbySameProvideronSameDateValidationList(objSavingInputModel);
                        if (EFNotesStartTimeAndEndtimeValidationInfoList != null && EFNotesStartTimeAndEndtimeValidationInfoList.Count > 0)
                        {
                            foreach (EFNotesStartTimeAndEndtimeValidationInfoModel eachItem in EFNotesStartTimeAndEndtimeValidationInfoList)
                            {

                                eachItem.StartTime = GetNormaltimeFromMilatryTime(eachItem.MilatryStartTime.ToString());
                                eachItem.EndTime = GetNormaltimeFromMilatryTime(eachItem.MilatryEndTime.ToString());
                            }
                            easyFormSavingResponse.EFNotesStartTimeAndEndtimeValidationInfoList = EFNotesStartTimeAndEndtimeValidationInfoList;
                            return easyFormSavingResponse;
                        }
                    }

                }

                //ASSIGNING PROCEDURE RX MODEL FROM STATIC HCI ITEM TO RESPONSE MODEL.....
                if (objSavingInputModel.proceduresrxmedicinesmodelList != null)
                    easyFormSavingResponse.proceduresrxmedicinesmodelList = objSavingInputModel.proceduresrxmedicinesmodelList;

                //ASSIGING THE BILLING RENDERING PROVIDER VALUE
                easyFormSavingResponse.BillingSuperBillMngtRenderingProviderID = objSavingInputModel.BillingSuperBillMngtRenderingProviderID;

                //ASSIGING THE PROGRAM ID TO RESPONSE
                //ADDED BY PHANI KUMAR M ON 19 TH APRIL 2017
                //easyFormSavingResponse.ProgramID = objSavingInputModel.ProgramID;
                easyFormSavingResponse.ProgramServicesLinkedInfoID = objSavingInputModel.ProgramServicesLinkedInfoID;
                easyFormSavingResponse.ProgramServicesLinkedOnlyProgramID = objSavingInputModel.ProgramServicesLinkedOnlyProgramID;
                //ASSIGING THE EYE RESULT TO RESPONSE
                //ADDED BY PHANI KUMAR M ON 6 TH JUNE 2017
                if (!string.IsNullOrWhiteSpace(objSavingInputModel.ProcedureEyeExamResult))
                    easyFormSavingResponse.ProcedureEyeExamResult = objSavingInputModel.ProcedureEyeExamResult;

                //GETTING STATIC HCI REFERRAL AUTH ID TO FRONT END TO USE IT IN ADMISSION AUTO CREATION.....
                if (objSavingInputModel.DemographicsStaticHCIReferralAuthID > 0)
                    easyFormSavingResponse.DemographicsStaticHCIReferralAuthID = objSavingInputModel.DemographicsStaticHCIReferralAuthID;

                //ASSIGING THE PROGRAM START DATE
                //ADDED BY PHANI KUMAR M ON 19 TH SEP 2017
                if (!string.IsNullOrWhiteSpace(objSavingInputModel.ProgramStartDate))
                    easyFormSavingResponse.ProgramStartDate = objSavingInputModel.ProgramStartDate;

                #endregion
                timer.Stop();
                TimeSpan timespan = timer.Elapsed;
                eachAction = new EasyFormSaveActionWiseTimeInfo();
                eachAction.ActionName = "Save Or Update Notes With Static HCIs";
                eachAction.Duration = timespan.ToString();
                AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);

                #region "               SAVE OR UPDATE EASY FORM              "

                // added by mahesh p on 05/22/2017 
                // when it is offline sync then we are not going to check any of the validations
                if (objSavingInputModel.easyformIsOfflineSync)
                {
                    if (model != null && model.RequestExecutionStatus != null && model.RequestExecutionStatus < 0)
                        model = null;
                }

                if (model == null || (model.RequestExecutionStatus == null || model.RequestExecutionStatus == 0))
                {
                    if (model != null && model.ResponseID > 0)
                        objSavingInputModel.ReferToID = model.ResponseID;

                    model = SaveOrUpdateNotes(objSavingInputModel, ref easyFormSavingResponse, EasyFormSaveActionWiseTimeInfoList);
                }

                #endregion

            }
            finally
            {
                // ASSIGING THE RESPONSE MODEL
                easyFormSavingResponse.EasyFormResponseModel = model;
            }
            return easyFormSavingResponse;
        }
        #endregion

        private string GetNormaltimeFromMilatryTime(string timetoFormat)
        {
            string normalTime = "";
            string hours = "";
            string minutes = "";

            if (timetoFormat.Length > 0 && timetoFormat.Length == 3)
            {
                minutes = timetoFormat.Substring(1, 2);
                hours = "0" + timetoFormat.Substring(0, 1);
            }
            else if (timetoFormat.Length > 0 && timetoFormat.Length == 4)
            {
                minutes = timetoFormat.Substring(2, 2);
                hours = timetoFormat.Substring(0, 2);
            }
            DateTime dateTime = new DateTime(1994, 1, 2, Convert.ToInt32(hours), Convert.ToInt32(minutes), 0);
            normalTime = dateTime.ToString("hh:mm tt");

            return normalTime;
        }

        #region     "       SAVE OR UPDATE EASYFORM NOTES WITHOUT STATIC HCIS SAVING     "

        public ResponseModel SaveOrUpdateNotes(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, ref EasyFormSaveResponseModel objEasyFormSaveResponseModel, List<EasyFormSaveActionWiseTimeInfo> EasyFormSaveActionWiseTimeInfoList)
        {
            #region "               VARIABLE DECLARATION                "
            ResponseModel model = null;  //Used to maintain the User Data. 
            ResponseModel TempModel = null;
            string notesFormattedString = string.Empty;
            string notesString = string.Empty;
            string FormDOSForReminders = string.Empty;
            string strMulitpleAtandeeIDs = string.Empty;
            string originalTemplate = string.Empty;
            ResponseModel GTMultipleAttendeesResponseModel = null;
            string strerrormsg = "";
            DataTable dtGroupTheraphySessionAttendeeNotes = null;
            Boolean saveFormattedDataOnly = false;
            int curPatientDataID = 0;
            DataTable dtAutoForwardToSupervisors = null;
            DataTable dtEasyFormsPatientsInfo = null;
            HtmlTemplateSavingDetailsModel objEasyFormDetails = null;
            DataTable dtEasyFormsEHRSignedURLHX = null;
            DataTable dtEasyFormsPortalSignedURLHX = null;
            DataTable dtMoveBackwardUsers = null; //Used to Store Backward Users
            EasyFormSaveActionWiseTimeInfo eachAction = null;
            var timer = new Stopwatch();
            #endregion

            try
            {
                #region             "                GET HTML STRING FROM THE BASE 64 STRING        "
                if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64))
                    originalTemplate = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64));

                #endregion

                #region "               VALIDATING EASY FORM NOTES HTML BODY STRING BEFORE SAVE           "

                if (string.IsNullOrWhiteSpace(originalTemplate) || originalTemplate == "undefined" || IsTemplateBinaryHavingBody(originalTemplate) == false)
                {
                    return new ResponseModel()
                    {
                        RequestExecutionStatus = -2,
                        ErrorMessage = "Unable to Save the Notes...",
                    };
                }
                #endregion

                #region "               CHECKING EMR MANDATORY ELEMENTS VALIDATION           "

                // SAVE MANDATORY FIELDS FILLED OR NOT INFORMATION 1.MEANS NOT FILLED 2. MEANS FILLED 3.STILL NOT PROCESS
                try
                {
                    timer.Start();
                    // CustomMandatoryFieldsFilledType = 2 Means , Custom Validtions Failed for This Easy Form
                    if (htmltemplatesavinginputmodel.CustomMandatoryFieldsFilledType == 1)
                        htmltemplatesavinginputmodel.MandatoryFieldsFilledType = 1;//1 - for Pending ;2 - Completed
                    else
                    {
                        // CUSTOMMANDATORYFIELDSFILLEDTYPE = 0 ==> MEANS NOT VERIFIED FROM FRONEND
                        // CUSTOMMANDATORYFIELDSFILLEDTYPE = 1 ==> MEANS VERIFIED FROM FRONTENT AND ALL FIELDS ARE FILLED
                        // SO WE NEED TO CHECK FOR EMR MANDATORY ELEMENTS
                        // ASSIGN MANDATORY FIELDS FILLED OPTION TYPES
                        if (new ValidateMandatoryFieldsBF().CheckAnyMandatoryValidationsExistsInHTMLDocument(originalTemplate))
                            htmltemplatesavinginputmodel.MandatoryFieldsFilledType = 1; // All mandatory Fields Not Filled
                        else
                            htmltemplatesavinginputmodel.MandatoryFieldsFilledType = 2; //All mandatory fields filled (EHR AND EHR ONLY)
                    }
                    timer.Stop();
                    TimeSpan t1 = timer.Elapsed;
                    eachAction = new EasyFormSaveActionWiseTimeInfo();
                    eachAction.ActionName = "Checking EMR Mandatory Elements Validation";
                    eachAction.Duration = t1.ToString();
                    AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                }
                catch (Exception ex)
                {
                    new SendExpMail().SendExceptionmail(ex, "SaveOrUpdateNotes", htmltemplatesavinginputmodel);
                }

                #endregion

                #region "               GET AUTO FORWARD USERS INFO - NO SP CALL            "

                dtAutoForwardToSupervisors = _objUDTForAutoForwardUserBF.CreateTableForCustomizeSelectedSupervisorsInfo(htmltemplatesavinginputmodel);
                #endregion

                #region "               GET SIGN OFF & MOVE BACKWARD USERS INFO - NO SP CALL            "

                if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0)
                    dtMoveBackwardUsers = new CreatUdtForNotesAutoBackwardUsersInfoBF().GetBackwardUsersInboxList(htmltemplatesavinginputmodel);
                #endregion

                #region "               GET REQUIRED I/P PARAMETERS REQUIRED FOR SAVING / UPDATING EASY FORM                "

                timer.Start();
                objEasyFormDetails = _objReqInputsGetBF.GetEasyFormRequiredFieldsInfoFromDB(htmltemplatesavinginputmodel,
                                                                         ref dtEasyFormsPatientsInfo,
                                                                         ref dtEasyFormsEHRSignedURLHX,
                                                                         ref dtEasyFormsPortalSignedURLHX);
                timer.Stop();
                TimeSpan t2 = timer.Elapsed;
                eachAction = new EasyFormSaveActionWiseTimeInfo();
                eachAction.ActionName = "GET REQUIRED I/P PARAMETERS REQUIRED FOR SAVING / UPDATING EASY FORM";
                eachAction.Duration = t2.ToString();
                AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);

                #region "               VALIDATE FOR -  UPDATE THE APPOINTMENT STATUS BASED ON EASYFORM SAVE ACTION              "
               
                htmltemplatesavinginputmodel.ActionPerformedID = objEasyFormDetails.ActionPerformedID;
                if (htmltemplatesavinginputmodel.ApplicationType != 2 && htmltemplatesavinginputmodel.IsAppointmentStatusAutoUpdateStatusExists)
                {
                    timer.Start();
                    if ((!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PatientIDs) && htmltemplatesavinginputmodel.GroupTherapySessionType == 2 && htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0) || (htmltemplatesavinginputmodel.patientchartmodel != null && htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0))
                    {
                        TempModel = new ValidateChangeApptStatusOnFormSaveActionBF().ValidateChangeApptStatusOnFormSaveAction(ref htmltemplatesavinginputmodel);
                        if (TempModel != null && TempModel.RequestExecutionStatus == -2)
                            return TempModel;
                    }
                    timer.Stop();
                    TimeSpan t3 = timer.Elapsed;
                    eachAction = new EasyFormSaveActionWiseTimeInfo();
                    eachAction.ActionName = "VALIDATE FOR -  UPDATE THE APPOINTMENT STATUS BASED ON EASYFORM SAVE ACTION";
                    eachAction.Duration = t3.ToString();
                    AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                }
                #endregion

                if (objEasyFormDetails != null)
                {
                    timer.Start();
                    _objDOSFormatBF.GetEasyFormDOSInRequiredFormat(ref objEasyFormDetails);

                    _objSetPNFSActionFlagsBF.Assign_EasyForm_PNFS_Flags_Based_On_Condition(htmltemplatesavinginputmodel,
                                                                  ref objEasyFormDetails,
                                                                  ref dtEasyFormsPatientsInfo,
                                                                  ref dtEasyFormsEHRSignedURLHX,
                                                                  ref dtEasyFormsPortalSignedURLHX,
                                                                  dtAutoForwardToSupervisors,
                                                                  dtMoveBackwardUsers);

                    //To Get Electronically Created DOS
                    htmltemplatesavinginputmodel.DOSFiledinEasyForm = objEasyFormDetails.DosFilledInForm;
                    timer.Stop();
                    TimeSpan t4 = timer.Elapsed;
                    eachAction = new EasyFormSaveActionWiseTimeInfo();
                    eachAction.ActionName = "Assign_EasyForm_PNFS_Flags_Based_On_Condition";
                    eachAction.Duration = t4.ToString();
                    AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                }
                #endregion

                #region "               NOTES FORMATION, ELECTRONICALLYT SIGNED INFO, AMENDMENTS              "

                try
                {
                    timer.Start();
                    //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&//
                    //modified by Swaraj B on 03 Jan, 2024
                    //as per Ravi D, enabling notes formation for all the practices
                    //if (new[] { 999, 1, 467, 2, 8, 9, 29, 34, 36, 37, 43, 45, 51, 52, 58, 59, 61, 65, 71, 74, 75, 79, 80, 89, 91, 92, 93, 99,
                    //101, 103, 106, 108, 109, 116, 118, 119, 120, 130, 132, 141, 143, 152, 157, 158, 161, 164, 165, 166, 172, 174,
                    //179, 181, 186, 190, 203, 214, 219, 222, 223, 224, 227, 228, 232, 240, 241, 248,
                    //253, 260, 265, 266, 269, 276, 285, 287, 295, 299, 308, 314, 318,
                    //319, 321, 323, 2, 327, 328, 329, 330, 332, 333, 334, 335}.Contains(htmltemplatesavinginputmodel.PracticeID))
                    //{
                    //if notes formation flag is enabled for template then only performation notes formation else both original and formatted are same
                    if (htmltemplatesavinginputmodel.IsNotesFormationEnabled == true)
                    {
                        _objNotesFormationBF.GetNotesFormatedHtmlString(ref htmltemplatesavinginputmodel, ref originalTemplate, ref notesFormattedString);
                    }
                    else
                    {
                        notesFormattedString = string.Copy(originalTemplate);

                        if (htmltemplatesavinginputmodel.IsShowElectronicallyCreatedInformation == true)
                            notesFormattedString = new GetAndAppendElectronicalySignUserInfoToNotesBF().
                                                    GetAndAppendElectronicalySignUserInfoToNotes(htmltemplatesavinginputmodel, notesFormattedString);

                    }
                    timer.Stop();
                    TimeSpan t5 = timer.Elapsed;
                    eachAction = new EasyFormSaveActionWiseTimeInfo();
                    eachAction.ActionName = "NOTES FORMATION, ELECTRONICALLY SIGNED INFO, AMENDMENTS ";
                    eachAction.Duration = t5.ToString();
                    AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                    //}
                    //else
                    //{
                    //    _objNotesFormationBF.GetNotesFormatedHtmlString(ref htmltemplatesavinginputmodel, ref originalTemplate, ref notesFormattedString);
                    //}
                    //&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&//

                }
                catch (Exception currentException)
                {
                    new SendExpMail().SendExceptionmail(currentException, "SaveOrUpdateNotes", htmltemplatesavinginputmodel);
                }
                #endregion

                #region "               ZIP EASY FORM                           "

                if (!string.IsNullOrWhiteSpace(originalTemplate))
                    new ZipEasyFormOriginalAndFormatedBinaryBF().ZipEasyFormOriginalAndFormatedBinary(ref htmltemplatesavinginputmodel, originalTemplate, notesFormattedString);

                #endregion

                #region "               VALIDATING EASY FORM BINARY BEFORE SAVE           "

                if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat == null
                    || htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat.Length <= 0)
                    return new ResponseModel()
                    {
                        RequestExecutionStatus = -2,
                        ErrorMessage = "Unable to Save the Notes...",
                    };
                #endregion

                #region "               SAVE NOTES BINARY IN TEMP AND GET TEMP URLS                 "
                timer.Start();
                bool isEasyFormTempSavingSuccessFull = true;

                _objBinarySavingInTempCls.SaveAndGetNotesOriginalAndFormattedBinaryInTempURLs(ref htmltemplatesavinginputmodel, ref isEasyFormTempSavingSuccessFull);
                timer.Stop();
                TimeSpan t6 = timer.Elapsed;
                eachAction = new EasyFormSaveActionWiseTimeInfo();
                eachAction.ActionName = "SAVE NOTES BINARY IN TEMP AND GET TEMP URLS";
                eachAction.Duration = t6.ToString();
                AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                #endregion

                #region "               SAVE / UPDATE EASY FORM                 "
                timer.Start();
                if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID == 0)
                {
                    //======== EASY FORM - SAVING START =========
                    model = new EasyFormNotesSaveDA().SaveHtmlTemplate(htmltemplatesavinginputmodel, ref FormDOSForReminders, dtAutoForwardToSupervisors, objEasyFormDetails, dtEasyFormsPatientsInfo);
                    //======== EASY FORM - SAVING END =========
                    if (model != null && model.ResponseID > 0)
                        curPatientDataID = model.ResponseID;

                    #region "               STAIC HCI - UPDATE IN RDP TABLES                "

                    if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.VitalPatientIDS))
                    {
                        TempModel = new UpdateVitalsInfoFromNotesStaticHCIsDataDA().UpdateVitalsHTMLTemplatesPatientDataInfo(htmltemplatesavinginputmodel);
                        if (TempModel != null && TempModel.RequestExecutionStatus < 0)
                            return TempModel;
                    }

                    if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.QuickLabOrderTestOrderIDs))
                    {
                        TempModel = new UpdateQuickLabOrderInfoOnNotesStaticHCIsDataDA().UpdatePatientDataID_For_EasyFormQuickLabOrder(htmltemplatesavinginputmodel);
                        if (TempModel != null && TempModel.RequestExecutionStatus < 0)
                            return TempModel;
                    }

                    if (htmltemplatesavinginputmodel.ProgressNotesFollowupInfoID > 0)
                    {
                        TempModel = new UpdateFollowUpTextInfoFromNotesStaticHCIsDataDA().UpdateEasyFormsProgressNotesFollowupText(htmltemplatesavinginputmodel);
                        if (TempModel != null && TempModel.RequestExecutionStatus < 0)
                            return TempModel;
                    }

                    if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.QuickImagingOrderIDs))
                    {
                        TempModel = new UpdateQuickImagingOrdersFromNotesStaticHCIsDataDA().UpdatePatientDataID_For_EasyFormQuickImagingOrder(htmltemplatesavinginputmodel);
                        if (TempModel != null && TempModel.RequestExecutionStatus < 0)
                            return TempModel;
                    }
                    #endregion
                }
                else
                {
                    //======== EASY FORM - UPDATING START =========
                    model = new EasyFormNotesUpdateDA().UpdateHtmlTemplate(htmltemplatesavinginputmodel, ref FormDOSForReminders, dtAutoForwardToSupervisors, dtMoveBackwardUsers, objEasyFormDetails, dtEasyFormsEHRSignedURLHX, dtEasyFormsPortalSignedURLHX);
                    //======== EASY FORM - UPDATING START =========
                    curPatientDataID = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID;

                }

                if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0 &&
                    (htmltemplatesavinginputmodel.RefertoFreeTextHCI > 0 ||
                    htmltemplatesavinginputmodel.InstructionsFreeTextHCI > 0 ||
                    htmltemplatesavinginputmodel.RequestMedicalRecordsFreeTextHCI > 0))
                {
                    DataTable dtHCIAndFreeText = new DataTable();
                    DataRow drEachHCIandFreeText = null;
                    dtHCIAndFreeText.Columns.Add("EasyForms_HealthCareItems_StaticItem_InfoID", typeof(Int32));
                    dtHCIAndFreeText.Columns.Add("EF_Freetext", typeof(string));

                    if (htmltemplatesavinginputmodel.RefertoFreeTextHCI > 0 && !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.RefertoFreeText))
                    {
                        drEachHCIandFreeText = dtHCIAndFreeText.NewRow();
                        drEachHCIandFreeText["EasyForms_HealthCareItems_StaticItem_InfoID"] = htmltemplatesavinginputmodel.RefertoFreeTextHCI;
                        drEachHCIandFreeText["EF_Freetext"] = htmltemplatesavinginputmodel.RefertoFreeText;
                        dtHCIAndFreeText.Rows.Add(drEachHCIandFreeText);
                    }

                    if (htmltemplatesavinginputmodel.InstructionsFreeTextHCI > 0 && !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.InstructionsFreeText))
                    {
                        drEachHCIandFreeText = dtHCIAndFreeText.NewRow();
                        drEachHCIandFreeText["EasyForms_HealthCareItems_StaticItem_InfoID"] = htmltemplatesavinginputmodel.InstructionsFreeTextHCI;
                        drEachHCIandFreeText["EF_Freetext"] = htmltemplatesavinginputmodel.InstructionsFreeText;
                        dtHCIAndFreeText.Rows.Add(drEachHCIandFreeText);
                    }
                    if (htmltemplatesavinginputmodel.RequestMedicalRecordsFreeTextHCI > 0 && !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.RequestMedicalRecordsFreeText))
                    {
                        drEachHCIandFreeText = dtHCIAndFreeText.NewRow();
                        drEachHCIandFreeText["EasyForms_HealthCareItems_StaticItem_InfoID"] = htmltemplatesavinginputmodel.RequestMedicalRecordsFreeTextHCI;
                        drEachHCIandFreeText["EF_Freetext"] = htmltemplatesavinginputmodel.RequestMedicalRecordsFreeText;
                        dtHCIAndFreeText.Rows.Add(drEachHCIandFreeText);
                    }

                    if (dtHCIAndFreeText?.Rows?.Count > 0)
                    {
                        new InsertEFNotesEnteredFreeTextBasedOnHCIDataAcess().InsertEFNotesEnteredFreeTextBasedOnHCI(htmltemplatesavinginputmodel, dtHCIAndFreeText);
                    }

                }


                /*This Code snippet is used to insert or update start time and end time doctor id info to validate 
                 * over lapping times with users of an doctor
                 */
                if (htmltemplatesavinginputmodel.ApplicationType == 1 && htmltemplatesavinginputmodel.SendMessageifPatientisHighRisk && htmltemplatesavinginputmodel.IsHighRiskHomicideSucide &&
                    htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0 && htmltemplatesavinginputmodel.IsSignedOff) //
                {
                    new PatientHighRiskForSuicideOrHomicideDA().PatientHighRiskForSuicideOrHomicideInsert(htmltemplatesavinginputmodel);
                }

                /*This Code snippet is used to insert or update start time and end time doctor id info to validate 
                 * over lapping times with users of an doctor
                 */
                if (htmltemplatesavinginputmodel.ApplicationType == 1 && new[] { 999, 480, 467, 633 }.Contains(htmltemplatesavinginputmodel.PracticeID) &&
                    htmltemplatesavinginputmodel?.DocumentsFillableHTMLTemplatesPatientDataID > 0 &&
                    htmltemplatesavinginputmodel?.DoctorID > 0 && !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel?.DocumentedDate) &&
                    htmltemplatesavinginputmodel?.StartTime > 0 && htmltemplatesavinginputmodel?.EndTime > 0)
                {
                    new UpdateEasyFormNotesStartAndEndTimeOfServiceProvidedDA().UpdateEasyFormNotesStartAndEndTimeOfServiceProvided(htmltemplatesavinginputmodel);
                }


                /*This Code snippet  is used to insert or update Program and service id and Provider id 
                 *  To validate if provider is documenting same program service in same dos
                 */
                if (htmltemplatesavinginputmodel.ApplicationType == 1 && new[] { 999, 618, 467 }.Contains(htmltemplatesavinginputmodel.PracticeID) &&
                    htmltemplatesavinginputmodel?.DocumentsFillableHTMLTemplatesPatientDataID > 0 &&
                    htmltemplatesavinginputmodel?.ProgramServiceLinkedInfoIDForValidation > 0 &&
                    htmltemplatesavinginputmodel?.BillingSuperBillMngtRenderingProviderID > 0)
                {
                    new ProviderProgramserviceDOSCombinationInsertionDA().ProgramserviceDOSCombinationInsertOrUpdate(htmltemplatesavinginputmodel);
                }

                htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime = FormDOSForReminders;
                objEasyFormSaveResponseModel.AppointmentDateTime = htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime;
                objEasyFormSaveResponseModel.IsEasyFormGoldenThreadNeedsDxCodesSavingRequired = htmltemplatesavinginputmodel.IsEasyFormGoldenThreadNeedsDxCodesSavingRequired;
                objEasyFormSaveResponseModel.IsEasyFormOtherOrdersModuleRequired = htmltemplatesavinginputmodel.IsEasyFormOtherOrdersModuleRequired;
                objEasyFormSaveResponseModel.EasyFormReminderDueUserID = htmltemplatesavinginputmodel.EasyFormReminderDueUserID;

                if (!string.IsNullOrWhiteSpace(strerrormsg))
                {
                    strerrormsg = "Reason for Failed Notes Formation : " + strerrormsg;
                    if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EasyFormTemplateName))
                        strerrormsg = strerrormsg + "<BR> Easy Form Name: " + htmltemplatesavinginputmodel.EasyFormTemplateName;
                    strerrormsg += "<BR> Template ID : " + htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID;
                    strerrormsg += "<BR> Patient Data ID : " + curPatientDataID;
                }

                // if the form is successfully saved but the binary saving in temp file is fialed then send error information mail
                // by checking the temp url is exists or not and also any temp excpetion not found case then send error mail
                // if the url not generated then send error mail
                // this code is added by ajay on 05/12/2019
                if (string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.strSavedOriginalNotesLocalPathUrl) && isEasyFormTempSavingSuccessFull)
                    new SendErrorMailOnBinarySavingInTempPathFailedBF().SendErrorMailBczEasyFormBinarySavingInTempPathFailed(htmltemplatesavinginputmodel, new Exception("Easy Form Temp Url Not Created"));
                timer.Stop();
                TimeSpan t7 = timer.Elapsed;
                eachAction = new EasyFormSaveActionWiseTimeInfo();
                eachAction.ActionName = "SAVE / UPDATE EASY FORM";
                eachAction.Duration = t7.ToString();
                AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                #endregion

                #region"               GROUP THERAPHY - SAVE SESSION ATTENDEE NOTES FOR MULTIPLE ATTENDESS       "
                timer.Start();
                //THIS REGION IS USED TO SAVE GROUP THERAPHY INFORMATION
                if (htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0 && model != null && model.RequestExecutionStatus == 0 && htmltemplatesavinginputmodel.GroupTherapySessionType != 2)
                {
                    //Assigin Curretly Saved Patient Data
                    //if LinkNotestoOtherAttendees == true then this id is Attendee Notes ID
                    //if LinkSessionNotesAttendees == true then this id is Session Notes ID, So we are considering this as Parent ID
                    htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataIDCurrentlySaved = curPatientDataID;

                    if (htmltemplatesavinginputmodel.LinkNotestoOtherAttendees == true || htmltemplatesavinginputmodel.LinkSessionNotesAttendees == true)
                    {
                        GTMultipleAttendeesResponseModel = new SaveGTSessionAttendeeNotesLinkingForMultipleAttendeesBF().
                                                                SaveGroupTheraphySessionAttendeeNotesForMultipleAttendess(htmltemplatesavinginputmodel, ref strMulitpleAtandeeIDs, ref dtGroupTheraphySessionAttendeeNotes);

                        //IF MODEL EXCUTION STATUS IS 0 I.E., SUCCESS THEN ASSIGNING MULTIPLE ATANDEE IDS TO MODEL TO PASS THEM TO FIELDED SAVING EXE.
                        if (GTMultipleAttendeesResponseModel != null && GTMultipleAttendeesResponseModel.RequestExecutionStatus == 0)
                        {
                            if (strMulitpleAtandeeIDs != null && strMulitpleAtandeeIDs.Trim().Length > 0)
                            {
                                htmltemplatesavinginputmodel.strMultipleAtandeeIDs = strMulitpleAtandeeIDs;
                                objEasyFormSaveResponseModel.strMultipleAtandeeIDs = strMulitpleAtandeeIDs;
                            }

                            if (!string.IsNullOrWhiteSpace(GTMultipleAttendeesResponseModel.MultipleResponseID))
                            {
                                GTMultipleAttendeesResponseModel.MultipleResponseID += "," + model.ResponseID.ToString();
                                htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataIDs = GTMultipleAttendeesResponseModel.MultipleResponseID;
                                objEasyFormSaveResponseModel.InPatGroupTherapyMultipleResponseID = GTMultipleAttendeesResponseModel.MultipleResponseID;
                            }
                        }
                    }
                }
                timer.Stop();
                TimeSpan t8 = timer.Elapsed;
                eachAction = new EasyFormSaveActionWiseTimeInfo();
                eachAction.ActionName = "GROUP THERAPHY - SAVE SESSION ATTENDEE NOTES FOR MULTIPLE ATTENDESS";
                eachAction.Duration = t8.ToString();
                AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                #endregion


                #region "               UPDATE FORMS TO FILL STATUS WHEN PORTAL USER FINALIZED          "
                //.....CHECKING WHETHER EASYFORM IS OPENED FROM PPORTAL OR WEB
                //.....IF EASY FORM IS OPENED FROM PORTAL THEN CALLING THE DA METHOD
                //.....WHICH IS USED TO UPDATE THE STATUS OF A EASY FORM 
                //.....AFTER CLICKING AS A FINALIZE OR SAVE OR UPDATE 
                //.....TO PERFORM THE UPDATION ACTION ACCURATELY AND FASTLY WE ARE CALLING THE UPDATE STATUS METHOD
                //.....APPLICATION TYPE 2 MEANS THE ACTION WILL BE DONE ONLY IN PORTAL
                //CREATING THE INSTANCE FOR DATA ACCESS CLASS 
                //CALLING THE DA METHOD USING DA CLASS INSTANCE  
                if (htmltemplatesavinginputmodel.EasyFormsPortalUploadedFormInfoID > 0 && htmltemplatesavinginputmodel.ApplicationType == 2)
                    new UpdateFormsToFillStatusFromPortalSaveDataAccess().UpdateFormsToFillStatusFromPortalSave(htmltemplatesavinginputmodel, objEasyFormDetails);
                #endregion

                #region "               UPDATE PROCEDURES WITH EASY FORM ID             "

                if (!htmltemplatesavinginputmodel.isEmergencyWeb && model?.RequestExecutionStatus == 0
                    && htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0
                    && htmltemplatesavinginputmodel.proceduresinfomodel?.ProcedureGivenInfoID > 0)
                {
                    objEasyFormSaveResponseModel.ProcedureGivenInfoID = htmltemplatesavinginputmodel.proceduresinfomodel.ProcedureGivenInfoID;
                    htmltemplatesavinginputmodel.proceduresinfomodel.DocumentsFillableHTMLTemplatesPatientDataID = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID;
                    //htmltemplatesavinginputmodel.proceduresinfomodel.practicemodel = htmltemplatesavinginputmodel.practicemodel;
                    new PracticeInformationCopy().Copy(htmltemplatesavinginputmodel.proceduresinfomodel, htmltemplatesavinginputmodel);
                    new UpdateNotesLinkedProcedureInfoOnPtDataIdDA().UpdatePatientDataIDToProcedureGivenInfoID(htmltemplatesavinginputmodel.proceduresinfomodel);
                }

                #endregion


                #region "               SEND SMS / EMAIL TO USERS IF FORM FINALIZED IN PORTAL              "              
                // CALLING METHOD TO SEND MESSAGE IF FORM WAS FILLED IN PORTAL
                // WE CALL THE METHOD ONLY IF FORM WAS SAVE FROM PORTAL BASED ON APPLICATON TYPE. 
                // APPLICATION TYPE - 2 INDIACTES SAVED FROM PORTAL
                if (htmltemplatesavinginputmodel.ApplicationType == 2 && htmltemplatesavinginputmodel.IsSignedOff == true
                    && model != null && model.RequestExecutionStatus == 0)
                {
                    timer.Start();
                    new SendMsgOrEmailTousersWhenFormFinalizedInPortalBF().HTMLTemplatesSendMessagesIfFormFilledInPortal(htmltemplatesavinginputmodel);
                    timer.Stop();
                    TimeSpan t9 = timer.Elapsed;
                    eachAction = new EasyFormSaveActionWiseTimeInfo();
                    eachAction.ActionName = "SEND SMS / EMAIL TO USERS IF FORM FINALIZED IN PORTAL";
                    eachAction.Duration = t9.ToString();
                    AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                }


                #endregion

                #region "               UPLOAD EASY FORM PATIENT BINARY TO GOOGLE CLOUD          "

                // CHECKING EASYFORM BINARY TEMP FILE CREATED OR NOT IF NOT UPLOADING TO GOOGLE CLOUD
                if (!isEasyFormTempSavingSuccessFull)
                {
                    timer.Start();
                    new UploadEasyFormNotesBinarytoGcloudBF().htmltemplateUploadEasyFormSavedNotesBinarytoGcloud(htmltemplatesavinginputmodel, saveFormattedDataOnly);
                    timer.Stop();
                    TimeSpan ts10 = timer.Elapsed;
                    eachAction = new EasyFormSaveActionWiseTimeInfo();
                    eachAction.ActionName = "UPLOAD EASY FORM PATIENT BINARY TO GOOGLE CLOUD";
                    eachAction.Duration = ts10.ToString();
                    AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                }
                #endregion

                #region "               LETTERE TEMPLATE                "

                // if it is saving  from Reminder Dues Letter then we send Email after save easy form
                if (htmltemplatesavinginputmodel.CallingFromLetterTemplate && htmltemplatesavinginputmodel.LetterTemplateInfo != null)
                {
                    timer.Start();
                    new SendEmailBasedOnLetterTemplateTypeBF().ExecuteLetterTemplateDetails(htmltemplatesavinginputmodel);
                    timer.Stop();
                    TimeSpan ts11 = timer.Elapsed;
                    eachAction = new EasyFormSaveActionWiseTimeInfo();
                    eachAction.ActionName = "LETTER TEMPLATE";
                    eachAction.Duration = ts11.ToString();
                    AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                }


                #endregion

                #region "               EASY FORM FIELDED SAVING - SYNC (OFFLINE)            "

                if (htmltemplatesavinginputmodel.easyformIsOfflineSync)
                    new RunEasyFormFieldedSavingExeBF().RunEasyFormFieldedSavingExe(htmltemplatesavinginputmodel);
                #endregion

                #region "               EASY FORM FIELDED SAVING - ASYNC            "

                /*here we are checking if the IsNoNeedtoRunFieldedSaving  is  or false,if it is false  then only we are calling fielded saving asynchronously */
                // EASY FORM SYNC CALLED FROM WINFORMS FORMAT EHR OFFLINE APPLICATION
                // WE NEED TO CALL PNFS SYNCHORONOUSLY, BECAUSE OF NET FAILUERE WE ARE CALLING IMMDEIATLY 
                if (!htmltemplatesavinginputmodel.IsNoNeedtoRunFieldedSaving)
                {

                    if ((htmltemplatesavinginputmodel.IsEpisdeAutoCreationTemplate && (htmltemplatesavinginputmodel.ProgramServicesLinkedInfoID > 0 || htmltemplatesavinginputmodel.ProgramServicesLinkedOnlyProgramID > 0))
                        || htmltemplatesavinginputmodel.winFormsFormatEasyFormOfflineSync)
                        htmltemplatesavinginputmodel.CallFieldSavingSynchronously = true;

                    new RunEasyFormFieldedSavingExeAsynBF().CallEasyFormFieldedSavingAsyn(htmltemplatesavinginputmodel, objEasyFormSaveResponseModel, model);

                }
                #endregion

                #region "               UPDATE APPOINTMENT STATUS BASED ON EASYFORM SAVE ACTION           "

                if (htmltemplatesavinginputmodel.ApplicationType != 2 && htmltemplatesavinginputmodel.IsAppointmentStatusAutoUpdateStatusExists)
                {
                    timer.Start();
                    if ((!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PatientIDs) && htmltemplatesavinginputmodel.GroupTherapySessionType == 2 && htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0) || (htmltemplatesavinginputmodel.patientchartmodel != null && htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0))
                        new ChangeApptStatusOnEasyFormSaveStatusBF().changeApptStatusBasedOnEasyFormStatus(htmltemplatesavinginputmodel);
                    timer.Stop();
                    TimeSpan t12 = timer.Elapsed;
                    eachAction = new EasyFormSaveActionWiseTimeInfo();
                    eachAction.ActionName = "UPDATE APPOINTMENT STATUS BASED ON EASYFORM SAVE ACTION";
                    eachAction.Duration = t12.ToString();
                    AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                }
                #endregion

                #region "               ASSIGNING DATA TO RETRUN SAVED NOTES RESPONSE MODEL                "
                if (htmltemplatesavinginputmodel.sendFormattedDataBack == true)
                {
                    //ASSIGING ORIGINAL DATA & FORMATTED DATA TO RESPONSE MODEL TO DISPLAY IN SAVED NOTES
                    objEasyFormSaveResponseModel.EasyFormSavedNoteDetails = new EasyFormSavedDataDetailsModel();
                    objEasyFormSaveResponseModel.EasyFormSavedNoteDetails.DocumentsFillableHTMLTemplatesPatientDataID = curPatientDataID;
                    objEasyFormSaveResponseModel.EasyFormSavedNoteDetails.EasyFormPatientDataFormattedBase64 = htmltemplatesavinginputmodel.EasyFormSavedNotesFormattedBinaryFormatInBase64;
                }
                #endregion

                #region "               PatientDataID AuthEndDate_Gooden_ End Date saving              "
                if (curPatientDataID > 0 && htmltemplatesavinginputmodel.flagtoknowauthrizationcust)
                {
                    timer.Start();
                    TempModel = new PatientDataIDAuthEndDateGoodenInsertOrUpdate().PatientDataIDAuthEndDateGoodenInsertOrUpdateBF(htmltemplatesavinginputmodel, curPatientDataID);
                    if (TempModel != null && TempModel.RequestExecutionStatus < 0)
                    {
                        return TempModel;
                    }
                    timer.Stop();
                    TimeSpan t13 = timer.Elapsed;
                    eachAction = new EasyFormSaveActionWiseTimeInfo();
                    eachAction.ActionName = "PatientDataID AuthEndDate_Gooden_ End Date saving";
                    eachAction.Duration = t13.ToString();
                    AppendtoSaveActionWiseTimeInfoList(EasyFormSaveActionWiseTimeInfoList, eachAction);
                }
                #endregion

                #region"   Save action wise time Info Async"
                if (EasyFormSaveActionWiseTimeInfoList?.Count > 0)
                {

                    ClsInputforPostServiceModel clsinputforpostservicemodel = new ClsInputforPostServiceModel();
                    clsinputforpostservicemodel.requestURL = HttpContext.Current.Request.Url.Scheme + @"://" + HttpContext.Current.Request.Url.Host + "/EHR_EasyFormSaveV2_API/EasyFormSave/SaveEasyFromActionWiseTimeInfo";
                    var InputModel = new
                    {
                        PracticeID = htmltemplatesavinginputmodel.PracticeID,
                        LoggedUserID = htmltemplatesavinginputmodel.LoggedUserID,
                        DBServerName = htmltemplatesavinginputmodel.DBServerName,
                        DocumentsFillableHTMLTemplatesPatientDataID = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID,
                        EasyFormSaveActionWiseTimeInfoList = EasyFormSaveActionWiseTimeInfoList
                    };
                    clsinputforpostservicemodel.strJsonstring = JsonConvert.SerializeObject(InputModel);
                    new Cls_RestApiCalling().CallPostServiceWithNoResponse(clsinputforpostservicemodel);
                }
                #endregion
            }
            finally
            {
                #region "               DISPOSING UNUSED OBJECTS                "
                //Send Erromail information 
                if (!string.IsNullOrWhiteSpace(strerrormsg))
                    //new EMRWebExceptionTraceLogModel().SendInformationMailToEMR(htmltemplatesavinginputmodel, strerrormsg);
                    new EFCommonMailSendingBF().EFSendInformationMailToEMR(htmltemplatesavinginputmodel, strerrormsg, "EasyFormNotesSaveOrUpdateBF", "SaveOrUpdateNotes");
                GTMultipleAttendeesResponseModel = null;
                originalTemplate = string.Empty;
                #endregion
            }
            return model;
        }


        private void AppendtoSaveActionWiseTimeInfoList(List<EasyFormSaveActionWiseTimeInfo> EasyFormSaveActionWiseTimeInfoList, EasyFormSaveActionWiseTimeInfo eachAction)
        {
            if (EasyFormSaveActionWiseTimeInfoList == null)
            {
                EasyFormSaveActionWiseTimeInfoList = new List<EasyFormSaveActionWiseTimeInfo>();
            }
            EasyFormSaveActionWiseTimeInfoList.Add(eachAction);
        }

        private bool IsTemplateBinaryHavingBody(string htmlstring)
        {
            bool isTemplateBinaryhavingBody = false;
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlstring);
            HtmlNode bodynode = htmlDoc.DocumentNode.SelectSingleNode("//body");
            if (bodynode != null)
            {
                isTemplateBinaryhavingBody = true;
            }
            else
            {
                isTemplateBinaryhavingBody = false;
            }
            return isTemplateBinaryhavingBody;

        }

        #endregion


    }

    #region "        SEND EXCEPTION MAIL     "
    internal class SendExpMail
    {
        #region"        SEND EXCEPTION MAIL "
        internal void SendExceptionmail(Exception ex, string MethodName, HtmlTemplateSavingInputModel inputInfo)
        {

            MailInputModel mailingModel = new MailInputModel();
            StringBuilder bodyBuilder = new StringBuilder(100);

            mailingModel.MailSubject = "EMR Exception - From Save Or Update HTML Template";
            bodyBuilder.AppendLine("<table border='1' style='border-collapse:collapse' >");
            bodyBuilder.AppendLine("<tr><th style='text-align:left'>API Name:</th> <td> EHR_EasyFormSaveV2_API </td><tr>");
            bodyBuilder.AppendLine($"<tr><th style='text-align:left'>Method Name:</th> <td> {MethodName} </td><tr>");
            bodyBuilder.AppendLine($"<tr><th style='text-align:left'>SystemName:</th> <td>{Environment.MachineName}</td><tr>");
            bodyBuilder.AppendLine($"<tr><th style='text-align:left'>Input Json:</th> <td>{JsonConvert.SerializeObject(inputInfo)}</td><tr>");
            if (ex != null)
            {
                bodyBuilder.AppendLine($"<tr><th style='text-align:left'>Exception :</th> <td>{ex.Message}</td><tr>");
                bodyBuilder.AppendLine($"<tr><th style='text-align:left'>Stack Trace :</th> <td>{ex.StackTrace}</td><tr>");
            }
            bodyBuilder.AppendLine("</table>");
            mailingModel.MailBody = bodyBuilder.ToString();
            mailingModel.FromMail = new MailAddress() { Address = "emrdevelopmentmonitoring@adaptamed.org" };
            mailingModel.ToMails = new List<MailAddress>();
            mailingModel.ToMails.Add(new MailAddress() { Address = "ehrerrors@adaptamed.org" });
            mailingModel.ToMails.Add(new MailAddress() { Address = "uday.veeramachaneni@ehryourway.com" });
            mailingModel.ToMails.Add(new MailAddress() { Address = "ramakrishna.m@ehryourway.com" });
            mailingModel.ToMails.Add(new MailAddress() { Address = "PavanKumar.Bharide@ehryourway.com" });
            mailingModel.ToMails.Add(new MailAddress() { Address = "ravi.dasoju@ehryourway.com" });
            mailingModel.BodyTextFormate = EmailBodyType.HtmlBody;


            // send mail method
            new EHRSendMail().SendMail(mailingModel);
        }
        #endregion

    }
    #endregion

    internal class RunEasyFormFieldedSavingExeBF
    {
        #region         "      RUN FIELDED SAVING EXE         "

        public ResponseModel RunEasyFormFieldedSavingExe(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            ResponseModel model = null;  //Used to maintain the User Data.


            // CHECKING WHERTHER THERE IS PRACTICE MODEL OR NOT , IF THERE IS NO PRACTICE MODEL THEN RETURNING THE FUNCTIONALITY 
            if (htmltemplatesavinginputmodel == null) return model;

            if (htmltemplatesavinginputmodel.isEmergencyWeb) return model;

            // FOLLWOING BLOCK OF CODE IS ADDED BY AJAY ON 15-05-2019
            #region             "  UPLOAD THE PENDING SAVED NOTES TEMP URLS TO GOOGLE WITH IN THE PRACTICE"

            // CHECKING IS TO UPLOAD PENDING TEMP URLS TO GOOGLE
            if (htmltemplatesavinginputmodel.isToUploadsavedNotesTempUrlsToGoogle == true)
                // MAKE A CALL TO GET THE PENDING SAVED TEMP URLS BASED ON THE PRACTICE ID TO UPLOAD GOOGLE
                new GetPendingNotesTempUrlsAndUplodToGcloudWithInThePracticeBF().GetPendingTempUrlsAndUploadToGoogleWithInThePractice(htmltemplatesavinginputmodel);

            #endregion

            //  FOR UPLOAD TO PORTAL FOR COSIGN WE HAVE TO PASS PATIENT DATA ID AND 
            //  SOME OTHER INFO TO TELL RUN SOME SPECIFIC METHODS 
            if (htmltemplatesavinginputmodel.winFormsFormatEasyFormOfflineSync == true)
            {
                htmltemplatesavinginputmodel.FieldedSavingExeRunSpecificActions = true;
                htmltemplatesavinginputmodel.PatientDataIDForRunningSpecificActions = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID;
            }

            //STARTING FIELDED SAVING EXE
            new EasyFormsInitializeFieldedSavingBusinessFacade().EMREasyFormFieldedSaving(htmltemplatesavinginputmodel);

            return model;
        }

        #endregion
    }

    internal class RunEasyFormFieldedSavingExeAsynBF
    {
        #region "               CALL EASYFORM FIELDED SAVING                "
        /// <summary>
        /// 
        /// </summary>
        /// <param name="htmltemplatesavinginputmodel"></param>
        /// <param name="objEasyFormSaveResponseModel"></param>
        /// <param name="model"></param>
        public void CallEasyFormFieldedSavingAsyn(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, EasyFormSaveResponseModel objEasyFormSaveResponseModel, ResponseModel model)
        {

            #region "                   ADDING EXTRA INPUT PARAMS                       "

            if (objEasyFormSaveResponseModel != null)
            {
                htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime = objEasyFormSaveResponseModel.AppointmentDateTime;
                htmltemplatesavinginputmodel.strMultipleAtandeeIDs = objEasyFormSaveResponseModel.strMultipleAtandeeIDs;
                htmltemplatesavinginputmodel.IsEasyFormGoldenThreadNeedsDxCodesSavingRequired = objEasyFormSaveResponseModel.IsEasyFormGoldenThreadNeedsDxCodesSavingRequired;
                htmltemplatesavinginputmodel.IsEasyFormOtherOrdersModuleRequired = objEasyFormSaveResponseModel.IsEasyFormOtherOrdersModuleRequired;
                htmltemplatesavinginputmodel.EasyFormReminderDueUserID = objEasyFormSaveResponseModel.EasyFormReminderDueUserID;
            }

            if (model != null && model.ResponseID > 0)
                htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID = model.ResponseID;

            #endregion

            if (htmltemplatesavinginputmodel.CallFieldSavingSynchronously)
                //===================== FIELD SAVING SYNCHRONOUSLY BLOCK START ====================
                new RunEasyFormFieldedSavingExeBF().RunEasyFormFieldedSavingExe(htmltemplatesavinginputmodel);
            //===================== FIELD SAVING SYNCHRONOUSLY BLOCK END ====================
            else
            {
                //===================== FIELD SAVING ASYNCHRONOUSLY BLOCK START ====================
                ClsInputforPostServiceModel clsinputforpostservicemodel = new ClsInputforPostServiceModel();
                clsinputforpostservicemodel.requestURL = HttpContext.Current.Request.Url.Scheme + @"://" + HttpContext.Current.Request.Url.Host + "/EHR_EasyFormSaveV2_API/EasyFormSave/ExecuteEasyFormFieldedSavingExe";
                clsinputforpostservicemodel.strJsonstring = JsonConvert.SerializeObject(htmltemplatesavinginputmodel);
                //new CallPostServiceWithModelInputBF().CallPostServiceWithModelInput(clsinputforpostservicemodel);
                new Cls_RestApiCalling().CallPostServiceWithNoResponse(clsinputforpostservicemodel);
                //new Cls_RestApiCalling().CallPostService(clsinputforpostservicemodel.requestURL, clsinputforpostservicemodel.strJsonstring);
                //===================== FIELD SAVING ASYNCHRONOUSLY BLOCK END ====================
            }

        }
        #endregion
    }

    internal class GetPendingNotesTempUrlsAndUplodToGcloudWithInThePracticeBF
    {
        // declaring the class level varible to hold the data access calss instance
        private GetPendingNotesTempUrlsToUploadToGcloudDA _objGetUDTForPendingNotesTempURLsBF;
        private UploadEasyFormNotesBinarytoGcloudBF _objCmnGcloudUploadBF;

        #region  "      Constructor Function To Create the Instance for the BF Class     "

        public GetPendingNotesTempUrlsAndUplodToGcloudWithInThePracticeBF()
        {
            _objGetUDTForPendingNotesTempURLsBF = new GetPendingNotesTempUrlsToUploadToGcloudDA();
            _objCmnGcloudUploadBF = new UploadEasyFormNotesBinarytoGcloudBF();
        }

        #endregion

        #region "        GET PENDING TEMP URLS AND UPLOAD TO GOOGLE WITH IN THE PRACTICE     "
        /// <summary>
        /// THIS IS USED TO GET PATIENT NOTES BINARY FROM PENDING TEMP URLS AND UPLOAD TO GOOGLE WITH IN THE PRACTICE
        /// ADDED BY AJAY NANDURI ON 15-05-2019 
        /// </summary>
        /// <param name=""></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>
        public ResponseModel GetPendingTempUrlsAndUploadToGoogleWithInThePractice(HtmlTemplateSavingInputModel htmlTemplateSavingInputModel)
        {
            DataTable dtPendingTempUrls = null; //DATATABLE TO HOLD PENDING Temp URLS INFO        
            int DocumentsFillableHTMLTemplatesPatientDataID = 0;//hold Easy Form Patient DAta ID
            string EasyFormTemplateName = string.Empty;
            StringBuilder Sb_strTraceLog = null;
            ResponseModel responseModel = null;
            string strPendingUrlsUploadToGoogleExceptionMessage = string.Empty;
            string errorString = string.Empty;
            HtmlTemplateSavingInputModel ObjLocalInputsData = null;


            // MAKE A CALL TO GET THE PENDING SAVED TEMP URLS BASED ON THE PRACTICE ID TO UPLOAD GOOGLE
            //GETTTING PENDING TEMP URLS FROM DATA BASE ( Both Original  & Formatted Temp URls )
            dtPendingTempUrls = _objGetUDTForPendingNotesTempURLsBF.GetPendingSavedNotesTempUrlsToUploadToGoogle(htmlTemplateSavingInputModel);

            // Checking any pending temp urls records exists or not
            if (dtPendingTempUrls != null && dtPendingTempUrls.Rows.Count > 0)
            {
                Sb_strTraceLog = new StringBuilder(500);

                //LOOPING THROUGH EACH PEDNING ROW TO ASSIGN IT TO MODEL AND UPLOADING TO GOOGLE
                foreach (DataRow dtRow in dtPendingTempUrls.Rows)
                {
                    //CLEARING TEMP VARIABLES
                    DocumentsFillableHTMLTemplatesPatientDataID = 0;
                    EasyFormTemplateName = string.Empty;

                    if (ObjLocalInputsData != null)
                    {
                        ObjLocalInputsData = null;
                    }

                    if (dtRow["Documents_Fillable_HTML_Templates_PatientDataID"] != DBNull.Value)
                        DocumentsFillableHTMLTemplatesPatientDataID = Convert.ToInt32(dtRow["Documents_Fillable_HTML_Templates_PatientDataID"]);

                    //ASSIGN TEMPLATE NAME
                    if (dtRow["Fillable_HTML_DocumentTemplateName"] != null && dtRow["Fillable_HTML_DocumentTemplateName"] != DBNull.Value)
                        EasyFormTemplateName = dtRow["Fillable_HTML_DocumentTemplateName"].ToString();

                    //IF EASY FORM PATIENT DATA ID NOT FOUND THEN EXIT
                    if (DocumentsFillableHTMLTemplatesPatientDataID <= 0)
                        continue;

                    ObjLocalInputsData = new HtmlTemplateSavingInputModel();
                    ObjLocalInputsData.DocumentsFillableHTMLTemplatesPatientDataID = DocumentsFillableHTMLTemplatesPatientDataID;
                    ObjLocalInputsData.EasyFormTemplateName = EasyFormTemplateName;
                    // ObjLocalInputsData.practicemodel = htmlTemplateSavingInputModel.practicemodel;
                    new PracticeInformationCopy().Copy(ObjLocalInputsData, htmlTemplateSavingInputModel);

                    // FOLLOWING BLOCK OF CODE IS TO UPLOAD ORIGINAL NOTES TEMP URL TOTHE GOOGLE
                    #region     "               UPLOAD ORIGINAL NOTES TEMP URL TO THE GOOGLE               "

                    // CHECKING THE SAVED ORIGINAL NOTES GOOGLE UPLOAD BINARY URL IS EXISTS OR NOT AND ALSO CHECK SAVED ORIGINAL NOTES TEMP URL IS EXISTS OR NOT
                    //IF SAVED ORIGINAL NOTES GOOGLE UPLOAD BINARY URL IS NOT EXISTS THEN IF ORIGINAL NOTES TEMP URL IS EXIST THEN UPLOAD TO GOOGLE
                    if (dtRow["EasyForms_GCloudStorage_PatientData_OriginalSignedBodyURL"] == DBNull.Value &&
                        dtRow["LocalTempOriginalURL"] != DBNull.Value)
                    {
                        //ASSIGNING THE ORIGINAL Temp URL
                        //htmlTemplateSavingInputModel.strSavedOriginalNotesLocalPathUrl = dtRow["LocalTempOriginalURL"].ToString();
                        ObjLocalInputsData.strSavedOriginalNotesLocalPathUrl = dtRow["LocalTempOriginalURL"].ToString();

                        try
                        {

                            //Clearing Trace Log
                            Sb_strTraceLog.Clear();
                            strPendingUrlsUploadToGoogleExceptionMessage = string.Empty;

                            Sb_strTraceLog.AppendLine("Uploading <b>Easy Form Original Temp URL to GCloud</b> START <br/>");
                            Sb_strTraceLog.AppendLine("Easy Form Patient Data ID <b>" + DocumentsFillableHTMLTemplatesPatientDataID + "</b> <br/>");

                            // checking the is original url is exists or not
                            if (!string.IsNullOrWhiteSpace(ObjLocalInputsData.strSavedOriginalNotesLocalPathUrl))
                            {
                                Sb_strTraceLog.AppendLine("Easy Form Original Temp URL : <br /> <b>" + ObjLocalInputsData.strSavedOriginalNotesLocalPathUrl + "</b> <br/>");

                                // CREATING A OBJECT FOR THE WEB CLIENT CLASS TO GET SAVED ORIGINAL NOTES CONTENT TO UPLOAD TO GOOGLE
                                var webClient = new WebClient();

                                Sb_strTraceLog.AppendLine("Browsing URL using WebClient START");
                                // GETTING THE ORGINGAL NOTES IN BINARY FORMAT USING THE WEBCLIENT OBJECT
                                ObjLocalInputsData.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat = webClient.DownloadData(ObjLocalInputsData.strSavedOriginalNotesLocalPathUrl);

                                Sb_strTraceLog.AppendLine("Browsing URL using WebClient END");

                                // CHECKING ORIGINAL NOTES BINARY EXIST OR NOT
                                if (ObjLocalInputsData.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat != null)
                                {
                                    //MAKE A CALL TO UPLOAD NOTES BINARY TO THE GOOGLE
                                    Sb_strTraceLog.AppendLine("Uploading Easy Form Original Info to GCloud Start");
                                    _objCmnGcloudUploadBF.htmltemplateUploadEasyFormSavedNotesBinarytoGcloud(ObjLocalInputsData, false);
                                    Sb_strTraceLog.AppendLine("Uploading Easy Form Original Info to GCloud END");
                                }
                                else
                                {
                                    errorString = "Empty Data is getting After Browsing Original Easy Form URL using WEBCLIENT";
                                    throw new Exception(errorString);
                                }
                            }
                            else
                            {
                                Sb_strTraceLog.AppendLine("<span style='font-weight:bold; background-color: #f7e2ca;'> Easy Form Original Temp URL is Empty </span>");
                                throw new Exception("Easy Form Temp URL (Original) was Empty");
                            }

                        }
                        catch (Exception ex)
                        {
                            strPendingUrlsUploadToGoogleExceptionMessage += "<b>" + ex.Message + "</b><br/><br/>";
                            strPendingUrlsUploadToGoogleExceptionMessage += "Trace Log <br/>";
                            strPendingUrlsUploadToGoogleExceptionMessage += "================<br/>";
                            strPendingUrlsUploadToGoogleExceptionMessage += Sb_strTraceLog.ToString() + "<br/>";

                            // SENDING THE ERROR MAIL 
                            if (strPendingUrlsUploadToGoogleExceptionMessage != null && strPendingUrlsUploadToGoogleExceptionMessage.Trim().Length > 0)
                            {

                                new EFCommonMailSendingBF().EFSendInformationMailToEMR(ObjLocalInputsData, strPendingUrlsUploadToGoogleExceptionMessage.ToString(), "GetPendingNotesTempUrlsAndUplodToGcloudWithInThePracticeBF", "GetPendingTempUrlsAndUploadToGoogleWithInThePractice");


                            }
                        }
                    }

                    #endregion

                    // FOLLOWING BLOCK OF CODE IS TO UPLOAD FORMATED NOTES TEMP URL TOTHE GOOGLE

                    #region     "               UPLOAD FORMATTED NOTES TEMP URL TO THE GOOGLE               "
                    // CHECKING THE SAVED FORMATTED NOTES GOOGLE UPLOAD BINARY URL IS EXISTS OR NOT AND ALSO CHECK SAVED FORMATED NOTES TEMP URL IS EXISTS OR NOT
                    //IF SAVED FORMATTED NOTES GOOGLE UPLOAD BINARY URL IS NOT EXISTS THEN IF FORMATTED NOTES TEMP URL IS EXIST THEN UPLOAD TO GOOGLE
                    if (dtRow["EasyForms_GCloudStorage_PatientData_FormattedSignedBodyURL"] == DBNull.Value && dtRow["LocalTempFormattedURL"] != DBNull.Value)
                    {
                        //ASSIGNING THE FORMATTED URL
                        ObjLocalInputsData.strSavedFormattedNotesLocalPathUrl = dtRow["LocalTempFormattedURL"].ToString();

                        try
                        {
                            //Clearing Trace Log
                            Sb_strTraceLog.Clear();
                            strPendingUrlsUploadToGoogleExceptionMessage = string.Empty;
                            Sb_strTraceLog.AppendLine("Uploading Pending temp urls with in the Practice</b> START <br/>");
                            Sb_strTraceLog.AppendLine("Uploading <b>Easy Form Formatted Temp URL to GCloud</b> START <br/>");
                            Sb_strTraceLog.AppendLine("PatientDataID <b>" + DocumentsFillableHTMLTemplatesPatientDataID + "</b> <br/>");

                            // checking the is formated url is exists or not
                            if (!string.IsNullOrWhiteSpace(ObjLocalInputsData.strSavedFormattedNotesLocalPathUrl))
                            {
                                Sb_strTraceLog.AppendLine("URL : <b>" + ObjLocalInputsData.strSavedFormattedNotesLocalPathUrl + "</b> <br/>");

                                // CREATING A OBJECT FOR THE WEB CLIENT CLASS TO GET SAVED FORMATTED NOTES CONTENT TO UPLOAD TO GOOGLE
                                var webClient = new WebClient();

                                Sb_strTraceLog.AppendLine("Browsing URL using WebClient START");
                                // GETTING THE FORMATTED NOTES IN BINARY FORMAT USING THE WEBCLIENT OBJECT
                                ObjLocalInputsData.DocumentsFillableHTMLTemplatesPatientDataNotesFormattedBinaryFormat = webClient.DownloadData(ObjLocalInputsData.strSavedFormattedNotesLocalPathUrl);
                                Sb_strTraceLog.AppendLine("Browsing URL using WebClient END");

                                // CHECKING FORMATTED NOTES BINARY EXIST OR NOT
                                if (ObjLocalInputsData.DocumentsFillableHTMLTemplatesPatientDataNotesFormattedBinaryFormat != null)
                                {
                                    //MAKE A CALL TO UPLOAD NOTES BINARY TO THE GOOGLE
                                    Sb_strTraceLog.AppendLine("Uploading Easy Form Formatted Info to GCloud Start");
                                    _objCmnGcloudUploadBF.htmltemplateUploadEasyFormSavedNotesBinarytoGcloud(ObjLocalInputsData, true);
                                    Sb_strTraceLog.AppendLine("Uploading Easy Form Formatted Info to GCloud END");
                                }
                                else
                                {
                                    errorString = "Empty Data is getting After Browsing Foramted Easy Form URL using WEBCLIENT";
                                    throw new Exception(errorString);
                                }
                            }
                            else
                            {
                                Sb_strTraceLog.AppendLine("<span style='font-weight:bold; background-color: #f7e2ca;'> Easy Form Formatted Temp URL is Empty </span>");
                                throw new Exception("Easy Form Temp URL (Formatted) was Empty");
                            }
                        }
                        catch (Exception ex)
                        {
                            strPendingUrlsUploadToGoogleExceptionMessage += "<b>" + ex.Message + "</b><br/><br/>";
                            strPendingUrlsUploadToGoogleExceptionMessage += "Trace Log <br/>";
                            strPendingUrlsUploadToGoogleExceptionMessage += "================<br/>";
                            strPendingUrlsUploadToGoogleExceptionMessage += Sb_strTraceLog.ToString() + "<br/>";

                            // SENDING THE ERROR MAIL 
                            if (strPendingUrlsUploadToGoogleExceptionMessage != null && strPendingUrlsUploadToGoogleExceptionMessage.Trim().Length > 0)
                            {

                                new EFCommonMailSendingBF().EFSendInformationMailToEMR(ObjLocalInputsData, strPendingUrlsUploadToGoogleExceptionMessage.ToString(), "GetPendingNotesTempUrlsAndUplodToGcloudWithInThePracticeBF", "GetPendingTempUrlsAndUploadToGoogleWithInThePractice");
                                // obj.SendInformationMailToEMR(ObjLocalInputsData, strPendingUrlsUploadToGoogleExceptionMessage.ToString());

                            }
                        }
                    }
                    #endregion
                }
            }

            return responseModel;
        }
        #endregion
    }

    internal class PatientDataIDAuthEndDateGoodenInsertOrUpdate
    {
        public void AssignADMDetailsData(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, DataTable dtFieldsValues)
        {
            #region"                                    TRY BLOCK                                      "

            //CREATING OBJECT FOR PAST HISTORY MODEL
            htmltemplatesavinginputmodel.admissiondetailsipmodel = new cls_AdmissiondetailsIPModel();

            //ASSINGNING PATIENT ID TO PAST HISTORY MODEL
            htmltemplatesavinginputmodel.admissiondetailsipmodel.PatienID = htmltemplatesavinginputmodel.patientchartmodel.PatientID;
            htmltemplatesavinginputmodel.admissiondetailsipmodel.AuthEndDate = htmltemplatesavinginputmodel.AuthEndDate;

            //CHECKING DATA TABLE FOR THE STATIC HEALTH CARE ITEM ID
            foreach (DataRow drnew in dtFieldsValues.Rows)
            {
                switch (Convert.ToInt32(drnew["EasyForms_HealthCareItems_StaticItem_InfoID"]))
                {
                    //STATIC HCI ITEM ID AND PASTHX REVIEWED ENUM ID ARE EQUAL THEN ASSINGING THE LINKED HCI ITEM VALUE
                    case (int)AdmDetails.AuthorizationEndDate_Gooden:

                        if (drnew["EasyForms_HealthCareItems_StaticItem_Value"] != null)//bool
                        {
                            //ASSINGING THE LINKED ITEM VALUE TO PAST HISTORY MODEL
                            htmltemplatesavinginputmodel.admissiondetailsipmodel.AuthEndDate = Convert.ToString(drnew["EasyForms_HealthCareItems_StaticItem_Value"]);
                            htmltemplatesavinginputmodel.flagtoknowauthrizationcust = true;
                        }
                        break;
                }
            }

            #endregion
        }

        public ResponseModel PatientDataIDAuthEndDateGoodenInsertOrUpdateBF(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, int curPatientDataID)
        {
            ResponseModel model = null;

            if (curPatientDataID > 0 && htmltemplatesavinginputmodel.admissiondetailsipmodel.PatienID > 0)
            {
                model = new Cls_PatientDataIDAuthEndDateGoodenInsertOrUpdateDA().PatientDataIDAuthEndDateGoodenInsertOrUpdateDA(htmltemplatesavinginputmodel, curPatientDataID);
            }

            return model;
        }
    }

    internal class EFCommonMailSendingBF
    {

        public ResponseModel EFSendInformationMailToEMR(string mailSubject, string mailBody)
        {

            ResponseModel model = null;

            MailResponseModel mailresponseModel = null;
            MailInputModel mailinputmodel = new MailInputModel();
            mailinputmodel.FromMail = new MailAddress() { Address = "emrdevelopmentmonitoring@adaptamed.org" };
            mailinputmodel.ToMails = new List<MailAddress>();
            mailinputmodel.ToMails.Add(new MailAddress() { Address = "technicalsupport@adaptamed.org" });
            mailinputmodel.MailSubject = mailSubject;
            mailinputmodel.MailBody = mailBody;
            mailinputmodel.BodyTextFormate = EmailBodyType.HtmlBody;
            mailinputmodel.MailCredentials = new MailServerCredentials()
            {
                ServerName = ConfigurationManager.AppSettings.Get("SmarterMailSMTPServernName"),
                UserName = ConfigurationManager.AppSettings.Get("SmarterMailSMTPUserName"),
                Password = ConfigurationManager.AppSettings.Get("SmarterMailSMTPPassword"),
                port = Convert.ToInt32(ConfigurationManager.AppSettings.Get("SmarterMailSMTPPort")),
                useSSL = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("SmarterMailUseSSL"))

            };
            try
            {

                mailresponseModel = new EHRSendMail().SendMail(mailinputmodel);

            }
            catch (Exception)
            {
                if (mailresponseModel != null)
                {
                    model = new ResponseModel();
                    model.ErrorID = -1;
                    model.ErrorMessage = mailresponseModel.ErrorMessage;
                }
            }
            return model;
        }

        #region""
        public void EFSendInformationMailToEMR(BaseModel currentExecutionObject, string strSendInformationToEMR, [Optional] string ClassName, [Optional] string MethodName)
        {
            string Subject;

            currentExecutionObject.RequestExecutionStatus = -1;
            currentExecutionObject.ErrorMessage = strSendInformationToEMR;

            if (currentExecutionObject != null)
            {

                Subject = "EMR WEB WCF Required Information on " + System.Environment.MachineName;


                EFSendInformationMailToEMR(Subject, FrameInformationMail(currentExecutionObject, strSendInformationToEMR, ClassName, MethodName));

            }

        }
        private string FrameInformationMail(BaseModel currentExecutionObject, string strInformationMessage, [Optional] string ClassName, [Optional] string MethodName)
        {
            StringBuilder Sb_body = new StringBuilder(128);
            string clientIPAddress = "";

            clientIPAddress = new Cls_GetClientIPAddress().GetClientIPAddress();


            Sb_body.Append("<html><body>");



            Sb_body.Append("<B>Practice ID     : </B> " + currentExecutionObject.PracticeID + "<br>");
            Sb_body.Append("<B>System Name       : </B> " + System.Environment.MachineName + "<br>");

            Sb_body.Append("<B>Date & Time       : </B> " + DateTime.Now.ToString() + "<br>");
            if (!string.IsNullOrWhiteSpace(ClassName)) Sb_body.Append("<B>Class Name      : </B> " + ClassName + "<br>");
            if (!string.IsNullOrWhiteSpace(MethodName)) Sb_body.Append("<B>Method Name       : </B> " + MethodName + "<br><br>");// 'appending the function name after the form name while forming the error message
            Sb_body.Append("<B>Error Message     : </B> " + strInformationMessage + "<br><br>");
            Sb_body.Append("<B>Client IP     : </B> " + clientIPAddress + "<br><br>");

            if (!string.IsNullOrWhiteSpace(currentExecutionObject.WCFRequestGUID))
                Sb_body.Append("<B>RequestLog GUID   : </B> " + currentExecutionObject.WCFRequestGUID + "<br><br>");


            Sb_body.Append("</body></html>");

            return Sb_body.ToString();
        }

        #endregion
    }

    internal class ChangeApptStatusOnEasyFormSaveStatusBF
    {
        // declaring the class level varible to hold the data access calss instance
        private ChangeApptStatusOnEasyFormSaveStatusDA _objApptStatusDA;

        #region  "      Constructor Function To Create the Instance for the DA Class     "

        public ChangeApptStatusOnEasyFormSaveStatusBF()
        {
            _objApptStatusDA = new ChangeApptStatusOnEasyFormSaveStatusDA();
        }
        #endregion

        #region "       CHANGE STATUS OF APPT BASED ON EASY FORM STATUS         "
        /// <summary>
        /// *******PURPOSE:THIS IS USED FOR CHANGE STATUS OF APPT BASED ON EASY FORM STATUS
        ///*******CREATED BY:RAVI TEJA.P
        ///*******CREATED DATE: 11/7/2017
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="htmltemplateinputmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>
        public ResponseModel changeApptStatusBasedOnEasyFormStatus(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {

            return _objApptStatusDA.changeApptStatusBasedOnEasyFormStatus(htmltemplatesavinginputmodel);

        }

        #endregion
    }

    internal class SendMsgOrEmailTousersWhenFormFinalizedInPortalBF
    {
        // declaring the class level varible to hold the data access calss instance
        private GetUsersToSendMsgWhenFormFinalizedInPortalDA _objMailMsgSendCustomizedUsersBF;


        #region  "      Constructor Function To Create the Instance for the DA Class     "

        public SendMsgOrEmailTousersWhenFormFinalizedInPortalBF()
        {
            _objMailMsgSendCustomizedUsersBF = new GetUsersToSendMsgWhenFormFinalizedInPortalDA();
        }

        #endregion

        #region "               SEND MESSAGES               "

        public void HTMLTemplatesSendMessagesIfFormFilledInPortal(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            DataTable dtEmailAndSMSUsers = null;
            DataRow[] drEmailAddress = null;
            DataRow[] drSMS = null;

            //sending internal message and getting Email and SMS details
            dtEmailAndSMSUsers = _objMailMsgSendCustomizedUsersBF.SendMessageIfFilledFromPortal(htmltemplatesavinginputmodel);



            //validating datatable 
            if (dtEmailAndSMSUsers != null && dtEmailAndSMSUsers.Rows.Count > 0)
            {
                //getting email address details to datarow
                drEmailAddress = dtEmailAndSMSUsers.Select("Portal_Messages_ReceiverInfo_Messages_Type IN(2)");

                //getting SMS details to data row
                drSMS = dtEmailAndSMSUsers.Select("Portal_Messages_ReceiverInfo_Messages_Type IN(3)");

                //calling method to send Email
                if (drEmailAddress != null && drEmailAddress.Length > 0)
                    new SendEmailToUsersBF().SendEmailToUsersAfterEasyFormSavedInPortal(drEmailAddress);

                //calling method to send SMS
                if (drSMS != null && drSMS.Length > 0)
                {
                    if (htmltemplatesavinginputmodel.SMSProvidertoUseWhileSendingSMS == 2)
                    {
                        new Send_SMS_ToUsersBF().SendTelnyxSMSAfterEasyFormSavedInPortal(drSMS, htmltemplatesavinginputmodel);
                    }
                    else
                    {
                        new Send_SMS_ToUsersBF().SendSMSAfterEasyFormSavedInPortal(drSMS, htmltemplatesavinginputmodel);
                    }
                }

            }

        }

        #endregion
    }

    internal class Send_SMS_ToUsersBF
    {

        #region"                SEND SMS TO USERS AFTER SAVING EASY FORM IN PORTAL              "
        /// <summary>
        ///*******PURPOSE:THIS IS USED TO SEND SMS TO CUSTOMIZED USERS AFTER SAVING EASY FORM IN PORTAL
        ///*******CREATED BY:UDAY KIRAN V
        ///*******CREATED DATE: 08/03/2015
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>
        public ResponseModel SendSMSAfterEasyFormSavedInPortal(DataRow[] drSMS, HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            ResponseModel model = null;
            DataTable dtSendSMS = null;
            ClsSendSmsData sendsmsmodel = null;
            string strMessage = string.Empty;
            AppointmentRemainderResult _objSMSSendResult = null;

            sendsmsmodel = new ClsSendSmsData();

            sendsmsmodel.FacilityName = htmltemplatesavinginputmodel.LoggedFacilityName;
            strMessage = drSMS[0]["Subject"].ToString();


            dtSendSMS = CreateStructureForSendSMS(drSMS);
            sendsmsmodel.dbServerName = htmltemplatesavinginputmodel.DBServerName;
            sendsmsmodel.PracticeId = htmltemplatesavinginputmodel.PracticeID;
            sendsmsmodel.FacilityID = htmltemplatesavinginputmodel.LoggedFacilityID;
            sendsmsmodel.SMSText = strMessage;
            sendsmsmodel.dtSendToDetails = dtSendSMS;
            sendsmsmodel.DoctorOrResourceName = htmltemplatesavinginputmodel.LoggedUserName;
            sendsmsmodel.ProviderID = htmltemplatesavinginputmodel.LoggedUserID;
            sendsmsmodel.SMSSendingFrom = htmltemplatesavinginputmodel.APICallingFromLocation;

            string requestURL = HttpContext.Current.Request.Url.Scheme + @"://" + HttpContext.Current.Request.Url.Host + "/EHR_SendSms_Api/EHRSendSMS/SendSMS";
            string strjson = JsonConvert.SerializeObject(sendsmsmodel);

            string settingsresponse = new Cls_RestApiCalling().CallPostService(requestURL, strjson);

            if (settingsresponse != null)
            {
                _objSMSSendResult = JsonConvert.DeserializeObject<AppointmentRemainderResult>(settingsresponse);
            }
            if (_objSMSSendResult == null)
            {

                model = new EFAssignValidationInfoBF().AssigningValidationInfo("Unable to send SMS, Please Try Again Later.");

            }
            if (_objSMSSendResult.StatusCode == StatusCode.VALIDATION)
            {

                model = new EFAssignValidationInfoBF().AssigningValidationInfo(_objSMSSendResult.Message);

            }
            else if (_objSMSSendResult.StatusCode == StatusCode.ERROR)
            {
                model = new EFAssignValidationInfoBF().AssigningValidationInfo(_objSMSSendResult.Message);

            }


            return model;
        }
        public ResponseModel SendTelnyxSMSAfterEasyFormSavedInPortal(DataRow[] drSMS, HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            ResponseModel model = null;

            TelnyxSendSMSData sendsmsmodel = null;
            string strMessage = string.Empty;


            sendsmsmodel = new TelnyxSendSMSData();


            strMessage = drSMS[0]["Subject"].ToString();


            sendsmsmodel.ReceiversDetailsList = CreateStructureForTelnyxSendSMS(drSMS);
            sendsmsmodel.DBServerName = htmltemplatesavinginputmodel.DBServerName;
            sendsmsmodel.PracticeID = htmltemplatesavinginputmodel.PracticeID;
            sendsmsmodel.FacilityID = htmltemplatesavinginputmodel.LoggedFacilityID;
            sendsmsmodel.SmsBody = strMessage;
            sendsmsmodel.SenderName = htmltemplatesavinginputmodel.LoggedUserName;
            sendsmsmodel.SenderID = htmltemplatesavinginputmodel.LoggedUserID;
            sendsmsmodel.APICallingFromLocation = htmltemplatesavinginputmodel.APICallingFromLocation + ">> EHR_EasyFormSaveV2_API/EasyFormSaveV3/SaveHtmlTemplateV3  ";
            sendsmsmodel.SenderUserType = 2; //Patient

            string requestURL = HttpContext.Current.Request.Url.Scheme + @"://" + HttpContext.Current.Request.Url.Host + "/EHR_Telnyx_SendSMS_Core/TelnyxSendSMSCore/SendSMSToMultipleReceivers";
            string strjson = JsonConvert.SerializeObject(sendsmsmodel);

            string settingsresponse = new Cls_RestApiCalling().CallPostService(requestURL, strjson);

            if (settingsresponse != null)
            {
                model = JsonConvert.DeserializeObject<ResponseModel>(settingsresponse);
            }

            return model;
        }
        #endregion

        #region"                CREATE DATATABLE STRUCTURE FOR SENDING SMS              "
        private DataTable CreateStructureForSendSMS(DataRow[] drSMS)
        {
            DataTable dtSendSMS = null;
            DataRow drNewRow = null;


            dtSendSMS = new DataTable();
            dtSendSMS.Columns.Add("SentToPhoneNumber", typeof(System.String));
            dtSendSMS.Columns.Add("SentToSourceID", typeof(System.Int32));
            dtSendSMS.Columns.Add("SentToSourceIDType", typeof(System.Int32));
            dtSendSMS.Columns.Add("SentToSourceName", typeof(System.String));
            dtSendSMS.Columns.Add("SMSSentStatus", typeof(System.Int32));
            dtSendSMS.Columns.Add("Comments", typeof(System.String));

            foreach (DataRow drRow in drSMS)
            {
                // REFRESH VARIABLE
                drNewRow = null;

                // CREATE A NEW ROW IN DATA TABLE
                drNewRow = dtSendSMS.NewRow();
                // ASSIGN VALUES TO DATA ROW 

                drNewRow["SentToPhoneNumber"] = drRow["EmailAddress"].ToString();
                drNewRow["SentToSourceID"] = drRow["UserID"].ToString();
                drNewRow["SentToSourceIDType"] = 1;
                drNewRow["SentToSourceName"] = null;
                drNewRow["SMSSentStatus"] = 1;
                drNewRow["Comments"] = null;

                //ADD DATA ROW TO TABLE
                dtSendSMS.Rows.Add(drNewRow);
            }
            return dtSendSMS;

        }
        private List<ReceiversDetailsModel> CreateStructureForTelnyxSendSMS(DataRow[] drSMS)
        {
            List<ReceiversDetailsModel> telnyxSMSDataToSend = new List<ReceiversDetailsModel>();

            ReceiversDetailsModel detailsmodel = null;

            foreach (DataRow drRow in drSMS)
            {

                detailsmodel = new ReceiversDetailsModel()
                {
                    ToPhoneNumber = drRow["EmailAddress"].ToString(),
                    ReceiverID = (int)drRow["UserID"],

                    ReceiverName = drRow["PoviderName"].ToString(),
                    ReceiverType = 1, // Provider

                };
                telnyxSMSDataToSend.Add(detailsmodel);
            }
            return telnyxSMSDataToSend;

        }
        #endregion
    }

    internal class EFAssignValidationInfoBF
    {
        #region
        /// <summary>
        /// *******PURPOSE: BY USING THIS USED TO ASSIGNING THE VALIDATION INFORMATION TO THE RESPONSE CLASS
        /// *******CREATED BY: SIVA PRASAD
        /// *******CREATED DATE: 11/17/2014
        /// *******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="strValidation"></param>
        /// <returns></returns>

        public ResponseModel AssigningValidationInfo(string strValidation)
        {
            ResponseModel responsemodel = null;

            responsemodel = new ResponseModel();
            responsemodel.ErrorMessage = strValidation;
            responsemodel.RequestExecutionStatus = -2; //TO IDENTIFY THAT IT IS A VALIDATION MESSAGE WE ARE USING -2


            return responsemodel;
        }



        #endregion

    }

    public class SendEmailToUsersBF
    {
        #region"                SEND EMAIL TO USERS AFTER SAVING EASY FORM IN PORTAL BY PATIENT                "



        public ResponseModel SendEmailToUsersAfterEasyFormSavedInPortal(DataRow[] drEmailAddress)
        {
            ResponseModel model = null;
            string ToMailIds = string.Empty;
            MailResponseModel mailresponseModel = null;
            foreach (DataRow drRow in drEmailAddress)
            {
                mailresponseModel = null;
                ToMailIds = string.Empty;
                MailInputModel mailinputmodel = new MailInputModel();
                mailinputmodel.FromMail = new MailAddress() { Address = "noreply@ehrYOURway.com" };
                ToMailIds = drRow["EmailAddress"].ToString();
                mailinputmodel.ToMails = new List<MailAddress>();
                if (!string.IsNullOrWhiteSpace(ToMailIds))
                {
                    if (ToMailIds.Contains(";"))
                    {
                        string[] strArrays = ToMailIds.Split(new char[] { ';' });
                        for (int i = 0; i < strArrays.Length; i++)
                        {
                            mailinputmodel.ToMails.Add(new MailAddress() { Address = strArrays[i] });
                        }
                    }
                    else
                    {
                        mailinputmodel.ToMails.Add(new MailAddress() { Address = ToMailIds });
                    }
                }
                mailinputmodel.MailSubject = drRow["Subject"].ToString();
                mailinputmodel.MailBody = drRow["Body"].ToString();
                mailinputmodel.BodyTextFormate = EmailBodyType.TextBody;
                mailinputmodel.MailCredentials = new MailServerCredentials()
                {
                    ServerName = ConfigurationManager.AppSettings.Get("SmarterMailSMTPServernName"),
                    UserName = ConfigurationManager.AppSettings.Get("SmarterMailSMTPUserName"),
                    Password = ConfigurationManager.AppSettings.Get("SmarterMailSMTPPassword"),
                    port = Convert.ToInt32(ConfigurationManager.AppSettings.Get("SmarterMailSMTPPort")),
                    useSSL = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("SmarterMailUseSSL"))

                };

                try
                {

                    mailresponseModel = new EHRSendMail().SendMail(mailinputmodel);
                }
                catch (Exception)
                {
                    if (mailresponseModel != null)
                    {
                        model = new ResponseModel();
                        model.ErrorID = -1;
                        model.ErrorMessage = mailresponseModel.ErrorMessage;
                    }
                }
            }


            return model;
        }
        #endregion

    }

    public class SendErrorMailOnBinarySavingInTempPathFailedBF
    {
        #region "        SEND ERROR MAIL WHILE SAVING EASYFORM BINARY IN TEMP PATH IS FAILED     "

        /// <summary>
        /// THIS IS USED TO SEND ERROR MAIL WHILE SAVING EASYFORM BINARY IN TEMP PATH IS FAILED
        /// ADDED BY AJAY NANDURI ON 05/12/2019
        /// </summary>
        public void SendErrorMailBczEasyFormBinarySavingInTempPathFailed(HtmlTemplateSavingInputModel htmlTemplateSavingInputModel,
                                                                         Exception ex)
        {
            // creating the local variable to hold the error mail body
            StringBuilder Sb_strMailBody = null;

            // creating the instace for the string builder variable
            Sb_strMailBody = new StringBuilder(500);

            // appending the mail header
            Sb_strMailBody.AppendLine("<span style='color:red;font-size:22px;font-weight: bold;'>Easy Form Temp File Saving Failed</span> <br/><br/>");

            #region "               PRACTICE INFO               "
            //POPULATING PRACTICE INFO like

            //Practice Name : Development Testing Practice
            //Practice ID: 999
            //Logged User Name: sjames
            //Logged User ID : 6

            Sb_strMailBody.AppendLine("<b><u>Practice Info : </u></b><br/><br/>");
            Sb_strMailBody.AppendLine("<b>Practice Name : </b> " + htmlTemplateSavingInputModel.PracticeName + "<br/>");// Practice Naem
            Sb_strMailBody.AppendLine("<b>Practice ID :</b> " + htmlTemplateSavingInputModel.PracticeID + "<br/>");//Practice ID
            Sb_strMailBody.AppendLine("<b>Logged User Name :</b> " + htmlTemplateSavingInputModel.LoggedUserName + "<br/>");// appending the logged userid
            Sb_strMailBody.AppendLine("<b>Logged User ID :</b> " + htmlTemplateSavingInputModel.LoggedUserID + "<br/><br/>");// appending the logged userid
            #endregion

            #region "               EASY FORM INFO               "
            //EasyForm ID :672
            //EasyForm Name :3C - CheckOut Sheet
            //Patient ID: 36788
            //WCFRequestGUID: emr - 31b7bdf4 - c1be - 579 - a27a - 169cb2c0dc9e
            //System Date: 12 / 6 / 2019 6:03:52 PM
            Sb_strMailBody.AppendLine("<b><u>Easy Form Info : </u></b><br/><br/>");
            Sb_strMailBody.AppendLine("<b>EasyForm ID :</b>" + htmlTemplateSavingInputModel.FillableHTMLDocumentTemplateID + "<br/>");// appending the logged userid
            Sb_strMailBody.AppendLine("<b>EasyForm Name :</b>" + htmlTemplateSavingInputModel.EasyFormTemplateName + "<br/>");// appending the logged userid
            if (htmlTemplateSavingInputModel.PatientID > 0)
                Sb_strMailBody.AppendLine("<b>Patient ID :</b>" + htmlTemplateSavingInputModel.PatientID.ToString() + "<br/>");// appending the logged userid
            else if (htmlTemplateSavingInputModel.patientchartmodel != null && htmlTemplateSavingInputModel.patientchartmodel.PatientID > 0)
                Sb_strMailBody.AppendLine("<b>Patient ID :</b>" + htmlTemplateSavingInputModel.patientchartmodel.PatientID.ToString() + "<br/>");// appending the logged userid
            Sb_strMailBody.AppendLine("<b>WCFRequestGUID :</b>" + htmlTemplateSavingInputModel.WCFRequestGUID + "<br/>");// appending the logged userid
            Sb_strMailBody.AppendLine("<b>System Date :</b>" + DateTime.Now + "<br/><br/>");// appending the logged userid
            #endregion

            #region "               ERROR MESSAGE               "
            //ERROR MESSAGE
            Sb_strMailBody.AppendLine("<b><u>Error Info : </u></b><br/><br/>");
            Sb_strMailBody.AppendLine("<b>Error Message :</b><br/>" + ex.Message + "<br/>");// appending the logged userid
            Sb_strMailBody.AppendLine("<b>Error Stack Trace :</b><br/>" + ex.StackTrace + "<br/><br/>");// appending the logged userid


            Sb_strMailBody.AppendLine("<b>Input JSON :</b><br/>" + JsonConvert.SerializeObject(htmlTemplateSavingInputModel) + "<br/><br/>");


            #endregion

            #region "               SEND ERROR MAIL             "

            // make a call to send the error mail
            // _objMailSendingCls.SendInformationMailToEMR("Easy Form Temp File Saving Failed", Sb_strMailBody.ToString());
            new EFCommonMailSendingBF().EFSendInformationMailToEMR("Easy Form Temp File Saving Failed", Sb_strMailBody.ToString());
            #endregion

        }

        #endregion
    }

    internal class PracticeInformationCopy
    {
        public void Copy(BaseModel inputTo, BaseModel inputFrom)
        {
            inputTo.LoggedUserID = inputFrom.LoggedUserID;
            inputTo.DBServerName = inputFrom.DBServerName;
            inputTo.PracticeID = inputFrom.PracticeID;
            inputTo.ReportsDBName = inputFrom.ReportsDBName;
            inputTo.FacilityIDForTempUse = inputFrom.FacilityIDForTempUse;
            inputTo.ProgramIdForTempUse = inputFrom.ProgramIdForTempUse;
        }
    }

    internal class CreatUdtForAutoForwardedUsersInfoBF
    {
        #region"                CREATE USER DEFINED DATATABLE FOR CUSTOMIZED SUPERVISORS INFO              "
        /// *******PURPOSE      : THIS METHOD IS USED TO CREATE USER DEFINED DATATABLE FOR CUSTOMIZED SUPERVISORS INFO
        ///*******CREATED BY    : AJAY
        ///*******CREATED DATE  : 11/20/2018
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        public DataTable CreateTableForCustomizeSelectedSupervisorsInfo(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            DataTable dtSupervisorsInfo = null;
            DataRow dtRow = null;


            dtSupervisorsInfo = new DataTable();

            dtSupervisorsInfo.Columns.Add("SupervisorID", typeof(System.Int32));
            dtSupervisorsInfo.Columns.Add("ActionRequiredInfoID", typeof(System.Int32));

            if (htmltemplatesavinginputmodel.easyFormDirAutoFwdUsersList != null && htmltemplatesavinginputmodel.easyFormDirAutoFwdUsersList.Count > 0)
            {
                foreach (EasyFormCustomizeForwardToSupervisorsModel item in htmltemplatesavinginputmodel.easyFormDirAutoFwdUsersList)
                {
                    dtRow = dtSupervisorsInfo.NewRow();

                    dtRow["SupervisorID"] = item.SupervisorID;
                    dtRow["ActionRequiredInfoID"] = item.ActionRequiredInfoID;

                    dtSupervisorsInfo.Rows.Add(dtRow);
                }
            }

            return dtSupervisorsInfo;
        }
        #endregion

    }

    internal class CreatUdtForNotesAutoBackwardUsersInfoBF
    {
        public DataTable GetBackwardUsersInboxList(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            DataTable dtUsersInfo = null;
            DataRow drNew = null;

            dtUsersInfo = new DataTable();
            dtUsersInfo.Columns.Add("EasyForms_ShowInInbox_UserID", typeof(Int32));
            dtUsersInfo.Columns.Add("AutoRemoveFrom_BackWardTo_UserInbox", typeof(bool));

            if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOBACKWARD)
            {
                if (htmltemplatesavinginputmodel.EasyformsMoveBackwardUsersModelList != null && htmltemplatesavinginputmodel.EasyformsMoveBackwardUsersModelList.Count > 0)
                {
                    foreach (EasyformsMoveBackwardUsersModel item in htmltemplatesavinginputmodel.EasyformsMoveBackwardUsersModelList)
                    {
                        drNew = dtUsersInfo.NewRow();
                        drNew["EasyForms_ShowInInbox_UserID"] = item.BackwardEasyFormShowInInboxUserID;
                        drNew["AutoRemoveFrom_BackWardTo_UserInbox"] = item.AutoRemoveFrom_BackWardTo_UserInbox;

                        dtUsersInfo.Rows.Add(drNew);
                    }
                }
            }

            return dtUsersInfo;
        }

        public DataTable GetBackwardUsersInboxList(HtmlTemplateInputModel htmltemplatesavinginputmodel)
        {
            DataTable dtUsersInfo = null;
            DataRow drNew = null;

            dtUsersInfo = new DataTable();
            dtUsersInfo.Columns.Add("EasyForms_ShowInInbox_UserID", typeof(Int32));
            dtUsersInfo.Columns.Add("AutoRemoveFrom_BackWardTo_UserInbox", typeof(bool));

            if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOBACKWARD)
            {
                if (htmltemplatesavinginputmodel.EasyformsMoveBackwardUsersModelList != null && htmltemplatesavinginputmodel.EasyformsMoveBackwardUsersModelList.Count > 0)
                {
                    foreach (EasyformsMoveBackwardUsersModel item in htmltemplatesavinginputmodel.EasyformsMoveBackwardUsersModelList)
                    {
                        drNew = dtUsersInfo.NewRow();
                        drNew["EasyForms_ShowInInbox_UserID"] = item.BackwardEasyFormShowInInboxUserID;
                        drNew["AutoRemoveFrom_BackWardTo_UserInbox"] = item.AutoRemoveFrom_BackWardTo_UserInbox;

                        dtUsersInfo.Rows.Add(drNew);
                    }
                }
            }

            return dtUsersInfo;
        }
    }

    internal class ValidateChangeApptStatusOnFormSaveActionBF
    {
        public ResponseModel ValidateChangeApptStatusOnFormSaveAction(ref HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            if (htmltemplatesavinginputmodel.LinkNotestoOtherAttendees == true && htmltemplatesavinginputmodel.grouptheraphyattendeesinfomodelList != null)
            {
                htmltemplatesavinginputmodel.StatusUpdatePatientIds = htmltemplatesavinginputmodel.patientchartmodel.PatientID + ",";
                foreach (GroupTheraphyAttendeesInfoModel item in htmltemplatesavinginputmodel.grouptheraphyattendeesinfomodelList)
                {
                    htmltemplatesavinginputmodel.StatusUpdatePatientIds += item.PatientID + ",";
                }
            }

            return new ValidateChangeApptStatusOnFormSaveActionDA().ValidationForchangeApptStatusBasedOnEasyFormStatus(htmltemplatesavinginputmodel);

        }
    }

    public static class EasyFormsExtensionsMethodsV3
    {
        #region "               REMOVE EHR GUID KEY FROM STRING             "
        /// <summary>
        /// creating the one static method remove ehr guid key from the given string ...
        /// Whic helps us to remove dynamically generated key from the geiven field name / id for easyforms format 2 (Div forms)
        /// </summary>
        /// <param name="strInput"></param>
        /// <param name="easyformGuidKey"></param>
        /// <returns></returns>
        public static string removeEhrGuidKeyFromString(this string strInput, string easyformGuidKey)
        {
            /// declaring the one local variable to hold the final return string value which we are replaced string 
            /// by default it hlds the empty value
            string replacedFieldName = "";

            /// creating the instance for the REGEX CLASS with required mathced regx expression
            /// which helps us to remove the matched string form the given string
            /// so that by passing the required regex expression constructor call
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("^ehr_[0-9a-zA-Z]{4}_ehr_");

            /// checking the replaceable guid key string is exists or not 
            /// if exists then go with the string replaceable method
            /// or else go with the regex replace method
            if (!string.IsNullOrWhiteSpace(easyformGuidKey))
            {
                replacedFieldName = strInput.Replace("ehr_" + easyformGuidKey + "_ehr_", "");
            }
            else
            {
                replacedFieldName = regex.Replace(strInput, "");
            }

            /// after succesfull replaceable guid key from the given string return the final string
            return replacedFieldName;
        }
        #endregion


        #region "               ADD EHR GUID KEY FROM STRING             "
        /// <summary>
        /// This is Extension Method used to add Div Guid key to Easy Form Field
        /// As WE are implementing Div Easy Form Format 2, In this format each Element caontains Dynamic Part + static Part
        /// we are saving only Static part in our data base, in compatble with Iframe Easy Forms
        /// So there are many functionalites based on Easy Form Fields ID , so we are getting GUID from front end 
        /// and recreasting the Element Id when ever required
        /// </summary>
        /// <param name="strInput"></param>
        /// <param name="easyformGuidKey"></param>
        /// <returns></returns>
        public static string addEhrGuidKeytoString(this string strInput, string easyformGuidKey, bool isToAddGuidKeyToEntireHTMLDocument = false)
        {
            /// declaring the one local variable to hold the final return string value which we are replaced string 
            /// by default it hlds the empty value
            string rtrString = "";

            //IF KEY DOES NOT EXISTS THEN , THEN WE ARE SENDING THE SAME FIELD VALUE BACK , THINKING IFRAME EASY FORM FIELD ID IN MIND
            if (!isToAddGuidKeyToEntireHTMLDocument)
            {
                //To Avoid Duplicate adding of divGuid key
                //First we are removing Old Key if Exists
                strInput.removeEhrGuidKeyFromString(easyformGuidKey);

                if (string.IsNullOrWhiteSpace(easyformGuidKey))
                {
                    rtrString = strInput;
                }
                else
                {
                    //FORMING GUID KEY
                    rtrString = "ehr_" + easyformGuidKey + "_ehr_" + strInput;
                }

            }
            else
            {
                /// creating the instance for the REGEX CLASS with required mathced regx expression
                /// which helps us to remove the matched string form the given string
                /// so that by passing the required regex expression constructor call
                System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("ehr_[0-9a-zA-Z]{4}_ehr_");

                /// checking the replaceable guid key string is exists or not 
                /// if exists then go with the string replaceable method
                /// or else go with the regex replace method
                rtrString = regex.Replace(strInput, "ehr_" + easyformGuidKey + "_ehr_");

            }

            /// after succesfull replaceable guid key from the given string return the final string
            return rtrString;
        }
        #endregion

        /// <summary>
        /// Following func is to generate and return a 4 digit random number
        /// by using default classes Random and Math class we are generating the 4 digit number
        /// </summary>
        /// <returns></returns>
        public static string adminGet4DigitRandomNum()
        {
            // creating the instance for the Random class
            Random rnd = new Random();

            // generating the 4 digit random number 
            var randomNumber = Math.Truncate(rnd.NextDouble() * 9) + "" + Math.Truncate(rnd.NextDouble() * 9) + "" + Math.Truncate(rnd.NextDouble() * 9) + "" + Math.Truncate(rnd.NextDouble() * 9);

            // return the number
            return randomNumber;
        }

        /// <summary>
        /// Following func is to generate and return a 6 digit random number
        /// by using default classes Random and Math class we are generating the 4 digit number
        /// </summary>
        /// <returns></returns>
        public static string adminGet6DigitRandomNum()
        {
            // creating the instance for the Random class
            Random rnd = new Random();

            // generating the 6 digit random number 
            var randomNumber = Math.Truncate(rnd.NextDouble() * 9) + "" + Math.Truncate(rnd.NextDouble() * 9) + "" + Math.Truncate(rnd.NextDouble() * 9) + "" + Math.Truncate(rnd.NextDouble() * 9) + "" + Math.Truncate(rnd.NextDouble() * 9) + "" + Math.Truncate(rnd.NextDouble() * 9);

            // return the number
            return randomNumber;
        }
    }

    internal class SaveEasyFormDocNotesInputjsoninTempBF
    {

        #region    " THIS FUNCTION IS USED TO SAVE DOCUMENTED NOTES SAVING  INPUT JSON META DATA IN TEMP  "
        /// <summary>
        /// THIS FUNCTION IS USED TO SAVE DOCUMENTED NOTES SAVING  INPUT JSON META DATA IN TEMP
        /// </summary>
        /// <param name="htmltemplatesavinginputmodel"></param>

        public void ExecuteDocNotesInputJsonSavinginTemp(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            htmltemplatesavinginputmodel.EFTempFileNameLinkingGuid = EasyFormsExtensionsMethodsV3.adminGet6DigitRandomNum();
            Task.Run(() => SaveEasyFormDocNotesSavingInputjsoninTemp(htmltemplatesavinginputmodel));

        }
        public async Task SaveEasyFormDocNotesSavingInputjsoninTemp(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            string fileName = string.Empty;
            string strTempFileInfo = string.Empty;
            string strPracticeInfo = string.Empty;
            string strPendingFolderPath = string.Empty;//Holds Physical temp File Path


            if (htmltemplatesavinginputmodel != null)
            {
                strTempFileInfo = new ClsCommonPath().GetTempfilePath() + @"\EasyFormsTempData\";

                strPracticeInfo = "Pending_" + htmltemplatesavinginputmodel.PracticeID.ToString() + "_" + DateTime.Now.ToString("yyyyMMdd") + "_InputJson";


                strPendingFolderPath = strTempFileInfo + strPracticeInfo + @"\" + htmltemplatesavinginputmodel.LoggedUserID.ToString() + @"\";

                //here we are checking if the path exits or not,if not exits then we create the folder
                if (!System.IO.Directory.Exists(strPendingFolderPath))
                    System.IO.Directory.CreateDirectory(strPendingFolderPath);

                fileName = !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EFTempFileNameLinkingGuid) ? htmltemplatesavinginputmodel.EFTempFileNameLinkingGuid + "_" + htmltemplatesavinginputmodel.WCFRequestGUID : htmltemplatesavinginputmodel.WCFRequestGUID;
                fileName += "_TID_" + htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID.ToString();

                if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0)
                    fileName += "_PDID_" + htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString();

                if (htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0)
                    fileName += "_PatientID_" + htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString();

                fileName = strPendingFolderPath + fileName + "_" + DateTime.Now.ToString("HHmmss");

                var config = new MapperConfiguration(cfg =>
                {
                    cfg.CreateMap<HtmlTemplateSavingInputModel, EasyFormSavingInputjsonLogModel>()

                    .ForMember(dest => dest.patientchartmodel, map => map.MapFrom(src => src.patientchartmodel));
                    cfg.CreateMap<PatientChartModel, EFLogPatientChartModel>();
                });

                //here by using mapper we are copying saving input model data to the local varibale
                var efSavingInputjsonLogModel = config.CreateMapper().Map<HtmlTemplateSavingInputModel, EasyFormSavingInputjsonLogModel>(htmltemplatesavinginputmodel);


                string jsonString = string.Empty;
                //here we are converting the input model to json string
                jsonString = JsonConvert.SerializeObject(efSavingInputjsonLogModel);
                //here we are calling this function for to get compressed byte array
                byte[] buff = new CommonFunction_EasyFormsZipAndUnZipBusinessFacade().zipEasyFormData(jsonString);

                fileName = fileName + ".zip";

                if (!File.Exists(fileName))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite))
                    {
                        using (BinaryWriter bw = new BinaryWriter(fs))
                        {
                            // CREATING FILE 
                            bw.Write(buff, 0, buff.Length);
                            bw.Close();

                        }
                        fs.Close();
                    }

                }

            }


        }
        #endregion

    }

    internal class IsBase64StringBF
    {
        public bool IsBase64String(string s)
        {
            s = s.Trim();
            return (s.Length % 4 == 0) && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", System.Text.RegularExpressions.RegexOptions.None);

        }
    }

    internal class EFCompressDecompressLZStringBF
    {
        public string decompressLzString(string compreesedNeed)
        {
            string decompressedstr = string.Empty;


            decompressedstr = LZStringCSharp.LZString.DecompressFromUTF16(compreesedNeed);

            if (string.IsNullOrWhiteSpace(decompressedstr) || string.IsNullOrWhiteSpace(decompressedstr) || decompressedstr.ToString().Trim().Length == 1)
            {
                decompressedstr = string.Empty;
            }


            return decompressedstr;

        }


    }

    internal class EasyFormsLZStringDeCompressBF
    {
        #region     "     EASYFORM LZ DECOMPRESSION  "

        /// <summary>
        /// *******PURPOSE:THIS IS USED FOR SAVING THE HTML ORIGINAL BINARY FORMAT ALONG WITH THE USER ENTERED INFORMATION IN LOG TABLE
        ///*******CREATED BY: MAHESH P
        ///*******CREATED DATE: 08/28/2015
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="htmltemplateinputmodel"></param>
        /// <returns></returns>
        public void EasyFormsLZStringDeCompress(ref HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            string easyFormDocInfo = string.Empty;

            if (!htmltemplatesavinginputmodel.easyformIsOfflineSync)
            {

                if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64.EndsWith("==") || new IsBase64StringBF().IsBase64String(htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64))
                {
                    //Base64 file No Need to Decompress
                }
                else
                {
                    easyFormDocInfo = new EFCompressDecompressLZStringBF().decompressLzString(htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64);

                    if (!string.IsNullOrWhiteSpace(easyFormDocInfo))
                        htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(easyFormDocInfo));
                }

            }

        }

        #endregion
    }

    internal class GetReqInputsInfoFromDBOnSaveActionBF
    {
        // declaring the class level varible to hold the data access calss instance
        private GetReqInputsForNotesSaveOrUpdateDA _objReqInputsGetDA;

        #region  "      Constructor Function To Create the Instance for the DA Class     "

        public GetReqInputsInfoFromDBOnSaveActionBF()
        {
            _objReqInputsGetDA = new GetReqInputsForNotesSaveOrUpdateDA();
        }

        #endregion

        #region"                GET EASYFORMS REQUIRED FIELDS FOR SAVE              "

        ///// <summary>
        /////*******PURPOSE             : THIS IS USED TO GET REQUIRED FIELDS TO SAVE EASY FORM 
        /////*******CREATED BY          : Balakrishna D
        /////*******CREATED DATE        : 5/26/2017
        /////*******MODIFIED DEVELOPER  : DATE - NAME - WHAT IS MODIFIED; *************************
        ///// </summary>
        public HtmlTemplateSavingDetailsModel GetEasyFormRequiredFieldsInfoFromDB(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel,
                                                                                    ref DataTable dtEasyFormPatientsInfo,
                                                                                    ref DataTable dtEasyFormsEHRSignedURLHX,
                                                                                    ref DataTable dtEasyFormsPortalSignedURLHX)
        {
            DataSet dsEasyFormDetails = null;//Used to Hold the Easy Form Required Fields 
            DataRow drEasyFormDetails = null;
            HtmlTemplateSavingDetailsModel objResultModel = null;
            ResponseModel responsemodel = null;


            //===================== GET EASY FORM LINKED PROGRAM RELATED LATEST PATIENT EPISODE BLOCK START=====================================
            //WE WILL GET THIS EPISODEID OTHER THAN PATIENTCHART
            //HERE WE ARE CHECKING PATIENT CHART ID LESS THAN 0 OR NOT
            //IF YES THEN GETTING THE LATEST EPISODE INFO BY CALLING SP
            if (htmltemplatesavinginputmodel.patientchartmodel.PatientChartId <= 0)
            {
                GetPtLatestEpisodeInfoIdOnFormLinkedPrgmsDA objDataAccess = new GetPtLatestEpisodeInfoIdOnFormLinkedPrgmsDA();

                //here we are checking if the value in the saving input model or not and the value in IsfromDasboardGroupTherapy or not
                //if it is true then we are go into this method for to get latest episode program linked to the patient for the session
                if (htmltemplatesavinginputmodel != null && htmltemplatesavinginputmodel.IsfromDasboardGroupTherapy)
                {
                    responsemodel = new GetLatestEpisodeNoFromDashboardGTNavigationDA().GetLatestEpisodeNumberForDashboardGroupTherapy(htmltemplatesavinginputmodel);

                    //here we are checking if the value in the response model  or not and the response id is less than zero or not
                    //if the response id less than zero than we are going  to this method for to get latest number
                    if (responsemodel != null && responsemodel.ResponseID <= 0)
                        responsemodel = objDataAccess.GetLatestEpisodeInfoId(htmltemplatesavinginputmodel);
                }
                //we are executing normal flow
                else
                    responsemodel = objDataAccess.GetLatestEpisodeInfoId(htmltemplatesavinginputmodel);

                //HERE WE ARE CHECKING RESPONSE ID GREATER THAN 0 OR NOT
                //THEN WE ASSIGNING RESPONSE ID TO LATEST EPISODE ID
                if (responsemodel != null && responsemodel.ResponseID > 0)
                    htmltemplatesavinginputmodel.patientchartmodel.LatestEpisodeInfoID = responsemodel.ResponseID;
            }
            //===================== GET EASY FORM LINKED PROGRAM RELATED LATEST PATIENT EPISODE BLOCK START=====================================

            dsEasyFormDetails = _objReqInputsGetDA.EasyForms_RequiredFields_ForSave_Get(htmltemplatesavinginputmodel);

            if (dsEasyFormDetails != null)
            {
                if (dsEasyFormDetails.Tables[0] != null && dsEasyFormDetails.Tables[0].Rows.Count > 0)
                    drEasyFormDetails = dsEasyFormDetails.Tables[0].Rows[0];

                // THIS TABLES HOLDS PATIENT INFO LINKED TO EASY FORM
                // IN GENERAL ONE PATIET WAS LINKED, BUT FROM COUPLE THERAPHY WE GET MULTIPLE PATIENTS
                if (dsEasyFormDetails.Tables[1] != null)
                    dtEasyFormPatientsInfo = dsEasyFormDetails.Tables[1];

                // GETTING DATA FOR STORING EHR SIGNED URL HX 
                if (dsEasyFormDetails.Tables[2] != null)
                    dtEasyFormsEHRSignedURLHX = dsEasyFormDetails.Tables[2];

                // GETTING DATA FOR STORING PORTAL SIGNED URL HX 
                if (dsEasyFormDetails.Tables[3] != null)
                    dtEasyFormsPortalSignedURLHX = dsEasyFormDetails.Tables[3];


                objResultModel = new HtmlTemplateSavingDetailsModel();

                if (drEasyFormDetails != null)
                {
                    if (drEasyFormDetails["EasyForms_Electronically_Saved_InfoID"] != null && drEasyFormDetails["EasyForms_Electronically_Saved_InfoID"] != DBNull.Value)
                        objResultModel.EasyForms_Electronically_Saved_InfoID = Convert.ToInt32(drEasyFormDetails["EasyForms_Electronically_Saved_InfoID"]);

                    if (drEasyFormDetails["DateID"] != null && drEasyFormDetails["DateID"] != DBNull.Value)
                        objResultModel.DateID = Convert.ToInt32(drEasyFormDetails["DateID"]);

                    if (drEasyFormDetails["TimeID"] != null && drEasyFormDetails["TimeID"] != DBNull.Value)
                        objResultModel.TimeID = Convert.ToInt32(drEasyFormDetails["TimeID"]);

                    if (drEasyFormDetails["GETDATETIME"] != null && drEasyFormDetails["GETDATETIME"] != DBNull.Value)
                        objResultModel.GETDATETIME = drEasyFormDetails["GETDATETIME"].ToString();

                    if (drEasyFormDetails["IsPatientsExist"] != null && drEasyFormDetails["IsPatientsExist"] != DBNull.Value)
                        objResultModel.IsPatientsExist = Convert.ToBoolean(drEasyFormDetails["IsPatientsExist"]);

                    if (drEasyFormDetails["IsLoggedUserHasSignOffPermission"] != null && drEasyFormDetails["IsLoggedUserHasSignOffPermission"] != DBNull.Value)
                        objResultModel.IsLoggedUserHasSignOffPermission = Convert.ToBoolean(drEasyFormDetails["IsLoggedUserHasSignOffPermission"]);

                    if (drEasyFormDetails["IsEasyFormSignedInPortal"] != null && drEasyFormDetails["IsEasyFormSignedInPortal"] != DBNull.Value)
                        objResultModel.IsEasyFormSignedInPortal = Convert.ToBoolean(drEasyFormDetails["IsEasyFormSignedInPortal"]);

                    if (drEasyFormDetails["IsEasyFormFinalSignedInEHR"] != null && drEasyFormDetails["IsEasyFormFinalSignedInEHR"] != DBNull.Value)
                        objResultModel.IsEasyFormFinalSignedInEHR = Convert.ToBoolean(drEasyFormDetails["IsEasyFormFinalSignedInEHR"]);

                    if (drEasyFormDetails["EasyForm_SignedStatus"] != null && drEasyFormDetails["EasyForm_SignedStatus"] != DBNull.Value)
                        objResultModel.EasyForm_SignedStatus = Convert.ToInt32(drEasyFormDetails["EasyForm_SignedStatus"]);

                    if (drEasyFormDetails["IsEasyFormSignedInEHR"] != null && drEasyFormDetails["IsEasyFormSignedInEHR"] != DBNull.Value)
                        objResultModel.IsEasyFormSignedInEHR = Convert.ToBoolean(drEasyFormDetails["IsEasyFormSignedInEHR"]);

                    if (drEasyFormDetails["IsChangeGTID"] != null && drEasyFormDetails["IsChangeGTID"] != DBNull.Value)
                        objResultModel.IsChangeGTID = Convert.ToBoolean(drEasyFormDetails["IsChangeGTID"]);

                    if (drEasyFormDetails["IsChangeGTST"] != null && drEasyFormDetails["IsChangeGTST"] != DBNull.Value)
                        objResultModel.IsChangeGTST = Convert.ToBoolean(drEasyFormDetails["IsChangeGTST"]);

                    if (drEasyFormDetails["IsChangeSysDefDOS"] != null && drEasyFormDetails["IsChangeSysDefDOS"] != DBNull.Value)
                        objResultModel.IsChangeSysDefDOS = Convert.ToBoolean(drEasyFormDetails["IsChangeSysDefDOS"]);

                    if (drEasyFormDetails["IsChangeAppSysDefDOS"] != null && drEasyFormDetails["IsChangeAppSysDefDOS"] != DBNull.Value)
                        objResultModel.IsChangeAppSysDefDOS = Convert.ToBoolean(drEasyFormDetails["IsChangeAppSysDefDOS"]);

                    if (drEasyFormDetails["IsChangeAppSysDefDOS"] != null && drEasyFormDetails["IsChangeAppSysDefDOS"] != DBNull.Value)
                        objResultModel.IsChangeAppSysDefDOS = Convert.ToBoolean(drEasyFormDetails["IsChangeAppSysDefDOS"]);

                    if (drEasyFormDetails["IsChangeAppID"] != null && drEasyFormDetails["IsChangeAppID"] != DBNull.Value)
                        objResultModel.IsChangeAppID = Convert.ToBoolean(drEasyFormDetails["IsChangeAppID"]);

                    if (drEasyFormDetails["IsChangeDOS"] != null && drEasyFormDetails["IsChangeDOS"] != DBNull.Value)
                        objResultModel.IsChangeDOS = Convert.ToBoolean(drEasyFormDetails["IsChangeDOS"]);

                    if (drEasyFormDetails["App_StartTime"] != null && drEasyFormDetails["App_StartTime"] != DBNull.Value)
                        objResultModel.App_StartTime = drEasyFormDetails["App_StartTime"].ToString();

                    if (drEasyFormDetails["EasyForm_SignOff_ActionRequired_InfoID"] != null && drEasyFormDetails["EasyForm_SignOff_ActionRequired_InfoID"] != DBNull.Value)
                        objResultModel.EasyForm_SignOff_ActionRequired_InfoID = Convert.ToInt32(drEasyFormDetails["EasyForm_SignOff_ActionRequired_InfoID"]);

                    if (drEasyFormDetails["ActionPerformedID"] != null && drEasyFormDetails["ActionPerformedID"] != DBNull.Value)
                        objResultModel.ActionPerformedID = Convert.ToInt32(drEasyFormDetails["ActionPerformedID"]);

                    if (drEasyFormDetails["GETDATETIME_AMPMFORMAT"] != null && drEasyFormDetails["GETDATETIME_AMPMFORMAT"] != DBNull.Value)
                        objResultModel.GETDATETIME_AMPMFORMAT = drEasyFormDetails["GETDATETIME_AMPMFORMAT"].ToString();

                    if (drEasyFormDetails["EasyForms_Electronically_Saved_SignOffActionType"] != null && drEasyFormDetails["EasyForms_Electronically_Saved_SignOffActionType"] != DBNull.Value)
                        objResultModel.EasyForms_Electronically_Saved_SignOffActionType = Convert.ToInt32(drEasyFormDetails["EasyForms_Electronically_Saved_SignOffActionType"]);

                    if (drEasyFormDetails["CreatedByType"] != null && drEasyFormDetails["CreatedByType"] != DBNull.Value)
                        objResultModel.CreatedByType = Convert.ToInt32(drEasyFormDetails["CreatedByType"]);

                    if (drEasyFormDetails["IsLetterTemplate"] != null && drEasyFormDetails["IsLetterTemplate"] != DBNull.Value)
                        objResultModel.IsLetterTemplate = Convert.ToBoolean(drEasyFormDetails["IsLetterTemplate"]);

                    if (drEasyFormDetails["CreatedUserID"] != null && drEasyFormDetails["CreatedUserID"] != DBNull.Value)
                        objResultModel.CreatedUserID = Convert.ToInt32(drEasyFormDetails["CreatedUserID"]);

                    if (drEasyFormDetails["Fillable_HTML_DocumentTemplateID"] != null && drEasyFormDetails["Fillable_HTML_DocumentTemplateID"] != DBNull.Value)
                        objResultModel.Fillable_HTML_DocumentTemplateID = Convert.ToInt32(drEasyFormDetails["Fillable_HTML_DocumentTemplateID"]);

                    if (drEasyFormDetails["Saved_AppID"] != null && drEasyFormDetails["Saved_AppID"] != DBNull.Value)
                        objResultModel.Saved_AppID = Convert.ToInt32(drEasyFormDetails["Saved_AppID"]);

                    if (drEasyFormDetails["InPat_GroupTherapy_Session_InfoID"] != null && drEasyFormDetails["InPat_GroupTherapy_Session_InfoID"] != DBNull.Value)
                        objResultModel.InPatGroupTherapySessionInfoID = Convert.ToInt32(drEasyFormDetails["InPat_GroupTherapy_Session_InfoID"]);

                    if (drEasyFormDetails["Saved_GTID"] != null && drEasyFormDetails["Saved_GTID"] != DBNull.Value)
                        objResultModel.Saved_GTID = Convert.ToInt32(drEasyFormDetails["Saved_GTID"]);

                    if (drEasyFormDetails["Saved_DOS"] != null && drEasyFormDetails["Saved_DOS"] != DBNull.Value)
                        objResultModel.Saved_DOS = drEasyFormDetails["Saved_DOS"].ToString();

                    if (drEasyFormDetails["EasyForms_Electronically_Saved_ActionDoneSeq"] != null && drEasyFormDetails["EasyForms_Electronically_Saved_ActionDoneSeq"] != DBNull.Value)
                        objResultModel.EasyForms_Electronically_Saved_ActionDoneSeq = Convert.ToInt32(drEasyFormDetails["EasyForms_Electronically_Saved_ActionDoneSeq"]);

                    if (drEasyFormDetails["EasyForms_Electronically_Saved_ActionWise_Sequence"] != null && drEasyFormDetails["EasyForms_Electronically_Saved_ActionWise_Sequence"] != DBNull.Value)
                        objResultModel.EasyForms_Electronically_Saved_ActionWise_Sequence = Convert.ToInt32(drEasyFormDetails["EasyForms_Electronically_Saved_ActionWise_Sequence"]);

                    if (drEasyFormDetails["SysDefDOS"] != null && drEasyFormDetails["SysDefDOS"] != DBNull.Value)
                        objResultModel.SysDefDOS = drEasyFormDetails["SysDefDOS"].ToString();

                    if (drEasyFormDetails["NewClient_Registration_FromPortal_InfoID"] != null && drEasyFormDetails["NewClient_Registration_FromPortal_InfoID"] != DBNull.Value)
                        objResultModel.NewClient_Registration_FromPortal_InfoID = Convert.ToInt32(drEasyFormDetails["NewClient_Registration_FromPortal_InfoID"]);

                    // cheksing the dos atribute field value is exists or not
                    // if exists then need to consider thsat dos attribute field value as the easyform dos value
                    // else consider the dos staitc hci value as easyform dos
                    if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EasyFormDOSFromFieldHavingDOSAttribute))
                        objResultModel.DosFilledInForm = htmltemplatesavinginputmodel.EasyFormDOSFromFieldHavingDOSAttribute;
                    else
                    {
                        if (drEasyFormDetails["DosFilledInForm"] != null && drEasyFormDetails["DosFilledInForm"] != DBNull.Value)
                            objResultModel.DosFilledInForm = drEasyFormDetails["DosFilledInForm"].ToString();
                    }

                    //''''
                    if (drEasyFormDetails["CallApptChangeDOSSP"] != null && drEasyFormDetails["CallApptChangeDOSSP"] != DBNull.Value)
                        objResultModel.CallApptChangeDOSSP = Convert.ToInt16(drEasyFormDetails["CallApptChangeDOSSP"]);

                    if (drEasyFormDetails["CallGroupTherapyApptChangeDOSSP"] != null && drEasyFormDetails["CallGroupTherapyApptChangeDOSSP"] != DBNull.Value)
                        objResultModel.CallGroupTherapyApptChangeDOSSP = Convert.ToInt16(drEasyFormDetails["CallGroupTherapyApptChangeDOSSP"]);

                    if (drEasyFormDetails["ApptType"] != null && drEasyFormDetails["ApptType"] != DBNull.Value)
                        objResultModel.ApptType = Convert.ToInt32(drEasyFormDetails["ApptType"]);


                    if (drEasyFormDetails["IsAutoForwardCustExist"] != null && drEasyFormDetails["IsAutoForwardCustExist"] != DBNull.Value)
                        objResultModel.IsAutoForwardCustExist = Convert.ToBoolean(drEasyFormDetails["IsAutoForwardCustExist"]);


                    if (drEasyFormDetails["IsCallAutoUploadStaticSP"] != null && drEasyFormDetails["IsCallAutoUploadStaticSP"] != DBNull.Value)
                        objResultModel.IsCallAutoUploadStaticSP = Convert.ToBoolean(drEasyFormDetails["IsCallAutoUploadStaticSP"]);

                    if (drEasyFormDetails["IsCallAutoUploadDynamicSP"] != null && drEasyFormDetails["IsCallAutoUploadDynamicSP"] != DBNull.Value)
                        objResultModel.IsCallAutoUploadDynamicSP = Convert.ToBoolean(drEasyFormDetails["IsCallAutoUploadDynamicSP"]);
                }
            }

            return objResultModel;
        }
        #endregion
    }

    internal class GetNotesDOSInReqFormatBF
    {
        #region     "         GET NOTES DOS IN REQ FORMAT         "

        public void GetEasyFormDOSInRequiredFormat(ref HtmlTemplateSavingDetailsModel objResultModel)
        {
            string strDate = string.Empty;

            strDate = objResultModel.DosFilledInForm;
            //strDate = DateTime.ParseExact(objResultModel.DosFilledInForm, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

            objResultModel.EasyFormDOS_In_DateFormat = Convert.ToDateTime(strDate).ToString("MM/dd/yyyy");
            objResultModel.EasyFormDOS_In_DateAMPMFormat = Convert.ToDateTime(strDate).ToString("MM/dd/yyyy hh:mm tt").Replace("12:00 AM", "").Replace("12:00:00 AM", "");

        }


        #endregion
    }

    internal class SetPNFS_ActionsFlagsOnNotesSaveOrUpdateInputsBF
    {
        #region"                ASSIGN EASY FORM PNFS FLAGS BASED ON CONDITION              "

        ///// <summary>
        /////*******PURPOSE             : THIS IS USED TO ASSIGN EASY FORM PNFS FLAGS BASED ON CONDITION
        /////*******CREATED BY          : Balakrishna D
        /////*******CREATED DATE        : 5/18/2019
        /////*******MODIFIED DEVELOPER  : DATE - NAME - WHAT IS MODIFIED; *************************
        ///// </summary>
        public void Assign_EasyForm_PNFS_Flags_Based_On_Condition(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel,
                                                                ref HtmlTemplateSavingDetailsModel objResultModel,
                                                                ref DataTable dtEasyFormPatientsInfo,
                                                                ref DataTable dtEasyFormsEHRSignedURLHX,
                                                                ref DataTable dtEasyFormsPortalSignedURLHX,
                                                                DataTable dtAutoForwardToSupervisors,
                                                                DataTable dtMoveBackwardUsers)
        {

            objResultModel.EasyFormSavingStatusFlags = new EasyFormSavingStatusFlagsModel();

            // Step 1  : EHR - Uploading Local Temp Urls to GCloud
            //NO NEED TO CHECK ANY CONDITION HERE
            //Reason : Always Need to Upload Temp URLs to GCloud
            objResultModel.EasyFormSavingStatusFlags.Original_TempFile_Uploaded_To_GCloud = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            objResultModel.EasyFormSavingStatusFlags.Formatted_TempFile_Uploaded_To_GCloud = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;


            // Step 2  : Easy Form - Fielded Data Saving
            //Note : From Direct Sign Off -- no Need of Fielded Saving, but this is doing in SP itself
            //NO NEED TO CHECK ANY CONDITION HERE
            //Reason : Fielded Saving has to do Every Time
            objResultModel.EasyFormSavingStatusFlags.Fielded_Saving_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;


            // Step 3  : Easy Form - Statemaintaine Data Saving
            //Note : This One Already Checking in Get SP ----> Assign As It is
            objResultModel.EasyFormSavingStatusFlags.Statemaintaine_Data_Saving_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;


            // Step 4  : Easy Form - Notes Formation Saving
            //NO NEED TO CHECK ANY CONDITION HERE
            //Reason : Notes Formation has to do Every Time
            objResultModel.EasyFormSavingStatusFlags.NotesFormation_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;


            // Step 5  : Billing - Performing Billing Activity
            //NO NEED TO CHECK ANY CONDITION HERE
            //Reason : Need to Ask Afroz
            objResultModel.EasyFormSavingStatusFlags.Perform_BillingActivity_REST_Call_Performed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;


            // Step 6  : Appointments - Update Appt With EasyForm
            //Condition : 
            if (objResultModel.Saved_AppID > 0 && (htmltemplatesavinginputmodel.IsClinicalDocument == true || htmltemplatesavinginputmodel.IsEasyFormBillable == true))
            {
                if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID == 0)
                    objResultModel.EasyFormSavingStatusFlags.CallCallApptSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
                else
                {
                    if (objResultModel.CallApptChangeDOSSP == (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired)
                        objResultModel.EasyFormSavingStatusFlags.CallCallApptSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
                    else
                        objResultModel.EasyFormSavingStatusFlags.CallCallApptSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
                }
            }
            else
                objResultModel.EasyFormSavingStatusFlags.CallCallApptSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;




            // Step 7  : Appointments - UPDATE APPOINTMENT INFO WHEN EASY FORM DOS CHANGED IN EDIT MODE
            //Conditon : Here We Need to Compare Easy Form DOS with Old DOS If Changed then Only we need to Call the SP
            //As Prev DOS is Not Exists at the Time of Saving, So DB Person Checking in SP
            if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID == 0)
            {
                //CHANGE DOS FACILITY IS AVIALBLE IN EDIT MODE ONLY, SO ADD MODE IT IS NOT REQUIRED
                objResultModel.EasyFormSavingStatusFlags.CallApptChangeDOSSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;// (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                //HERE CALL APPT CHANGE DOS IS REQUIRED OR NOT IS CHECKING IN SP ITSLEF,
                //SO ASSIGING VALUE DIRECTLY
                objResultModel.EasyFormSavingStatusFlags.CallApptChangeDOSSP = objResultModel.CallApptChangeDOSSP; // (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }



            // Step 8  : Easy Forms - UPDATE EASY FORM PATIENT DATA LOG
            //Condition : Here We are Checking LogID Or GUID, Previosly we are checking with LogID, Now we are checking based on GUID
            if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataLogID > 0 ||
                string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataLogGUID) == false)

                objResultModel.EasyFormSavingStatusFlags.CallLogUpdationSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallLogUpdationSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;




            // Step 9  : Easy Forms - INSERT EASY FORMS PATIENT DATA AUTO FORWARD TO USERS 
            //Conditon : the Statement says everything 
            //IsAutoForwardCustExist - for Dynamyic Users 
            //IsFormComesAsBackWard - Need to Move Document for same User from where he got 
            //dtAutoForwardToSupervisors - User Mannualy Selected Users 
            if (objResultModel.ActionPerformedID > 0 && objResultModel.ActionPerformedID != 9 &&
               (objResultModel.IsAutoForwardCustExist == true || (dtAutoForwardToSupervisors != null && dtAutoForwardToSupervisors.Rows.Count > 0)))
                objResultModel.EasyFormSavingStatusFlags.CallAutoForwardSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallAutoForwardSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;


            if (dtAutoForwardToSupervisors != null && dtAutoForwardToSupervisors.Rows.Count > 0)
                objResultModel.IsForwardUsersSelected = true;




            // Step 10 : Messages - SEND MESSAGE TO SUPERVISORS AFTER EASY FORM SAVING 
            //Note : Condition tells the information itself 
            //Need to Ask Gopi : --@SignOffActionTypePerformed > 0 AND @SignOffActionTypePerformed <> 9 AND @IsLetterTemplate = 0 - Send Message While FYI/Write SETTING TRUE  
            //How to Check setting 
            objResultModel.EasyFormSavingStatusFlags.CallMsgsSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;

            if (objResultModel.EasyFormSavingStatusFlags.CallAutoForwardSP == (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending)
            {
                if (objResultModel.ActionPerformedID > 0 && objResultModel.ActionPerformedID != 9 &&
                htmltemplatesavinginputmodel.CallingFromLetterTemplate == false && htmltemplatesavinginputmodel.EasyFormSendMessageWhileFYIOrWrite == true)
                {
                    objResultModel.EasyFormSavingStatusFlags.CallMsgsSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
                }
            }

            // 9 save as drft //12 ok and move 
            if (objResultModel.ActionPerformedID > 0 && objResultModel.ActionPerformedID != 9 && objResultModel.ActionPerformedID != 12)
            {
                objResultModel.IsFormComesAsBackWard = true;
            }


            // Step 11 : REFER TO - REFER TO SENT INFO - UPDATING REFERAL SENT INFO FOR EASYFORM
            if (htmltemplatesavinginputmodel.ReferToID > 0)
                objResultModel.EasyFormSavingStatusFlags.CallReferToSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallReferToSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;



            // Step 12 : WHEN EASYFORM SIGNED FROM EMR - "REMOVE PENDING FORMS TO FILL IN PORTAL" 
            //Note : Gopi Checking in Inputs Get
            //CONDITION : NEED TO CHECK FO THAT PATIETNT ID, FOR THAT TEMPLATE, FOR THAT SAVED FORM ID(PATIENT DATA ID) , 
            //IF ANY FORM EXISTS IN "TBL_EASYFORMS_PORTAL_UPLOADEDFORMS", THEN WE NEED TO REMOVE THAT
            //THIS CHECKING IS CHECKING BY DB PERSON 
            if (objResultModel.ActionPerformedID > 0 && objResultModel.ActionPerformedID != 9)
                objResultModel.EasyFormSavingStatusFlags.CallEHRSignoff_FormsToFill_DeleteSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallEHRSignoff_FormsToFill_DeleteSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;

            // Step 13 : PORTAL - WHEN EASY FORM FINALIZED FROM FORMS TO FILL UDATING STATUS
            if (htmltemplatesavinginputmodel.EasyFormsPortalUploadedFormInfoID > 0)
                objResultModel.EasyFormSavingStatusFlags.CallFormsToComplete_FillStatusSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallFormsToComplete_FillStatusSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;




            // Step 14 : SURVEYFORMS - UPDATE SURVEY FORMS SUBMIT STATUS INFO
            //Note : Here NavigationFrom == 43 ==> Survay Forms
            //CONDTION : IF EASY FORM SAVED AS SURVEY FORMS THEN NEED TO CALL THIS SP
            if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 43 && htmltemplatesavinginputmodel.SurveyClientRequestSentInfoId > 0)//SURVEYFORMSCREATENEW
                objResultModel.EasyFormSavingStatusFlags.CallSurveyFormsSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallSurveyFormsSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;




            // Step 15 : LINKED DOCUMENTS - UPDATE LINKED DOCUMENTS AND EASY FORMS INFO 
            if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EasyFormLinkedDocumentInfoIDs))
                objResultModel.EasyFormSavingStatusFlags.CallLinkedDocumentsSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallLinkedDocumentsSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;




            // Step 16 : Patient Portal - Update Patient Portal Disease Questions Answers Easy Forms
            if (htmltemplatesavinginputmodel.PatientPortalDiseaseQuestionsAnswersInfoID > 0)
                objResultModel.EasyFormSavingStatusFlags.CallPortal_DiseaseQuestionsSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallPortal_DiseaseQuestionsSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;




            // Step 17 : Easy Form Static Fields - When Easy Form Signed - INSERT EASYFORMS STATIC FIELDS FIELD DATA
            //Note : This Must Call Only from Easy Form Direct Sign Off , Not from Save / Update
            //At Present we calling this Method only from Save /update
            objResultModel.EasyFormSavingStatusFlags.OnlyStaticFieldsData_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;



            // Step 18 : Reminders - Update EasyForms Reminders Data Refresh 
            //Note : Setting from front end
            if (htmltemplatesavinginputmodel.IsRefreshEasyFormBasedReminderRuleTypes == true)
                objResultModel.EasyFormSavingStatusFlags.CallReminderFlagUpdateSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallReminderFlagUpdateSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;



            // Step 19 : Easy Form Move Backward - Save EasyForms PatientData BackWardTo Users
            if (dtMoveBackwardUsers != null && dtMoveBackwardUsers.Rows.Count > 0)
                objResultModel.EasyFormSavingStatusFlags.CallBackWardSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallBackWardSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;




            // Step 20 : Tasks - Task Status Update Execution AFTER EASY FORM SAVING
            //Note : Need to Check ModeOfSaving
            //@CallTasksSP  TINYINT               = NULL, -- EasyFormModeOfSaving = 1 && ObjInputs.PatientID > 0 
            if ((htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID == 0) && (htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0))
                objResultModel.EasyFormSavingStatusFlags.CallTasksSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallTasksSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;



            // Step 21 : Gloden Thread - Save EasyForm_GoldenThread_NeedsDxCodes_Details
            if (htmltemplatesavinginputmodel.IsEasyFormGoldenThreadNeedsDxCodesSavingRequired == true)
                objResultModel.EasyFormSavingStatusFlags.EasyFormGoldenThreadNeedsDxCodesSavingRequired = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.EasyFormGoldenThreadNeedsDxCodesSavingRequired = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;



            // Step 22 : Other Module - SaveOtherOrdersModuleInformationFromEasyForms
            if (htmltemplatesavinginputmodel.IsEasyFormOtherOrdersModuleRequired == true)
                objResultModel.EasyFormSavingStatusFlags.EasyFormOtherOrdersModuleRequired = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.EasyFormOtherOrdersModuleRequired = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;



            // Step 23 : Auto Upload Easy Form To Portal Execution
            if (objResultModel.IsCallAutoUploadStaticSP == true)
                objResultModel.EasyFormSavingStatusFlags.CallAutoUploadStaticSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallAutoUploadStaticSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;



            // Step 24 : AutoUploadToPortal_ELS_Dynamic_Execution_EasyForms_Execution
            if (objResultModel.IsCallAutoUploadDynamicSP == true)
                objResultModel.EasyFormSavingStatusFlags.CallAutoUploadDynamicSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallAutoUploadDynamicSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;


            // Step 25 : Billing - ChangePatientStatusBasedOnEasyFormSaveActionsRequired
            //Note : This Must Call Only from Easy Form Direct Sign Off , Not from Save / Update
            //At Present we calling this Method only from Save /update
            objResultModel.EasyFormSavingStatusFlags.ChangePatientStatusBasedOnEasyFormSaveActionsRequired = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;



            //Step 26 : 
            // Note : This flag is used to set insertion of a cpt and dx code saving
            // This flag is always in pending because each and every time need to get cpt and dx codes and insertion action must be performed
            // CONDITION ADDED TO CHECK WHTHER PATIENT IS LINKED OR NOT IF LINKED THEN ONLY SET FLAG IS TO BE PENDING OTHER WISE IT IS NOT REQUIRED
            if (htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0 || !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PatientIDs))
            {
                objResultModel.EasyFormSavingStatusFlags.CPTCodes_DxCodes_Saving_Into_CommonTable_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                objResultModel.EasyFormSavingStatusFlags.CPTCodes_DxCodes_Saving_Into_CommonTable_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
            }



            //Step 27 : 
            //Note : This flag is used to set auto claim creation status
            //this flag is in Pending status when form is signed off and auto create claim is set to true in easy form settings
            // 1 - Complete ; 10 - Edit & Complted
            //if ((htmltemplatesavinginputmodel.IsSignedOff || (objResultModel.ActionPerformedID > 0 && (objResultModel.ActionPerformedID == 1 || objResultModel.ActionPerformedID == 10))) && htmltemplatesavinginputmodel.AutoCreateClaimWhileSignOffDocuments)
            //according to easy forms team !9 means completed/signoff
            // CONDITION ADDED TO CHECK WHTHER PATIENT IS LINKED OR NOT IF LINKED THEN ONLY SET FLAG IS TO BE PENDING OTHER WISE IT IS NOT REQUIRED

            // requirement given by Kaladhar garu and approved by kumara garu
            // if practice local testing or Garcia then need to check whether EF is completed then no flag is set to not rquired to do auto claim creation
            if ((htmltemplatesavinginputmodel.PracticeID == 999 || htmltemplatesavinginputmodel.PracticeID == 573) && objResultModel.ActionPerformedID > 0 && objResultModel.ActionPerformedID == 1)
            {
                objResultModel.EasyFormSavingStatusFlags.AutoClaimCreation_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
            }
            else if ((htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0 || !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PatientIDs))
                && objResultModel.ActionPerformedID > 0 && objResultModel.ActionPerformedID != 9 && htmltemplatesavinginputmodel.AutoCreateClaimWhileSignOffDocuments)
            {
                objResultModel.EasyFormSavingStatusFlags.AutoClaimCreation_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                objResultModel.EasyFormSavingStatusFlags.AutoClaimCreation_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
            }


            //11-- Final Sign Off
            //Upload EASY Form document to PointClickCare
            //ActionPerformedID 11 = Sign Off
            //Fillable_HTML_DocumentTemplateID = 26 Consent Form in Practice 480
            if (htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0 && objResultModel.ActionPerformedID > 0 && objResultModel.ActionPerformedID == 11 && ((htmltemplatesavinginputmodel.PracticeID == 480 && htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID != 26) || (htmltemplatesavinginputmodel.PracticeID == 999 && htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID != 4308)))
            {
                objResultModel.EasyFormSavingStatusFlags.PCCDocumentUpload_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                objResultModel.EasyFormSavingStatusFlags.PCCDocumentUpload_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
            }


            //Step 28 : 
            //Note : This flag is used to set auto create super bill status
            //this flag is in Pending status when auto create super bill flag set to true
            // CONDITION ADDED TO CHECK WHTHER PATIENT IS LINKED OR NOT IF LINKED THEN ONLY SET FLAG IS TO BE PENDING OTHER WISE IT IS NOT REQUIRED
            if ((htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0 || !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PatientIDs))
                && htmltemplatesavinginputmodel.AutoCreateSuperBillWhileSavingCheck) //need to confirm this flag
            {
                objResultModel.EasyFormSavingStatusFlags.AutoSuperBillCreation_Completed_RDP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                objResultModel.EasyFormSavingStatusFlags.AutoSuperBillCreation_Completed_RDP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
            }



            //Step 29 : 
            //Note : This flag is used to set auth count deduction
            //this flag is in Pending status when form is signed off 
            //if ((htmltemplatesavinginputmodel.IsSignedOff || (objResultModel.ActionPerformedID > 0 && (objResultModel.ActionPerformedID == 1 || objResultModel.ActionPerformedID == 10))))
            //according to easy forms team !9 means completed/signoff
            // CONDITION ADDED TO CHECK WHTHER PATIENT IS LINKED OR NOT IF LINKED THEN ONLY SET FLAG IS TO BE PENDING OTHER WISE IT IS NOT REQUIRED
            if ((htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0 || !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PatientIDs))
                && objResultModel.ActionPerformedID > 0 && objResultModel.ActionPerformedID != 9)
            {
                objResultModel.EasyFormSavingStatusFlags.AuthCountDeduction_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                objResultModel.EasyFormSavingStatusFlags.AuthCountDeduction_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
            }


            //STEP 30 : GROUP THERAPHY APPT- STATUS SAVING
            objResultModel.EasyFormSavingStatusFlags.GreoupTheraphyApptStatus_Updated = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;

            if (objResultModel.InPatGroupTherapySessionInfoID > 0 && htmltemplatesavinginputmodel.ApplicationType == 1 &&
                (htmltemplatesavinginputmodel.IsClinicalDocument == true || htmltemplatesavinginputmodel.IsEasyFormBillable == true))
            {
                if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0 && (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 77 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 21))
                {
                    objResultModel.EasyFormSavingStatusFlags.GreoupTheraphyApptStatus_Updated = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
                }
                else
                {
                    if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID == 0)
                        objResultModel.EasyFormSavingStatusFlags.GreoupTheraphyApptStatus_Updated = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
                    else
                    {
                        if (objResultModel.CallGroupTherapyApptChangeDOSSP == (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired)
                            objResultModel.EasyFormSavingStatusFlags.GreoupTheraphyApptStatus_Updated = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
                    }
                }
            }



            //Step 31 : Group theraphy Update DOS
            if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0 && objResultModel.InPatGroupTherapySessionInfoID > 0)
            {
                //HERE CALL APPT CHANGE DOS IS REQUIRED OR NOT IS CHECKING IN SP ITSLEF,
                //SO ASSIGING VALUE DIRECTLY
                objResultModel.EasyFormSavingStatusFlags.CallGroupTherapyApptChangeDOSSP = objResultModel.CallGroupTherapyApptChangeDOSSP; // (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                //CHANGE DOS FACILITY IS AVIALBLE IN EDIT MODE ONLY, SO ADD MODE IT IS NOT REQUIRED
                objResultModel.EasyFormSavingStatusFlags.CallGroupTherapyApptChangeDOSSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;// (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }

            //  Step 32 : Easy Form - CHECKING DUPLICATE HCI VALUES FLAG
            //  Purpose : TO CHECK ANY DUPLICATE HCI VALUES OR EXISTS OR NOT
            //  Reason  : BY DEFAULT FIELDED SAVING IS PEDNING IN EASYFORM SAVE/EDIT MODE SO CHECKING DUPLICATE HCI VALUES FLAG IS TO BE PENDING
            objResultModel.EasyFormSavingStatusFlags.CallHCIDuplicatesRemovalSP = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;

            //  Step 33 : Easy Form - GOLDEN THREAD SP CALLING 
            //  Purpose : TO CHECK WHETHER TO CALL GOLDEN THREAD SAVING SP 
            if (htmltemplatesavinginputmodel.IsGoldenThreadStaticsHCIsLinked)
                // WHEN ANY ONE GOLDEN THREAD STATIC HCI LINKED , WE HAVE TO CHECK FUNCTIONALITY IN FIELDED SAVING EXE
                objResultModel.EasyFormSavingStatusFlags.CallGoldenThreadSavingSp = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallGoldenThreadSavingSp = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;

            //  Step 34 : Easy Form - ROI SAVING SP CALLING 
            //CHECKING THE FLAG  IsROIStaticHCILinked THAT HCI LINKED OR NOT
            //IF THE HCI IS LINKED THEN SET THE FLAG TO PENDING TO SAVE THE ROI DETAILS
            //IF THE HCI IS NOT LINKED THEN SET THE FLAG TO NOT REQUIRED 
            if (htmltemplatesavinginputmodel.IsROIStaticHCILinked)
                // WHEN ANY ONE ROI STATIC HCI LINKED , WE HAVE TO CHECK FUNCTIONALITY IN FIELDED SAVING EXE
                objResultModel.EasyFormSavingStatusFlags.CallROISavingSp = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallROISavingSp = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;

            //  Step 36 : Easy Form - Substance abuse hx SAVING SP CALLING 
            //CHECKING THE FLAG  IsAdmissionUsedSubstanceStaticHCILinked THAT HCI LINKED OR NOT
            //IF THE HCI IS LINKED THEN SET THE FLAG TO PENDING TO SAVE THE ROI DETAILS
            //IF THE HCI IS NOT LINKED THEN SET THE FLAG TO NOT REQUIRED 
            if (htmltemplatesavinginputmodel.IsAdmissionUsedSubstanceStaticHCILinked)
                // WHEN ANY ONE IsAdmissionUsedSubstanceStaticHCILinked STATIC HCI LINKED , WE HAVE TO CHECK FUNCTIONALITY IN FIELDED SAVING EXE
                objResultModel.EasyFormSavingStatusFlags.CallSubstanceAbuseAdmissionSp = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallSubstanceAbuseAdmissionSp = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;

            if (htmltemplatesavinginputmodel.IsDischargeUsedSubstanceStaticHCILinked)
                // WHEN ANY ONE IsAdmissionUsedSubstanceStaticHCILinked STATIC HCI LINKED , WE HAVE TO CHECK FUNCTIONALITY IN FIELDED SAVING EXE
                objResultModel.EasyFormSavingStatusFlags.CallSubstanceAbuseDischargeSp = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            else
                objResultModel.EasyFormSavingStatusFlags.CallSubstanceAbuseDischargeSp = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;

            if ((htmltemplatesavinginputmodel.PracticeID == 999 ||
                htmltemplatesavinginputmodel.PracticeID == 467 ||
                htmltemplatesavinginputmodel.PracticeID == 681) &&
                htmltemplatesavinginputmodel.IsLongTermGoalandShortGoalHciLinked)
            {
                objResultModel.EasyFormSavingStatusFlags.LongTermGoal_ShortTermGoalInfo_Saving_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                objResultModel.EasyFormSavingStatusFlags.LongTermGoal_ShortTermGoalInfo_Saving_Completed = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
            }

            if (htmltemplatesavinginputmodel.ApplicationType == 2 &&
                htmltemplatesavinginputmodel.EnableBasedonPatientPreferredLanguageConvertandUploadEasyFormTemplatetoPatientPortal &&
                htmltemplatesavinginputmodel.IsSignedOff == true &&
                htmltemplatesavinginputmodel.PreferredLanguageID > 0 && !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PreferredLanguageCodeQRDA) &&
                !htmltemplatesavinginputmodel.PreferredLanguageCodeQRDA.Contains("en"))
            {
                objResultModel.EasyFormSavingStatusFlags.PreferredLanguage_Original_TempFile_Uploaded_To_GCloud = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
                objResultModel.EasyFormSavingStatusFlags.PreferredLanguage_Formatted_TempFile_Uploaded_To_GCloud = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                objResultModel.EasyFormSavingStatusFlags.PreferredLanguage_Original_TempFile_Uploaded_To_GCloud = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
                objResultModel.EasyFormSavingStatusFlags.PreferredLanguage_Formatted_TempFile_Uploaded_To_GCloud = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
            }

            if (htmltemplatesavinginputmodel.IsSignedOff == true && objResultModel.ActionPerformedID > 0 && objResultModel.ActionPerformedID == 11)
            {
                objResultModel.EasyFormSavingStatusFlags.SendNotificationEmailAfterSignoff = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
                objResultModel.EasyFormSavingStatusFlags.SendMessageToMailBoxForSelectedUsersAfterFormSingedOff = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.Pending;
            }
            else
            {
                objResultModel.EasyFormSavingStatusFlags.SendNotificationEmailAfterSignoff = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
                objResultModel.EasyFormSavingStatusFlags.SendMessageToMailBoxForSelectedUsersAfterFormSingedOff = (int)PNFSReportStatusFlagsEnum.EhrEasyFormsSavingFlagStatus.NotRequired;
            }


        }

        #endregion
    }

    internal class NotesFormationBF
    {
        #region  "      CONSTRUCTOR FUNCTION TO CREATE THE INSTANCE FOR THE DIFFERERENT CHILD CLASSes USED IN THIS CLASS     "

        // declaring the class level varible to hold the child calsses instance which are used in this class
        private clsNotesFormatting _objNotesFormationCls;

        public NotesFormationBF()
        {
            _objNotesFormationCls = new clsNotesFormatting();
        }

        #endregion

        public void GetNotesFormatedHtmlString(ref HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, ref string originalTemplate, ref string notesFormattedString)
        {

            // Check whether notes formation is Enabled or not, 
            // if enables then we did Notes formation otherwise skip this functionality asynchronously
            if (htmltemplatesavinginputmodel.EasyFormsSettingsEnableNotesFormationSaving == false)
            {

                #region "               NOTES FORMATION             "

                if (!string.IsNullOrWhiteSpace(originalTemplate) && htmltemplatesavinginputmodel.EasyFormDesignType != 2)
                    notesFormattedString = _objNotesFormationCls.GetFormattedNotes(originalTemplate, htmltemplatesavinginputmodel.EasyFormNotesFormationLogicType, htmltemplatesavinginputmodel.EasyFormDesignType);

                #endregion

                #region "               APPEND ELECTRONICALLY SAVED/SIGNED INFO             "

                if (htmltemplatesavinginputmodel.IsShowElectronicallyCreatedInformation == true)
                    notesFormattedString = new GetAndAppendElectronicalySignUserInfoToNotesBF().
                                            GetAndAppendElectronicalySignUserInfoToNotes(htmltemplatesavinginputmodel, notesFormattedString);

                #endregion

                #region "               APPEND WATERMARK TO NOTES          "

                if (htmltemplatesavinginputmodel.ApplicationType == 1
                   && htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0
                   && htmltemplatesavinginputmodel.IsCustomizedWaterMarkExist == true)
                    new GetandAppendWatermarktoNotesBF().GetandAppendWatermarktoNotes(htmltemplatesavinginputmodel, ref notesFormattedString);

                #endregion

            }
            else
            {
                #region "               NOTES FORMATION  FAST FORWARD THAT MEANS WITHOUT STATEMAINTADATA ONLY            "

                if (!string.IsNullOrWhiteSpace(originalTemplate) && htmltemplatesavinginputmodel.EasyFormDesignType != 2)
                    notesFormattedString = _objNotesFormationCls.GetFormattedNotesWithOutStateMainteDataOnly(originalTemplate, htmltemplatesavinginputmodel.EasyFormNotesFormationLogicType);
                #endregion
            }

        }

        private int SkipEscapeSequencesEnabledByPractice(HtmlTemplateSavingInputModel inputModel)
        {
            //we skip escape sequences when the flag is 1
            return inputModel.EasyFormNotesFormationLogicType == 3
                && inputModel.EasyFormDesignType == 3
                && (inputModel.LoggedUserID == 6 && inputModel.PracticeID == 999)
                 ? 1
                 : 0;
        }
    }

    internal class GetandAppendWatermarktoNotesBF
    {
        public void GetandAppendWatermarktoNotes(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, ref string notesFormattedString)
        {
            string strEasyForm = string.Empty;


            strEasyForm = notesFormattedString;


            GetWatermarkTextbasedonPatientDataIDModel getWatermarkTextbasedonPatientDataIDModel = new GetandAppendWatermarktoNotesDA().GetWatermarkTextbasedonPatientDataID(htmltemplatesavinginputmodel);

            if (!string.IsNullOrWhiteSpace(getWatermarkTextbasedonPatientDataIDModel?.WatermarkText))
            {
                AddCustomizedWatermarktoNotes(ref strEasyForm, true, getWatermarkTextbasedonPatientDataIDModel);
            }

            notesFormattedString = strEasyForm;

        }

        internal void AddCustomizedWatermarktoNotes(ref string strEasyFormDocument, bool AppendWaterMark, GetWatermarkTextbasedonPatientDataIDModel getWatermarkTextbasedonPatientDataIDModel)
        {
            HtmlDocument doc = null; //to assign whitespaces removed html string
            string strAmendmentsInfo = string.Empty;
            HtmlNode htmlbodyPart = null;
            HtmlNode htmlpElement = null;
            HtmlNodeCollection voidNodesToRemove = null;

            //CHECKING DOCUMENT BINARY FORMAT
            if (!string.IsNullOrWhiteSpace(strEasyFormDocument))
            {

                doc = new HtmlDocument(); //create instance for html document
                doc.OptionWriteEmptyNodes = false;

                doc.LoadHtml(strEasyFormDocument); //load html document

                if (doc != null)
                {
                    // REMOVE WATER MARK INFORMATION FROM HTML
                    htmlbodyPart = doc.DocumentNode.SelectSingleNode("//body");//getting only body part

                    if (htmlbodyPart != null)
                    {
                        voidNodesToRemove = doc.DocumentNode.SelectNodes("//*[@class=\"customizedwatermark\"]");

                        if (voidNodesToRemove != null && voidNodesToRemove.Count > 0)
                        {
                            foreach (var node in voidNodesToRemove)
                            {
                                node.Remove();
                            }
                        }
                    }

                    // APPEND WATER MARK TO HTML
                    if (AppendWaterMark == true)
                    {

                        HtmlNode parent = doc.CreateElement("div");
                        parent.SetAttributeValue("class", "customizedwatermark");

                        HtmlNode child1 = doc.CreateElement("div");
                        child1.SetAttributeValue("class", "customizedwatermarkLine1");
                        child1.InnerHtml = getWatermarkTextbasedonPatientDataIDModel.WatermarkText;

                        StringBuilder sb_strtableInf = new StringBuilder(128);

                        sb_strtableInf.Append("<tr><th style='padding: 2px;text-align: right; width: 30%\'>Author:</th>");
                        sb_strtableInf.Append("<td style='padding: 2px;text-align: left;'>" + getWatermarkTextbasedonPatientDataIDModel?.LoggedUserName + "</td></tr>");

                        sb_strtableInf.Append("<tr><th style='padding: 2px;text-align: right;'>Date/Time:</th>");
                        sb_strtableInf.Append("<td style='padding: 2px;text-align: left;'>" + getWatermarkTextbasedonPatientDataIDModel?.DowlodedDateTime + "</td></tr>");

                        HtmlNode tablei = doc.CreateElement("table");
                        tablei.SetAttributeValue("align", "left");
                        tablei.SetAttributeValue("style", "padding-left:10px;table-layout: fixed;width: 500px;");
                        tablei.InnerHtml = sb_strtableInf.ToString();

                        parent.AppendChild(child1);
                        parent.AppendChild(tablei);

                        doc.DocumentNode.SelectSingleNode("//body").PrependChild(parent);
                    }
                }

                strEasyFormDocument = doc.DocumentNode.OuterHtml;

            }

        }

    }

    internal class SaveAndGetNotesOriginalAndFormattedBinaryInTempURLsBF
    {
        // declaring the class level varible to hold the data access calss instance
        private SaveAndGetNotesBinaryInTempPathBF _objSaveBinaryInTempBF;

        #region  "      Constructor Function To Create the Instance for the DA Class     "

        public SaveAndGetNotesOriginalAndFormattedBinaryInTempURLsBF()
        {
            _objSaveBinaryInTempBF = new SaveAndGetNotesBinaryInTempPathBF();
        }

        #endregion

        #region     "         SAVE NOTES ORIGINAL AND FORMATED BINARYS AND RETURN THOSE TEMP URLS      "

        public void SaveAndGetNotesOriginalAndFormattedBinaryInTempURLs(ref HtmlTemplateSavingInputModel htmltemplatesavinginputmodel,
                                                                        ref bool isEasyFormTempSavingSuccessFull)
        {
            try
            {
                // MAKE A CALL TO GET A LOCAL TEMP FILE PATH FOR ORIGINAL NOTES BINARY
                htmltemplatesavinginputmodel.strSavedOriginalNotesLocalPathUrl = _objSaveBinaryInTempBF.SaveEasyFormBinaryInTempAndGetURL(htmltemplatesavinginputmodel, false);

                // MAKE A CALL TO GET A LOCAL TEMP FILE PATH FOR FORMATED NOTES BINARY
                htmltemplatesavinginputmodel.strSavedFormattedNotesLocalPathUrl = _objSaveBinaryInTempBF.SaveEasyFormBinaryInTempAndGetURL(htmltemplatesavinginputmodel, true);

                // Easy Form Original Temp URL is mandatory for further actions
                //So if Temp file Saving is Failed then we need to send error mail
                //even throgh we are saving Binary Data to table, just as alert we are sinding this Emails
                if (string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.strSavedOriginalNotesLocalPathUrl))
                    throw new Exception("Easy Form Temp File Saving Failed from Easy Form");
            }
            catch (Exception ex)
            {
                isEasyFormTempSavingSuccessFull = false;
                htmltemplatesavinginputmodel.strSavedOriginalNotesLocalPathUrl = string.Empty;
                htmltemplatesavinginputmodel.strSavedFormattedNotesLocalPathUrl = string.Empty;

                // if the any excpetion occured while saving the binary saving in temp file is fialed then send error information mail
                // if any error occured while saving easyform binary in temp then excpetion mail
                // this code is added by ajay on 05/12/2019
                new SendErrorMailOnBinarySavingInTempPathFailedBF().SendErrorMailBczEasyFormBinarySavingInTempPathFailed(htmltemplatesavinginputmodel, ex);
            }
        }

        #endregion

    }

    internal class SaveAndGetNotesBinaryInTempPathBF
    {

        #region  "      CONSTRUCTOR FUNCTION     "

        // declaring the class level varible to hold the data access calss instance
        private CreateAndGetFileNameWithDataInReqFormatBF _objFileCreateCls;

        public SaveAndGetNotesBinaryInTempPathBF()
        {
            _objFileCreateCls = new CreateAndGetFileNameWithDataInReqFormatBF();
        }

        #endregion

        #region "          GET TEMP FILE PATH FOR SAVED NOTES BINARY              "

        /// <summary>
        /// *******PURPOSE      : THIS METHOD IS TO GET TEMP FILE PATH FOR SAVED NOTES BINARY 
        ///*******CREATED BY    : AJAY.NANDURI
        ///*******CREATED DATE  : 21-03-2019
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// <returns></returns>
        public string SaveEasyFormBinaryInTempAndGetURL(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel,
                                                   bool IsTempFilePathForFormatedBinary)
        {
            string fileName = string.Empty;//USED TO HOLD FILE NAME
            string strTempFileInfo = string.Empty; //Used to Hold Physical Pathe from Config
            string strPracticeInfo = string.Empty;//Just to Hold "Pending_PracticeID"
            string strPendingFolderPath = string.Empty;//Holds Physical temp File Path
            string strSavedNotesFilName = string.Empty;//HOLDS CREATED FILE NAME

            string strSavedNotesPathUrl = string.Empty;
            string strSavedNotesFilePath = string.Empty;
            string fileNotFoundExceptionMessage = string.Empty;

            if (htmltemplatesavinginputmodel == null)
                return string.Empty;

            fileName = "PraID_" + htmltemplatesavinginputmodel.PracticeID.ToString() + "_TID_" + htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID.ToString();

            // CHECKING THE IS PATIENT DATA ID IS EXISTS OR NOT IF EXISTS THEN INCLUDE IT IN THE FILE NAME
            if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0)
                fileName = fileName + "_PDID_" + htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString() + "_";

            //GETTING THE FOLDER PATH FOR CREATING THE FILE WITH THE DATA OF THE HTML FILE 
            // As per mohan sir discussion easyform binary is saved outside the temp folder and folder name as "EasyFormsTempData"
            // so following line is commented 
            //strTempFileInfo = new ClsCommonPath().GetTempfilePath() + @"\temp\EasyFormSavedInfo\";
            strTempFileInfo = new ClsCommonPath().GetTempfilePath() + @"\EasyFormsTempData\";

            // IF EASYFORM SAVED INFO DIRECTORY DOES NOT EXIST, CREATE IT.
            if (!System.IO.Directory.Exists(strTempFileInfo))
                System.IO.Directory.CreateDirectory(strTempFileInfo);

            // appending the practice id and date
            strPracticeInfo = "Pending_" + htmltemplatesavinginputmodel.PracticeID.ToString() + "_" + DateTime.Now.ToString("yyyyMMdd");

            strPendingFolderPath = strTempFileInfo + strPracticeInfo + @"\";

            // IF PENDING DIRECTORY DOES NOT EXIST, CREATE IT.
            if (!System.IO.Directory.Exists(strPendingFolderPath))
                System.IO.Directory.CreateDirectory(strPendingFolderPath);

            //CHECKING WHETHER THE  TEMFILE PATH CREATION IS FOR FORMATED BINARY OR NOT ELSE TEMP FILE PATH FOR ORIGINAL BINARY
            if (IsTempFilePathForFormatedBinary == true)
            {
                fileName += !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EFTempFileNameLinkingGuid) ? "_FB" + "_" + htmltemplatesavinginputmodel.EFTempFileNameLinkingGuid : "_FormatedBinary";

                fileName += "_" + DateTime.Now.ToString("yyyyMMdd") + "_" + DateTime.Now.ToString("HHmmss");

                // CREATE A FILE AND GETING FILE NAME FOR THE NOTES FORMATED BINARY
                strSavedNotesFilName = _objFileCreateCls.createAndGetFileNameWithDataInReqFormat(htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesFormattedBinaryFormat, strPendingFolderPath, fileName);

                // Following block of code is to check file name is exist or not if not throw exception
                // CHECKING THE FILE NAME IS EXISTS OR NOT
                if (string.IsNullOrWhiteSpace(strSavedNotesFilName))
                {
                    //BUILD EXCEPTION MESSAGE TO MAIL
                    // IF THE TEMP FILE CREATION FOR FORMATED BINARY
                    fileNotFoundExceptionMessage = "<b>EasyForm Saving(Formated) is in LocalDrive is Failed, So Uploaded to Gcloud </b><br/>";
                    fileNotFoundExceptionMessage = fileNotFoundExceptionMessage + "<br /> File Name: <br/>";
                    fileNotFoundExceptionMessage = fileNotFoundExceptionMessage + "============== <br />";
                    fileNotFoundExceptionMessage = fileNotFoundExceptionMessage + fileName + "<br />";  // File name
                    fileNotFoundExceptionMessage = fileNotFoundExceptionMessage + "============== <br /><br />";

                    throw new Exception(fileNotFoundExceptionMessage);
                }

                htmltemplatesavinginputmodel.strSavedNotesFormatedBinaryFileName = strSavedNotesFilName;

                //FORMING THE URL FOR ACCESSING THE CREATED FILE FROM THE SERVER, USING THE INPUT REQUESTED FILE
                strSavedNotesFilePath = strPendingFolderPath + strSavedNotesFilName;

                htmltemplatesavinginputmodel.strLocalFormatedBinaryNotesPathUrl = strSavedNotesFilePath;
            }
            else
            {
                fileName += !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EFTempFileNameLinkingGuid) ? "_OB" + "_" + htmltemplatesavinginputmodel.EFTempFileNameLinkingGuid : "_OriginalBinary";

                fileName += "_" + DateTime.Now.ToString("yyyyMMdd") + "_" + DateTime.Now.ToString("HHmmss");

                // CREATE A FILE AND GETING FILE NAME FOR THE ORIGINAL NOTES BINARY
                strSavedNotesFilName = _objFileCreateCls.createAndGetFileNameWithDataInReqFormat(htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat, strPendingFolderPath, fileName);

                // Following block of code is to check file name is exist or not if not throw exception
                // CHECKING THE FILE NAME IS EXISTS OR NOT
                if (string.IsNullOrWhiteSpace(strSavedNotesFilName))
                {
                    //BUILD EXCEPTION MESSAGE TO MAIL
                    // IF THE TEMP FILE CREATION FOR FORMATED BINARY
                    fileNotFoundExceptionMessage = "<b>EasyForm Saving(Original) is in LocalDrive is Failed, So Uploaded to Gcloud </b><br/>";
                    fileNotFoundExceptionMessage = fileNotFoundExceptionMessage + "<br /> File Name: <br/>";
                    fileNotFoundExceptionMessage = fileNotFoundExceptionMessage + "============== <br />";
                    fileNotFoundExceptionMessage = fileNotFoundExceptionMessage + fileName + "<br />";  // File name
                    fileNotFoundExceptionMessage = fileNotFoundExceptionMessage + "============== <br /><br />";

                    throw new Exception(fileNotFoundExceptionMessage);
                }

                htmltemplatesavinginputmodel.strSavedNotesOriginalBinaryFileName = strSavedNotesFilName;

                //FORMING THE URL FOR ACCESSING THE CREATED FILE FROM THE SERVER, USING THE INPUT REQUESTED FILE
                strSavedNotesFilePath = strPendingFolderPath + strSavedNotesFilName;

                htmltemplatesavinginputmodel.strLocalSavedOriginalBinaryNotesPathUrl = strSavedNotesFilePath;
            }

            if (htmltemplatesavinginputmodel.EnableJWTToken)
            {
                if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel?.EHRRequestCallingWebAPIUrl))
                {
                    strSavedNotesPathUrl = $@"{htmltemplatesavinginputmodel.EHRRequestCallingWebAPIUrl}{new ClsCommonPath().GetCurrentRequestUrlSegments()}EasyFormsTempData/{strPracticeInfo}/{strSavedNotesFilName}";
                }
                else
                {
                    //FORMING THE URL FOR ACCESSING THE CREATED FILE FROM THE SERVER, USING THE INPUT REQUESTED FILE 
                    strSavedNotesPathUrl = HttpContext.Current.Request.Url.Scheme + @"://" +
                                                                                            HttpContext.Current.Request.Url.Host + HttpContext.Current.Request.Url.Segments[0] +
                                                                                            new ClsCommonPath().GetCurrentRequestUrlSegments() + @"EasyFormsTempData/" + strPracticeInfo + @"/" + strSavedNotesFilName;
                }
            }
            else
            {
                //FORMING THE URL FOR ACCESSING THE CREATED FILE FROM THE SERVER, USING THE INPUT REQUESTED FILE 
                strSavedNotesPathUrl = HttpContext.Current.Request.Url.Scheme + @"://" +
                                                                                        HttpContext.Current.Request.Url.Host + HttpContext.Current.Request.Url.Segments[0] +
                                                                                        new ClsCommonPath().GetCurrentRequestUrlSegments() + @"EasyFormsTempData/" + strPracticeInfo + @"/" + strSavedNotesFilName;
            }

            //returning generated saving path url
            return strSavedNotesPathUrl;
        }

        #endregion
    }

    internal class CreateAndGetFileNameWithDataInReqFormatBF
    {



        #region   "       FILE TYPE ENUM      "

        public enum FileTypes
        {
            PDF = 1,
            TIFF = 2,
            GIF = 3,
            JPEG = 4,
            TXT = 5,
            RTF = 6,
            HTML = 7,
            DOC = 8,
            PNG = 9,
            BMP = 10,
            XSL = 11,
            XML = 12,
            DOCX = 13,
            ZIP = 14,
            CCDAXMLASREADABLEFORMAT = 15,
            DCM = 16,
            XLS = 17,
            XLSX = 18,
            TIF = 19,
            JPG = 20,
            AnnotaionPNG = 21,
            JSON = 22,
            JPEG_ORIGINAL = 23,
            GIF_ORIGINAL = 24,
            BMP_ORIGINAL = 25,
            PNG_ORIGINAL = 26,
            TIFF_ORIGINAL = 27,
            HL7 = 28,
            CSV = 29,
            WAV = 30,
            MP4 = 31
        }
        #endregion

        #region   "     GET FILE EXTENSION TYPE BASED ON ENUM   "
        public string getFileExtensionsBasedOnFileType(FileTypes fileType)
        {
            string fileExtensionType = string.Empty;

            //ASSIGNING THE FILE DEPENDING ON THE BINARY FORMAT FILE TYPE 
            switch (fileType)
            {
                case FileTypes.GIF:
                    fileExtensionType = ".gif";
                    break;
                case FileTypes.JPEG:
                    fileExtensionType = ".jpg";
                    break;
                case FileTypes.PDF:
                    fileExtensionType = ".pdf";
                    break;
                case FileTypes.RTF:
                    fileExtensionType = ".rtf";
                    break;
                case FileTypes.TIFF:
                    fileExtensionType = ".tiff";
                    break;
                case FileTypes.TXT:
                    fileExtensionType = ".txt";
                    break;
                case FileTypes.HTML:
                    fileExtensionType = ".html";
                    break;
                case FileTypes.PNG:
                    fileExtensionType = ".png";
                    break;
                case FileTypes.BMP:
                    fileExtensionType = ".bmp";
                    break;
                case FileTypes.ZIP:
                    fileExtensionType = ".zip";
                    break;
                default:
                    break;
            }

            return fileExtensionType;
        }
        #endregion

        #region  "     CREATE AND GET FILE NAME WITH DATA IN REQFORMAT            "
        public string createAndGetFileNameWithDataInReqFormat(byte[] buff, string filepath, string fileName)
        {
            string strFilePath = String.Empty;
            string strguid = string.Empty;
            string strfilename = string.Empty;

            // GETTING THE FILE NAME USING THE GUID FUNCTION
            strguid = fileName + "_" + Guid.NewGuid().ToString().Replace("-", "");

            strfilename = strguid + getFileExtensionsBasedOnFileType(CreateAndGetFileNameWithDataInReqFormatBF.FileTypes.ZIP);

            strFilePath = filepath + strfilename;

            if (!writeByteArrayToFile(buff, strFilePath))
                strfilename = "";


            return strfilename;
        }

        #endregion

        #region    "     WRITE DATA TO GIVE FILE NAME   "
        private bool writeByteArrayToFile(byte[] buff, string fileName)
        {
            bool response = false;

            using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    // CREATING FILE 
                    bw.Write(buff, 0, buff.Length);

                    // CHECKING WHETHER FILE CREATED OR NOT 
                    if (!File.Exists(fileName))
                    {
                        //============= TEST ERROR MAIL IF FILE NOT CREATED START ================
                        sendErrorMailWhenTempFileNotCreated("Error From writeByteArrayToFile - Attempt 1", false);
                        //============= TEST ERROR MAIL IF FILE NOT CREATED END ================

                        // IF FILE NOT CREATED THEN WAIT FOR ANOTHER 100 MS
                        System.Threading.Thread.Sleep(50);

                        // AFTER THAT WE WILL CHECK WHETHER FILE CREATED OR NOT
                        if (!File.Exists(fileName))
                        {
                            //============= TEST ERROR MAIL IF FILE NOT CREATED START ================
                            sendErrorMailWhenTempFileNotCreated("Error From writeByteArrayToFile - Attempt 2", false);
                            //============= TEST ERROR MAIL IF FILE NOT CREATED END ================

                            // AGAIN WE WILL WAIT FOR ANOTHER 100 MS
                            System.Threading.Thread.Sleep(50);

                            if (!File.Exists(fileName))
                            {
                                response = false;
                                //============= TEST ERROR MAIL IF FILE NOT CREATED START ================
                                sendErrorMailWhenTempFileNotCreated("Error From writeByteArrayToFile - Attempt 3", true);
                                //============= TEST ERROR MAIL IF FILE NOT CREATED END ================
                            }
                            else
                            {
                                response = true;
                            }
                        }
                        else
                        {
                            response = true;
                        }
                    }
                    else
                    {
                        response = true;
                    }

                    bw.Close();
                }
            }

            return response;
        }
        #endregion

        #region     "    SEND ERROR EMAIL       "
        /// <summary>
        /// CODE FOR SENDING ERROR MAIL SENDING WHEN TEMP FILE NOT CREATED 
        /// ADDED BY AJAY KUMAR.NANDURI
        /// </summary>
        /// <param name="subject"></param>
        /// <returns></returns>
        private void sendErrorMailWhenTempFileNotCreated(string subject, bool isFinalAttempt)
        {
            string mailBody = string.Empty;

            #region "               SEND ERROR MAIL             "

            mailBody = isFinalAttempt ? "File Creation Failed in Local Drive." : "File Creation Inprocess in Local Drive, waiting for 50 Milli Sec.";

            // make a call to send the error mail
            //  _objMailSendingCls.SendInformationMailToEMR(subject, mailBody.ToString());
            new EFCommonMailSendingBF().EFSendInformationMailToEMR(subject, mailBody.ToString());
            #endregion

        }
        #endregion

    }

    internal class ValidateMandatoryFieldsBF
    {

        #region     "       CHECK ANY MANDATORY VALIDATIONS ARE EXISTS IN HTML DOCUMENT OR NOT        "

        public bool CheckAnyMandatoryValidationsExistsInHTMLDocument(string htmlDocument)
        {
            HtmlDocument htmlAgilityForMandatory = null;
            HtmlNodeCollection htmlNodesMandatoryFieldsCollection = null;
            bool MandatoryFieldsNotFilled = false;

            // LOAD DOCUMENT INTO AGILITYPACK SERVICE AND GET MANDATORY FIELDS AND CHECK VALUE FOR THOSE FIELDS
            htmlAgilityForMandatory = new HtmlDocument(); //create instance for html document

            htmlAgilityForMandatory.LoadHtml(htmlDocument); //load html document

            if (htmlAgilityForMandatory != null)
            {
                htmlNodesMandatoryFieldsCollection = htmlAgilityForMandatory.DocumentNode.SelectNodes("//*[@emrmandatoryelement='1' or @emrmandatoryelement='3']"); //get EHR and EHR only Fields

                if (htmlNodesMandatoryFieldsCollection != null && htmlNodesMandatoryFieldsCollection.Count > 0)
                {
                    foreach (HtmlNode htmElementItem in htmlNodesMandatoryFieldsCollection)
                    {
                        if (VerifyValueExistsInHTMLElement(htmElementItem, htmlAgilityForMandatory) == false)
                        {
                            MandatoryFieldsNotFilled = true;
                            break;
                        }
                    }
                }
            }

            return MandatoryFieldsNotFilled;
        }

        #endregion


        #region     "       VERFIY HTML ELEMENT IS HAVING VALUE OR NOT           "

        /// <summary>
        ///  /// *******PURPOSE:   THIS METHOS IS USEFUL IN GETTING THE FIELD VALUES
        ///*******CREATED BY: DURGA PRASAD V
        ///*******CREATED DATE: 09/22/2017
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// <param name="htmInputElement"></param>
        /// <param name="HtmlDocument"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>
        private bool VerifyValueExistsInHTMLElement(HtmlNode htmInputElement, HtmlAgilityPack.HtmlDocument HtmlDocument)
        {
            bool blnValueExistsInField = false;
            string strFieldValue = "";
            string strOuterHtml = "";
            bool IsDepthParsingErrorOccured = false;
            try
            {
                //FOR TEXTBOX 
                if (htmInputElement.GetAttributeValue("type", "") != null && htmInputElement.Name.ToLower() == "input" && (htmInputElement.GetAttributeValue("type", "").ToLower() == string.Empty || htmInputElement.GetAttributeValue("type", "").ToLower() == "text"))
                {
                    strFieldValue = htmInputElement.GetAttributeValue("value", "");

                    //FOR CHECK BOX 
                }
                else if (htmInputElement.GetAttributeValue("type", "") != null && htmInputElement.GetAttributeValue("type", "").Trim().ToLower() == "checkbox" && htmInputElement.OuterHtml.Contains("checked"))
                {
                    strFieldValue = "true";

                    //FOR TEXT AREA 
                }
                else if (htmInputElement.Name.Trim().ToLower() == "textarea")
                {

                    try
                    {

                        if (IsDepthParsingErrorOccured == true)
                        {
                            if (HtmlDocument != null && HtmlDocument.GetElementbyId(htmInputElement.Id) != null)
                            {
                                strFieldValue = HtmlDocument.GetElementbyId(htmInputElement.Id).InnerText;
                            }
                        }
                        else
                        {
                            strFieldValue = htmInputElement.InnerText;
                        }


                    }
                    catch (Exception)
                    {
                        IsDepthParsingErrorOccured = true;

                        if (HtmlDocument != null && HtmlDocument.GetElementbyId(htmInputElement.Id) != null)
                        {
                            strFieldValue = HtmlDocument.GetElementbyId(htmInputElement.Id).InnerText;
                        }
                    }
                }
                else if (htmInputElement.Name.Trim().ToLower() == "select")
                {

                    //'modified by mahesh p on 01/07/2016 for supporting the multi select ddl 
                    if (htmInputElement.GetAttributeValue("multiple", "") != null && htmInputElement.GetAttributeValue("multiple", "").ToString().Trim().Length > 0)
                    {
                        strFieldValue = htmInputElement.GetAttributeValue("value", "").ToString();
                    }
                    else
                    {
                        if (htmInputElement.SelectNodes(".//option") != null)
                        {
                            //this xpath expression returns current node options. here DOT• refers current node 
                            foreach (HtmlNode node in htmInputElement.SelectNodes(".//option"))
                            {
                                strOuterHtml = node.OuterHtml;
                                if (strOuterHtml.Contains("selected"))
                                {
                                    strFieldValue = node.GetAttributeValue("value", "");
                                    break; // TODO: might not be correct. Was : Exit For
                                }
                            }
                        }
                    }

                }
                else if (htmInputElement.Name.Trim().ToLower() == "div" && htmInputElement.GetAttributeValue("contenteditable", "") != null && htmInputElement.GetAttributeValue("contenteditable", "").ToString().Trim().Length > 0)
                {
                    try
                    {

                        if (IsDepthParsingErrorOccured == true)
                        {
                            if (HtmlDocument != null && HtmlDocument.GetElementbyId(htmInputElement.Id) != null)
                            {
                                strFieldValue = HtmlDocument.GetElementbyId(htmInputElement.Id).InnerText;
                            }
                        }
                        else
                        {
                            strFieldValue = htmInputElement.InnerText;
                        }


                    }
                    catch (Exception)
                    {
                        IsDepthParsingErrorOccured = true;

                        if (HtmlDocument != null && HtmlDocument.GetElementbyId(htmInputElement.Id) != null)
                        {
                            strFieldValue = HtmlDocument.GetElementbyId(htmInputElement.Id).InnerText;
                        }
                    }

                }
                else if (htmInputElement.Name.Trim().ToLower() == "img")
                {
                    if (htmInputElement.OuterHtml.Contains("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABoAAAAaCAYAAACpSkzOAAACtklEQVRIie2Tz2sVZxSGn/eSGUSsZBGCi4uCpNaFtKJFRbHOfKgtCNKiVVyICNIuLGTnPyAqgiB2oa4KXZXiQmgXgpSZj7ZY0RKoWvqDrCQoZBGCqFxmJG8X93pj4tVcxIUUz+wOZ85z3vc7B97Gmx561R9DWQ2AvrL8e5mlvy5UP/BKkFgBuoA5IjQOvPtaQCHWqe19kmKRJRM2pySOWAbzbz89FrQuxPoT2wcl/Wj8OWYA2I0A87ekLUWWTC3U54WKQqwBThgPIg4VWfIkxGrG8D0AZlLSrn4gAI1eybysGsbngEego2WWPsnL6iObbwEE05ImjQ/nZdWX/T2ty8vqrNA/iItFlhBivQbzC2IQ0zJ8jHxD6BhmF2K0yJLrIdargOXANDBle1piqsjS50GhrFch9hZZchIgxLoJ/AY0OyWjRZZ83a2P9QhwGnhoWCt4/9l+treVefpzD9neZ3O+o2zQ+IpQEwPiKszdsiJLxoE9Heinti8jEMI2kjYDc0Eh1mDeKfNkKsQa25cwa4wROo+5b3ztRe8AjLVnBeOnufdg3jLYXodohrJqAJuEtgshKVr+zvI1RBZivbMnxp4QaonZDxh+DiTpM+C2YQjzJeIO4hvjMWCHrA+FhsFFL06RpzPGD7rctqoGPHNHIdYDtgEmgc3gRaAPMBvKPL3+Eru60bm9pd3B24oezAHZXiJpL7CkI3djkSUzoaxXA32BbC+XtGheemIOCFgJLMY0gYfg46GsWmDysroHLJW0DFhB+1ZmgJvAPWAEs9V4w+wOABikv7qgjuSVwHrLf8o6UORpEWI1THspmsBd2+OIn4CG0BCwzPZiwVXDGeCc5S/myRybVWS2AxvBQ0j7izyJAEWWTgI/9GNbZ+ALmD+QH2O1EC3gVrcgj9XOEKuRfhu+jf9n/AfHki0S/5bmsQAAAABJRU5ErkJggg=="))
                    {
                        strFieldValue = string.Empty;

                    }
                    else
                    {
                        strFieldValue = "imageexists";
                    }
                }
                else
                {
                    try
                    {

                        if (IsDepthParsingErrorOccured == true)
                        {
                            if (HtmlDocument != null && HtmlDocument.GetElementbyId(htmInputElement.Id) != null)
                            {
                                strFieldValue = HtmlDocument.GetElementbyId(htmInputElement.Id).InnerHtml;
                            }
                        }
                        else
                        {
                            strFieldValue = htmInputElement.InnerHtml;
                        }


                    }
                    catch (Exception)
                    {
                        IsDepthParsingErrorOccured = true;

                        if (HtmlDocument != null && HtmlDocument.GetElementbyId(htmInputElement.Id) != null)
                        {
                            strFieldValue = HtmlDocument.GetElementbyId(htmInputElement.Id).InnerHtml;
                        }
                    }
                }

                //IF VALUE EXISTS THEN MAKING VALUE EXISTS FLAG TRUE. IF FLAG RETURNS FALSE THEN WE ARE REMOVING THAT PARTICULAR ELEMENT 
                if (strFieldValue != null && strFieldValue.Trim().Length > 0)
                {
                    blnValueExistsInField = true;
                }
                else
                {
                    blnValueExistsInField = false;
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                strFieldValue = null;
                strOuterHtml = null;

            }
            return blnValueExistsInField;
        }

        #endregion
        #region "THIS METHOD IS USED IS CHECK CUSTOM MAPPED FIELD VALIDATION EXITS OR NOT"
        /// <summary>
        /// THIS METHOD IS USED IS CHECK CUSTOM MAPPED FIELD VALIDATION EXITS OR NOT
        /// </summary>
        /// <param name="htmlTemplateSavingInputModel"></param>
        /// <param name="originalTemplate"></param>
        /// <param name="ehrEasyformGuidKey"></param>
        /// <returns></returns>
        public HtmlTemplateSavingInputModel CheckCustomMappedFieldValidationExits(HtmlTemplateSavingInputModel htmlTemplateSavingInputModel, string originalTemplate, string ehrEasyformGuidKey)
        {
            EasyFormsFieldValidationsCustomizationContainerModel fieldValidationsCustomizationContainerModel = null;
            List<EasyFormsFieldsValidationsCustomizationModel> ValidateFieldList = null;


            EasyFormsFieldValidationsCustomizationContainerModel objInputModel = new EasyFormsFieldValidationsCustomizationContainerModel();
            objInputModel.FieldValidationInfo = new EasyFormsFieldsValidationsCustomizationModel();
            objInputModel.FieldValidationInfo.FillableHTMLDocumentTemplateID = htmlTemplateSavingInputModel.FillableHTMLDocumentTemplateID;
            objInputModel.ApplicationType = htmlTemplateSavingInputModel.ApplicationType;
            //  objInputModel.practicemodel = htmlTemplateSavingInputModel.practicemodel;
            new PracticeInformationCopy().Copy(objInputModel, htmlTemplateSavingInputModel);
            //  EMRWebExceptionTraceLogModel emrwebexceptiontracelogmodel = new EMRWebExceptionTraceLogModel();
            /*here we are calling this function for to Custom Mapped Validation  Fields Linked to EasyForms List*/
            fieldValidationsCustomizationContainerModel = new FieldValidationsDataAccess().EasyFormsFieldsValidationCustomizationInfoList(objInputModel);
            //if the value exits in FieldValidationInfoList then we execute if block*/
            if (fieldValidationsCustomizationContainerModel?.FieldValidationInfoList?.Count > 0)
            {
                //here we are filtering Mandatory validation Fields List
                ValidateFieldList = fieldValidationsCustomizationContainerModel.FieldValidationInfoList.Where(x => x.ValidationType == 1).ToList();
                //if value exits in  ValidateFieldList and any mandatory Fields does not have value then we execute if block*/
                if (ValidateFieldList?.Count > 0 && CheckAnyMandatoryFieldsValidationExists(ValidateFieldList, originalTemplate, ehrEasyformGuidKey))
                {
                    htmlTemplateSavingInputModel.MandatoryFieldsFilledType = 1;
                    htmlTemplateSavingInputModel.MandatoryFieldsValidationMessage = "<label> One or more <span><strong style=\"background-color: yellow;color: red;\">fields</strong></span> in the Form need to be Filled. Please fill those <span><strong style=\"background-color: yellow;color: red;\">fields</strong></span> and try again.</label>";

                    return htmlTemplateSavingInputModel;
                }

                ValidateFieldList = null;

                //here we are filtering warning Validation Fields List
                ValidateFieldList = fieldValidationsCustomizationContainerModel.FieldValidationInfoList.Where(x => x.ValidationType == 2).ToList();
                //if value exits in  ValidateFieldList and any Warning  Fields does not have value then we execute if block*/
                if (ValidateFieldList?.Count > 0 && CheckAnyMandatoryFieldsValidationExists(ValidateFieldList, originalTemplate, ehrEasyformGuidKey))
                {
                    htmlTemplateSavingInputModel.WarningFieldsFilledStatusType = 1;
                    htmlTemplateSavingInputModel.WarningFieldsValidationMessage = "<label> One or more <span><strong style=\"background-color: yellow;color: red;\">fields</strong></span> in the Form need to be Filled </label>";
                    return htmlTemplateSavingInputModel;
                }
            }
            return htmlTemplateSavingInputModel;
        }
        #endregion
        #region "THIS METHOD IS USED TO CHECK ANY MANDATORY FIELD VALIDATION EXITS OR NOT"
        /// <summary>
        /// This method is used to check any mandatory Field Validation Exits or not
        /// </summary>
        /// <param name="MandatoryFieldsList"></param>
        /// <param name="htmlDocument"></param>
        /// <param name="ehrEasyformGuidKey"></param>
        /// <returns></returns>
        public bool CheckAnyMandatoryFieldsValidationExists(List<EasyFormsFieldsValidationsCustomizationModel> MandatoryFieldsList, string htmlDocument, string ehrEasyformGuidKey)
        {
            HtmlDocument htmlAgilityForMandatory = null;
            bool MandatoryFieldsNotFilled = false;
            HtmlNode htmlElement = null;

            htmlAgilityForMandatory = new HtmlDocument(); //create instance for html document
            /*here we are loading the htmlDocument into Agility pack*/
            htmlAgilityForMandatory.LoadHtml(htmlDocument);
            //if value exits in htmlAgilityForMandatory and MandatoryFieldsList then we execute if block*/
            if (htmlAgilityForMandatory != null && MandatoryFieldsList?.Count > 0)
            {
                //here we are lopping through mandatory fields list*/
                foreach (EasyFormsFieldsValidationsCustomizationModel EachFieldInfo in MandatoryFieldsList)
                {
                    /*here we are assigning field name to varible*/
                    string FieldName = EachFieldInfo.FillableHTMLDocumentTemplateFieldName;
                    //if field name is not empty then we execute if block*/
                    if (!string.IsNullOrWhiteSpace(FieldName))
                    {
                        /*here we are finding the Element in the Document based on Field Id*/
                        htmlElement = htmlAgilityForMandatory.GetElementbyId(FieldName.addEhrGuidKeytoString(ehrEasyformGuidKey));
                        /*if htmlElement exits and Field value not exits in field then we execute if block*/
                        if (htmlElement != null && VerifyValueExistsInHTMLElement(htmlElement, htmlAgilityForMandatory) == false)
                        {
                            MandatoryFieldsNotFilled = true;
                            break;
                        }
                    }

                }

            }

            return MandatoryFieldsNotFilled;
        }
        #endregion
    }

    internal class GetAndAppendElectronicalySignUserInfoToNotesBF
    {

        #region "          GET AND APPEND ELECTRONICALY SIGNED USER INFO TO NOTES     "

        public string GetAndAppendElectronicalySignUserInfoToNotes(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, string strFormattedNotes)
        {
            string strFormSavedDataInfo = string.Empty;
            string strElectronicInfo = string.Empty;


            if (htmltemplatesavinginputmodel == null || htmltemplatesavinginputmodel.isEmergencyWeb)
                return strFormattedNotes;

            strFormSavedDataInfo = htmltemplatesavinginputmodel.EasyFormsCurrentDateTime;

            if (!string.IsNullOrWhiteSpace(strFormSavedDataInfo))
                htmltemplatesavinginputmodel.EasyFormElectronicallyCreatedInfoDateTime = DateTime.Parse(strFormSavedDataInfo).ToString("MM/dd/yyyy hh:mm tt");

            strElectronicInfo = new GetNotesElectronicallySavedInfoBF().GetEasyFormElectronicallySavedInfo(htmltemplatesavinginputmodel);

            if (!string.IsNullOrWhiteSpace(strElectronicInfo))
            {
                //if (EndsWithDateTime(strElectronicInfo))
                //{
                //    strElectronicInfo = strElectronicInfo + " " + htmltemplatesavinginputmodel.PracticeTimeZoneShortName;
                //}

                return new AppendElectronicalySignedInfoToNotesBF().AppendElectronicaCreatedInformation(strFormattedNotes, strElectronicInfo);
            }
            else
                return strFormattedNotes;



        }

        #endregion

        private bool EndsWithDateTime(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // Define the format to match
            string dateTimeFormat = "MM/dd/yyyy hh:mm tt";

            // Attempt to extract the substring that should match the datetime format
            int dateTimeLength = dateTimeFormat.Length;
            if (input.Length < dateTimeLength)
                return false;

            string possibleDateTimeString = input.Substring(input.Length - dateTimeLength, dateTimeLength);

            // Try to parse the extracted substring
            DateTime parsedDateTime;
            bool isValidDateTime = DateTime.TryParseExact(
                possibleDateTimeString,
                dateTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedDateTime);

            return isValidDateTime;
        }


    }

    internal class AppendElectronicalySignedInfoToNotesBF
    {

        #region"            APPEND ELECTRONICALLY CREATED INFO              "

        public string AppendElectronicaCreatedInformation(string strFormattedNotes, string strElectronicallyCreatedText)
        {
            HtmlDocument doc = null;
            string strElectronicInfo = string.Empty;
            HtmlNode curElement = null;
            HtmlNode electronicalInfo = null;
            string strAppenedNotes = string.Empty;


            doc = new HtmlDocument(); //create instance for html document

            doc.OptionWriteEmptyNodes = false;

            if (!string.IsNullOrWhiteSpace(strFormattedNotes))
            {
                doc.LoadHtml(strFormattedNotes); //load html document
            }

            strElectronicInfo = strElectronicInfo + "";

            curElement = doc.GetElementbyId("tblEasyFormsElectronicInfo");

            curElement = null;

            curElement = doc.GetElementbyId("ELECTRONICINFO");
            curElement = null;

            curElement = doc.GetElementbyId("ELECTRONICINFONewLine");
            curElement = null;

            strElectronicInfo = strElectronicInfo + "<table id='tblEasyFormsElectronicInfo' style='width:99%;' > ";

            strElectronicInfo = strElectronicInfo + "<tr id='trEasyFormsElectronicCreatedInfo' > ";
            strElectronicInfo = strElectronicInfo + "<td id='tdEasyFormsElectronicCreatedInfo' style='padding-left:2%' > ";
            strElectronicInfo = strElectronicInfo + strElectronicallyCreatedText;
            strElectronicInfo = strElectronicInfo + "</td>";
            strElectronicInfo = strElectronicInfo + "</tr>";


            strElectronicInfo = strElectronicInfo + "</table>";

            electronicalInfo = HtmlNode.CreateNode("<span class=" + "label" + ">ECInfo</span>");

            if (strElectronicInfo.Trim().Length > 0)
            {
                HtmlNode divElement = doc.CreateElement("DIV");
                divElement.Id = "ELECTRONICINFO";

                HtmlNode spaceElement = doc.CreateElement("BR");
                spaceElement.Id = "ELECTRONICINFONewLine";

                if (doc.DocumentNode != null)
                {
                    divElement.InnerHtml = strElectronicInfo;
                    doc.DocumentNode.SelectSingleNode("//body").AppendChild(spaceElement);
                    doc.DocumentNode.SelectSingleNode("//body").AppendChild(divElement);
                }
            }

            strAppenedNotes = doc.DocumentNode.OuterHtml;


            return strAppenedNotes;
        }

        #endregion
    }

    internal class GetNotesElectronicallySavedInfoBF
    {
        // declaring the class level varible to hold the data access calss instance
        private GetNotesElectronicallySavedInfoDA _objGetElectronicalySignInfoDA;

        #region  "      Constructor Function To Create the Instance for the DA Class     "

        public GetNotesElectronicallySavedInfoBF()
        {
            _objGetElectronicalySignInfoDA = new GetNotesElectronicallySavedInfoDA();
        }

        #endregion

        #region     "         GET NOTES ELECTRONICALY SIGN USER INFO      "

        public string GetEasyFormElectronicallySavedInfo(HtmlTemplateSavingInputModel htmltemplateinputmodel)
        {
            EasyFormsElectronicallyCreatedInfoModel objEasyFormsElectronicallyCreatedInfoModel = null;

            string strElectronicInfo = "";


            objEasyFormsElectronicallyCreatedInfoModel = _objGetElectronicalySignInfoDA.GetEasyFormElectronicallySavedInfo(htmltemplateinputmodel);

            if (objEasyFormsElectronicallyCreatedInfoModel != null)
                strElectronicInfo = objEasyFormsElectronicallyCreatedInfoModel.EasyFormElectronicallyCreatedInfo;

            return strElectronicInfo;
        }


        #endregion
    }

    internal class ZipEasyFormOriginalAndFormatedBinaryBF
    {

        #region  "      Constructor Function To Create the Instance for the Class     "

        // declaring the class level varible to hold the calss instance
        private CommonFunction_EasyFormsZipAndUnZipBusinessFacade _objZipAndUnzipEasyFormDataBF;

        public ZipEasyFormOriginalAndFormatedBinaryBF()
        {
            _objZipAndUnzipEasyFormDataBF = new CommonFunction_EasyFormsZipAndUnZipBusinessFacade();
        }

        #endregion

        #region     "      ZIP EASYFORM ORIGINAL AND FORMATED BINARYS     "

        public void ZipEasyFormOriginalAndFormatedBinary(ref HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, string originalTemplate, string notesFormattedString)
        {
            byte[] notesFormattedByte = null;


            htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalTemplate));

            htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat = _objZipAndUnzipEasyFormDataBF.ZipedEasyFormInfo(htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64);

            if (htmltemplatesavinginputmodel.EasyFormDesignType != 2)
            {
                if (!string.IsNullOrWhiteSpace(notesFormattedString))
                {
                    notesFormattedByte = System.Text.Encoding.UTF8.GetBytes(notesFormattedString);
                    notesFormattedString = Convert.ToBase64String(notesFormattedByte);

                    htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesFormattedBinaryFormat = _objZipAndUnzipEasyFormDataBF.ZipedEasyFormInfo(notesFormattedString);
                    htmltemplatesavinginputmodel.EasyFormSavedNotesFormattedBinaryFormatInBase64 = notesFormattedString;
                }
            }
            else
            {
                htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesFormattedBinaryFormat = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat;
                htmltemplatesavinginputmodel.EasyFormSavedNotesFormattedBinaryFormatInBase64 = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64;
            }

        }


        #endregion
    }

    internal class SaveGTSessionAttendeeNotesLinkingForMultipleAttendeesBF
    {

        #region"        SAVE GROUP THERAPHY SESSION ATTENDEE NOTES FOR MULTIPLE ATTENDESS       "
        /// <summary>
        ///*******PURPOSE:THIS IS USED FOR SAVING THE GROUP THERAPHY SESSION SAVING OR UPDATE IN THE DATA BASE
        ///*******CREATED BY:Jaya Raju
        ///*******CREATED DATE: 04/24/2015(comments added date)
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="htmltemplatesavinginputmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>
        public ResponseModel SaveGroupTheraphySessionAttendeeNotesForMultipleAttendess(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, ref string strMultipleAtandeeIDs, ref DataTable dtGroupTheraphySessionAttendeeNotes)
        {

            ResponseModel model = null;  //Used to maintain the User Data.
                                         // DataTable dtGroupTheraphySessionAttendeeNotes = null;
            string supervisorIDs = "";
            //here we create data table for storing session attendee field values details
            //this datatable is used to hold the session attendee field values details
            //for inserting session attendee field values details we are decalre this datatable
            DataTable dtAttendeeSpecificFieldValuesList = null;

            if (htmltemplatesavinginputmodel.grouptheraphyattendeesinfomodelList != null && htmltemplatesavinginputmodel.grouptheraphyattendeesinfomodelList.Count > 0)
                //HERE BUILDING THE CREATING AND ASSIGNING THE DATA TO THE DATA-TABLE FROM THE LIST OF THE SELECTED ATTENDEES LIST
                dtGroupTheraphySessionAttendeeNotes = BuildAttendeeNotesDatatableForMulitipleAttendee(htmltemplatesavinginputmodel);

            /// By checking the attendee specific field values list is exists or not
            /// if exists then make a call to build the datatabel with column attendee id and fieldinfo if and field value
            if (htmltemplatesavinginputmodel.GTSessionNotesAttendeeSpecificFieldValuesList != null && htmltemplatesavinginputmodel.GTSessionNotesAttendeeSpecificFieldValuesList.Count > 0)
                //HERE BUILDING THE CREATING AND ASSIGNING THE DATA TO THE DATA-TABLE FROM THE LIST OF THE SELECTED ATTENDEES LIST
                dtAttendeeSpecificFieldValuesList = CreateUDTForAttendeeSpecificFieldValuesList(htmltemplatesavinginputmodel.GTSessionNotesAttendeeSpecificFieldValuesList);

            if (dtGroupTheraphySessionAttendeeNotes != null && dtGroupTheraphySessionAttendeeNotes.Rows.Count > 0)
            {
                model = new SaveGTSessionAttendeeNotesLinkingForMultipleAttendeesDA().
                                 SaveGroupTheraphySessionAttendeeNotesForMultipleAttendess(htmltemplatesavinginputmodel, dtGroupTheraphySessionAttendeeNotes, dtAttendeeSpecificFieldValuesList, ref supervisorIDs);
            }
            //IF MULTIPLE ATANDEE NOTES SAVING IS COMPLETED THEN ONLY WE ARE FORMING PATIENT IDS
            if (model != null && model.ErrorID != null && model.ErrorID == 0)
            {
                strMultipleAtandeeIDs = htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString() + ",";

                //LOOPING THROUGH EACH ROW IN ATANDEE NOTES AND FORMING PATIENT ID STRING
                foreach (DataRow drRow in dtGroupTheraphySessionAttendeeNotes.Rows)
                {
                    if (drRow["PatientID"] != null && drRow["PatientID"] != DBNull.Value)
                    {
                        strMultipleAtandeeIDs += drRow["PatientID"] + ",";
                    }
                }

                strMultipleAtandeeIDs = strMultipleAtandeeIDs.TrimEnd(',');
            }

            return model;
        }

        private DataTable BuildAttendeeNotesDatatableForMulitipleAttendee(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            DataTable dtGroupTheraphySessionAttendeeNotes = null;
            DataRow drnewRow = null;
            //int sequencenumber = 0;
            String strhtmlbasestringformat = null;
            string strPatientDataNotesNameFieldNamesData = string.Empty;

            dtGroupTheraphySessionAttendeeNotes = CreateDataTableStructure();

            if (htmltemplatesavinginputmodel != null)
            {
                foreach (GroupTheraphyAttendeesInfoModel grouptheraphyattendeesinfomodel in htmltemplatesavinginputmodel.grouptheraphyattendeesinfomodelList)
                {
                    drnewRow = dtGroupTheraphySessionAttendeeNotes.NewRow();

                    strPatientDataNotesNameFieldNamesData = "";

                    drnewRow["PatientID"] = grouptheraphyattendeesinfomodel.PatientID;

                    //HERE FIRST VERIFYING THAT THE PATIENT(ATTENDEE) IS CURRENTLY SELECTED MEANS NOTES ISOPENED FOR THAT ATTENDEE OR NOT
                    //IN CASE OPENED FOR THAT ATTENDEE THEN NO MODIFICATIONS IS DONE FOR THAT HTML INFORMATION
                    //ELSE PATIENT RELATED MODIFICATIONS ARE DONE IN THE HTML  LoadPatientInformationAndGetBinaryFormat
                    //strhtmlbasestringformat = ReplacingDataforOtherAttendeesAndBuildDataTableforSaving(htmltemplatesavinginputmodel, grouptheraphyattendeesinfomodel.PatientName);

                    //strhtmlbasestringformat = LoadPatientInformationAndGetBinaryFormat(htmltemplatesavinginputmodel, grouptheraphyattendeesinfomodel,  ref strPatientDataNotesNameFieldNamesData);

                    if (!string.IsNullOrWhiteSpace(strhtmlbasestringformat))
                    {

                        CommonFunction_EasyFormsZipAndUnZipBusinessFacade objUnzipEasyFormData = new CommonFunction_EasyFormsZipAndUnZipBusinessFacade();
                        {
                            byte[] notesFormattedByte = System.Text.Encoding.UTF8.GetBytes(strhtmlbasestringformat);
                            string notesFormattedString = Convert.ToBase64String(notesFormattedByte);
                            drnewRow["Documents_Fillable_HTML_Templates_PatientData_BinaryFormat"] = objUnzipEasyFormData.ZipedEasyFormInfo(notesFormattedString);
                        }
                        if (!string.IsNullOrWhiteSpace(strPatientDataNotesNameFieldNamesData))
                        {
                            drnewRow["Documents_Fillable_HTML_Templates_PatientData_NotesName_FieldNames_Data"] = strPatientDataNotesNameFieldNamesData;
                        }
                    }
                    dtGroupTheraphySessionAttendeeNotes.Rows.Add(drnewRow);
                }
            }

            return dtGroupTheraphySessionAttendeeNotes;
        }


        /// <summary>
        /// *******PURPOSE:THIS IS USED FOR CREATING THE DATA TABLE STRUCTURE ACCORDING TO THE UDT OF THE SAVING SP OF ATTENDEE NOTES
        ///*******CREATED BY:Jaya Raju
        ///*******CREATED DATE: 04/24/2015
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>

        public DataTable CreateDataTableStructure()
        {
            DataTable dtGroupTheraphySessionAttendeeNotes = null;


            dtGroupTheraphySessionAttendeeNotes = new DataTable();
            dtGroupTheraphySessionAttendeeNotes.Columns.Add("PatientID", typeof(int));
            dtGroupTheraphySessionAttendeeNotes.Columns.Add("Documents_Fillable_HTML_Templates_PatientData_BinaryFormat", typeof(byte[]));
            dtGroupTheraphySessionAttendeeNotes.Columns.Add("Documents_Fillable_HTML_Templates_PatientData_NotesName_FieldNames_Data", typeof(string));

            return dtGroupTheraphySessionAttendeeNotes;
        }

        #region "               CREATE UDT FOR THE ATTENDEE SPECIFIC FIELDS VALUES LIST  "
        /// <summary>
        /// THIS IS USED TO CREATE UDT FOR THE ATTENDEE SPECIFIC FIELDS VALUES LIST
        /// ADDED BY AJAY.NANDURI ONB 16-10-2019
        /// </summary>
        /// <param name="GTSessionAttendeeSpecificFieldsDataToPopulateModel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>
        private DataTable CreateUDTForAttendeeSpecificFieldValuesList(List<GTSessionAttendeeSpecificFieldsDataToPopulateModel> GTSessionAttendeeSpecificFieldsValuesList)
        {
            //here we create data table for storing session attendee patients linked field details
            //this datatable is used to hold the linked field details for the patients
            //for inserting session attendee field linked details we are decalre this datatable
            DataTable dtAttendeeSpecificFieldValuesList = null;
            DataRow dRow = null;

            //here we create intilizing Datatable for storing session attendee patients linked field details
            //this datatable is used to hold the linked field details for the patients
            //for inserting session attendee field linked details we are decalre this datatable
            dtAttendeeSpecificFieldValuesList = new DataTable();
            //here we add columns in the data Table for storing field details for the patient
            //here we add field Id and field value and pateint id columns for storing linked fields
            //for the respective patient in the group therapy session attendee notes
            dtAttendeeSpecificFieldValuesList.Columns.Add("EasyForms_SessionNotes_AttendeeSpecific_Field_InfoID", typeof(int));//here we add field Id column
            dtAttendeeSpecificFieldValuesList.Columns.Add("EasyForms_SessionNotes_AttendeeSpecific_FieldValue", typeof(string));//here we add field value column
            dtAttendeeSpecificFieldValuesList.Columns.Add("PatientID", typeof(int));//here we add patient id column in the datatable

            foreach (GTSessionAttendeeSpecificFieldsDataToPopulateModel eachField in GTSessionAttendeeSpecificFieldsValuesList)
            {
                dRow = dtAttendeeSpecificFieldValuesList.NewRow();
                dRow["EasyForms_SessionNotes_AttendeeSpecific_Field_InfoID"] = eachField.AttendeeSpecificFieldInfoID;//here we are assigning field info id into the data row
                dRow["EasyForms_SessionNotes_AttendeeSpecific_FieldValue"] = eachField.AttendeeSpecificFieldValue;//here we are assigning field valuye into the data row
                dRow["PatientID"] = eachField.AttendeeID;//here we are assigning patient id into the row
                                                         //here we are adding the row which has consists of field details of patient into the datatable                           
                dtAttendeeSpecificFieldValuesList.Rows.Add(dRow);

            }

            //here we return the datatable which has formed with field details of the patient
            return dtAttendeeSpecificFieldValuesList;
        }
        #endregion


        #endregion
    }

    internal class SendEmailBasedOnLetterTemplateTypeBF
    {

        // declaring the class level varible to hold the calsses instance
        private LettersSentStatusDetailsSaveUpdateBF _objLettersSentDetailsSaveBF;

        #region  "      Constructor Function To Create the Instance for the other which are used in this Class     "

        public SendEmailBasedOnLetterTemplateTypeBF()
        {
            _objLettersSentDetailsSaveBF = new LettersSentStatusDetailsSaveUpdateBF();
        }

        #endregion


        #region         "     END EMAIL BASED ON LETTER TEMPLATE TYPE         "

        public ResponseModel ExecuteLetterTemplateDetails(HtmlTemplateSavingInputModel objSavingModel)
        {
            ResponseModel model = null;  //Used to maintain the User Data.
            SaveOrSendMessageContainerModel objSaveOrSendMessageContainerModel = null;
            SaveOrSendMessageIPModel emailModel = null;
            LettersModel objLettersModel = null;

            if (objSavingModel.LetterTemplateInfo == null) return model;

            //check for Letter Send type is Email or not , If it is Email then we will Send Email
            if (objSavingModel.LetterTemplateInfo.LetterSendType == 6) //Email
            {

                if (objSavingModel.isEmergencyWeb) return model;

                if (objSavingModel.LetterTemplateInfo == null) return model;

                objSaveOrSendMessageContainerModel = new SaveOrSendMessageContainerModel();
                // objSaveOrSendMessageContainerModel.practicemodel = objSavingModel.practicemodel;
                new PracticeInformationCopy().Copy(objSaveOrSendMessageContainerModel, objSavingModel);
                objSaveOrSendMessageContainerModel.listSaveOrSendMessageIPModel = new List<SaveOrSendMessageIPModel>();

                emailModel = new SaveOrSendMessageIPModel();
                emailModel.MessageID = 0;
                emailModel.RootMessageID = 0;
                emailModel.Subject = objSavingModel.LetterTemplateInfo.LetterTemplateName;
                emailModel.Message = "";
                emailModel.Priority = "Normal";
                emailModel.FromUserID = 0;
                emailModel.MessageSentToIDs = "0";
                emailModel.MessageSentCCIDs = "";
                emailModel.MessageFolder = 1;//1 - Inbox
                emailModel.MessageStartPoint = 1;
                emailModel.MessageType = 1;
                emailModel.MessageSentEmailIDs = objSavingModel.LetterTemplateInfo.PatientEmail;
                emailModel.MessageSentCCEmailIDs = "";
                emailModel.DocumentID = "";
                emailModel.EmailSourceFrom = 1;//1 - Letter //2 - Billing pring ; 3 - Billing double daller
                emailModel.LoggedFacilityID = objSavingModel.LoggedFacilityID;
                emailModel.SentLetterID = objSavingModel.DocumentsFillableHTMLTemplatesPatientDataID;
                emailModel.LetterAttachmentName = objSavingModel.LetterTemplateInfo.LetterTemplateName + ".pdf";

                emailModel.lettersEmailSendLetterAsType = 2;//Send Letter as 1 - Email Attachment; 2 - email body

                objSaveOrSendMessageContainerModel.listSaveOrSendMessageIPModel.Add(emailModel);
                objSaveOrSendMessageContainerModel.LoggedFacilityID = objSavingModel.LoggedFacilityID;
                objSaveOrSendMessageContainerModel.EHREasyFormsCommonJSFileName = objSavingModel.EHREasyFormsCommonJSFileName;
                objSaveOrSendMessageContainerModel.ApplicationType = objSavingModel.ApplicationType;
                string requestURL = HttpContext.Current.Request.Url.Scheme + @"://" + HttpContext.Current.Request.Url.Host + "/EHR_EasyFormPrint_API/EasyFormPrint/GetSentLettersandAttachmentMergedFormatList";
                string strjson = JsonConvert.SerializeObject(objSaveOrSendMessageContainerModel);

                string settingsresponse = new Cls_RestApiCalling().CallPostService(requestURL, strjson);

                if (settingsresponse != null)
                {
                    model = JsonConvert.DeserializeObject<ResponseModel>(settingsresponse);
                }
                // model = _objLettersSendEmailBF.SendExternalEmailFromLetters(objSaveOrSendMessageContainerModel);
            }

            // ***  UPDATE SEND STATUS BLOCK START*****
            //Print: 1,
            //Export: 2,
            //Fax: 3,
            //Upload: 4,
            //SAVED: 5,
            //EMAIL: 6,
            //DELETE: 7,
            objLettersModel = new LettersModel();
            //objLettersModel.practicemodel = objSavingModel.practicemodel;
            new PracticeInformationCopy().Copy(objLettersModel, objSavingModel);
            objLettersModel.SentLetterID = objSavingModel.DocumentsFillableHTMLTemplatesPatientDataID;
            objLettersModel.LetterStatus = objSavingModel.LetterTemplateInfo.LetterSendType.ToString();
            objLettersModel.LetterActionDoneNavigationID = objSavingModel.EasyFormSavingModelNavigationFrom;

            model = _objLettersSentDetailsSaveBF.InsertLetterSentStatusDetails(objLettersModel);

            return model;
        }
        #endregion
    }

    internal class LettersSentStatusDetailsSaveUpdateBF
    {
        private LettersSentStatusDetailsSaveUpdateDA _LettersSentStatusDetailsSaveUpdateDA;
        public LettersSentStatusDetailsSaveUpdateBF()
        {
            _LettersSentStatusDetailsSaveUpdateDA = new LettersSentStatusDetailsSaveUpdateDA();
        }
        #region  " METHOD IS USED TO INSERT LETTER STATUS DATA  "
        /// *******PURPOSE:THIS METHOD IS USED TO METHOD IS USED TO INSERT LETTER STATUS DATA
        ///*******CREATED BY: PHANI KUMAR M
        ///*******CREATED DATE: 7 TH MARCH 2016
        /// *******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************

        public ResponseModel InsertLetterSentStatusDetails(LettersModel lettersmodel)
        {

            return _LettersSentStatusDetailsSaveUpdateDA.InsertLetterSentStatusDetails(lettersmodel);
        }

        #endregion
    }


    internal class UploadEasyFormNotesBinarytoGcloudBF
    {
        #region         "         UPLOAD EASYFORM NOTES BINARY TO GCLOUD      "
        /// <summary>
        /// DEVELOPED BY RAVI TEJA.P
        /// DATE:11/20/2017
        /// PURPOSE :  THIS IS USED UPLOAD EASY FORM PATIEN DTAA BINARY TO GOOGLE
        /// </summary>
        /// <param name="htmltemplatesavinginputmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        public ResponseModel htmltemplateUploadEasyFormSavedNotesBinarytoGcloud(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, Boolean SaveFormaatedDataOnly = false)
        {
            EasyFormTemplateBinaryGCloudModel googleBinaryInputModel = null;
            ResponseModel returnModel = null;

            //PREPARING INPUTS TO GOOGLE TO UPLOAD
            googleBinaryInputModel = new EasyFormTemplateBinaryGCloudModel();
            //googleBinaryInputModel.practicemodel = htmltemplatesavinginputmodel.practicemodel;
            new PracticeInformationCopy().Copy(googleBinaryInputModel, htmltemplatesavinginputmodel);
            googleBinaryInputModel.WCFRequestGUID = htmltemplatesavinginputmodel.WCFRequestGUID;

            if (SaveFormaatedDataOnly == false)
                googleBinaryInputModel.EasyFormBodyGZipBinary = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat;

            googleBinaryInputModel.EasyFormFormattedGZipBinary = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesFormattedBinaryFormat;
            if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EasyFormTemplateName))
                googleBinaryInputModel.FillableHtmlTemplateName = htmltemplatesavinginputmodel.EasyFormTemplateName;
            else
                googleBinaryInputModel.FillableHtmlTemplateName = "";

            googleBinaryInputModel.EasyFormUploadedNavigation = htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom;
            googleBinaryInputModel.FillableHTMLDocumentTemplateID = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString();

            UploadEasyFormPatientBinarytoGoogleCloudStorageBF GCloud = new UploadEasyFormPatientBinarytoGoogleCloudStorageBF();
            {
                //UPLOADING METHOD

                returnModel = GCloud.uploadEasyFormPatientBinarytoGoogleCloudStorage(googleBinaryInputModel);

                if (returnModel != null && returnModel.RequestExecutionStatus != -2)
                {
                    htmltemplatesavinginputmodel.EHRJSONRequestLogEMRDateTime = returnModel.MultipleResponseID;
                }

            }


            return returnModel;
        }
        #endregion
    }

    internal class EasyFormsInitializeFieldedSavingBusinessFacade
    {
        #region "               FIELDED SAVING             "

        /// <summary>
        ///*******PURPOSE:THIS IS USED FOR INITIATING FIELDED SAVING DLL
        ///*******CREATED BY: UDAY KIRAN V
        ///*******CREATED DATE: 12/15/2015
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="htmltemplatesavinginputmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>


        public ResponseModel EMREasyFormFieldedSaving(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {

            //EMREasyFormsFieldedSavingBot.clsEasyFormFieldedSavingDataProperties model = null;

            clsEasyFormFieldedSavingDataProperties model = null;

            ResponseModel mod = null;
            bool isErrorOccured = false;
            string requestURL = string.Empty;
            string response = null;
            string CounttoRefreshIds = string.Empty;
            ClsInputforPostServiceModel clsinputforpostservicemodel = null;
            //Boolean IsSavedLocalTempFilePathUrlDataUploadedToGoogle = false;     // Indicates the local temp file data is uploaded to google or not
            string strTempFileInfo = string.Empty;
            string strEasyFormsSavedInfoFolderPath = string.Empty;

            //emrwebexceptiontracelogmodel.AddToExecutionLog("EHR_EasyForms_BusinessFacade.HtmlTemplate", "EasyFormsInitializeFieldedSavingBusinessFacade", "EMREasyFormFieldedSaving", "EMREasyFormFieldedSaving start", ExecutionLogType.Detail);

            model = new clsEasyFormFieldedSavingDataProperties();
            model = SetFieldedSavingProperties(htmltemplatesavinginputmodel);
            try
            {

                EHREasyFormsFieldedSaving obj = new EHREasyFormsFieldedSaving(model);
                {
                    obj.EMREasyFormsFieldedSavingStart();


                }
            }
            catch (Exception)
            {
                isErrorOccured = true;
                throw;
            }

            if (htmltemplatesavinginputmodel.RunOnlyFieldedsaving) return mod;

            if (htmltemplatesavinginputmodel.IsFromEasyFormsRDP == false)
            {


                #region"                AUTO DISCHARGE HCI LINKED PROGRAM OF ESAY FORM              "

                //THIS FUNTIONS IS USED TO AUTO DISCHARGE EPISODE BASED ON HCI LINKED PROGRAM 
                HtmlTemplateDataAccess htmltemplatedataaccess = new HtmlTemplateDataAccess();
                {
                    htmltemplatedataaccess.AutoHCILinkedProgramEpisodeWhileSavingEasyForm(htmltemplatesavinginputmodel);
                }

                #endregion

                #region "           REMINDERS MARQUEE DUES REFRESH                "

                if (htmltemplatesavinginputmodel.ApplicationType != 2 &&
                    (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel?.emrurlsmodel?.EMRCentralizerSocketPrimaryURL) ||
                    !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel?.emrurlsmodel?.EMRCentralizerSocketSecondaryURL)))
                {
                    if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PatientIDs))
                    {
                        CounttoRefreshIds = htmltemplatesavinginputmodel.PatientIDs;
                    }
                    // checking patient id condtion also 
                    else if (htmltemplatesavinginputmodel?.patientchartmodel?.PatientID > 0)
                    {
                        CounttoRefreshIds = htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString();
                    }
                    if (!string.IsNullOrWhiteSpace(CounttoRefreshIds))
                        AutoRefreshUsingWebSocket(CounttoRefreshIds, htmltemplatesavinginputmodel, EHRCentralizerMessageTypes.EHREasyFormReminderDuesRefresh);
                }

                #endregion
            }

            return mod;

        }

        #endregion
        #region "               AUTO REFRESH INBOX COUNT            "

        /// <summary> 
        /// THIS METHOD IS USED TO AUTO REFRESH THE COUNT FOR INPUT GIVEN ENUMERATOR 
        /// </summary> 
        /// <param name="intAutoRefreshToCallFor"></param> 
        /// <param name="PhysicianID"></param> 
        /// <param name="baseModelData"></param> 
        public ResponseModel AutoRefreshUsingWebSocket(string ToUserIDsToRefreshInboxCount, HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, EHRCentralizerMessageTypes RefreshModuleType, int ApplicationType = 1)
        {
            string requestURL = string.Empty;
            ResponseModel responsemodel = null;


            EHRCentralizerMessage message = new EHRCentralizerMessage();
            new PracticeInformationCopy().Copy(message, htmltemplatesavinginputmodel);
            //practicemodel.emrurlsmodel.EMRCentralizerSocketPrimaryURL = practicemodel.emrurlsmodel.EMRCentralizerSocketPrimaryURL.Replace("192.168.0.56", "localhost");

            //  message.practicemodel = practicemodel;

            message.ToUserIDs = ToUserIDsToRefreshInboxCount;

            message.FromUserID = htmltemplatesavinginputmodel.LoggedUserID;

            message.Type = RefreshModuleType;

            message.MessageText = string.Empty;

            message.ApplicationType = 1;
            if (htmltemplatesavinginputmodel.emrurlsmodel != null)
            {
                message.EMRCentralizerSocketPrimaryURL = htmltemplatesavinginputmodel.emrurlsmodel.EMRCentralizerSocketPrimaryURL;

                message.EMRCentralizerSocketSecondaryURL = htmltemplatesavinginputmodel.emrurlsmodel.EMRCentralizerSocketSecondaryURL;
            }
            //calling to refresh the messages
            message.EHRCentralizerRefresh();

            return responsemodel;
        }
        #endregion

        #region"                ASSIGN FIELDED SAVING PROPERTIES             "
        private clsEasyFormFieldedSavingDataProperties SetFieldedSavingProperties(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            clsEasyFormFieldedSavingDataProperties modelProperties = null;
            string strPracUsernameAndPassword = string.Empty;
            Uri uriAddress = null;
            string internalurl = string.Empty;


            //getting current internal url address
            internalurl = HttpContext.Current.Request.Url.AbsoluteUri;

            //validating and getting domain name
            if (internalurl.Trim().Length > 0)
            {
                uriAddress = new Uri(internalurl);
                internalurl = string.Empty;
                internalurl = uriAddress.GetLeftPart(UriPartial.Authority);
            }



            modelProperties = new clsEasyFormFieldedSavingDataProperties();
            modelProperties.EasyFormDataInformation = new clsEasyFormDataVariables();
            modelProperties.EasyFormDBAccessInformation = new clsDBConnectionProperties();
            modelProperties.EasyFormDBAccessInformation.PracticeID = htmltemplatesavinginputmodel.PracticeID;
            modelProperties.EasyFormDBAccessInformation.EMRDBServerName = htmltemplatesavinginputmodel.DBServerName;
            modelProperties.EasyFormDBAccessInformation.EnableJWTToken = htmltemplatesavinginputmodel.EnableJWTToken;
            modelProperties.EasyFormDBAccessInformation.EHRRequestCallingWebAPIUrl = htmltemplatesavinginputmodel.EHRRequestCallingWebAPIUrl;
            modelProperties.EasyFormDBAccessInformation.ReportsDBName = htmltemplatesavinginputmodel.ReportsDBName;
            modelProperties.EasyFormDBAccessInformation.FacilityIDForTempUse = htmltemplatesavinginputmodel.FacilityIDForTempUse;
            modelProperties.EasyFormDBAccessInformation.ProgramIdForTempUse = htmltemplatesavinginputmodel.ProgramIdForTempUse;
            modelProperties.EasyFormDBAccessInformation.EHRPracticeFromEMailAddress = htmltemplatesavinginputmodel.EHRPracticeFromEMailAddress;
            if (htmltemplatesavinginputmodel != null)
                strPracUsernameAndPassword = "; User Id=xpert" + htmltemplatesavinginputmodel.PracticeID + ";pwd=xpert" + htmltemplatesavinginputmodel.PracticeID + "; ";

            modelProperties.EasyFormDBAccessInformation.EMRDBServerUserNameAndPassword = strPracUsernameAndPassword;
            modelProperties.EasyFormDBAccessInformation.LoggedPhysicianId = htmltemplatesavinginputmodel.LoggedUserID;
            modelProperties.EasyFormDBAccessInformation.LoggedUserId = htmltemplatesavinginputmodel.LoggedUserID;
            modelProperties.EasyFormDBAccessInformation.LoggedPhysicianName = htmltemplatesavinginputmodel.LoggedUserName;
            modelProperties.EasyFormDBAccessInformation.LoggedFacilityId = htmltemplatesavinginputmodel.LoggedFacilityID;
            modelProperties.EasyFormDBAccessInformation.EHRJSONRequestLogEMRDateTime = htmltemplatesavinginputmodel.EHRJSONRequestLogEMRDateTime;
            modelProperties.EasyFormDBAccessInformation.PracticeName = htmltemplatesavinginputmodel.PracticeName;
            modelProperties.EasyFormDBAccessInformation.QuestBusinessUnitsUsingForLoggedPractice = htmltemplatesavinginputmodel.QuestBusinessUnitsUsingForLoggedPractice;
            modelProperties.EasyFormDBAccessInformation.EHREasyFormsCommonJSFileName = htmltemplatesavinginputmodel.EHREasyFormsCommonJSFileName;
            modelProperties.EasyFormDBAccessInformation.FirstName = htmltemplatesavinginputmodel.FirstName;
            modelProperties.EasyFormDBAccessInformation.LastName = htmltemplatesavinginputmodel.LastName;
            modelProperties.EasyFormDBAccessInformation.MiddleInitial = htmltemplatesavinginputmodel.MiddleInitial;
            modelProperties.EasyFormDBAccessInformation.LoggedFacilityName = htmltemplatesavinginputmodel.LoggedFacilityName;
            modelProperties.EasyFormDBAccessInformation.PortalUserType = htmltemplatesavinginputmodel.PortalUserType;
            if (htmltemplatesavinginputmodel?.emrurlsmodel != null)
            {
                modelProperties.EasyFormDBAccessInformation.EMRWebWCFPrimaryExternalURL = htmltemplatesavinginputmodel.emrurlsmodel.EMRWebWCFPrimaryExternalURL;
                modelProperties.EasyFormDBAccessInformation.EMRWebWCFSecondaryExternalURL = htmltemplatesavinginputmodel.emrurlsmodel.EMRWebWCFSecondaryExternalURL;

                modelProperties.EasyFormDBAccessInformation.EMRCentralizerSocketPrimaryURL = htmltemplatesavinginputmodel.emrurlsmodel.EMRCentralizerSocketPrimaryURL;
                modelProperties.EasyFormDBAccessInformation.EMRCentralizerSocketSecondaryURL = htmltemplatesavinginputmodel.emrurlsmodel.EMRCentralizerSocketSecondaryURL;

                modelProperties.EasyFormDBAccessInformation.EMRWebWCFCurrentExternalURL = htmltemplatesavinginputmodel.emrurlsmodel.EMRWebWCFPrimaryExternalURL;

            }
            //  modelProperties.EasyFormDBAccessInformation.PracticeModelForFieldedSaving = htmltemplatesavinginputmodel.practicemodel;
            if (internalurl.Trim().Length > 0)
            {
                modelProperties.EasyFormDBAccessInformation.EMRWebWCFCurrentInternalURL = internalurl;
                modelProperties.EasyFormDBAccessInformation.EMRWebWCFPrimaryInternalURL = internalurl;
                modelProperties.EasyFormDBAccessInformation.EMRWebWCFSecondaryInternalURL = internalurl;
            }
            //application type whether t is portal or ehr
            modelProperties.EasyFormDBAccessInformation.ApplicationType = htmltemplatesavinginputmodel.ApplicationType;

            if (htmltemplatesavinginputmodel.ApplicationType == 1)//ehr
            {
                //user type whether it is portal patient,non patient(2) or portal external provider (1) 
                modelProperties.EasyFormDBAccessInformation.UserType = 4;//means ehr user either provider or nurse 
            }
            else//portal
            {
                //user type whether it is portal patient,non patient(2) or portal external provider (1) 
                modelProperties.EasyFormDBAccessInformation.UserType = htmltemplatesavinginputmodel.PortalUserType;
            }


            modelProperties.EasyFormDataInformation.EasyFormTemplateID = htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID;

            if (htmltemplatesavinginputmodel.patientchartmodel.AppointmentId > 0)
                modelProperties.EasyFormDataInformation.AppointmentID = (int)htmltemplatesavinginputmodel.patientchartmodel.AppointmentId;

            if (htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0)
            {
                modelProperties.EasyFormDataInformation.EasyFormIsFromGroupTherapy = true;
            }
            else
            {
                modelProperties.EasyFormDataInformation.EasyFormIsFromGroupTherapy = false;
            }



            if (htmltemplatesavinginputmodel.patientchartmodel != null && htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime != null && htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime.ToString().Trim().Length > 0)
            {
                modelProperties.EasyFormDataInformation.EasyFormDOS = Convert.ToDateTime(htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime);
                //modelProperties.EasyFormDataInformation.EasyFormDOS = CheckValidDateTimeOrnot(htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime);
            }
            else if (htmltemplatesavinginputmodel.EasyFormApptDateTime != null && htmltemplatesavinginputmodel.EasyFormApptDateTime.ToString().Trim().Length > 0)
            {
                modelProperties.EasyFormDataInformation.EasyFormDOS = Convert.ToDateTime(htmltemplatesavinginputmodel.EasyFormApptDateTime);
                //modelProperties.EasyFormDataInformation.EasyFormDOS = CheckValidDateTimeOrnot(htmltemplatesavinginputmodel.EasyFormApptDateTime);
            }


            if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0 && htmltemplatesavinginputmodel.RunOnlyEasyFormsNotesFormation == true)
            {
                modelProperties.EasyFormDataInformation.EasyFormRunOnlyNotesFormation = true;
            }

            if (htmltemplatesavinginputmodel.RunOnlyEasyFormDues == true)
            {
                modelProperties.EasyFormDataInformation.EasyFormRunOnlyReminderDues = true;
            }

            if (htmltemplatesavinginputmodel.EasyFormReminderDueUserID > 0)
            {
                modelProperties.EasyFormDataInformation.EasyFormReminderDuesUserID = htmltemplatesavinginputmodel.EasyFormReminderDueUserID;
            }

            //GroupTherapySessionType == 2 ->COUPE THERAPY
            //IF IS FROM COUPLE THERAPY THEN RUNNING FIELDED SAVING FOR MULTIPLE PATIENTS AND REMINDERS FOR ALL PATIENTS ALSO
            if (htmltemplatesavinginputmodel.GroupTherapySessionType == 2 && htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0 && !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PatientIDs))
            {
                modelProperties.EasyFormDataInformation.strPatientID = htmltemplatesavinginputmodel.PatientIDs;
            }

            else if (htmltemplatesavinginputmodel.patientchartmodel != null && htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0)
            {
                modelProperties.EasyFormDataInformation.strPatientID = htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString();
            }

            //ADDED BY S.SUDHEER(03/18/2016)
            //THIS IS USED TO CHECK FROM WHERE WE ARE RUNNING FIELDED SAVING EXE. BY USING THIS FLAG WE WILL RESTIRCT USER TO NOT CREATE CLAIM BASED ON SECTIONS MAPPING.
            modelProperties.EasyFormDataInformation.isFieldedSavingFromWeb = true;
            modelProperties.EasyFormDataInformation.IsCustomizedWaterMarkExist = htmltemplatesavinginputmodel.IsCustomizedWaterMarkExist;
            modelProperties.EasyFormDataInformation.IsEasyFormGoldenThreadNeedsDxCodesSavingRequired = htmltemplatesavinginputmodel.IsEasyFormGoldenThreadNeedsDxCodesSavingRequired;

            modelProperties.EasyFormDataInformation.IsEasyFormOtherOrdersModuleRequired = htmltemplatesavinginputmodel.IsEasyFormOtherOrdersModuleRequired;

            if (htmltemplatesavinginputmodel.ApplicationType == 1)
            {
                modelProperties.EasyFormDataInformation.IsChangePatientStatusBasedOnEasyFormSaveActionsRequired = htmltemplatesavinginputmodel.IsChangePatientStatusBasedOnEasyFormSaveActionsRequired;
            }

            if (htmltemplatesavinginputmodel != null && htmltemplatesavinginputmodel.IsFromEasyFormsRDP == true)
                modelProperties.EasyFormDataInformation.isFieldedSavingFromWeb = false;

            //modelProperties.EasyFormDataInformation.strEasyFormSavedNotesOriginal = htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormatInBase64;
            //modelProperties.EasyFormDataInformation.strEasyFormSavedNotesFormatted = htmltemplatesavinginputmodel.EasyFormSavedNotesFormattedBinaryFormatInBase64;

            // ASSIGNING WHEN TO PERFORM ID , THIS IS USED TO EXECUTING EASY FORM REMINDERS - ADDED BY AHMED BASHA SHAIK 12/06/2018
            // WHEN IS SIGNED OFF FLAG IS TRUE , THEN IT IS "COMPLETED OR FINAL SIGN OFF"
            if (htmltemplatesavinginputmodel.IsSignedOff == true)
                modelProperties.EasyFormDataInformation.RemindersWhenToPerform = 8; // EasyForm_Signed_Action_Performed = 8
            else if (htmltemplatesavinginputmodel.IsSignedOff == false)
                modelProperties.EasyFormDataInformation.RemindersWhenToPerform = 7;// EasyForm_Saved_as_Draft_Action_Performed: 7

            //  FOR UPLOAD TO PORTAL FOR COSIGN WE HAVE TO PASS PATIENT DATA ID AND 
            //  SOME OTHER INFO TO TELL RUN SOME SPECIFIC METHODS 
            modelProperties.EasyFormDataInformation.FieldedSavingExeRunSpecificActions = htmltemplatesavinginputmodel.FieldedSavingExeRunSpecificActions;
            modelProperties.EasyFormDataInformation.EasyFormPatientDataID = htmltemplatesavinginputmodel.PatientDataIDForRunningSpecificActions;
            // assignin the current api request handeled host address 
            // based on this we are getting binary physical path or using webclient
            modelProperties.EasyFormDataInformation.ApplicationhostAddress = HttpContext.Current.Request.Url.Host;


            return modelProperties;
        }

        private DateTime CheckValidDateTimeOrnot(string dateString)
        {

            string format = "MM/dd/yyyy hh:mm tt";
            DateTime dateTime;
            dateTime = DateTime.ParseExact(dateString, format, System.Globalization.CultureInfo.InvariantCulture);
            return dateTime;
        }
        #endregion
    }


    #endregion

    #region"    Data Access"

    internal class EasyFormNotesSaveDA
    {
        private SetReqInputsForNotesSaveOrUpdateDA _objReqInputsForNotesSaveDA;
        private SetPNFS_StatusFlagsForNotesSaveOrUpdateDA _objPnfsStatusFlagsForNotesSaveDA;

        #region     "         CONSTRUCTOR      "

        public EasyFormNotesSaveDA()
        {
            _objReqInputsForNotesSaveDA = new SetReqInputsForNotesSaveOrUpdateDA();
            _objPnfsStatusFlagsForNotesSaveDA = new SetPNFS_StatusFlagsForNotesSaveOrUpdateDA();
        }

        #endregion

        #region "             SAVE HTML TEMPLATE              "

        /// <summary>
        ///  *******PURPOSE:THIS IS USED FOR SAVING THE HTML BINARY FORMAT ALONG WITH THE USER ENTERED INFORMATION
        ///*******CREATED BY:SIVA PRASAD
        ///*******CREATED DATE: 08/09/2014(comments added date)
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="htmltemplatesavinginputmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>

        public ResponseModel SaveHtmlTemplate(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel,
                                                ref string FormDOSForReminders,
                                                DataTable dtSupervisorsInfo, HtmlTemplateSavingDetailsModel htmlTemplateSavingDetailsModel, DataTable EasyFormPtDetails)
        {
            ResponseModel model = null;
            // DataTable dtMedReconInfo=null;
            //string strMedReconMandatoryValidationInfo = null;

            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);
                    //usp_Documents_Fillable_HTML_Templates_PatientData_Insert
                    using (SqlCommand command = new SqlCommand("usp_EasyForms_PatientData_Main_Insert", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@UserType", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.PortalUserType > 0 && htmltemplatesavinginputmodel.ApplicationType == 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@UserType"], htmltemplatesavinginputmodel.PortalUserType.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@UserType"], "4");//4 means ehr provider


                        command.Parameters.Add(new SqlParameter("@Fillable_HTML_DocumentTemplateID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Fillable_HTML_DocumentTemplateID"], htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID.ToString());

                        command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.GroupTherapySessionType == 2 && htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0)//GroupTherapySessionType == 2 couple therapy
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], null);
                        }
                        else if (htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString());
                        }
                        else
                        {
                            command.Parameters["@PatientID"].Value = DBNull.Value;
                        }

                        command.Parameters.Add(new SqlParameter("@PatientIDs", SqlDbType.VarChar, 8000));
                        if (htmltemplatesavinginputmodel.GroupTherapySessionType == 2 && htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0)//GroupTherapySessionType == 2 couple therapy
                        {
                            commonhelper.SetSqlParameterValue(command.Parameters["@PatientIDs"], htmltemplatesavinginputmodel.PatientIDs.ToString());
                        }
                        else
                        {
                            command.Parameters["@PatientIDs"].Value = DBNull.Value;
                        }

                        command.Parameters.Add(new SqlParameter("@AppointmentID", SqlDbType.Int));
                        //added by sudheer.kommuri on 18/01/2020
                        //when the user has one to one appointment and form is opened  with Selected appointment from group therapy encounter pop up then we have sending both one to one appontment id and group therapy id
                        //but due to this some issue is occuring for billing team so they want only selected appointment id from Group therapy encounter pop up and one to one appointment id   should be null
                        //so here we are assigning zero to the AppointmentId when only appointment selected from Group therapy encounter pop up
                        //by using this IsGroupTherapyEncounterPopUpOpened flag we can know group therapy encounter pop up open are not    
                        if (htmltemplatesavinginputmodel.IsGroupTherapyEncounterPopUpOpened == true)
                        {
                            command.Parameters["@AppointmentID"].Value = DBNull.Value;

                        }
                        //otherwise we are go with the normal flow of execution
                        else
                        {

                            if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0 && (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 77 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 21))
                            {
                                command.Parameters["@AppointmentID"].Value = DBNull.Value;
                            }
                            else if (htmltemplatesavinginputmodel.ClientSelectedAppointmentId > 0)
                            {
                                commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentID"], htmltemplatesavinginputmodel.ClientSelectedAppointmentId.ToString());
                            }
                            else if (htmltemplatesavinginputmodel.patientchartmodel.AppointmentId != null && htmltemplatesavinginputmodel.patientchartmodel.AppointmentId > 0)
                            {
                                //ISSUE : IF EASY FORM SAVED FROM GROUP THERAPHY / ATTENDY NOTES, CLAIMS ARE CREATING
                                //So from 11/08/2018 we are Not Saving One - One Appointment ID from below Navigations
                                //9 	-  	Group Theraphy Create New
                                //10 	- 	Group Theraphy Edit
                                //36	-	GROUPTHERAY SESSIONNOTES CREATE NEW MODE
                                //37	-	GROUPTHERAY SESSIONNOTE EDIT MODE
                                //59    -   ATTENDEE NOTES OPEN AS NEW 
                                //60    -   SESSION NOTES OPEN AS NEW
                                if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0 && (
                                    htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 9 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 10 ||
                                    htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 36 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 37 ||
                                    htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 59 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 60))
                                {
                                    command.Parameters["@AppointmentID"].Value = DBNull.Value;
                                }
                                else
                                {
                                    commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentID"], htmltemplatesavinginputmodel.patientchartmodel.AppointmentId.ToString());
                                }
                            }
                            else
                                command.Parameters["@AppointmentID"].Value = DBNull.Value;

                        }


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientData_NotesName_FieldNames_Data", SqlDbType.VarChar, -1));
                        commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_PatientData_NotesName_FieldNames_Data"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesNameFieldNamesData);

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_Binary_Formatting_Required_Status", SqlDbType.Int));
                        command.Parameters["@Documents_Fillable_HTML_Templates_Binary_Formatting_Required_Status"].Value = 1;


                        command.Parameters.Add(new SqlParameter("@InPat_GroupTherapy_Session_InfoID", SqlDbType.Int));
                        //added by sudheer.kommuri on 18/01/2020
                        //when the patient has group therapy appointment and user has opened the patient chart from group therapy View attende List  and he select appointment from one to one encounter pop then we have sending both AppointmentGroupTherapySessionInfoID and appointment id while saving 
                        //but due to this some issue is occuring for billing team so they want only selected appointment id from one to one appontment encounter pop up and Group therapy session appointment ids should be null
                        //so here we are assigning zero to the InPatGroupTherapySessionInfoID when only appointment selected from one to one appointment encounter pop up
                        //by using this IsOnetoOneAppointmentEncounterPopupOpened flag we can know one to one appointment encounter pop up open are not
                        if (htmltemplatesavinginputmodel.IsOnetoOneAppointmentEncounterPopupOpened == true)
                        {
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], null);
                        }
                        //otherwise we are executing normal flow
                        else
                        {
                            if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0 && (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 77 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 21))
                            {
                                commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], null);
                            }
                            else if (htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0 && htmltemplatesavinginputmodel.ApplicationType == 1)
                            {
                                commonhelper.SetSqlParameterInt32(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID.ToString());
                            }
                            else
                            {
                                commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], null);
                            }
                        }



                        command.Parameters.Add(new SqlParameter("@ApplicationType", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ApplicationType.ToString() != null)
                        {
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@ApplicationType"], htmltemplatesavinginputmodel.ApplicationType);
                        }
                        else
                        {
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@ApplicationType"], 1);// IF NULL DEFAULT WE ARE PASSING 1  MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION
                        }



                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataIDLogID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataLogID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataIDLogID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataLogID.ToString());
                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataIDLogID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@SignOffActionTypePerformed", SqlDbType.Int));
                        //previously we are not given sign off for easyforms in portal,so other than the portal only if sign off action is done  we have assigning ButtonClickActionType is 11
                        //but we have give sign off  for consent forms in portal ,so we have checking the sign off action from portal or not code is removed
                        //if the sign off is done and ButtonClickActionType is SIGNOFFANDMOVETOUC then we are assigning 11 to SignOffActionTypePerformed sql parameter
                        // if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.practicemodel.ApplicationType != 2 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)BtnClickType.SIGNOFFANDMOVETOUC)
                        if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOUC)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "11");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType != 2 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOBACKWARD)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "12");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType == 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "9");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType != 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "1");
                        else
                            command.Parameters["@SignOffActionTypePerformed"].Value = 0;// IF NULL DEFAULT WE ARE PASSING 0  MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION

                        command.Parameters.Add(new SqlParameter("@ReferToID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ReferToID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ReferToID"], htmltemplatesavinginputmodel.ReferToID.ToString());

                        else
                            command.Parameters["@ReferToID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@CreatedOrModifiedNavigationID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@CreatedOrModifiedNavigationID"], htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom.ToString());

                        else
                            command.Parameters["@CreatedOrModifiedNavigationID"].Value = 0; // IF NULL DEFAULT WE ARE PASSING 0  MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_ClientInternalIP", SqlDbType.VarChar, 256));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.clientSystemLocalIP))
                            commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_ClientInternalIP"], htmltemplatesavinginputmodel.clientSystemLocalIP);

                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_ClientInternalIP"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_IPAddress", SqlDbType.VarChar, 256));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.clientIP))
                            commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_IPAddress"], htmltemplatesavinginputmodel.clientIP);

                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_IPAddress"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@EasyFormSaved_BehaviourType", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormSavingModelBehaviourType > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyFormSaved_BehaviourType"], htmltemplatesavinginputmodel.EasyFormSavingModelBehaviourType.ToString());
                        }
                        else
                        {
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EasyFormSaved_BehaviourType"], 1); // IF NULL DEFAULT WE ARE PASSING 1  MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION
                        }

                        command.Parameters.Add(new SqlParameter("@IsSavedFrom_OffLineAndroid", SqlDbType.Bit));
                        if (htmltemplatesavinginputmodel.easyformIsOfflineSync == true)
                        {
                            commonhelper.SetSqlParameterBit(command.Parameters["@IsSavedFrom_OffLineAndroid"], htmltemplatesavinginputmodel.easyformIsOfflineSync);
                        }
                        else
                        {
                            commonhelper.SetSqlParameterBit(command.Parameters["@IsSavedFrom_OffLineAndroid"], false);// IF NOT TRUE DEFAULT WE ARE PASSING FALSE  MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION
                        }


                        //used to hold episode info
                        command.Parameters.Add(new SqlParameter("@EasyForm_InPat_CareLevel_Event_Patient_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.patientchartmodel.LatestEpisodeInfoID > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_InPat_CareLevel_Event_Patient_InfoID"], htmltemplatesavinginputmodel.patientchartmodel.LatestEpisodeInfoID.ToString());
                        }
                        else
                        {
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EasyForm_InPat_CareLevel_Event_Patient_InfoID"], null);
                        }

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************
                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"].Direction = ParameterDirection.Output;

                        command.Parameters.Add(new SqlParameter("@SupervisorIDs", SqlDbType.VarChar, 1024));
                        command.Parameters["@SupervisorIDs"].Direction = ParameterDirection.Output;

                        command.Parameters.Add(new SqlParameter("@DateOfService", SqlDbType.VarChar, 256));
                        command.Parameters["@DateOfService"].Direction = ParameterDirection.Output;

                        command.Parameters.Add(new SqlParameter("@PatientPortalDiseaseQuestionsAnswersInfoID", SqlDbType.Int));

                        if (htmltemplatesavinginputmodel.PatientPortalDiseaseQuestionsAnswersInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@PatientPortalDiseaseQuestionsAnswersInfoID"], htmltemplatesavinginputmodel.PatientPortalDiseaseQuestionsAnswersInfoID.ToString());
                        else
                            command.Parameters["@PatientPortalDiseaseQuestionsAnswersInfoID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@EasyForms_LinkedDocuments_InfoIDs", SqlDbType.VarChar, 2048));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EasyFormLinkedDocumentInfoIDs))
                            commonhelper.SetSqlParameterValue(command.Parameters["@EasyForms_LinkedDocuments_InfoIDs"], htmltemplatesavinginputmodel.EasyFormLinkedDocumentInfoIDs);

                        else
                            command.Parameters["@EasyForms_LinkedDocuments_InfoIDs"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@EasyForms_ChiefComplaints_Templates_Linked_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ChiefComplaintsTemplatesLinkedInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_ChiefComplaints_Templates_Linked_InfoID"], htmltemplatesavinginputmodel.ChiefComplaintsTemplatesLinkedInfoID.ToString());
                        else
                            command.Parameters["@EasyForms_ChiefComplaints_Templates_Linked_InfoID"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@LinkedActivityID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormsLinkedActivityID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@LinkedActivityID"], htmltemplatesavinginputmodel.EasyFormsLinkedActivityID.ToString());
                        else
                            command.Parameters["@LinkedActivityID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@Activity_Type", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormsActivityType > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Activity_Type"], htmltemplatesavinginputmodel.EasyFormsActivityType.ToString());
                        else
                            command.Parameters["@Activity_Type"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@LoggedFacilityID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.LoggedFacilityID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@LoggedFacilityID"], htmltemplatesavinginputmodel.LoggedFacilityID.ToString());
                        else
                            command.Parameters["@LoggedFacilityID"].Value = 0;// IF NULL DEFAULT WE ARE PASSING 0 MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION


                        if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 43)//SURVEYFORMSCREATENEW
                        {
                            command.Parameters.Add(new SqlParameter("@EasyForms_SurveyForms_SentInfo_Client_InfoID", SqlDbType.Int));
                            if (htmltemplatesavinginputmodel.SurveyClientRequestSentInfoId > 0)
                                commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_SurveyForms_SentInfo_Client_InfoID"], htmltemplatesavinginputmodel.SurveyClientRequestSentInfoId.ToString());
                            else
                                command.Parameters["@EasyForms_SurveyForms_SentInfo_Client_InfoID"].Value = DBNull.Value;
                        }


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientData_Log_GUID", SqlDbType.VarChar, 256));
                        commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_PatientData_Log_GUID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataLogGUID);

                        command.Parameters.Add(new SqlParameter("@MandatoryFieldsFilled_Type", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.MandatoryFieldsFilledType > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@MandatoryFieldsFilled_Type"], htmltemplatesavinginputmodel.MandatoryFieldsFilledType.ToString());
                        else
                            command.Parameters["@MandatoryFieldsFilled_Type"].Value = 3; //default value means No Validation Performed


                        command.Parameters.Add(new SqlParameter("@PrivateComments", SqlDbType.VarChar, 1024));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.AutoForwardBackwardPrivateComments))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PrivateComments"], htmltemplatesavinginputmodel.AutoForwardBackwardPrivateComments);
                        else
                            command.Parameters["@PrivateComments"].Value = "";


                        command.Parameters.Add(new SqlParameter("@PrivateCommentsForMe", SqlDbType.VarChar, 1024));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.AutoForwardBackwardPrivateCommentstoAuthor))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PrivateCommentsForMe"], htmltemplatesavinginputmodel.AutoForwardBackwardPrivateCommentstoAuthor);

                        else
                            command.Parameters["@PrivateCommentsForMe"].Value = "";


                        command.Parameters.Add(new SqlParameter("@PublicComments", SqlDbType.VarChar, 1024));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.AutoForwardBackwardPublicComments))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PublicComments"], htmltemplatesavinginputmodel.AutoForwardBackwardPublicComments);

                        else
                            command.Parameters["@PublicComments"].Value = DBNull.Value;

                        // ===================== ASSIGING IMPRESONATE DATA BLOCK START ==========================================

                        command.Parameters.Add(new SqlParameter("@EHR_User_Impersonate_Audit_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ehrIsImpersonatedUserAuditID > 0)
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EHR_User_Impersonate_Audit_InfoID"], htmltemplatesavinginputmodel.ehrIsImpersonatedUserAuditID);
                        else
                            command.Parameters["@EHR_User_Impersonate_Audit_InfoID"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@EasyForms_Electronically_Saved_OriginalUserID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ehrImpersonateLoggedUserID > 0)
                        {
                            if (htmltemplatesavinginputmodel.LoggedUserID == htmltemplatesavinginputmodel.ehrImpersonateLoggedUserID)
                                command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"].Value = DBNull.Value;
                            else
                                commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"], htmltemplatesavinginputmodel.ehrImpersonateLoggedUserID);
                        }
                        else
                            command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@EasyForms_AutoForwardTo_Users_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@EasyForms_AutoForwardTo_Users_DT"], dtSupervisorsInfo);

                        command.Parameters.Add(new SqlParameter("@IsRosDataExist", SqlDbType.TinyInt));
                        if (htmltemplatesavinginputmodel.IsRosDataExist > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@IsRosDataExist"], htmltemplatesavinginputmodel.IsRosDataExist.ToString());
                        else
                            command.Parameters["@IsRosDataExist"].Value = 0;// IF NULL DEFAULT WE ARE PASSING 0 MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION

                        command.Parameters.Add(new SqlParameter("@IsExamDataExist", SqlDbType.TinyInt));
                        if (htmltemplatesavinginputmodel.IsExamDataExist > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@IsExamDataExist"], htmltemplatesavinginputmodel.IsExamDataExist.ToString());
                        else
                            command.Parameters["@IsExamDataExist"].Value = 0;// IF NULL DEFAULT WE ARE PASSING 0 MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION

                        command.Parameters.Add(new SqlParameter("@IsTriggerCustomized", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsTriggerCustomized"], htmltemplatesavinginputmodel.IsTriggerCustomized);
                        // ===================== ASSIGING IMPRESONATE DATA BLOCK END ==========================================

                        // @TriggerCustomizedEventID
                        command.Parameters.Add(new SqlParameter("@TriggerCustomizedEventID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@TriggerCustomizedEventID"], htmltemplatesavinginputmodel.TriggerCustomizedEventID ?? null);

                        // @ChargeCaptureLoggedEventsJSON
                        command.Parameters.Add(new SqlParameter("@ChargeCaptureLoggedEventsJSON", SqlDbType.VarChar));
                        commonhelper.SetSqlParameterValue(command.Parameters["@ChargeCaptureLoggedEventsJSON"], htmltemplatesavinginputmodel.SerializedChargeCaptureLoggedEventsJSON);

                        //IsEasyFormBillable
                        command.Parameters.Add(new SqlParameter("@IsBillable", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsBillable"], htmltemplatesavinginputmodel.IsEasyFormBillable);

                        command.Parameters.Add(new SqlParameter("@IsCallEasyFormDoneByOthersBasedOnForm", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsCallEasyFormDoneByOthersBasedOnForm"], htmltemplatesavinginputmodel.IsCallEasyFormDoneByOthersBasedOnForm);

                        command.Parameters.Add(new SqlParameter("@IsCallEasyFormDoneByMeBasedOnForm", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsCallEasyFormDoneByMeBasedOnForm"], htmltemplatesavinginputmodel.IsCallEasyFormDoneByMeBasedOnForm);

                        //command.Parameters.Add(new SqlParameter("@IsRefreshEasyFormBasedReminderRuleTypes", SqlDbType.Bit));
                        //commonhelper.SetSqlParameterBit(command.Parameters["@IsRefreshEasyFormBasedReminderRuleTypes"], htmltemplatesavinginputmodel.IsRefreshEasyFormBasedReminderRuleTypes);

                        command.Parameters.Add(new SqlParameter("@FosterCare_Demographics_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormFosterHomeId > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@FosterCare_Demographics_InfoID"], htmltemplatesavinginputmodel.EasyFormFosterHomeId.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@FosterCare_Demographics_InfoID"], null);


                        command.Parameters.Add(new SqlParameter("@LocalTempOriginalURL", SqlDbType.VarChar));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.strSavedOriginalNotesLocalPathUrl))
                            commonhelper.SetSqlParameterValue(command.Parameters["@LocalTempOriginalURL"], htmltemplatesavinginputmodel.strSavedOriginalNotesLocalPathUrl);

                        else
                            command.Parameters["@LocalTempOriginalURL"].Value = "";

                        command.Parameters.Add(new SqlParameter("@LocalTempFormattedURL", SqlDbType.VarChar));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.strSavedFormattedNotesLocalPathUrl))
                            commonhelper.SetSqlParameterValue(command.Parameters["@LocalTempFormattedURL"], htmltemplatesavinginputmodel.strSavedFormattedNotesLocalPathUrl);

                        else
                            command.Parameters["@LocalTempFormattedURL"].Value = "";

                        command.Parameters.Add(new SqlParameter("@EasyForms_PatientsLinked_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@EasyForms_PatientsLinked_DT"], EasyFormPtDetails);

                        // AFTER TABLE MERGING REGARDING PORTAL OPTIMIZATION 
                        // THIS INPUT COMES ONLY FROM PORTAL NAVIGATION 
                        command.Parameters.Add(new SqlParameter("@EasyForms_Portal_UploadedForm_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormsPortalUploadedFormInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Portal_UploadedForm_InfoID"], htmltemplatesavinginputmodel.EasyFormsPortalUploadedFormInfoID.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Portal_UploadedForm_InfoID"], null);

                        //--------------
                        // Change the parameter type to varchar by AJAY ON 24-05-2019
                        command.Parameters.Add(new SqlParameter("@DOS_DateFormat", SqlDbType.VarChar, 16));
                        commonhelper.SetSqlParameterValue(command.Parameters["@DOS_DateFormat"], htmlTemplateSavingDetailsModel.EasyFormDOS_In_DateFormat.ToString());

                        // Change the parameter type to varchar by AJAY ON 24-05-2019
                        command.Parameters.Add(new SqlParameter("@DOS_AMPM_Format", SqlDbType.VarChar, 32));
                        commonhelper.SetSqlParameterValue(command.Parameters["@DOS_AMPM_Format"], htmlTemplateSavingDetailsModel.EasyFormDOS_In_DateAMPMFormat.ToString());

                        command.Parameters.Add(new SqlParameter("@ShowNotesDOSasElectronicallySavedDate", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@ShowNotesDOSasElectronicallySavedDate"], htmltemplatesavinginputmodel.ShowNotesDOSasElectronicallySavedDate);

                        command.Parameters.Add(new SqlParameter("@IsAutoCreateSuperBillWhileSaving", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsAutoCreateSuperBillWhileSaving"], htmltemplatesavinginputmodel.AutoCreateSuperBillWhileSavingCheck);

                        command.Parameters.Add(new SqlParameter("@IsAutoCreateClaimwhileSaving", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsAutoCreateClaimwhileSaving"], htmltemplatesavinginputmodel.AutoCreateClaimWhileSignOffDocuments);

                        command.Parameters.Add(new SqlParameter("@IsShowElectronicallyCreatedInformation", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsShowElectronicallyCreatedInformation"], htmltemplatesavinginputmodel.IsShowElectronicallyInfo);

                        command.Parameters.Add(new SqlParameter("@ElectrinicallyCreatedInfoDisplayType", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@ElectrinicallyCreatedInfoDisplayType"], htmltemplatesavinginputmodel.ElectronicallyCreatedInfoDisplayType.ToString());

                        //==========ASSIGN REQUIRED FIELDS FOR SAVE & SAVING STATUS FLGS BLOCK START ==============================
                        //WE ARE NOT PLACING PARAMETRS HERE ITSELF
                        //REASON 1 : WE ARE USING SAME PARAMETERS IN UPDATE SP ALSO. SO WE PLACED A SEPERATE COMMON METHOD
                        //REASON 2 : TO GET CALRITY ON WHAT WE ARE GETTING FROM DB, AS WE NEED TO REMOVE THESE PARAMS IN FUTURE ASAP
                        _objReqInputsForNotesSaveDA.Get_EasyForm_RequiredFields_ForSave(command, commonhelper, htmlTemplateSavingDetailsModel, 1);
                        _objPnfsStatusFlagsForNotesSaveDA.Get_EasyFormSaving_Status_Flags(command, commonhelper, htmlTemplateSavingDetailsModel.EasyFormSavingStatusFlags);
                        //==========ASSIGN REQUIRED FIELDS FOR SAVE & SAVING STATUS FLGS BLOCK END ==============================

                        command.Parameters.Add(new SqlParameter("@IsClinicalForm", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsClinicalForm"], htmltemplatesavinginputmodel.IsClinicalDocument);

                        command.Parameters.Add(new SqlParameter("@IsBillableForm", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsBillableForm"], htmltemplatesavinginputmodel.IsEasyFormBillable);

                        command.Parameters.Add(new SqlParameter("@WCFRequestGUID", SqlDbType.VarChar, 128));
                        commonhelper.SetSqlParameterValue(command.Parameters["@WCFRequestGUID"], htmltemplatesavinginputmodel.WCFRequestGUID);

                        // PASSING LOGGED CONTACT ID - IN CONTACT LOGIN AT THE TIME OF FINALIZE WE ARE PASSING CONTACT ID
                        command.Parameters.Add(new SqlParameter("@ContactID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.LoggedContactID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ContactID"], htmltemplatesavinginputmodel.LoggedContactID.ToString());
                        else // IN ALL NAVIGATIONS 
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ContactID"], null);

                        // Madnatory fields validation message
                        command.Parameters.Add(new SqlParameter("@Mandatory_Validation_Message", SqlDbType.VarChar));
                        commonhelper.SetSqlParameterValue(command.Parameters["@Mandatory_Validation_Message"], htmltemplatesavinginputmodel.MandatoryFieldsValidationMessage);

                        // Warning fields validation message
                        command.Parameters.Add(new SqlParameter("@Confirmation_Validation_Message", SqlDbType.VarChar));
                        commonhelper.SetSqlParameterValue(command.Parameters["@Confirmation_Validation_Message"], htmltemplatesavinginputmodel.WarningFieldsValidationMessage);

                        // Warning fields FILLED STATUS TYPE
                        command.Parameters.Add(new SqlParameter("@WarningFieldsFilled_Type", SqlDbType.TinyInt));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@WarningFieldsFilled_Type"], htmltemplatesavinginputmodel.WarningFieldsFilledStatusType.ToString());

                        // Warning fields FILLED STATUS TYPE
                        command.Parameters.Add(new SqlParameter("@IsCallingFromOfflineSync", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsCallingFromOfflineSync"], htmltemplatesavinginputmodel.winFormsFormatEasyFormOfflineSync);

                        // Warning fields FILLED STATUS TYPE
                        command.Parameters.Add(new SqlParameter("@EhrOfflineSavedEasyFormGUID", SqlDbType.VarChar, 64));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EHROfflinePatientDataGuid))
                            commonhelper.SetSqlParameterValue(command.Parameters["@EhrOfflineSavedEasyFormGUID"], htmltemplatesavinginputmodel.EHROfflinePatientDataGuid);
                        else
                            command.Parameters["@EhrOfflineSavedEasyFormGUID"].Value = DBNull.Value;

                        // OFFLINE EASYFORMS SAVED DATE TIME
                        // to identify in offline app saved jcon file creation date time
                        // if exists then pass otherwise null
                        command.Parameters.Add(new SqlParameter("@OffLineSavedDateTime", SqlDbType.DateTime));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.ehrOfflineEasyFormSavedEDateTime))
                            commonhelper.SetSqlParameterValue(command.Parameters["@OffLineSavedDateTime"], htmltemplatesavinginputmodel.ehrOfflineEasyFormSavedEDateTime);
                        else
                            command.Parameters["@OffLineSavedDateTime"].Value = DBNull.Value;

                        // set the output parameter
                        // which is helps us after easyform save methods operations
                        // which is output parameter
                        command.Parameters.Add(new SqlParameter("@EasyForms_Saved_Inputs_InfoID", SqlDbType.Int));
                        command.Parameters["@EasyForms_Saved_Inputs_InfoID"].Direction = ParameterDirection.Output;


                        command.Parameters.Add(new SqlParameter("@Original_BinaryFormat", SqlDbType.VarBinary));
                        commonhelper.SetSQLParameterByteArray(command.Parameters["@Original_BinaryFormat"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat);


                        //DIV EASY FORM GUID
                        command.Parameters.Add(new SqlParameter("@DivEasyFormsGuidKey", SqlDbType.VarChar, 20));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.easyFormsCustDivDirReplacableGuidKey))
                            commonhelper.SetSqlParameterValue(command.Parameters["@DivEasyFormsGuidKey"], htmltemplatesavinginputmodel.easyFormsCustDivDirReplacableGuidKey);
                        else
                            command.Parameters["@DivEasyFormsGuidKey"].Value = DBNull.Value;

                        // Assigning the non medication order id to indicate currently saved notes is saved to this order id
                        // which is helpful identify the notes from which it is saved and linked to which order
                        command.Parameters.Add(new SqlParameter("@PtDataId_Linked_NonMedication_OrderId", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.PtDataIdLinkedNonMedicationOrderID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@PtDataId_Linked_NonMedication_OrderId"], htmltemplatesavinginputmodel.PtDataIdLinkedNonMedicationOrderID.ToString());
                        else
                            command.Parameters["@PtDataId_Linked_NonMedication_OrderId"].Value = 0;
                        /*here we are assigning IsFromExceltoEasyFormSaving to the sql paramter
                         * by assigning this falg we can know if the EasyFor is saved from Import Excel to EasyForm or not
                         */
                        command.Parameters.Add(new SqlParameter("@IsSavedFromExcelTool", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsSavedFromExcelTool"], htmltemplatesavinginputmodel.IsFromExceltoEasyFormSaving);

                        //htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat
                        //DocumentsFillableHTMLTemplatesPatientDataNotesFormattedBinaryFormat

                        //Added By Sai srinivas busam on 10/28/2020
                        //To save the checkin time
                        command.Parameters.Add(new SqlParameter("@CheckIn_Time", SqlDbType.VarChar));
                        commonhelper.SetSqlParameterValue(command.Parameters["@CheckIn_Time"], htmltemplatesavinginputmodel.CheckInTime);


                        command.Parameters.Add(new SqlParameter("@Patient_Linked_Easyform_Auth_Data_Linking_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@Patient_Linked_Easyform_Auth_Data_Linking_DT"], GetRefAuthValidationinputDT(htmltemplatesavinginputmodel));


                        command.Parameters.Add(new SqlParameter("@PatientPayorID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PatientPayorID"], htmltemplatesavinginputmodel.PayorId);

                        command.Parameters.Add(new SqlParameter("@PayorLinkedUserType", SqlDbType.TinyInt));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PayorLinkedUserType"], htmltemplatesavinginputmodel.PayorLinkedUserType);

                        command.Parameters.Add(new SqlParameter("@Collection_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.CollectionTypeInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Collection_InfoID"], htmltemplatesavinginputmodel.CollectionTypeInfoID.ToString());
                        else
                            command.Parameters["@Collection_InfoID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@Collections_EasyFormSavedNotes_PatientsLinked_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@Collections_EasyFormSavedNotes_PatientsLinked_DT"], GetCollectionEFPatientlinkedDT(htmltemplatesavinginputmodel));

                        command.Parameters.Add(new SqlParameter("@AzureAutoSave_UniqueNumber", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.UniqueNumbertoMapAutoSaveandEasyFormInstanceTable > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@AzureAutoSave_UniqueNumber"], htmltemplatesavinginputmodel.UniqueNumbertoMapAutoSaveandEasyFormInstanceTable.ToString());
                        else
                            command.Parameters["@AzureAutoSave_UniqueNumber"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@PreferredLanguageID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EnableBasedonPatientPreferredLanguageConvertandUploadEasyFormTemplatetoPatientPortal &&
                            htmltemplatesavinginputmodel.PreferredLanguageID > 0 && !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PreferredLanguageCodeQRDA) &&
                !htmltemplatesavinginputmodel.PreferredLanguageCodeQRDA.Contains("en"))
                            commonhelper.SetSqlParameterInt32(command.Parameters["@PreferredLanguageID"], htmltemplatesavinginputmodel.PreferredLanguageID.ToString());
                        else
                            command.Parameters["@PreferredLanguageID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@ShowInViewDocumentsAfterFinalizingInPatientPortal", SqlDbType.SmallInt));
                        //we well pass the following property only from one navigation
                        //finalize action from patient portal > forms to complete
                        //we will set '1: pending' for this navigation and '3: not required' for other navigations
                        commonhelper.SetSqlParameterInt32(command.Parameters["@ShowInViewDocumentsAfterFinalizingInPatientPortal"], htmltemplatesavinginputmodel.ShowThisTemplateNotesInViewDocumentsAfterFinalizingInPatientPortal ? 1 : 3);

                        command.Parameters.Add(new SqlParameter("@PracticeTimeZoneShortName", SqlDbType.VarChar, 8));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PracticeTimeZoneShortName))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PracticeTimeZoneShortName"], htmltemplatesavinginputmodel.PracticeTimeZoneShortName);
                        else
                            command.Parameters["@PracticeTimeZoneShortName"].Value = DBNull.Value;

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();

                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);

                        //assigning the output paramter 
                        if (command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"].Value != null && command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"].Value != DBNull.Value)
                        {
                            model.ResponseID = (int)command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"].Value;
                            htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID = (int)command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"].Value;
                        }

                        if (command.Parameters["@SupervisorIDs"].Value != null && command.Parameters["@SupervisorIDs"].Value != DBNull.Value)
                        {
                            model.MultipleResponseID = command.Parameters["@SupervisorIDs"].Value.ToString();
                        }

                        if (command.Parameters["@DateOfService"].Value != null && command.Parameters["@DateOfService"].Value != DBNull.Value)
                        {
                            FormDOSForReminders = (Convert.ToDateTime(command.Parameters["@DateOfService"].Value.ToString())).ToString();
                        }

                        //// ASSIGNING THE OUTPUT PARAMTER VALUE TO SAVED INPUTS INFO ID 
                        //// chekcing the is output parameter value is exists or not
                        /// if exissts then read it to output model
                        if (command.Parameters["@EasyForms_Saved_Inputs_InfoID"].Value != null && command.Parameters["@EasyForms_Saved_Inputs_InfoID"].Value != DBNull.Value)
                        {
                            htmltemplatesavinginputmodel.EasyFormsSavedInputsInfoID = (int)command.Parameters["@EasyForms_Saved_Inputs_InfoID"].Value;
                        }
                    }
                }
            }

            return model;
        }

        #endregion

        #region "GET REF AUTH STATINMAIN DATA TABLE TO UPDATE "
        DataTable GetRefAuthValidationinputDT(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            DataTable dtRefauthInputInfo = null;
            DataRow drrow = null;

            dtRefauthInputInfo = new DataTable();
            dtRefauthInputInfo.Columns.Add("ReferalAuthId", typeof(int));
            dtRefauthInputInfo.Columns.Add("DocumentedCount", typeof(decimal));
            dtRefauthInputInfo.Columns.Add("EFStmtAuthorizationType", typeof(int));
            dtRefauthInputInfo.Columns.Add("EFStmtPrevUsedCountForThisVisit", typeof(decimal));
            dtRefauthInputInfo.Columns.Add("EFStmtNoOfVisitsLeft", typeof(decimal));


            drrow = dtRefauthInputInfo.NewRow();
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.ReferalAuthID != null)
            {
                drrow["ReferalAuthId"] = htmltemplatesavinginputmodel.RefAuthValidationModel.ReferalAuthID;
            }
            else
            {
                drrow["ReferalAuthId"] = DBNull.Value;
            }
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.DocumnetedAuthCount != null)
            {
                drrow["DocumentedCount"] = htmltemplatesavinginputmodel.RefAuthValidationModel.DocumnetedAuthCount;
            }
            else
            {
                drrow["DocumentedCount"] = DBNull.Value;
            }
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtAuthorizationType != null)
            {
                drrow["EFStmtAuthorizationType"] = htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtAuthorizationType;
            }
            else
            {
                drrow["EFStmtAuthorizationType"] = DBNull.Value;
            }
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtPrevUsedCountForThisVisit != null)
            {
                drrow["EFStmtPrevUsedCountForThisVisit"] = htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtPrevUsedCountForThisVisit;
            }
            else
            {
                drrow["EFStmtPrevUsedCountForThisVisit"] = DBNull.Value;
            }
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtNoOfVisitsLeft != null)
            {
                drrow["EFStmtNoOfVisitsLeft"] = htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtNoOfVisitsLeft;
            }
            else
            {
                drrow["EFStmtNoOfVisitsLeft"] = DBNull.Value;
            }






            dtRefauthInputInfo.Rows.Add(drrow);





            return dtRefauthInputInfo;
        }

        #endregion

        #region" GET COLLECTOIN EF PATIENT LINKED DATA TABLE"
        DataTable GetCollectionEFPatientlinkedDT(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            DataTable dtCollectionEFInfo = null;
            DataRow dtRow = null;
            dtCollectionEFInfo = new DataTable();
            dtCollectionEFInfo.Columns.Add("PatientID", typeof(int));

            if (!string.IsNullOrEmpty(htmltemplatesavinginputmodel.CollectionTypeEFLinkedPatientIDs))
            {

                foreach (string patientID in htmltemplatesavinginputmodel.CollectionTypeEFLinkedPatientIDs.Split(','))
                {
                    //Verify Claim Provider ID should be Grather than Zero.
                    if (!string.IsNullOrEmpty(patientID))
                    {
                        dtRow = dtCollectionEFInfo.NewRow();
                        dtRow["PatientID"] = Convert.ToInt32(patientID);
                        dtCollectionEFInfo.Rows.Add(dtRow);
                    }
                }


            }

            return dtCollectionEFInfo;
        }
        #endregion
    }

    internal class EasyFormNotesUpdateDA
    {
        private SetReqInputsForNotesSaveOrUpdateDA _objReqInputsForNotesSaveDA;
        private SetPNFS_StatusFlagsForNotesSaveOrUpdateDA _objPnfsStatusFlagsForNotesSaveDA;

        #region     "         CONSTRUCTOR      "

        public EasyFormNotesUpdateDA()
        {
            _objReqInputsForNotesSaveDA = new SetReqInputsForNotesSaveOrUpdateDA();
            _objPnfsStatusFlagsForNotesSaveDA = new SetPNFS_StatusFlagsForNotesSaveOrUpdateDA();
        }

        #endregion

        #region "             UPDATE HTML TEMPLATE              "

        /// <summary>
        ///  *******PURPOSE:THIS IS USED FOR UPDATING THE HTML BINARY FORMAT ALONG WITH THE USER ENTERED INFORMATION
        ///*******CREATED BY:MALINI 
        ///*******CREATED DATE: 08/22/2014
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="htmltemplatesavinginputmodel"></param>
        /// <returns></returns>

        public ResponseModel UpdateHtmlTemplate(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, ref string FormDOSForReminders,
                                                DataTable dtSupervisorsInfo, DataTable dtMoveBackwardUsers,
                                                 HtmlTemplateSavingDetailsModel htmlTemplateSavingDetailsModel,
                                                    DataTable dtEasyFormsEHRSignedURLHX, DataTable dtEasyFormsPortalSignedURLHX)
        {
            ResponseModel model = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);
                    //usp_Documents_Fillable_HTML_Templates_PatientData_Update
                    using (SqlCommand command = new SqlCommand("usp_EasyForms_PatientData_Main_Update", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientData_BinaryFormat", SqlDbType.VarBinary));
                        commonhelper.SetSQLParameterByteArray(command.Parameters["@Documents_Fillable_HTML_Templates_PatientData_BinaryFormat"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataBinaryFormat);

                        command.Parameters.Add(new SqlParameter("@DocumentWasSignedByPatient_Guarantor", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@DocumentWasSignedByPatient_Guarantor"], htmltemplatesavinginputmodel.DocumentWasSignedByPatientGuarantor);

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientData_NotesName_FieldNames_Data", SqlDbType.VarChar, -1));
                        if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesNameFieldNamesData != null)
                            commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_PatientData_NotesName_FieldNames_Data"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesNameFieldNamesData);
                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_PatientData_NotesName_FieldNames_Data"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@AppointmentID", SqlDbType.Int));
                        //added by sudheer.kommuri on 18/01/2020
                        //when the user has one to one appointment and form is opened  with Selected appointment from group therapy encounter pop up then we have sending both one to one appontment id and group therapy id
                        //but due to this some issue is occuring for billing team so they want only selected appointment id from Group therapy encounter pop up and one to one appointment id   should be null
                        //so here we are assigning zero to the AppointmentId when only appointment selected from Group therapy encounter pop up
                        //by using this IsGroupTherapyEncounterPopUpOpened flag we can know group therapy encounter pop up open are not  
                        if (htmltemplatesavinginputmodel.IsGroupTherapyEncounterPopUpOpened == true)
                        {
                            command.Parameters["@AppointmentID"].Value = DBNull.Value;
                        }
                        else
                        {
                            if (htmltemplatesavinginputmodel.patientchartmodel.AppointmentId != null && htmltemplatesavinginputmodel.patientchartmodel.AppointmentId > 0)
                                commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentID"], htmltemplatesavinginputmodel.patientchartmodel.AppointmentId.ToString());
                            else
                                command.Parameters["@AppointmentID"].Value = DBNull.Value;
                        }

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_Binary_Formatting_Required_Status", SqlDbType.Int));
                        command.Parameters["@Documents_Fillable_HTML_Templates_Binary_Formatting_Required_Status"].Value = 1;

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientData_Formatted_BinaryFormat", SqlDbType.VarBinary));
                        if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesFormattedBinaryFormat != null)
                            commonhelper.SetSQLParameterByteArray(command.Parameters["@Documents_Fillable_HTML_Templates_PatientData_Formatted_BinaryFormat"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesFormattedBinaryFormat);
                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_PatientData_Formatted_BinaryFormat"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@ApplicationType", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@ApplicationType"], htmltemplatesavinginputmodel.ApplicationType);

                        command.Parameters.Add(new SqlParameter("@SignOffActionTypePerformed", SqlDbType.Int));
                        //previously we are not given sign off for easyforms in portal,so other than the portal only if sign off action is done  we have assigning ButtonClickActionType is 11
                        //but we have give sign off  for consent forms in portal ,so we have checking the sign off action from portal or not code is removed
                        //if the sign off is done and ButtonClickActionType is SIGNOFFANDMOVETOUC then we are assigning 11 to SignOffActionTypePerformed sql parameter
                        // if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.practicemodel.ApplicationType != 2 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)BtnClickType.SIGNOFFANDMOVETOUC)
                        if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOUC)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "11");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType != 2 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOBACKWARD)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "12");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType == 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "9");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType != 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "1");
                        else
                            command.Parameters["@SignOffActionTypePerformed"].Value = 0;

                        command.Parameters.Add(new SqlParameter("@SupervisorIDs", SqlDbType.VarChar, 1024));
                        command.Parameters["@SupervisorIDs"].Direction = ParameterDirection.Output;

                        command.Parameters.Add(new SqlParameter("@DateOfService", SqlDbType.VarChar, 256));
                        command.Parameters["@DateOfService"].Direction = ParameterDirection.Output;



                        command.Parameters.Add(new SqlParameter("@ReferToID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ReferToID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ReferToID"], htmltemplatesavinginputmodel.ReferToID.ToString());
                        else
                            command.Parameters["@ReferToID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@UserType", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.PortalUserType > 0 && htmltemplatesavinginputmodel.ApplicationType == 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@UserType"], htmltemplatesavinginputmodel.PortalUserType.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@UserType"], "4");//4 means ehr provider

                        command.Parameters.Add(new SqlParameter("@CreatedOrModifiedNavigationID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@CreatedOrModifiedNavigationID"], htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom.ToString());

                        else
                            command.Parameters["@CreatedOrModifiedNavigationID"].Value = 0; // IF NULL DEFAULT WE ARE PASSING 0  MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_ClientInternalIP", SqlDbType.VarChar, 256));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.clientSystemLocalIP))
                            commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_ClientInternalIP"], htmltemplatesavinginputmodel.clientSystemLocalIP);

                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_ClientInternalIP"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_IPAddress", SqlDbType.VarChar, 256));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.clientIP))
                            commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_IPAddress"], htmltemplatesavinginputmodel.clientIP);
                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_IPAddress"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@EasyForms_LinkedDocuments_InfoIDs", SqlDbType.VarChar, 2048));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EasyFormLinkedDocumentInfoIDs))
                            commonhelper.SetSqlParameterValue(command.Parameters["@EasyForms_LinkedDocuments_InfoIDs"], htmltemplatesavinginputmodel.EasyFormLinkedDocumentInfoIDs);
                        else
                            command.Parameters["@EasyForms_LinkedDocuments_InfoIDs"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@IsSavedFrom_OffLineAndroid", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsSavedFrom_OffLineAndroid"], htmltemplatesavinginputmodel.easyformIsOfflineSync);

                        command.Parameters.Add(new SqlParameter("@LoggedFacilityID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.LoggedFacilityID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@LoggedFacilityID"], htmltemplatesavinginputmodel.LoggedFacilityID.ToString());
                        else
                            command.Parameters["@LoggedFacilityID"].Value = 0;

                        command.Parameters.Add(new SqlParameter("@EasyForm_TobeSignedBy_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormTobeSignedByInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_TobeSignedBy_InfoID"], htmltemplatesavinginputmodel.EasyFormTobeSignedByInfoID.ToString());
                        else command.Parameters["@EasyForm_TobeSignedBy_InfoID"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@MandatoryFieldsFilled_Type", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.MandatoryFieldsFilledType > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@MandatoryFieldsFilled_Type"], htmltemplatesavinginputmodel.MandatoryFieldsFilledType.ToString());
                        else
                            command.Parameters["@MandatoryFieldsFilled_Type"].Value = 3; //default value means No Validation Performed


                        command.Parameters.Add(new SqlParameter("@PrivateComments", SqlDbType.VarChar, 1024));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.AutoForwardBackwardPrivateComments))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PrivateComments"], htmltemplatesavinginputmodel.AutoForwardBackwardPrivateComments);
                        else
                            command.Parameters["@PrivateComments"].Value = "";


                        command.Parameters.Add(new SqlParameter("@PrivateCommentsForMe", SqlDbType.VarChar, 1024));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.AutoForwardBackwardPrivateCommentstoAuthor))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PrivateCommentsForMe"], htmltemplatesavinginputmodel.AutoForwardBackwardPrivateCommentstoAuthor);
                        else
                            command.Parameters["@PrivateCommentsForMe"].Value = "";


                        command.Parameters.Add(new SqlParameter("@PublicComments", SqlDbType.VarChar, 1024));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.AutoForwardBackwardPublicComments))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PublicComments"], htmltemplatesavinginputmodel.AutoForwardBackwardPublicComments);
                        else
                            command.Parameters["@PublicComments"].Value = "";

                        command.Parameters.Add(new SqlParameter("@AppointmentIDFromChangeDOS", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.AppointmentIDFromChangeDOS > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentIDFromChangeDOS"], htmltemplatesavinginputmodel.AppointmentIDFromChangeDOS.ToString());
                        else command.Parameters["@AppointmentIDFromChangeDOS"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@InPatGroupTherapySessionInfoIDFromChangeDOS", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoIDFromChangeDOS > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@InPatGroupTherapySessionInfoIDFromChangeDOS"], htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoIDFromChangeDOS.ToString());
                        else command.Parameters["@InPatGroupTherapySessionInfoIDFromChangeDOS"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@ShowSignoffButton", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@ShowSignoffButton"], htmltemplatesavinginputmodel.showSignoffFinalizebutton);

                        command.Parameters.Add(new SqlParameter("@EHR_User_Impersonate_Audit_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ehrIsImpersonatedUserAuditID > 0)
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EHR_User_Impersonate_Audit_InfoID"], htmltemplatesavinginputmodel.ehrIsImpersonatedUserAuditID);
                        else
                            command.Parameters["@EHR_User_Impersonate_Audit_InfoID"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@EasyForms_Electronically_Saved_OriginalUserID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ehrImpersonateLoggedUserID > 0)
                        {
                            if (htmltemplatesavinginputmodel.LoggedUserID == htmltemplatesavinginputmodel.ehrImpersonateLoggedUserID)
                                command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"].Value = DBNull.Value;
                            else
                                commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"], htmltemplatesavinginputmodel.ehrImpersonateLoggedUserID);
                        }
                        else
                            command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@EasyForms_AutoForwardTo_Users_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@EasyForms_AutoForwardTo_Users_DT"], dtSupervisorsInfo);

                        //command.Parameters.Add(new SqlParameter("@IsRefreshEasyFormBasedReminderRuleTypes", SqlDbType.Bit));
                        //commonhelper.SetSqlParameterBit(command.Parameters["@IsRefreshEasyFormBasedReminderRuleTypes"], htmltemplatesavinginputmodel.IsCallEasyFormDoneByMeBasedOnForm);

                        command.Parameters.Add(new SqlParameter("@IsRosDataExist", SqlDbType.TinyInt));
                        if (htmltemplatesavinginputmodel.IsRosDataExist > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@IsRosDataExist"], htmltemplatesavinginputmodel.IsRosDataExist.ToString());
                        else
                            command.Parameters["@IsRosDataExist"].Value = 0;

                        command.Parameters.Add(new SqlParameter("@IsExamDataExist", SqlDbType.TinyInt));
                        if (htmltemplatesavinginputmodel.IsExamDataExist > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@IsExamDataExist"], htmltemplatesavinginputmodel.IsExamDataExist.ToString());
                        else
                            command.Parameters["@IsExamDataExist"].Value = 0;


                        command.Parameters.Add(new SqlParameter("@EasyForms_BackWardTo_Users_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@EasyForms_BackWardTo_Users_DT"], dtMoveBackwardUsers);

                        command.Parameters.Add(new SqlParameter("@IsCallEasyFormDoneByOthersBasedOnForm", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsCallEasyFormDoneByOthersBasedOnForm"], htmltemplatesavinginputmodel.IsCallEasyFormDoneByOthersBasedOnForm);

                        command.Parameters.Add(new SqlParameter("@IsCallEasyFormDoneByMeBasedOnForm", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsCallEasyFormDoneByMeBasedOnForm"], htmltemplatesavinginputmodel.IsCallEasyFormDoneByMeBasedOnForm);

                        // ===================== ASSIGING IMPRESONATE DATA BLOCK END ==========================================

                        //@IsTriggerCustomized
                        command.Parameters.Add(new SqlParameter("@IsTriggerCustomized", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsTriggerCustomized"], htmltemplatesavinginputmodel.IsTriggerCustomized);
                        // ===================== ASSIGING IMPRESONATE DATA BLOCK END ==========================================

                        //IsEasyFormBillable
                        command.Parameters.Add(new SqlParameter("@IsBillable", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsBillable"], htmltemplatesavinginputmodel.IsEasyFormBillable);

                        // @TriggerCustomizedEventID
                        command.Parameters.Add(new SqlParameter("@TriggerCustomizedEventID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@TriggerCustomizedEventID"], htmltemplatesavinginputmodel.TriggerCustomizedEventID ?? null);

                        command.Parameters.Add(new SqlParameter("@LocalTempOriginalURL", SqlDbType.VarChar));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.strSavedOriginalNotesLocalPathUrl))
                            commonhelper.SetSqlParameterValue(command.Parameters["@LocalTempOriginalURL"], htmltemplatesavinginputmodel.strSavedOriginalNotesLocalPathUrl);

                        else
                            command.Parameters["@LocalTempOriginalURL"].Value = "";

                        command.Parameters.Add(new SqlParameter("@LocalTempFormattedURL", SqlDbType.VarChar));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.strSavedFormattedNotesLocalPathUrl))
                            commonhelper.SetSqlParameterValue(command.Parameters["@LocalTempFormattedURL"], htmltemplatesavinginputmodel.strSavedFormattedNotesLocalPathUrl);

                        else
                            command.Parameters["@LocalTempFormattedURL"].Value = "";

                        // UDT PASSING FOR OPTIMIZATION CODE ADDED BY AHMED BASHA SHAIK ON 04/30/2019
                        command.Parameters.Add(new SqlParameter("@EasyForms_PatientData_Hx_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@EasyForms_PatientData_Hx_DT"], dtEasyFormsEHRSignedURLHX);

                        command.Parameters.Add(new SqlParameter("@EasyForms_PatientData_Portal_Hx_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@EasyForms_PatientData_Portal_Hx_DT"], dtEasyFormsPortalSignedURLHX);

                        // AFTER TABLE MERGING REGARDING PORTAL OPTIMIZATION 
                        // THIS INPUT COMES ONLY FROM PORTAL NAVIGATION 
                        command.Parameters.Add(new SqlParameter("@EasyForms_Portal_UploadedForm_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormsPortalUploadedFormInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Portal_UploadedForm_InfoID"], htmltemplatesavinginputmodel.EasyFormsPortalUploadedFormInfoID.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Portal_UploadedForm_InfoID"], null);

                        // Change the parameter type to varchar by AJAY ON 24-05-2019
                        command.Parameters.Add(new SqlParameter("@DOS_DateFormat", SqlDbType.VarChar, 16));
                        commonhelper.SetSqlParameterValue(command.Parameters["@DOS_DateFormat"], htmlTemplateSavingDetailsModel.EasyFormDOS_In_DateFormat.ToString());

                        // Change the parameter type to varchar by AJAY ON 24-05-2019
                        command.Parameters.Add(new SqlParameter("@DOS_AMPM_Format", SqlDbType.VarChar, 32));
                        commonhelper.SetSqlParameterValue(command.Parameters["@DOS_AMPM_Format"], htmlTemplateSavingDetailsModel.EasyFormDOS_In_DateAMPMFormat.ToString());

                        command.Parameters.Add(new SqlParameter("@ShowNotesDOSasElectronicallySavedDate", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@ShowNotesDOSasElectronicallySavedDate"], htmltemplatesavinginputmodel.ShowNotesDOSasElectronicallySavedDate);

                        command.Parameters.Add(new SqlParameter("@IsAutoCreateSuperBillWhileSaving", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsAutoCreateSuperBillWhileSaving"], htmltemplatesavinginputmodel.AutoCreateSuperBillWhileSavingCheck);

                        command.Parameters.Add(new SqlParameter("@IsAutoCreateClaimwhileSaving", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsAutoCreateClaimwhileSaving"], htmltemplatesavinginputmodel.AutoCreateClaimWhileSignOffDocuments);

                        command.Parameters.Add(new SqlParameter("@IsShowElectronicallyCreatedInformation", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsShowElectronicallyCreatedInformation"], htmltemplatesavinginputmodel.IsShowElectronicallyInfo);

                        command.Parameters.Add(new SqlParameter("@ElectrinicallyCreatedInfoDisplayType", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@ElectrinicallyCreatedInfoDisplayType"], htmltemplatesavinginputmodel.ElectronicallyCreatedInfoDisplayType.ToString());

                        //==========ASSIGN REQUIRED FIELDS FOR SAVE & SAVING STATUS FLGS BLOCK START ==============================
                        //WE ARE NOT PLACING PARAMETRS HERE ITSELF
                        //REASON 1 : WE ARE USING SAME PARAMETERS IN UPDATE SP ALSO. SO WE PLACED A SEPERATE COMMON METHOD
                        //REASON 2 : TO GET CALRITY ON WHAT WE ARE GETTING FROM DB, AS WE NEED TO REMOVE THESE PARAMS IN FUTURE ASAP
                        _objReqInputsForNotesSaveDA.Get_EasyForm_RequiredFields_ForSave(command, commonhelper, htmlTemplateSavingDetailsModel, 2);
                        _objPnfsStatusFlagsForNotesSaveDA.Get_EasyFormSaving_Status_Flags(command, commonhelper, htmlTemplateSavingDetailsModel.EasyFormSavingStatusFlags);
                        //==========ASSIGN REQUIRED FIELDS FOR SAVE & SAVING STATUS FLGS BLOCK END ==============================

                        command.Parameters.Add(new SqlParameter("@IsClinicalForm", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsClinicalForm"], htmltemplatesavinginputmodel.IsClinicalDocument);

                        command.Parameters.Add(new SqlParameter("@IsBillableForm", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsBillableForm"], htmltemplatesavinginputmodel.IsEasyFormBillable);

                        command.Parameters.Add(new SqlParameter("@WCFRequestGUID", SqlDbType.VarChar, 128));
                        commonhelper.SetSqlParameterValue(command.Parameters["@WCFRequestGUID"], htmltemplatesavinginputmodel.WCFRequestGUID);

                        // Madnatory fields validation message
                        command.Parameters.Add(new SqlParameter("@Mandatory_Validation_Message", SqlDbType.VarChar));
                        commonhelper.SetSqlParameterValue(command.Parameters["@Mandatory_Validation_Message"], htmltemplatesavinginputmodel.MandatoryFieldsValidationMessage);

                        // Warning fields validation message
                        command.Parameters.Add(new SqlParameter("@Confirmation_Validation_Message", SqlDbType.VarChar));
                        commonhelper.SetSqlParameterValue(command.Parameters["@Confirmation_Validation_Message"], htmltemplatesavinginputmodel.WarningFieldsValidationMessage);

                        // Warning fields FILLED STATUS TYPE
                        command.Parameters.Add(new SqlParameter("@WarningFieldsFilled_Type", SqlDbType.TinyInt));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@WarningFieldsFilled_Type"], htmltemplatesavinginputmodel.WarningFieldsFilledStatusType.ToString());

                        // Warning fields FILLED STATUS TYPE
                        command.Parameters.Add(new SqlParameter("@IsCallingFromOfflineSync", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsCallingFromOfflineSync"], htmltemplatesavinginputmodel.winFormsFormatEasyFormOfflineSync);

                        // Warning fields FILLED STATUS TYPE
                        command.Parameters.Add(new SqlParameter("@EhrOfflineSavedEasyFormGUID", SqlDbType.VarChar, 64));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.EHROfflinePatientDataGuid))
                            commonhelper.SetSqlParameterValue(command.Parameters["@EhrOfflineSavedEasyFormGUID"], htmltemplatesavinginputmodel.EHROfflinePatientDataGuid);
                        else
                            command.Parameters["@EhrOfflineSavedEasyFormGUID"].Value = DBNull.Value;
                        //added by sudheer.kommuri on 28/10/2019
                        //we add this column for to know where the sign off is done
                        //if the inbox folder type is workhandover unsigned easyforms done by others then we are asssigning 1 to the inbox folder type
                        //otherwise we are  assigning 0 to it on the basis of this flag they remove the documnet in other inbox folder also after signoff
                        command.Parameters.Add(new SqlParameter("@InboxFolderType", SqlDbType.TinyInt));
                        if (htmltemplatesavinginputmodel.InboxFolderType > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@InboxFolderType"], htmltemplatesavinginputmodel.InboxFolderType.ToString());
                        }
                        else
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@InboxFolderType"], null);
                        }

                        // OFFLINE EASYFORMS SAVED DATE TIME
                        // to identify in offline app saved jcon file creation date time
                        // if exists then pass otherwise null
                        command.Parameters.Add(new SqlParameter("@OffLineSavedDateTime", SqlDbType.DateTime));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.ehrOfflineEasyFormSavedEDateTime))
                            commonhelper.SetSqlParameterValue(command.Parameters["@OffLineSavedDateTime"], htmltemplatesavinginputmodel.ehrOfflineEasyFormSavedEDateTime);
                        else
                            command.Parameters["@OffLineSavedDateTime"].Value = DBNull.Value;

                        // set the output parameter
                        // which is helps us after easyform save methods operations
                        // which is output parameter
                        command.Parameters.Add(new SqlParameter("@EasyForms_Saved_Inputs_InfoID", SqlDbType.Int));
                        command.Parameters["@EasyForms_Saved_Inputs_InfoID"].Direction = ParameterDirection.Output;

                        //DIV EASY FORM GUID
                        command.Parameters.Add(new SqlParameter("@DivEasyFormsGuidKey", SqlDbType.VarChar, 20));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.easyFormsCustDivDirReplacableGuidKey))
                            commonhelper.SetSqlParameterValue(command.Parameters["@DivEasyFormsGuidKey"], htmltemplatesavinginputmodel.easyFormsCustDivDirReplacableGuidKey);
                        else
                            command.Parameters["@DivEasyFormsGuidKey"].Value = DBNull.Value;

                        // asssignin the easyform linked pt active episode id
                        // linking the notes to current selected episode id
                        command.Parameters.Add(new SqlParameter("@InPat_CareLevel_Event_Patient_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.patientchartmodel.LatestEpisodeInfoID > 0 && htmltemplatesavinginputmodel.ApplicationType != 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@InPat_CareLevel_Event_Patient_InfoID"], htmltemplatesavinginputmodel.patientchartmodel.LatestEpisodeInfoID.ToString());
                        else
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@InPat_CareLevel_Event_Patient_InfoID"], null);

                        //Added By Sai srinivas Busam on 10/29/2020
                        //TO UPDATE THE CHECKIN TIME
                        command.Parameters.Add(new SqlParameter("@CheckIn_Time", SqlDbType.VarChar));
                        commonhelper.SetSqlParameterValue(command.Parameters["@CheckIn_Time"], htmltemplatesavinginputmodel.CheckInTime);

                        command.Parameters.Add(new SqlParameter("@Patient_Linked_Easyform_Auth_Data_Linking_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@Patient_Linked_Easyform_Auth_Data_Linking_DT"], GetRefAuthValidationinputDT(htmltemplatesavinginputmodel));


                        command.Parameters.Add(new SqlParameter("@PatientPayorID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PatientPayorID"], htmltemplatesavinginputmodel.PayorId);

                        command.Parameters.Add(new SqlParameter("@PayorLinkedUserType", SqlDbType.TinyInt));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PayorLinkedUserType"], htmltemplatesavinginputmodel.PayorLinkedUserType);

                        command.Parameters.Add(new SqlParameter("@AzureAutoSave_UniqueNumber", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.UniqueNumbertoMapAutoSaveandEasyFormInstanceTable > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@AzureAutoSave_UniqueNumber"], htmltemplatesavinginputmodel.UniqueNumbertoMapAutoSaveandEasyFormInstanceTable.ToString());
                        else
                            command.Parameters["@AzureAutoSave_UniqueNumber"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@PreferredLanguageID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EnableBasedonPatientPreferredLanguageConvertandUploadEasyFormTemplatetoPatientPortal &&
                            htmltemplatesavinginputmodel.PreferredLanguageID > 0 &&
                            !string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PreferredLanguageCodeQRDA) &&
                            !htmltemplatesavinginputmodel.PreferredLanguageCodeQRDA.Contains("en"))
                            commonhelper.SetSqlParameterInt32(command.Parameters["@PreferredLanguageID"], htmltemplatesavinginputmodel.PreferredLanguageID.ToString());
                        else
                            command.Parameters["@PreferredLanguageID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@ShowInViewDocumentsAfterFinalizingInPatientPortal", SqlDbType.SmallInt));
                        //we well pass the following property only from one navigation
                        //finalize action from patient portal > forms to complete
                        //we will set '1: pending' for this navigation and '3: not required' for other navigations
                        commonhelper.SetSqlParameterInt32(command.Parameters["@ShowInViewDocumentsAfterFinalizingInPatientPortal"], htmltemplatesavinginputmodel.ShowThisTemplateNotesInViewDocumentsAfterFinalizingInPatientPortal ? 1 : 3);

                        command.Parameters.Add(new SqlParameter("@PracticeTimeZoneShortName", SqlDbType.VarChar, 8));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.PracticeTimeZoneShortName))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PracticeTimeZoneShortName"], htmltemplatesavinginputmodel.PracticeTimeZoneShortName);
                        else
                            command.Parameters["@PracticeTimeZoneShortName"].Value = DBNull.Value;

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);

                        if (command.Parameters["@SupervisorIDs"].Value != null && command.Parameters["@SupervisorIDs"].Value != DBNull.Value)
                        {
                            model.MultipleResponseID = command.Parameters["@SupervisorIDs"].Value.ToString();
                        }
                        if (command.Parameters["@DateOfService"].Value != null && command.Parameters["@DateOfService"].Value != DBNull.Value)
                        {
                            FormDOSForReminders = (Convert.ToDateTime(command.Parameters["@DateOfService"].Value.ToString())).ToString();
                        }



                        //// ASSIGNING THE OUTPUT PARAMTER VALUE TO SAVED INPUTS INFO ID 
                        //// chekcing the is output parameter value is exists or not
                        /// if exissts then read it to output model
                        if (command.Parameters["@EasyForms_Saved_Inputs_InfoID"].Value != null && command.Parameters["@EasyForms_Saved_Inputs_InfoID"].Value != DBNull.Value)
                        {
                            htmltemplatesavinginputmodel.EasyFormsSavedInputsInfoID = (int)command.Parameters["@EasyForms_Saved_Inputs_InfoID"].Value;
                        }

                    }
                }
            }

            return model;
        }

        #endregion

        #region "GET REF AUTH STATINMAIN DATA TABLE TO UPDATE "
        DataTable GetRefAuthValidationinputDT(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            DataTable dtRefauthInputInfo = null;
            DataRow drrow = null;


            dtRefauthInputInfo = new DataTable();
            dtRefauthInputInfo.Columns.Add("ReferalAuthId", typeof(int));
            dtRefauthInputInfo.Columns.Add("DocumentedCount", typeof(decimal));
            dtRefauthInputInfo.Columns.Add("EFStmtAuthorizationType", typeof(int));
            dtRefauthInputInfo.Columns.Add("EFStmtPrevUsedCountForThisVisit", typeof(decimal));
            dtRefauthInputInfo.Columns.Add("EFStmtNoOfVisitsLeft", typeof(decimal));


            drrow = dtRefauthInputInfo.NewRow();
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.ReferalAuthID != null)
            {
                drrow["ReferalAuthId"] = htmltemplatesavinginputmodel.RefAuthValidationModel.ReferalAuthID;
            }
            else
            {
                drrow["ReferalAuthId"] = DBNull.Value;
            }
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.DocumnetedAuthCount != null)
            {
                drrow["DocumentedCount"] = htmltemplatesavinginputmodel.RefAuthValidationModel.DocumnetedAuthCount;
            }
            else
            {
                drrow["DocumentedCount"] = DBNull.Value;
            }
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtAuthorizationType != null)
            {
                drrow["EFStmtAuthorizationType"] = htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtAuthorizationType;
            }
            else
            {
                drrow["EFStmtAuthorizationType"] = DBNull.Value;
            }
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtPrevUsedCountForThisVisit != null)
            {
                drrow["EFStmtPrevUsedCountForThisVisit"] = htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtPrevUsedCountForThisVisit;
            }
            else
            {
                drrow["EFStmtPrevUsedCountForThisVisit"] = DBNull.Value;
            }
            if (htmltemplatesavinginputmodel.RefAuthValidationModel != null && htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtNoOfVisitsLeft != null)
            {
                drrow["EFStmtNoOfVisitsLeft"] = htmltemplatesavinginputmodel.RefAuthValidationModel.EFStmtNoOfVisitsLeft;
            }
            else
            {
                drrow["EFStmtNoOfVisitsLeft"] = DBNull.Value;
            }






            dtRefauthInputInfo.Rows.Add(drrow);





            return dtRefauthInputInfo;
        }

        #endregion



    }

    internal class SetReqInputsForNotesSaveOrUpdateDA
    {
        #region "               EASY FORM SAVING INPUTS                "

        /// <summary>
        /// THIS METHOD IS USED TO ASSIGN EASY FORM SAVING STATUS FLAGS 
        /// THIS METHOD IS GOING TO BE CALLED IN EASY FORM SAVE & UPDATE METHODS , SO THIS METHOD PLACED SEPERATLY, TO AVOID DUPLICATE CALLING
        /// </summary>
        /// <param name="command"></param>
        /// <param name="commonhelper"></param>
        /// <param name="objEasyFormSavingFlags"></param>

        public void Get_EasyForm_RequiredFields_ForSave(SqlCommand command,
                                                         DBConnectHelper commonhelper,
                                                         HtmlTemplateSavingDetailsModel htmlTemplateSavingDetailsModel,
                                                         Int16 SavingMode)
        {

            if (SavingMode == 1)
            {
                //SAVE
                //====
                command.Parameters.Add(new SqlParameter("@NewClient_Registration_FromPortal_InfoID", SqlDbType.Int));
                if (htmlTemplateSavingDetailsModel.NewClient_Registration_FromPortal_InfoID > 0)
                    commonhelper.SetSqlParameterInt32(command.Parameters["@NewClient_Registration_FromPortal_InfoID"], htmlTemplateSavingDetailsModel.NewClient_Registration_FromPortal_InfoID.ToString());
                else
                    commonhelper.SetSqlParameterInt32(command.Parameters["@NewClient_Registration_FromPortal_InfoID"], null);

                command.Parameters.Add(new SqlParameter("@IsPatientsExist", SqlDbType.Bit));
                commonhelper.SetSqlParameterBit(command.Parameters["@IsPatientsExist"], htmlTemplateSavingDetailsModel.IsPatientsExist);

                command.Parameters.Add(new SqlParameter("@DosFilledInForm", SqlDbType.DateTime));
                commonhelper.SetSqlParameterDateTime(command.Parameters["@DosFilledInForm"], htmlTemplateSavingDetailsModel.DosFilledInForm);

                command.Parameters.Add(new SqlParameter("@GETDATETIME", SqlDbType.DateTime));
                commonhelper.SetSqlParameterDateTime(command.Parameters["@GETDATETIME"], htmlTemplateSavingDetailsModel.GETDATETIME);

                command.Parameters.Add(new SqlParameter("@SysDefDOS", SqlDbType.DateTime));
                commonhelper.SetSqlParameterDateTime(command.Parameters["@SysDefDOS"], htmlTemplateSavingDetailsModel.SysDefDOS);
            }
            else if (SavingMode == 2)
            {
                //UPDATE
                //========
                command.Parameters.Add(new SqlParameter("@DOSExistsInForm", SqlDbType.DateTime));
                commonhelper.SetSqlParameterDateTime(command.Parameters["@DOSExistsInForm"], htmlTemplateSavingDetailsModel.DosFilledInForm);

                command.Parameters.Add(new SqlParameter("@IsChangeGTID", SqlDbType.Bit));
                commonhelper.SetSqlParameterBit(command.Parameters["@IsChangeGTID"], htmlTemplateSavingDetailsModel.IsChangeGTID);

                command.Parameters.Add(new SqlParameter("@IsChangeGTST", SqlDbType.Bit));
                commonhelper.SetSqlParameterBit(command.Parameters["@IsChangeGTST"], htmlTemplateSavingDetailsModel.IsChangeGTST);

                command.Parameters.Add(new SqlParameter("@IsChangeSysDefDOS", SqlDbType.Bit));
                commonhelper.SetSqlParameterBit(command.Parameters["@IsChangeSysDefDOS"], htmlTemplateSavingDetailsModel.IsChangeSysDefDOS);

                command.Parameters.Add(new SqlParameter("@IsChangeAppSysDefDOS", SqlDbType.Bit));
                commonhelper.SetSqlParameterBit(command.Parameters["@IsChangeAppSysDefDOS"], htmlTemplateSavingDetailsModel.IsChangeAppSysDefDOS);

                command.Parameters.Add(new SqlParameter("@IsChangeAppID", SqlDbType.Bit));
                commonhelper.SetSqlParameterBit(command.Parameters["@IsChangeAppID"], htmlTemplateSavingDetailsModel.IsChangeAppID);

                command.Parameters.Add(new SqlParameter("@IsChangeDOS", SqlDbType.Bit));
                commonhelper.SetSqlParameterBit(command.Parameters["@IsChangeDOS"], htmlTemplateSavingDetailsModel.IsChangeDOS);

                command.Parameters.Add(new SqlParameter("@App_StartTime", SqlDbType.DateTime));
                if (htmlTemplateSavingDetailsModel.App_StartTime != null)
                    commonhelper.SetSqlParameterDateTime(command.Parameters["@App_StartTime"], htmlTemplateSavingDetailsModel.App_StartTime);
                else
                    commonhelper.SetSqlParameterDateTime(command.Parameters["@App_StartTime"], null);

                command.Parameters.Add(new SqlParameter("@EasyForms_Electronically_Saved_InfoID", SqlDbType.Int));
                if (htmlTemplateSavingDetailsModel.EasyForms_Electronically_Saved_InfoID > 0)
                    commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Electronically_Saved_InfoID"], htmlTemplateSavingDetailsModel.EasyForms_Electronically_Saved_InfoID.ToString());
                else
                    commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Electronically_Saved_InfoID"], null);

                command.Parameters.Add(new SqlParameter("@CreatedByType", SqlDbType.Int));
                commonhelper.SetSqlParameterInt32(command.Parameters["@CreatedByType"], htmlTemplateSavingDetailsModel.CreatedByType.ToString());

                command.Parameters.Add(new SqlParameter("@CreatedUserID", SqlDbType.Int));
                commonhelper.SetSqlParameterInt32(command.Parameters["@CreatedUserID"], htmlTemplateSavingDetailsModel.CreatedUserID.ToString());

                command.Parameters.Add(new SqlParameter("@Fillable_HTML_DocumentTemplateID", SqlDbType.Int));
                commonhelper.SetSqlParameterInt32(command.Parameters["@Fillable_HTML_DocumentTemplateID"], htmlTemplateSavingDetailsModel.Fillable_HTML_DocumentTemplateID.ToString());

                command.Parameters.Add(new SqlParameter("@Saved_AppID", SqlDbType.Int));
                commonhelper.SetSqlParameterInt32(command.Parameters["@Saved_AppID"], htmlTemplateSavingDetailsModel.Saved_AppID.ToString());

                command.Parameters.Add(new SqlParameter("@Saved_GTID", SqlDbType.Int));
                commonhelper.SetSqlParameterInt32(command.Parameters["@Saved_GTID"], htmlTemplateSavingDetailsModel.Saved_GTID.ToString());

                command.Parameters.Add(new SqlParameter("@Saved_DOS", SqlDbType.DateTime));
                commonhelper.SetSqlParameterDateTime(command.Parameters["@Saved_DOS"], htmlTemplateSavingDetailsModel.Saved_DOS);

                command.Parameters.Add(new SqlParameter("@Saved_SysDefDOS", SqlDbType.DateTime));
                commonhelper.SetSqlParameterDateTime(command.Parameters["@Saved_SysDefDOS"], htmlTemplateSavingDetailsModel.SysDefDOS);

                command.Parameters.Add(new SqlParameter("@ApptType", SqlDbType.Int));
                commonhelper.SetSqlParameterInt32(command.Parameters["@ApptType"], htmlTemplateSavingDetailsModel.ApptType.ToString());

                command.Parameters.Add(new SqlParameter("@GETEMRDATETIME", SqlDbType.DateTime));
                commonhelper.SetSqlParameterDateTime(command.Parameters["@GETEMRDATETIME"], htmlTemplateSavingDetailsModel.GETDATETIME);

                // SAVING IN PATIENT GROUP THERAPY SESSION INFO ID ADDED BY AJAY ON 13-06-2019
                command.Parameters.Add(new SqlParameter("@InPat_GroupTherapy_Session_InfoID", SqlDbType.Int));
                commonhelper.SetSqlParameterInt32(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], htmlTemplateSavingDetailsModel.InPatGroupTherapySessionInfoID.ToString());

            }

            //Common
            //========

            command.Parameters.Add(new SqlParameter("@DateID", SqlDbType.Int));
            commonhelper.SetSqlParameterInt32(command.Parameters["@DateID"], htmlTemplateSavingDetailsModel.DateID.ToString());

            command.Parameters.Add(new SqlParameter("@TimeID", SqlDbType.Int));
            commonhelper.SetSqlParameterInt32(command.Parameters["@TimeID"], htmlTemplateSavingDetailsModel.TimeID.ToString());

            command.Parameters.Add(new SqlParameter("@IsEasyFormSignedInPortal", SqlDbType.Bit));
            commonhelper.SetSqlParameterBit(command.Parameters["@IsEasyFormSignedInPortal"], htmlTemplateSavingDetailsModel.IsEasyFormSignedInPortal);

            command.Parameters.Add(new SqlParameter("@IsEasyFormFinalSignedInEHR", SqlDbType.Bit));
            commonhelper.SetSqlParameterBit(command.Parameters["@IsEasyFormFinalSignedInEHR"], htmlTemplateSavingDetailsModel.IsEasyFormFinalSignedInEHR);

            command.Parameters.Add(new SqlParameter("@EasyForm_SignedStatus", SqlDbType.Int));
            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_SignedStatus"], htmlTemplateSavingDetailsModel.EasyForm_SignedStatus.ToString());

            command.Parameters.Add(new SqlParameter("@IsEasyFormSignedInEHR", SqlDbType.Bit));
            commonhelper.SetSqlParameterBit(command.Parameters["@IsEasyFormSignedInEHR"], htmlTemplateSavingDetailsModel.IsEasyFormSignedInEHR);

            command.Parameters.Add(new SqlParameter("@IsLetterTemplate", SqlDbType.Bit));
            commonhelper.SetSqlParameterBit(command.Parameters["@IsLetterTemplate"], htmlTemplateSavingDetailsModel.IsLetterTemplate);

            command.Parameters.Add(new SqlParameter("@IsLoggedUserHasSignOffPermission", SqlDbType.Bit));
            commonhelper.SetSqlParameterBit(command.Parameters["@IsLoggedUserHasSignOffPermission"], htmlTemplateSavingDetailsModel.IsLoggedUserHasSignOffPermission);

            command.Parameters.Add(new SqlParameter("@EasyForm_SignOff_ActionRequired_InfoID", SqlDbType.Int));
            if (htmlTemplateSavingDetailsModel.EasyForm_SignOff_ActionRequired_InfoID > 0)
                commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_SignOff_ActionRequired_InfoID"], htmlTemplateSavingDetailsModel.EasyForm_SignOff_ActionRequired_InfoID.ToString());
            else
                commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_SignOff_ActionRequired_InfoID"], null);

            command.Parameters.Add(new SqlParameter("@EasyForms_Electronically_Saved_ActionDoneSeq", SqlDbType.Int));
            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Electronically_Saved_ActionDoneSeq"], htmlTemplateSavingDetailsModel.EasyForms_Electronically_Saved_ActionDoneSeq.ToString());

            command.Parameters.Add(new SqlParameter("@EasyForms_Electronically_Saved_ActionWise_Sequence", SqlDbType.Int));
            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Electronically_Saved_ActionWise_Sequence"], htmlTemplateSavingDetailsModel.EasyForms_Electronically_Saved_ActionWise_Sequence.ToString());

            command.Parameters.Add(new SqlParameter("@ActionPerformedID", SqlDbType.Int));
            commonhelper.SetSqlParameterInt32(command.Parameters["@ActionPerformedID"], htmlTemplateSavingDetailsModel.ActionPerformedID.ToString());

            command.Parameters.Add(new SqlParameter("@GETDATETIME_AMPMFORMAT", SqlDbType.VarChar));
            commonhelper.SetSqlParameterValue(command.Parameters["@GETDATETIME_AMPMFORMAT"], htmlTemplateSavingDetailsModel.GETDATETIME_AMPMFORMAT);


            command.Parameters.Add(new SqlParameter("@EasyForms_Electronically_Saved_SignOffActionType", SqlDbType.Int));
            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Electronically_Saved_SignOffActionType"], htmlTemplateSavingDetailsModel.EasyForms_Electronically_Saved_SignOffActionType.ToString());

            command.Parameters.Add(new SqlParameter("@IsAutoForwardCustExist", SqlDbType.Bit));
            commonhelper.SetSqlParameterBit(command.Parameters["@IsAutoForwardCustExist"], htmlTemplateSavingDetailsModel.IsAutoForwardCustExist);

            command.Parameters.Add(new SqlParameter("@IsForwardUsersSelected", SqlDbType.Bit));
            commonhelper.SetSqlParameterBit(command.Parameters["@IsForwardUsersSelected"], htmlTemplateSavingDetailsModel.IsForwardUsersSelected);

            command.Parameters.Add(new SqlParameter("@IsFormComesAsBackWard", SqlDbType.Bit));
            commonhelper.SetSqlParameterBit(command.Parameters["@IsFormComesAsBackWard"], htmlTemplateSavingDetailsModel.IsFormComesAsBackWard);

        }
        #endregion
    }

    internal class SetPNFS_StatusFlagsForNotesSaveOrUpdateDA
    {
        #region "               EASY FORM SAVING STATUS FLAGS               "

        /// <summary>
        /// THIS METHOD IS USED TO ASSIGN EASY FORM SAVING STATUS FLAGS 
        /// THIS METHOD IS GOING TO BE CALLED IN EASY FORM SAVE & UPDATE METHODS , SO THIS METHOD PLACED SEPERATLY, TO AVOID DUPLICATE CALLING
        /// </summary>
        /// <param name="command"></param>
        /// <param name="commonhelper"></param>
        /// <param name="objEasyFormSavingFlags"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>

        public void Get_EasyFormSaving_Status_Flags(SqlCommand command, DBConnectHelper commonhelper,
                                                        EasyFormSavingStatusFlagsModel objEasyFormSavingFlags)
        {

            //====================================== EASY FORM SAVING STATUS FLAGS BLOCK START ======================================================================

            // Step 1  : EHR - Uploading Local Temp Urls to GCloud
            command.Parameters.Add(new SqlParameter("@Original_TempFile_Uploaded_To_GCloud", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@Original_TempFile_Uploaded_To_GCloud"], objEasyFormSavingFlags.Original_TempFile_Uploaded_To_GCloud.ToString());

            command.Parameters.Add(new SqlParameter("@Formatted_TempFile_Uploaded_To_GCloud", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@Formatted_TempFile_Uploaded_To_GCloud"], objEasyFormSavingFlags.Formatted_TempFile_Uploaded_To_GCloud.ToString());

            // Step 2  : Easy Form - Fielded Data Saving
            command.Parameters.Add(new SqlParameter("@Fielded_Saving_Completed", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@Fielded_Saving_Completed"], objEasyFormSavingFlags.Fielded_Saving_Completed.ToString());

            // Step 3  : Easy Form - Statemaintaine Data Saving
            command.Parameters.Add(new SqlParameter("@Statemaintaine_Data_Saving_Completed", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@Statemaintaine_Data_Saving_Completed"], objEasyFormSavingFlags.Statemaintaine_Data_Saving_Completed.ToString());

            // Step 4  : Easy Form - Notes Formation Saving
            command.Parameters.Add(new SqlParameter("@NotesFormation_Completed", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@NotesFormation_Completed"], objEasyFormSavingFlags.NotesFormation_Completed.ToString());

            // Step 5  : Billing - Performing Billing Activity
            command.Parameters.Add(new SqlParameter("@Perform_BillingActivity_REST_Call_Performed", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@Perform_BillingActivity_REST_Call_Performed"], objEasyFormSavingFlags.Perform_BillingActivity_REST_Call_Performed.ToString());

            // Step 6  : Appointments - Update Appt With EasyForm
            command.Parameters.Add(new SqlParameter("@CallCallApptSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallCallApptSP"], objEasyFormSavingFlags.CallCallApptSP.ToString());

            // Step 7  : Appointments - UPDATE APPOINTMENT INFO WHEN EASY FORM DOS CHANGED IN EDIT MODE
            command.Parameters.Add(new SqlParameter("@CallApptChangeDOSSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallApptChangeDOSSP"], objEasyFormSavingFlags.CallApptChangeDOSSP.ToString());

            // Step 8  : Easy Forms - UPDATE EASY FORM PATIENT DATA LOG
            command.Parameters.Add(new SqlParameter("@CallLogUpdationSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallLogUpdationSP"], objEasyFormSavingFlags.CallLogUpdationSP.ToString());

            // Step 9  : Easy Forms - INSERT EASY FORMS PATIENT DATA AUTO FORWARD TO USERS
            command.Parameters.Add(new SqlParameter("@CallAutoForwardSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallAutoForwardSP"], objEasyFormSavingFlags.CallAutoForwardSP.ToString());

            // Step 10 : Messages - SEND MESSAGE TO SUPERVISORS AFTER EASY FORM SAVING
            command.Parameters.Add(new SqlParameter("@CallMsgsSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallMsgsSP"], objEasyFormSavingFlags.CallMsgsSP.ToString());

            // Step 11 : REFER TO - REFER TO SENT INFO - UPDATING REFERAL SENT INFO FOR EASYFORM
            command.Parameters.Add(new SqlParameter("@CallReferToSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallReferToSP"], objEasyFormSavingFlags.CallReferToSP.ToString());

            // Step 12 : WHEN EASYFORM SIGNED FROM EMR - "REMOVE PENDING FORMS TO FILL IN PORTAL" 
            command.Parameters.Add(new SqlParameter("@CallEHRSignoff_FormsToFill_DeleteSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallEHRSignoff_FormsToFill_DeleteSP"], objEasyFormSavingFlags.CallEHRSignoff_FormsToFill_DeleteSP.ToString());

            // Step 13 : PORTAL - WHEN EASY FORM FINALIZED FROM FORMS TO FILL UDATING STATUS
            command.Parameters.Add(new SqlParameter("@CallFormsToComplete_FillStatusSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallFormsToComplete_FillStatusSP"], objEasyFormSavingFlags.CallFormsToComplete_FillStatusSP.ToString());

            // Step 14 : SURVEYFORMS - UPDATE SURVEY FORMS SUBMIT STATUS INFO
            command.Parameters.Add(new SqlParameter("@CallSurveyFormsSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallSurveyFormsSP"], objEasyFormSavingFlags.CallSurveyFormsSP.ToString());

            // Step 15 : LINKED DOCUMENTS - UPDATE LINKED DOCUMENTS AND EASY FORMS INFO 
            command.Parameters.Add(new SqlParameter("@CallLinkedDocumentsSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallLinkedDocumentsSP"], objEasyFormSavingFlags.CallLinkedDocumentsSP.ToString());

            // Step 16 : Patient Portal - Update Patient Portal Disease Questions Answers Easy Forms
            command.Parameters.Add(new SqlParameter("@CallPortal_DiseaseQuestionsSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallPortal_DiseaseQuestionsSP"], objEasyFormSavingFlags.CallPortal_DiseaseQuestionsSP.ToString());

            // Step 17 : Easy Form Static Fields - When Easy Form Signed - INSERT EASYFORMS STATIC FIELDS FIELD DATA
            command.Parameters.Add(new SqlParameter("@OnlyStaticFieldsData_Completed", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@OnlyStaticFieldsData_Completed"], objEasyFormSavingFlags.OnlyStaticFieldsData_Completed.ToString());

            // Step 18 : Reminders - Update EasyForms Reminders Data Refresh 
            command.Parameters.Add(new SqlParameter("@RefreshEasyFormBasedReminderRuleTypes", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@RefreshEasyFormBasedReminderRuleTypes"], objEasyFormSavingFlags.CallReminderFlagUpdateSP.ToString());

            // Step 19 : Easy Form Move Backward - Save EasyForms PatientData BackWardTo Users
            command.Parameters.Add(new SqlParameter("@CallBackWardSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallBackWardSP"], objEasyFormSavingFlags.CallBackWardSP.ToString());

            // Step 20 : Tasks - Task Status Update Execution AFTER EASY FORM SAVING
            command.Parameters.Add(new SqlParameter("@CallTasksSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallTasksSP"], objEasyFormSavingFlags.CallTasksSP.ToString());

            // Step 21 : Gloden Thread - Save EasyForm_GoldenThread_NeedsDxCodes_Details
            command.Parameters.Add(new SqlParameter("@EasyFormGoldenThreadNeedsDxCodesSavingRequired", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyFormGoldenThreadNeedsDxCodesSavingRequired"], objEasyFormSavingFlags.EasyFormGoldenThreadNeedsDxCodesSavingRequired.ToString());

            // Step 22 : Other Module - SaveOtherOrdersModuleInformationFromEasyForms
            command.Parameters.Add(new SqlParameter("@EasyFormOtherOrdersModuleRequired", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyFormOtherOrdersModuleRequired"], objEasyFormSavingFlags.EasyFormOtherOrdersModuleRequired.ToString());

            // Step 23 : Auto Upload Easy Form To Portal Execution
            command.Parameters.Add(new SqlParameter("@CallAutoUploadStaticSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallAutoUploadStaticSP"], objEasyFormSavingFlags.CallAutoUploadStaticSP.ToString());

            // Step 24 : AutoUploadToPortal_ELS_Dynamic_Execution_EasyForms_Execution
            command.Parameters.Add(new SqlParameter("@CallAutoUploadDynamicSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallAutoUploadDynamicSP"], objEasyFormSavingFlags.CallAutoUploadDynamicSP.ToString());

            // step 25 - This Flag Required Only Form Sign Off
            //ChangePatientStatusBasedOnEasyFormSaveActionsRequired

            // step 26 - CPTCodes DxCodes Saving Into CommonTable Completed
            command.Parameters.Add(new SqlParameter("@CPTCodes_DxCodes_CommonTable_Saving", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CPTCodes_DxCodes_CommonTable_Saving"], objEasyFormSavingFlags.CPTCodes_DxCodes_Saving_Into_CommonTable_Completed.ToString());

            // step 27 - Auto Claim Creation Completed
            command.Parameters.Add(new SqlParameter("@AutoClaimCreation", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@AutoClaimCreation"], objEasyFormSavingFlags.AutoClaimCreation_Completed.ToString());

            command.Parameters.Add(new SqlParameter("@PCCDocumentUploaded", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@PCCDocumentUploaded"], objEasyFormSavingFlags.PCCDocumentUpload_Completed.ToString());

            // step 28 - Auto Super Bill Creation Completed RDP
            command.Parameters.Add(new SqlParameter("@AutoSuperBillCreation_RDP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@AutoSuperBillCreation_RDP"], objEasyFormSavingFlags.AutoSuperBillCreation_Completed_RDP.ToString());

            // step 29 - Auth Count Deduction Completed
            command.Parameters.Add(new SqlParameter("@AuthCountDeduction", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@AuthCountDeduction"], objEasyFormSavingFlags.AuthCountDeduction_Completed.ToString());

            //Step 34 : 
            //Note : This flag is used to set Billing Payment from Grant
            //this flag is in Pending status when form is Final signed off
            //Assign input for a sp
            //command.Parameters.Add(new SqlParameter("@BillingPaymentFromGrant", SqlDbType.TinyInt));
            //commonhelper.SetSqlParameterInt32(command.Parameters["@BillingPaymentFromGrant"], objEasyFormSavingFlags.Billing_Patient_Discount_Paid_By_Grants_Completed.ToString());

            // Step 30 : : Group theraphy Appt Status Update
            command.Parameters.Add(new SqlParameter("@CallGroupTherapyApptSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallGroupTherapyApptSP"], objEasyFormSavingFlags.GreoupTheraphyApptStatus_Updated.ToString());

            // Step 31 : Call Group Therapy Appt Change DOS SP
            command.Parameters.Add(new SqlParameter("@CallGroupTherapyApptChangeDOSSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallGroupTherapyApptChangeDOSSP"], objEasyFormSavingFlags.CallGroupTherapyApptChangeDOSSP.ToString());

            // Step 32 : Call Checking Duplicate HCI VALUES AND REMOVE SP STATUS ADDED BY AJAY ON 01-07-2019
            command.Parameters.Add(new SqlParameter("@CallHCIDuplicatesRemovalSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallHCIDuplicatesRemovalSP"], objEasyFormSavingFlags.CallHCIDuplicatesRemovalSP.ToString());
            //====================================== EASY FORM SAVING STATUS FLAGS BLOCK END ======================================================================

            // Step 33 : PASSING INFO WHETHER TO CALL GOLDEN THREAD SAVING SP OR NOT FROM STATIC HEALTH CARE ITEMS 
            command.Parameters.Add(new SqlParameter("@CallGoldenThreadSavingSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallGoldenThreadSavingSP"], objEasyFormSavingFlags.CallGoldenThreadSavingSp.ToString());
            //====================================== EASY FORM SAVING STATUS FLAGS BLOCK END ======================================================================

            // Step 34: PASSING INFO WHETHER TO CALL ROI SAVING SP OR NOT FROM STATIC HEALTH CARE ITEMS 
            //ADDING PARAMETER OF TINY INT TYPE NAMED AS CallROISavingSP TO COMMAND OBJECT
            //ADDING THE CallROISavingSP VALUE FROM objEasyFormSavingFlags MODEL ITS DEFAULT VALUE IS 3(NOT REQUIRED)
            //THIS IS THE INPUT ADDED TO SAVE THE ROI DETAILS FROM HCI LINKED EASY FORM WHEN IT IS 2(PENDING),THEN CALL INSERT ROI DETAILS SP
            command.Parameters.Add(new SqlParameter("@CallROISavingSP", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallROISavingSP"], objEasyFormSavingFlags.CallROISavingSp.ToString());

            // Step 36: PASSING INFO WHETHER TO CALL SUBSTANCE ABUSE HX OR NOT FROM STATIC HEALTH CARE ITEMS 
            //ADDING PARAMETER OF TINY INT TYPE NAMED AS CallSubstanceAbuseAdmissionSp TO COMMAND OBJECT
            //ADDING THE CallSubstanceAbuseAdmissionSp VALUE FROM objEasyFormSavingFlags MODEL ITS DEFAULT VALUE IS 3(NOT REQUIRED)
            //THIS IS THE INPUT ADDED TO SAVE THE ROI DETAILS FROM HCI LINKED EASY FORM WHEN IT IS 2(PENDING),THEN CALL INSERT ROI DETAILS SP
            command.Parameters.Add(new SqlParameter("@CallSubstanceAbuseAdmissionSp", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallSubstanceAbuseAdmissionSp"], objEasyFormSavingFlags.CallSubstanceAbuseAdmissionSp.ToString());

            // Step 37: PASSING INFO WHETHER TO CALL SUBSTANCE ABUSE HX SAVING SP OR NOT FROM STATIC HEALTH CARE ITEMS 
            //ADDING PARAMETER OF TINY INT TYPE NAMED AS CallSubstanceAbuseDischargeSp TO COMMAND OBJECT
            //ADDING THE CallSubstanceAbuseDischargeSp VALUE FROM objEasyFormSavingFlags MODEL ITS DEFAULT VALUE IS 3(NOT REQUIRED)
            //THIS IS THE INPUT ADDED TO SAVE THE ROI DETAILS FROM HCI LINKED EASY FORM WHEN IT IS 2(PENDING),THEN CALL INSERT ROI DETAILS SP
            command.Parameters.Add(new SqlParameter("@CallSubstanceAbuseDischargeSp", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@CallSubstanceAbuseDischargeSp"], objEasyFormSavingFlags.CallSubstanceAbuseDischargeSp.ToString());

            // step 38
            command.Parameters.Add(new SqlParameter("@LongTermGoal_ShortTermGoalInfo_Saving_Completed", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@LongTermGoal_ShortTermGoalInfo_Saving_Completed"], objEasyFormSavingFlags.LongTermGoal_ShortTermGoalInfo_Saving_Completed.ToString());

            // step 39
            command.Parameters.Add(new SqlParameter("@PreferredLanguage_Original_TempFile_Uploaded_To_GCloud", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@PreferredLanguage_Original_TempFile_Uploaded_To_GCloud"], objEasyFormSavingFlags.PreferredLanguage_Original_TempFile_Uploaded_To_GCloud.ToString());

            // step 40
            command.Parameters.Add(new SqlParameter("@PreferredLanguage_Formatted_TempFile_Uploaded_To_GCloud", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@PreferredLanguage_Formatted_TempFile_Uploaded_To_GCloud"], objEasyFormSavingFlags.PreferredLanguage_Formatted_TempFile_Uploaded_To_GCloud.ToString());


            command.Parameters.Add(new SqlParameter("@SendNotificationEmailAfterSignoff", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@SendNotificationEmailAfterSignoff"], objEasyFormSavingFlags.SendNotificationEmailAfterSignoff);

            command.Parameters.Add(new SqlParameter("@SendMessage_to_MailBox_for_SelectedUsers_AfterFormSingedOff", SqlDbType.TinyInt));
            commonhelper.SetSqlParameterInt32(command.Parameters["@SendMessage_to_MailBox_for_SelectedUsers_AfterFormSingedOff"], objEasyFormSavingFlags.SendMessageToMailBoxForSelectedUsersAfterFormSingedOff);

            //====================================== EASY FORM SAVING STATUS FLAGS BLOCK END ======================================================================

        }
        #endregion
    }

    internal class GetReqInputsForNotesSaveOrUpdateDA
    {
        #region"            EASYFORMS REQUIRED FIELDS FOR SAVE GET                "

        ///// <summary>
        /////*******PURPOSE             : THIS IS USED TO GET EASY FORM SAVED INFO
        /////*******CREATED BY          : Balakrishna D
        /////*******CREATED DATE        : 5/26/2017
        /////*******MODIFIED DEVELOPER  : DATE - NAME - WHAT IS MODIFIED; *************************
        ///// </summary>
        public DataSet EasyForms_RequiredFields_ForSave_Get(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            DataSet dsStaffInfo = null;

            DBConnectHelper commonhelper = new DBConnectHelper();
            {
                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_RequiredFields_ForSave_Get", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID <= 0)
                        {
                            //Save Mode
                            #region "               SAVE MODE               "

                            command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                            if (htmltemplatesavinginputmodel.GroupTherapySessionType == 2 && htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0)//GroupTherapySessionType == 2 couple therapy
                                commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], null);
                            else if (htmltemplatesavinginputmodel.patientchartmodel.PatientID > 0)
                                commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString());
                            else
                                command.Parameters["@PatientID"].Value = DBNull.Value;

                            command.Parameters.Add(new SqlParameter("@PatientIDs", SqlDbType.VarChar, 8000));
                            if (htmltemplatesavinginputmodel.GroupTherapySessionType == 2 && htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0)//GroupTherapySessionType == 2 couple therapy
                                commonhelper.SetSqlParameterValue(command.Parameters["@PatientIDs"], htmltemplatesavinginputmodel.PatientIDs.ToString());
                            else
                                command.Parameters["@PatientIDs"].Value = DBNull.Value;

                            command.Parameters.Add(new SqlParameter("@AppointmentID", SqlDbType.Int));
                            if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0 && (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 77 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 21))
                                command.Parameters["@AppointmentID"].Value = DBNull.Value;
                            else if (htmltemplatesavinginputmodel.ClientSelectedAppointmentId > 0)
                                commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentID"], htmltemplatesavinginputmodel.ClientSelectedAppointmentId.ToString());
                            else if (htmltemplatesavinginputmodel.patientchartmodel.AppointmentId != null && htmltemplatesavinginputmodel.patientchartmodel.AppointmentId > 0)
                            {
                                //ISSUE : IF EASY FORM SAVED FROM GROUP THERAPHY / ATTENDY NOTES, CLAIMS ARE CREATING
                                //So from 11/08/2018 we are Not Saving One - One Appointment ID from below Navigations
                                //9 	-  	Group Theraphy Create New
                                //10 	- 	Group Theraphy Edit
                                //36	-	GROUPTHERAY SESSIONNOTES CREATE NEW MODE
                                //37	-	GROUPTHERAY SESSIONNOTE EDIT MODE
                                //59    -   ATTENDEE NOTES OPEN AS NEW 
                                //60    -   SESSION NOTES OPEN AS NEW
                                if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0 && (
                                    htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 9 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 10 ||
                                    htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 36 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 37 ||
                                    htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 59 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 60))
                                {
                                    command.Parameters["@AppointmentID"].Value = DBNull.Value;
                                }
                                else
                                {
                                    commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentID"], htmltemplatesavinginputmodel.patientchartmodel.AppointmentId.ToString());
                                }
                            }
                            else
                                command.Parameters["@AppointmentID"].Value = DBNull.Value;

                            command.Parameters.Add(new SqlParameter("@InPat_GroupTherapy_Session_InfoID", SqlDbType.Int));
                            if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0 && (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 77 || htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 21))
                                commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], null);
                            else if (htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0 && htmltemplatesavinginputmodel.ApplicationType == 1)
                                commonhelper.SetSqlParameterInt32(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID.ToString());
                            else
                                commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], null);

                            command.Parameters.Add(new SqlParameter("@EasyForms_SurveyForms_SentInfo_Client_InfoID", SqlDbType.Int));
                            if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom == 43)//SURVEYFORMSCREATENEW
                            {
                                if (htmltemplatesavinginputmodel.SurveyClientRequestSentInfoId > 0)
                                    commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_SurveyForms_SentInfo_Client_InfoID"], htmltemplatesavinginputmodel.SurveyClientRequestSentInfoId.ToString());
                                else
                                    command.Parameters["@EasyForms_SurveyForms_SentInfo_Client_InfoID"].Value = DBNull.Value;
                            }
                            else
                            {
                                command.Parameters["@EasyForms_SurveyForms_SentInfo_Client_InfoID"].Value = DBNull.Value;
                            }

                            #endregion
                        }
                        else
                        {
                            #region "               UPDATE MODE         "

                            //Update Mode
                            command.Parameters.Add(new SqlParameter("@AppointmentID", SqlDbType.Int));
                            if (htmltemplatesavinginputmodel.patientchartmodel.AppointmentId != null && htmltemplatesavinginputmodel.patientchartmodel.AppointmentId > 0)
                                commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentID"], htmltemplatesavinginputmodel.patientchartmodel.AppointmentId.ToString());
                            else
                                command.Parameters["@AppointmentID"].Value = DBNull.Value;

                            command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                            if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0)
                            {
                                commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());
                            }
                            else
                            {
                                command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"].Value = DBNull.Value;
                            }

                            command.Parameters.Add(new SqlParameter("@ShowSignoffButton", SqlDbType.Bit));
                            commonhelper.SetSqlParameterBit(command.Parameters["@ShowSignoffButton"], htmltemplatesavinginputmodel.showSignoffFinalizebutton);

                            command.Parameters.Add(new SqlParameter("@AppointmentIDFromChangeDOS", SqlDbType.Int));
                            if (htmltemplatesavinginputmodel.AppointmentIDFromChangeDOS > 0)
                                commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentIDFromChangeDOS"], htmltemplatesavinginputmodel.AppointmentIDFromChangeDOS.ToString());
                            else command.Parameters["@AppointmentIDFromChangeDOS"].Value = DBNull.Value;

                            command.Parameters.Add(new SqlParameter("@InPatGroupTherapySessionInfoIDFromChangeDOS", SqlDbType.Int));
                            if (htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoIDFromChangeDOS > 0)
                                commonhelper.SetSqlParameterInt32(command.Parameters["@InPatGroupTherapySessionInfoIDFromChangeDOS"], htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoIDFromChangeDOS.ToString());
                            else command.Parameters["@InPatGroupTherapySessionInfoIDFromChangeDOS"].Value = DBNull.Value;

                            #endregion
                        }

                        command.Parameters.Add(new SqlParameter("@DosFilledInForm", SqlDbType.DateTime));
                        if (htmltemplatesavinginputmodel.DOSFiledinEasyForm != null)
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@DosFilledInForm"], htmltemplatesavinginputmodel.DOSFiledinEasyForm);
                        else
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@DosFilledInForm"], null);

                        command.Parameters.Add(new SqlParameter("@Fillable_HTML_DocumentTemplateID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Fillable_HTML_DocumentTemplateID"], htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID.ToString());

                        command.Parameters.Add(new SqlParameter("@ApplicationType", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ApplicationType.ToString() != null)
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@ApplicationType"], htmltemplatesavinginputmodel.ApplicationType);
                        else
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@ApplicationType"], 1);// IF NULL DEFAULT WE ARE PASSING 1  MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION

                        command.Parameters.Add(new SqlParameter("@EasyForm_InPat_CareLevel_Event_Patient_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.patientchartmodel.LatestEpisodeInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_InPat_CareLevel_Event_Patient_InfoID"], htmltemplatesavinginputmodel.patientchartmodel.LatestEpisodeInfoID.ToString());
                        else
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EasyForm_InPat_CareLevel_Event_Patient_InfoID"], null);

                        command.Parameters.Add(new SqlParameter("@SignOffActionTypePerformed", SqlDbType.Int));
                        //previously we are not given sign off for easyforms in portal,so other than the portal only if sign off action is done  we have assigning ButtonClickActionType is 11
                        //but we have give sign off  for consent forms in portal ,so we have checking the sign off action from portal or not code is removed
                        //if the sign off is done and ButtonClickActionType is SIGNOFFANDMOVETOUC then we are assigning 11 to SignOffActionTypePerformed sql parameter
                        // if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.practicemodel.ApplicationType != 2 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)BtnClickType.SIGNOFFANDMOVETOUC)
                        if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOUC)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "11");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType != 2 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOBACKWARD)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "12");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType == 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "9");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType != 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "1");
                        else
                            command.Parameters["@SignOffActionTypePerformed"].Value = 0;// IF NULL DEFAULT WE ARE PASSING 0  MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION

                        command.Parameters.Add(new SqlParameter("@CreatedOrModifiedNavigationID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@CreatedOrModifiedNavigationID"], htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom.ToString());
                        else
                            command.Parameters["@CreatedOrModifiedNavigationID"].Value = 0; // IF NULL DEFAULT WE ARE PASSING 0  MODIFIED BY AJAY IN THE PROCESS OF CODE AND SPS OPTIMIZATION

                        // this input comes only from portal navigation based on this we are getting the upload form linked progrm id
                        command.Parameters.Add(new SqlParameter("@EasyForms_Portal_UploadedForm_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormsPortalUploadedFormInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Portal_UploadedForm_InfoID"], htmltemplatesavinginputmodel.EasyFormsPortalUploadedFormInfoID.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Portal_UploadedForm_InfoID"], null);


                        command.Parameters.Add(new SqlParameter("@Dashboard_Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.DashBoardFillableTemplatePatientDataID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Dashboard_Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DashBoardFillableTemplatePatientDataID.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Dashboard_Documents_Fillable_HTML_Templates_PatientDataID"], null);


                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************
                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        //NEW INSTANTIATING THE DATA TABLEBEFORE FILLING WITH DATA
                        dsStaffInfo = new DataSet();
                        //by using the sql adapter class we are filling the data table
                        using (SqlDataAdapter dataAdapter = new SqlDataAdapter(command))
                        {
                            dataAdapter.Fill(dsStaffInfo);
                        }

                        if (con.State != ConnectionState.Closed)
                            con.Close();
                    }
                }
            }

            return dsStaffInfo;
        }

        #endregion
    }

    internal class GetPtLatestEpisodeInfoIdOnFormLinkedPrgmsDA
    {
        #region "         GET LATEST EPISODE NUMBER               "
        /// <summary>
        /// *******PURPOSE              : This method is used get latest episode number
        ///*******CREATED BY            : ASHOK.ANNEM
        ///*******CREATED DATE          : 01/20/2020
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>
        public ResponseModel GetLatestEpisodeInfoId(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            ResponseModel responseModel = null;

            /// creating the instance for the common helper class
            /// by using this class instance we are setting the values to sp nput params
            /// for this method we are using the setsqlparama type method to set input data to sp
            DBConnectHelper commonhelper = new DBConnectHelper();
            {
                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                /// creating the instance for the SqlConnection class with constructor call by passing the practice model and database connceted enum
                /// by using this class instance we are connect to database
                /// for this method we are using open and close methods in that instance class
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    /// open the currnet instance db connection
                    /// By using the open method from the sqlconnection class
                    commonhelper.OpenSqlConnection(con);

                    /// creating the instance for the SqlCommand Class
                    /// by using the the class instance we are setting the sp input output params
                    /// here we are using the constructor call by while instantiating the class with the sp name and cusrrent db connection
                    using (SqlCommand command = new SqlCommand("usp_Template_Patient_Latest_Episode_Details_Get", con))
                    {
                        // command executed type
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************    

                        /// adding the logged user id input param with datatype by using the command class instance
                        /// setting the logged user id input value by using the commonhelper class instance
                        /// this i/p param is used to indicate current login user is executing the sp
                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        /// adding the template id input param with datatype by using the command class instance
                        /// this i/p param is used to hold template id value

                        command.Parameters.Add(new SqlParameter("@Program_Service_Attendee_TemplateID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Program_Service_Attendee_TemplateID"], htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID.ToString());

                        /// adding the patient id input param with datatype by using the command class instance
                        /// this i/p param is used to hold template id value
                        command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString());

                        /// adding the latest episode number output param with datatype by using the command class instance
                        /// this o/p param is used to hold latest epiisode number value
                        command.Parameters.Add(new SqlParameter("@Latest_EpisodeNumber", SqlDbType.Int));
                        command.Parameters["@Latest_EpisodeNumber"].Direction = ParameterDirection.Output;


                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        // Excute command query
                        command.ExecuteNonQuery();


                        responseModel = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, responseModel);

                        // Connection closed statement
                        if (con.State != ConnectionState.Closed)
                            con.Close();

                        //IF SP IS NOT EXECUTED SUCCESSFULLY / THE SP IS EXECUTED WITH ERRORS 
                        // IE REQUESTEXECUTIONSTATUS WITH HAVE NEGATIVE VALUE
                        //IF NO ERRR FOUND SP EXECUTED SUCCESSFULLY
                        if (responseModel.RequestExecutionStatus != -1)
                        {
                            if (command.Parameters["@Latest_EpisodeNumber"].Value != null && command.Parameters["@Latest_EpisodeNumber"].Value != DBNull.Value)
                            {
                                responseModel.ResponseID = (int)command.Parameters["@Latest_EpisodeNumber"].Value;
                            }
                        }
                    }
                }
            }

            /// After successful LIS SP CALLING return the list response to its calling method
            /// This return model is container model class type
            /// It holds list response
            return responseModel;
        }
        #endregion
    }

    internal class GetLatestEpisodeNoFromDashboardGTNavigationDA
    {
        #region "          THIS METHOD IS USED GET LATEST EPISODE NUMBER FOR DASBOARD GROUP THERAPY               "
        /// <summary>
        /// *******PURPOSE              : THIS METHOD IS USED GET LATEST EPISODE NUMBER FOR DASBOARD GROUP THERAPY
        ///*******CREATED BY            : sudheer.kommuri
        ///*******CREATED DATE          : 12/02/2020
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>
        public ResponseModel GetLatestEpisodeNumberForDashboardGroupTherapy(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            ResponseModel responseModel = null;

            /// creating the instance for the common helper class
            /// by using this class instance we are setting the values to sp nput params
            /// for this method we are using the setsqlparama type method to set input data to sp
            DBConnectHelper commonhelper = new DBConnectHelper();
            {
                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                /// creating the instance for the SqlConnection class with constructor call by passing the practice model and database connceted enum
                /// by using this class instance we are connect to database
                /// for this method we are using open and close methods in that instance class
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    /// open the currnet instance db connection
                    /// By using the open method from the sqlconnection class
                    commonhelper.OpenSqlConnection(con);

                    /// creating the instance for the SqlCommand Class
                    /// by using the the class instance we are setting the sp input output params
                    /// here we are using the constructor call by while instantiating the class with the sp name and cusrrent db connection
                    using (SqlCommand command = new SqlCommand("usp_GroupTherapySession_Patient_Latest_Episode_Details_Get", con))
                    {
                        // command executed type
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************    

                        /// adding the logged user id input param with datatype by using the command class instance
                        /// setting the logged user id input value by using the commonhelper class instance
                        /// this i/p param is used to indicate current login user is executing the sp
                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        /// adding the template id input param with datatype by using the command class instance
                        /// this i/p param is used to hold template id value

                        command.Parameters.Add(new SqlParameter("@InPat_GroupTherapy_Session_InfoID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID.ToString());

                        /// adding the patient id input param with datatype by using the command class instance
                        /// this i/p param is used to hold template id value
                        command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString());

                        /// adding the latest episode number output param with datatype by using the command class instance
                        /// this o/p param is used to hold latest epiisode number value
                        command.Parameters.Add(new SqlParameter("@Latest_EpisodeNumber", SqlDbType.Int));
                        command.Parameters["@Latest_EpisodeNumber"].Direction = ParameterDirection.Output;


                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        // Excute command query
                        command.ExecuteNonQuery();


                        responseModel = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, responseModel);

                        // Connection closed statement
                        if (con.State != ConnectionState.Closed)
                            con.Close();

                        //IF SP IS NOT EXECUTED SUCCESSFULLY / THE SP IS EXECUTED WITH ERRORS 
                        // IE REQUESTEXECUTIONSTATUS WITH HAVE NEGATIVE VALUE
                        //IF NO ERRR FOUND SP EXECUTED SUCCESSFULLY
                        if (responseModel.RequestExecutionStatus != -1)
                        {
                            if (command.Parameters["@Latest_EpisodeNumber"].Value != null && command.Parameters["@Latest_EpisodeNumber"].Value != DBNull.Value)
                            {
                                responseModel.ResponseID = (int)command.Parameters["@Latest_EpisodeNumber"].Value;
                            }
                        }
                    }
                }
            }

            /// After successful LIS SP CALLING return the list response to its calling method
            /// This return model is container model class type
            /// It holds list response
            return responseModel;
        }
        #endregion
    }

    internal class ChangeApptStatusOnEasyFormSaveStatusDA
    {
        #region "            CHANGE STATUS OF APPT BASED ON EASY FORM STATUS                           "
        /// <summary>
        ///   *******PURPOSE:THIS METHOD IS USED TO CHANGE STATUS OF APPT BASED ON EASY FORM STATUS 
        ///*******CREATED BY:RAVI TEJA.P
        ///*******CREATED DATE: 11/7/2017
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="healthcareItemModel"></param>
        /// <returns></returns>
        public ResponseModel changeApptStatusBasedOnEasyFormStatus(HtmlTemplateSavingInputModel htmlinputmodel)
        {
            ResponseModel outputModel = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {
                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmlinputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);
                    //usp_EasyForms_Billing_RenderingProvider_StateMaintainData_Get
                    using (SqlCommand command = new SqlCommand("usp_EasyForms_AppStatus_ChangeOn_FormAction_Execution_Insert", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmlinputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmlinputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());

                        command.Parameters.Add(new SqlParameter("@PatientIDs", SqlDbType.VarChar, 1024));
                        if (!string.IsNullOrWhiteSpace(htmlinputmodel.StatusUpdatePatientIds))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PatientIDs"], htmlinputmodel.StatusUpdatePatientIds);
                        else if (!string.IsNullOrWhiteSpace(htmlinputmodel.PatientIDs) && htmlinputmodel.GroupTherapySessionType == 2 && htmlinputmodel.InPatGroupTherapySessionInfoID > 0)
                            commonhelper.SetSqlParameterValue(command.Parameters["@PatientIDs"], htmlinputmodel.PatientIDs);
                        else
                            commonhelper.SetSqlParameterValue(command.Parameters["@PatientIDs"], htmlinputmodel.patientchartmodel.PatientID.ToString());

                        command.Parameters.Add(new SqlParameter("@EasyForm_Action", SqlDbType.Int));
                        if (htmlinputmodel.IsSignedOff)
                        {
                            if (htmlinputmodel.ActionPerformedID == 1) // Completed
                            {
                                commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_Action"], 3);
                            }
                            else if (htmlinputmodel.ActionPerformedID == 11) // Is Signed off
                            {
                                commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_Action"], 2);
                            }
                        }
                        else
                            commonhelper.SetSqlParameterValue(command.Parameters["@EasyForm_Action"], "1");

                        command.Parameters.Add(new SqlParameter("@CheckinTime", SqlDbType.DateTime));
                        if (htmlinputmodel?.ApptStatuschangeCheckinCheckoutselectionModel != null && !string.IsNullOrWhiteSpace(htmlinputmodel.ApptStatuschangeCheckinCheckoutselectionModel.ApptStatusChangeCheckinTime))
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@CheckinTime"], htmlinputmodel.ApptStatuschangeCheckinCheckoutselectionModel.ApptStatusChangeCheckinTime);
                        else
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@CheckinTime"], null);


                        command.Parameters.Add(new SqlParameter("@CheckoutTime", SqlDbType.DateTime));
                        if (htmlinputmodel?.ApptStatuschangeCheckinCheckoutselectionModel != null && !string.IsNullOrWhiteSpace(htmlinputmodel.ApptStatuschangeCheckinCheckoutselectionModel.ApptStatusChangeCheckOutTime))
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@CheckoutTime"], htmlinputmodel.ApptStatuschangeCheckinCheckoutselectionModel.ApptStatusChangeCheckOutTime);
                        else
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@CheckoutTime"], null);



                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmlinputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************
                        command.ExecuteNonQuery();

                        outputModel = new ResponseModel();

                        commonhelper.GetOutParameterValuesWithResponseModel(command, outputModel);

                        if (con.State != ConnectionState.Closed)
                            con.Close();
                    }

                }
            }

            return outputModel;
        }
        #endregion
    }


    internal class Cls_PatientDataIDAuthEndDateGoodenInsertOrUpdateDA
    {
        public ResponseModel PatientDataIDAuthEndDateGoodenInsertOrUpdateDA(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, int curPatientDataID)
        {
            ResponseModel responsemodel = null;
            DBConnectHelper commonhelper = new DBConnectHelper();

            //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
            using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
            {
                commonhelper.OpenSqlConnection(con);

                using (SqlCommand command = new SqlCommand("USP_PatientDataID_AuthEndDate_Gooden_InsertOrUpdate", con))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    //***********INPUT PARAMETERS BLOCK START*****************                            

                    command.Parameters.Add(new SqlParameter("@LoggedUserID", SqlDbType.Int));
                    commonhelper.SetSqlParameterInt32(command.Parameters["@LoggedUserID"], htmltemplatesavinginputmodel.LoggedUserID);

                    command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                    commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], curPatientDataID);

                    command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                    commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], htmltemplatesavinginputmodel.admissiondetailsipmodel.PatienID);

                    command.Parameters.Add(new SqlParameter("@AuthEndDate", SqlDbType.Date));
                    commonhelper.SetSqlParameterDateTime(command.Parameters["@AuthEndDate"], htmltemplatesavinginputmodel.admissiondetailsipmodel.AuthEndDate);



                    //***********INPUT PARAMETERS BLOCK END*****************
                    //**********OUT PUT PARAMETERS BLOCK START***************
                    //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                    commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                    //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                    //**********OUT PUT PARAMETERS BLOCK END***************

                    command.ExecuteNonQuery();

                    responsemodel = new ResponseModel();
                    commonhelper.GetOutParameterValuesWithResponseModel(command, responsemodel);

                    if (con.State != ConnectionState.Closed)
                    {
                        con.Close();
                    }
                }
            }

            //Return the Data Object.
            return responsemodel;
        }

        public ResponseModel PatientDataIDAuthEndDateGoodenInActive(HtmlTemplateInputModel htmltemplatesavinginputmodel)
        {
            ResponseModel responsemodel = null;
            DBConnectHelper commonhelper = new DBConnectHelper();

            //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
            using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
            {
                commonhelper.OpenSqlConnection(con);

                using (SqlCommand command = new SqlCommand("USP_PatientDataID_AuthEndDate_Gooden_Delete_Web", con))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    //***********INPUT PARAMETERS BLOCK START*****************                            

                    command.Parameters.Add(new SqlParameter("@LoggedUserID", SqlDbType.Int));
                    commonhelper.SetSqlParameterInt32(command.Parameters["@LoggedUserID"], htmltemplatesavinginputmodel.LoggedUserID);

                    command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                    commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID);

                    //***********INPUT PARAMETERS BLOCK END*****************
                    //**********OUT PUT PARAMETERS BLOCK START***************
                    //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                    commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                    //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                    //**********OUT PUT PARAMETERS BLOCK END***************

                    command.ExecuteNonQuery();

                    responsemodel = new ResponseModel();
                    commonhelper.GetOutParameterValuesWithResponseModel(command, responsemodel);

                    if (con.State != ConnectionState.Closed)
                    {
                        con.Close();
                    }
                }
            }

            //Return the Data Object.
            return responsemodel;
        }
    }

    internal class GetUsersToSendMsgWhenFormFinalizedInPortalDA
    {
        #region "               GET MESSAGE DETAILS             "

        /// <summary>
        /// *******PURPOSE:THIS METHOD IS TO GET THE MESSAGE DETAILS
        ///*******CREATED BY: UDAY KIRAN V
        ///*******CREATED DATE:06/26/2015
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="inboxleftsidelinksmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>

        public DataTable SendMessageIfFilledFromPortal(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            // ResponseModel model = null;
            DataTable dtEmailSMSInfo = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {
                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_Authorized_Users_MessageGeneration_WhenFormFilled_FromPortal_Insert_Web", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());


                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************
                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        //NEW INSTANTIATING THE DATA TABLEBEFORE FILLING WITH DATA
                        dtEmailSMSInfo = new DataTable();
                        //by using the sql adapter class we are filling the data table
                        using (SqlDataAdapter dataAdapter = new SqlDataAdapter(command))
                        {
                            dataAdapter.Fill(dtEmailSMSInfo);
                        }

                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                    }
                }
            }

            return dtEmailSMSInfo;
        }



        #endregion
    }

    internal class GetPendingNotesTempUrlsToUploadToGcloudDA
    {
        #region"      THIS METHOD IS USED TO GET THE PENDING TEMP URLS TO UPLOAD TO GOOGLE WITH IN THE PRACTICE          "

        ///// <summary>
        /////*******PURPOSE             : THIS METHOD IS USED TO GET THE PENDING TEMP URLS TO UPLOAD TO GOOGLE WITH IN THE PRACTICE
        /////*******CREATED BY          : AJAY.NANDURI 
        /////*******CREATED DATE        : 15-05-2019
        /////*******MODIFIED DEVELOPER  : DATE - NAME - WHAT IS MODIFIED; *************************
        ///// </summary>
        public DataTable GetPendingSavedNotesTempUrlsToUploadToGoogle(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            DataTable dtPendingTempUrls = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_SignedUrls_UploadToGcloud_List", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************
                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        //NEW INSTANTIATING THE DATA TABLEBEFORE FILLING WITH DATA
                        dtPendingTempUrls = new DataTable();

                        //by using the sql adapter class we are filling the data table
                        using (SqlDataAdapter dataAdapter = new SqlDataAdapter(command))
                        {
                            dataAdapter.Fill(dtPendingTempUrls);
                        }

                        if (con.State != ConnectionState.Closed)
                            con.Close();
                    }
                }
            }

            return dtPendingTempUrls;
        }

        #endregion
    }

    internal class ValidateChangeApptStatusOnFormSaveActionDA
    {
        #region "            VALIDATION FOR CHANGE STATUS OF APPT BASED ON EASY FORM STATUS                           "
        /// <summary>
        ///   *******PURPOSE:THIS METHOD IS USED TO VALIDATE CHANGE STATUS OF APPT BASED ON EASY FORM STATUS 
        ///*******CREATED BY:RAVI TEJA.P
        ///*******CREATED DATE: 11/7/2017
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="healthcareItemModel"></param>
        /// <returns></returns>
        public ResponseModel ValidationForchangeApptStatusBasedOnEasyFormStatus(HtmlTemplateSavingInputModel htmlinputmodel)
        {
            ResponseModel outputModel = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmlinputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);
                    //usp_EasyForms_Billing_RenderingProvider_StateMaintainData_Get
                    using (SqlCommand command = new SqlCommand("usp_EasyForms_AppStatus_ChangeOn_FormAction_Execution_Validation", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmlinputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@Fillable_HTML_DocumentTemplateID", SqlDbType.Int));
                        commonhelper.SetSqlParameterValue(command.Parameters["@Fillable_HTML_DocumentTemplateID"], htmlinputmodel.FillableHTMLDocumentTemplateID.ToString());

                        command.Parameters.Add(new SqlParameter("@AppointmentID", SqlDbType.Int));
                        if (htmlinputmodel.patientchartmodel != null && htmlinputmodel.patientchartmodel.AppointmentId > 0 && (htmlinputmodel.grouptheraphyattendeesinfomodelList == null || htmlinputmodel.grouptheraphyattendeesinfomodelList.Count == 0))
                            commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentID"], htmlinputmodel.patientchartmodel.AppointmentId.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentID"], null);

                        command.Parameters.Add(new SqlParameter("@InPat_GroupTherapy_Session_InfoID", SqlDbType.Int));
                        if (htmlinputmodel.InPatGroupTherapySessionInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], htmlinputmodel.InPatGroupTherapySessionInfoID.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], null);

                        command.Parameters.Add(new SqlParameter("@PatientIDs", SqlDbType.VarChar, 1024));
                        if (!string.IsNullOrWhiteSpace(htmlinputmodel.StatusUpdatePatientIds))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PatientIDs"], htmlinputmodel.StatusUpdatePatientIds);
                        else if (!string.IsNullOrWhiteSpace(htmlinputmodel.PatientIDs) && htmlinputmodel.GroupTherapySessionType == 2 && htmlinputmodel.InPatGroupTherapySessionInfoID > 0)
                            commonhelper.SetSqlParameterValue(command.Parameters["@PatientIDs"], htmlinputmodel.PatientIDs);
                        else
                            commonhelper.SetSqlParameterValue(command.Parameters["@PatientIDs"], htmlinputmodel.patientchartmodel.PatientID.ToString());

                        command.Parameters.Add(new SqlParameter("@EasyForm_Action", SqlDbType.Int));
                        if (htmlinputmodel.IsSignedOff)
                        {
                            if (htmlinputmodel.ActionPerformedID == 1) // Completed
                            {
                                commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_Action"], 3);
                            }
                            else if (htmlinputmodel.ActionPerformedID == 11) // Is Signed off
                            {
                                commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_Action"], 2);
                            }
                        }
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_Action"], "1");

                        command.Parameters.Add(new SqlParameter("@CheckOutTime", SqlDbType.DateTime));
                        if (htmlinputmodel?.ApptStatuschangeCheckinCheckoutselectionModel != null && !string.IsNullOrWhiteSpace(htmlinputmodel.ApptStatuschangeCheckinCheckoutselectionModel.ApptStatusChangeCheckOutTime))
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@CheckOutTime"], htmlinputmodel.ApptStatuschangeCheckinCheckoutselectionModel.ApptStatusChangeCheckOutTime);
                        else
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@CheckOutTime"], null);

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmlinputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************
                        command.ExecuteNonQuery();

                        outputModel = new ResponseModel();
                        commonhelper.GetOutParameterValuesWithResponseModel(command, outputModel);

                        if (con.State != ConnectionState.Closed)
                            con.Close();
                    }

                }
            }

            return outputModel;
        }

        #endregion
    }
    internal class GetProgramServiceDOSCombinationValidationDA
    {
        #region "                GetProgramServiceDOSCombinationValidation                "

        /// <summary>
        /// *******PURPOSE              : THIS METHOD IS USED TO GET PROGRAM SERVICE DOS COMBINATION VALIDATION 
        ///*******CREATED BY            : Pavan Bharide
        ///*******CREATED DATE          : March 23 2023
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        public ResponseModel GetProgramServiceDOSCombinationValidation(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {



            ResponseModel model = null;

            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_Notes_Provider_Program_Service_of_allnotes_documented_by_SamePatient_Validation_List", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());
                        }
                        else
                        {
                            command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"].Value = DBNull.Value;
                        }

                        command.Parameters.Add(new SqlParameter("@ProviderID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.BillingSuperBillMngtRenderingProviderID > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ProviderID"], htmltemplatesavinginputmodel.BillingSuperBillMngtRenderingProviderID.ToString());
                        }
                        else
                        {
                            command.Parameters["@ProviderID"].Value = DBNull.Value;
                        }


                        command.Parameters.Add(new SqlParameter("@ProgramID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ProgramServiceLinkedInfoIDForValidation > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ProgramID"], 0);

                        }
                        else if (htmltemplatesavinginputmodel.ProgramIDForValidation > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ProgramID"], htmltemplatesavinginputmodel.ProgramIDForValidation.ToString());
                        }
                        else
                        {
                            command.Parameters["@ProgramID"].Value = DBNull.Value;
                        }



                        command.Parameters.Add(new SqlParameter("@ServiceID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@ServiceID"], 0);


                        command.Parameters.Add(new SqlParameter("@ProgramServiceLikedInfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ProgramServiceLinkedInfoIDForValidation > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ProgramServiceLikedInfoID"], htmltemplatesavinginputmodel.ProgramServiceLinkedInfoIDForValidation.ToString());
                        }
                        else
                        {
                            command.Parameters["@ProgramServiceLikedInfoID"].Value = DBNull.Value;
                        }

                        command.Parameters.Add(new SqlParameter("@DOS", SqlDbType.Date));
                        if (!string.IsNullOrEmpty(htmltemplatesavinginputmodel.DOSFiledinEasyForm))
                        {
                            commonhelper.SetSqlParameterValue(command.Parameters["@DOS"], htmltemplatesavinginputmodel.DOSFiledinEasyForm.ToString());
                        }
                        else
                        {
                            command.Parameters["@DOS"].Value = DBNull.Value;
                        }

                        command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString());

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@IsFromSavedNotesSignOff", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsFromSavedNotesSignOff"], false);

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************


                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);



                    }
                }
            }




            //RETURNING THE MODEL
            return model;
        }

        #endregion
    }

    internal class GetEasyFormsNotesStartEndTimeofallnotesdocumentedbySameProvideronSameDateValidationDA
    {
        #region "                GetEasyFormsNotesStartEndTimeofallnotesdocumentedbySameProvideronSameDateValidation                "

        /// <summary>
        /// *******PURPOSE              : THIS METHOD IS USED TO GET EASYFORMS NOTES START END TIME OF ALL NOTES DOCUMENTED BY SAME PROVIDER ON SAME DATE VALIDATION
        ///*******CREATED BY            : Pavan Bharide
        ///*******CREATED DATE          : March 23 2023
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>
        public List<EFNotesStartTimeAndEndtimeValidationInfoModel> GetEasyFormsNotesStartEndTimeofallnotesdocumentedbySameProvideronSameDateValidationList(HtmlTemplateSavingInputModel inpModel)
        {
            List<EFNotesStartTimeAndEndtimeValidationInfoModel> returnModel = null; // THIS MODEL OBJECT TO HOLD THE RESPONSE AFTER EXECUTING THE QUERY
            EFNotesStartTimeAndEndtimeValidationInfoModel model = null; // HOLDs LIST OF REPORTS


            // CREATING THE INSTANCE FOR THE COMMON HELPER CLASS
            DBConnectHelper commonhelper = new DBConnectHelper();
            {
                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, inpModel)))
                {
                    // OPEN THE CONNECTION STATEMENT
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_Notes_Start_End_Time_of_allnotes_documented_by_SameProvider_on_SameDate_Validation_List", con))
                    {
                        // command executed type
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************    

                        // logged user Id
                        command.Parameters.Add(new SqlParameter("@LoggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@LoggedUserID"], inpModel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@DoctorID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@DoctorID"], inpModel.DoctorID.ToString());



                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        if (inpModel?.DocumentsFillableHTMLTemplatesPatientDataID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], inpModel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());
                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date));
                        commonhelper.SetSqlParameterDateTime(command.Parameters["@Date"], inpModel.DocumentedDate.ToString());

                        command.Parameters.Add(new SqlParameter("@StartTime", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@StartTime"], inpModel.StartTime.ToString());

                        command.Parameters.Add(new SqlParameter("@EndTime", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@EndTime"], inpModel.EndTime.ToString());



                        /*******internal and external ip as input parameters end *****************/
                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, inpModel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************

                        using (IDataReader reader = command.ExecuteReader())
                        {
                            returnModel = new List<EFNotesStartTimeAndEndtimeValidationInfoModel>();

                            int dateOrdinal = reader.GetOrdinal("Date");
                            int patientNameOrdinal = reader.GetOrdinal("PatientName");
                            int starttimeOrdinal = reader.GetOrdinal("StartTime");
                            int endTimeOrdinal = reader.GetOrdinal("EndTime");
                            int notesNameOrdinal = reader.GetOrdinal("FormName");
                            int providerNameOrdinal = reader.GetOrdinal("Providername");

                            while (reader.Read())
                            {
                                model = new EFNotesStartTimeAndEndtimeValidationInfoModel()
                                {
                                    DocumentedDate = commonhelper.GetReaderDateString(reader, dateOrdinal),
                                    PatientName = commonhelper.GetReaderString(reader, patientNameOrdinal),
                                    MilatryStartTime = commonhelper.GetReaderInt32(reader, starttimeOrdinal),
                                    MilatryEndTime = commonhelper.GetReaderInt32(reader, endTimeOrdinal),
                                    NotesName = commonhelper.GetReaderString(reader, notesNameOrdinal),
                                    ProviderName = commonhelper.GetReaderString(reader, providerNameOrdinal)
                                };

                                // ADD ITEM TO THE CONTAINER MODEL
                                returnModel.Add(model);
                            }
                        }


                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                        ////if no data is returned due to any error then get output parameters in which the error message is sent from sp 
                        if (returnModel != null && returnModel.Count == 0)
                        {

                            model = new EFNotesStartTimeAndEndtimeValidationInfoModel();
                            commonhelper.GetOutParameterValues(command, model);
                            //if sp is not executed successfully / the sp is executed with errors 
                            // ie RequestExecutionStatus with have negative value
                            if (model.RequestExecutionStatus < 0)
                                returnModel.Add(model);

                        }
                        commonhelper.GetOutParameterValues(command, model);

                        if (con.State != ConnectionState.Closed)
                            con.Close();


                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********


                    }
                }
            }

            return returnModel;
        }
        #endregion
    }

    internal class UpdateVitalsInfoFromNotesStaticHCIsDataDA
    {
        #region "               METHOD TO UPDATE SAVED VITAL AND EASYFORM DETAILS              "

        /// <summary>
        /// *******PURPOSE: THIS METHOD TO UPDATE SAVED VITAL AND EASYFORM DETAILS     
        ///*******CREATED BY:Gautham R
        ///*******CREATED DATE: 8/31/2016
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="vitaltypeandvitalidlinkmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>

        public ResponseModel UpdateVitalsHTMLTemplatesPatientDataInfo(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            ResponseModel model = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_Vitals_Patient_HTMLTemplatesPatientDataID_Update", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());


                        command.Parameters.Add(new SqlParameter("@VitalPatientIDs", SqlDbType.VarChar, 2048));
                        commonhelper.SetSqlParameterValue(command.Parameters["@VitalPatientIDs"], htmltemplatesavinginputmodel.VitalPatientIDS);

                        command.Parameters.Add(new SqlParameter("@HTMLTemplatesPatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterValue(command.Parameters["@HTMLTemplatesPatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************

                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();

                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                        model = new ResponseModel();

                        ////if no data is returned due to any error then get output parameters in which the error message is sent from sp 
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                    }
                }
            }

            return model;
        }

        #endregion
    }

    internal class UpdateQuickLabOrderInfoOnNotesStaticHCIsDataDA
    {
        #region "                UPDATE PATIENT DATA ID FOR EASY FORM QUICK LAB ORDER               "
        /// <summary>
        /// *******PURPOSE              : THIS METHOD IS USED TO UPDATE PATIENT DATA ID FOR EASY FORM QUICK LAB ORDER
        ///*******CREATED BY            : BALAKRISHNA D
        ///*******CREATED DATE          : 09/02/2016
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>
        public ResponseModel UpdatePatientDataID_For_EasyFormQuickLabOrder(HtmlTemplateSavingInputModel EasyFormQuestionnarieModelinputmodel)
        {
            ResponseModel model = null;
            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR_Labs_HL7, EasyFormQuestionnarieModelinputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_QuickLabOrders_PatientData_Update_Web", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], EasyFormQuestionnarieModelinputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@TestOrderIDs", SqlDbType.VarChar, 512));
                        commonhelper.SetSqlParameterValue(command.Parameters["@TestOrderIDs"], EasyFormQuestionnarieModelinputmodel.QuickLabOrderTestOrderIDs);

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], EasyFormQuestionnarieModelinputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, EasyFormQuestionnarieModelinputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************


                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                    }
                }
            }

            return model;
        }

        #endregion
    }

    internal class UpdateFollowUpTextInfoFromNotesStaticHCIsDataDA
    {
        #region "                UPDATE EASY FORM PROGRESS NOTES FOLLOWUP TEXT               "
        /// <summary>
        /// *******PURPOSE              : THIS METHOD IS USED TO UPDATE EASY FORM PROGRESS NOTES FOLLOWUP TEXT
        ///*******CREATED BY            : SRINIVAS M
        ///*******CREATED DATE          : 10/05/2016
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>
        public ResponseModel UpdateEasyFormsProgressNotesFollowupText(HtmlTemplateSavingInputModel easyformsprogressnotesfollowupmodel)
        {
            #region"                VARIABLES USED              "
            ResponseModel model = null;
            #endregion

            #region"                TRY BLOCK               "

            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, easyformsprogressnotesfollowupmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_ProgressNotes_Followup_Text_Update", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@LoggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@LoggedUserID"], easyformsprogressnotesfollowupmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@EasyForm_ProgressNotes_FollowUpID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForm_ProgressNotes_FollowUpID"], easyformsprogressnotesfollowupmodel.ProgressNotesFollowupInfoID.ToString());

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], easyformsprogressnotesfollowupmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());


                        command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], easyformsprogressnotesfollowupmodel.patientchartmodel.PatientID.ToString());


                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, easyformsprogressnotesfollowupmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************


                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                    }
                }
            }
            #endregion

            return model;
        }

        #endregion
    }

    internal class UpdateQuickImagingOrdersFromNotesStaticHCIsDataDA
    {
        #region "                UPDATE PATIENT DATA ID FOR EASY FORM QUICK IMAGING ORDER               "
        /// <summary>
        /// *******PURPOSE              : THIS METHOD IS USED TO UPDATE PATIENT DATA ID FOR EASY FORM QUICK IMAGING ORDER
        ///*******CREATED BY            : AKBAR
        ///*******CREATED DATE          : 01/20/2018
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>
        public ResponseModel UpdatePatientDataID_For_EasyFormQuickImagingOrder(HtmlTemplateSavingInputModel EasyFormQuestionnarieModelinputmodel)
        {
            ResponseModel model = null;

            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR_Labs_HL7, EasyFormQuestionnarieModelinputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_QuickXrayOrders_PatientData_Update_Web", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], EasyFormQuestionnarieModelinputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@XrayOrderIDs", SqlDbType.VarChar, 512));
                        commonhelper.SetSqlParameterValue(command.Parameters["@XrayOrderIDs"], EasyFormQuestionnarieModelinputmodel.QuickImagingOrderIDs);

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], EasyFormQuestionnarieModelinputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, EasyFormQuestionnarieModelinputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************


                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                    }
                }
            }

            return model;
        }

        #endregion
    }

    internal class InsertEFNotesEnteredFreeTextBasedOnHCIDataAcess
    {
        public ResponseModel InsertEFNotesEnteredFreeTextBasedOnHCI(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, DataTable dtHCIAndFreeText)
        {

            ResponseModel model = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EF_Freetext_Notes_Insert_Update", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************

                        command.Parameters.Add(new SqlParameter("@dt_EF_Freetext_Notes", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@dt_EF_Freetext_Notes"], dtHCIAndFreeText);

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());


                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                    }
                }
            }

            return model;

        }
    }

    internal class UpdateEasyFormNotesStartAndEndTimeOfServiceProvidedDA
    {
        public ResponseModel UpdateEasyFormNotesStartAndEndTimeOfServiceProvided(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {

            ResponseModel model = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_Notes_Start_End_Time_of_Service_Provided_Insert_Update", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@DoctorID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@DoctorID"], htmltemplatesavinginputmodel.DoctorID.ToString());

                        command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString());

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());

                        command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date));
                        commonhelper.SetSqlParameterDateTime(command.Parameters["@Date"], htmltemplatesavinginputmodel.DocumentedDate.ToString());

                        command.Parameters.Add(new SqlParameter("@StartTime", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@StartTime"], htmltemplatesavinginputmodel.StartTime.ToString());

                        command.Parameters.Add(new SqlParameter("@EndTime", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@EndTime"], htmltemplatesavinginputmodel.EndTime.ToString());



                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                    }
                }
            }

            return model;

        }
    }

    internal class ProviderProgramserviceDOSCombinationInsertionDA
    {
        public ResponseModel ProgramserviceDOSCombinationInsertOrUpdate(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {

            ResponseModel model = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_Notes_Provider_Program_Service_Provided_Insert_Update", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());
                        }
                        else
                        {
                            command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"].Value = DBNull.Value;
                        }



                        command.Parameters.Add(new SqlParameter("@ProviderID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@ProviderID"], htmltemplatesavinginputmodel.BillingSuperBillMngtRenderingProviderID.ToString());



                        command.Parameters.Add(new SqlParameter("@ProgramID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ProgramServiceLinkedInfoIDForValidation > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ProgramID"], 0);
                        }
                        else
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ProgramID"], htmltemplatesavinginputmodel.ProgramIDForValidation);
                        }


                        command.Parameters.Add(new SqlParameter("@ServiceID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@ServiceID"], 0);

                        command.Parameters.Add(new SqlParameter("@ProgramServiceLikedInfoID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@ProgramServiceLikedInfoID"], htmltemplatesavinginputmodel.ProgramServiceLinkedInfoIDForValidation.ToString());


                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());




                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                    }
                }
            }

            return model;

        }
    }

    internal class UpdateFormsToFillStatusFromPortalSaveDataAccess
    {
        #region"    UPDATE FORMS TO FILL STATUS WHEN PORTAL USER FINALIZED       "
        /// <summary>
        /// ***PURPOSE: UPDATE FORMS TO FILL STATUS WHEN PORTAL USER FINALIZED 
        /// *******CREATED BY: MAHENDRA.G
        /// *******CREATED DATE: 29/05/2020
        /// *******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED;
        /// </summary>
        public ResponseModel UpdateFormsToFillStatusFromPortalSave(HtmlTemplateSavingInputModel htmlTemplateSavingInputModel, HtmlTemplateSavingDetailsModel htmlTemplateSavingDetailsModel)
        {
            //to hold the result
            ResponseModel outputModel = null;

            //initializing common helper
            DBConnectHelper commonHelper = new DBConnectHelper();

            //initilizing sql connection
            using (SqlConnection con = new SqlConnection(commonHelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmlTemplateSavingInputModel)))
            {
                //opening connection to the server
                commonHelper.OpenSqlConnection(con);

                //initilizing sql command
                using (SqlCommand command = new SqlCommand("usp_EasyForms_PatientData_FormFillStatus_Update", con))
                {
                    //setting command type to stored procedure
                    command.CommandType = CommandType.StoredProcedure;

                    //passing logged used id to track the request
                    command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                    commonHelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmlTemplateSavingInputModel.LoggedUserID.ToString());


                    //passing fillable html document template ID
                    command.Parameters.Add(new SqlParameter("@Fillable_HTML_DocumentTemplateID", SqlDbType.BigInt));
                    commonHelper.SetSqlParameterInt32(command.Parameters["@Fillable_HTML_DocumentTemplateID"], htmlTemplateSavingInputModel.FillableHTMLDocumentTemplateID);

                    //passing patient ID
                    command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                    if (htmlTemplateSavingInputModel.patientchartmodel?.PatientID > 0)
                        commonHelper.SetSqlParameterInt32(command.Parameters["@PatientID"], htmlTemplateSavingInputModel.patientchartmodel.PatientID.ToString());
                    else
                        command.Parameters["@PatientID"].Value = DBNull.Value;

                    //passing  the portal user type
                    command.Parameters.Add(new SqlParameter("@UserType", SqlDbType.Int));
                    commonHelper.SetSqlParameterInt32(command.Parameters["@UserType"], htmlTemplateSavingInputModel.PortalUserType.ToString());

                    //passing fillable html templates patient data ID
                    command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                    commonHelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmlTemplateSavingInputModel.DocumentsFillableHTMLTemplatesPatientDataID);

                    //passing the createdbytype parameter
                    command.Parameters.Add(new SqlParameter("@CreatedByType", SqlDbType.Int));
                    if (htmlTemplateSavingDetailsModel.CreatedByType > 0)
                        commonHelper.SetSqlParameterInt32(command.Parameters["@CreatedByType"], htmlTemplateSavingDetailsModel.CreatedByType);
                    else
                        commonHelper.SetSqlParameterInt32(command.Parameters["@CreatedByType"], htmlTemplateSavingInputModel.ApplicationType);

                    //passing easy forms saved input info ID
                    command.Parameters.Add(new SqlParameter("@EasyForms_Saved_Inputs_InfoID", SqlDbType.Int));
                    commonHelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Saved_Inputs_InfoID"], htmlTemplateSavingInputModel.EasyFormsSavedInputsInfoID);

                    //passing inputs saved Date  ID
                    command.Parameters.Add(new SqlParameter("@InputsSavedDateID", SqlDbType.Int));
                    commonHelper.SetSqlParameterInt32(command.Parameters["@InputsSavedDateID"], htmlTemplateSavingDetailsModel.DateID);

                    //passing easyforms auto upload form info id 
                    command.Parameters.Add(new SqlParameter("@EasyForms_AutoUploadToPortal_UploadedForm_InfoID", SqlDbType.Int));
                    commonHelper.SetSqlParameterInt32(command.Parameters["@EasyForms_AutoUploadToPortal_UploadedForm_InfoID"], null);

                    //passing the action datetime 
                    command.Parameters.Add(new SqlParameter("@ActionDoneDateTime", SqlDbType.DateTime));
                    commonHelper.SetSqlParameterDateTime(command.Parameters["@ActionDoneDateTime"], htmlTemplateSavingDetailsModel.GETDATETIME);

                    //passing the inputs saved dateID
                    command.Parameters.Add(new SqlParameter("@InputsSavedTimeID", SqlDbType.Int));
                    commonHelper.SetSqlParameterInt32(command.Parameters["@InputsSavedTimeID"], htmlTemplateSavingDetailsModel.TimeID);

                    //passing easy form portal uploaded form infoid
                    command.Parameters.Add(new SqlParameter("@EasyForms_Portal_UploadedForm_InfoID", SqlDbType.Int));
                    if (htmlTemplateSavingInputModel.EasyFormsPortalUploadedFormInfoID > 0)
                        commonHelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Portal_UploadedForm_InfoID"], htmlTemplateSavingInputModel.EasyFormsPortalUploadedFormInfoID.ToString());
                    else
                        commonHelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Portal_UploadedForm_InfoID"], null);

                    //passing the signoff action type performed by
                    //if it is 9 the action will be signoff
                    command.Parameters.Add(new SqlParameter("@SignOffActionTypePerformed", SqlDbType.Int));
                    if (htmlTemplateSavingInputModel.IsSignedOff == true)
                        commonHelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "9");
                    else
                        command.Parameters["@SignOffActionTypePerformed"].Value = 0;

                    //preparing regular output parameters
                    commonHelper.SetRegularOutputSqlParameters(command, htmlTemplateSavingInputModel);

                    //initializing output model
                    outputModel = new ResponseModel();

                    //executing the command
                    command.ExecuteNonQuery();

                    //getting regular output parameters to track request status
                    commonHelper.GetOutParameterValuesWithResponseModel(command, outputModel);
                }
            }

            //RETURNING OUTPUT MODEL
            return outputModel;
        }
        #endregion
    }

    internal class UpdateNotesLinkedProcedureInfoOnPtDataIdDA
    {
        #region"                METHOD TO SAVE PROCEDURE DOCUMENTED INFORMATION             "
        /// <summary>
        /// *******PURPOSE: THIS METHOD TO SAVE PROCEDURE DOCUMENTED INFORMATION
        ///*******CREATED BY: ABDUL RAHIMAN M
        ///*******CREATED DATE: 4/18/2016
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; ************************* 
        /// </summary>
        /// <param name="problemListViewModel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>
        public ResponseModel UpdatePatientDataIDToProcedureGivenInfoID(ProceduresInfoModel proceduresinfomodel)
        {
            ResponseModel model = null;

            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, proceduresinfomodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_Documents_Fillable_HTML_Templates_PatientDataID_Update_Web", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], proceduresinfomodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@Procedures_GiveninfoID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Procedures_GiveninfoID"], proceduresinfomodel.ProcedureGivenInfoID.ToString());


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], proceduresinfomodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());


                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************

                        //**********OUT PUT PARAMETERS BLOCK START***************
                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, proceduresinfomodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************
                        command.ExecuteNonQuery();

                        model = new ResponseModel();
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);

                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                    }
                }
            }

            //Return the Data Object.
            return model;
        }


        #endregion
    }

    internal class GetandAppendWatermarktoNotesDA
    {
        public GetWatermarkTextbasedonPatientDataIDModel GetWatermarkTextbasedonPatientDataID(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            GetWatermarkTextbasedonPatientDataIDModel model = new GetWatermarkTextbasedonPatientDataIDModel();
            DBConnectHelper dBConnectHelper = new DBConnectHelper();

            using (SqlConnection con = new SqlConnection(dBConnectHelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
            {
                con.Open();

                using (SqlCommand command = new SqlCommand("Usp_EasyForms_SavedNotes_WaterMarkText_Info_Get", con))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                    dBConnectHelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID);

                    command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                    dBConnectHelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID);

                    dBConnectHelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.WatermarkText = dBConnectHelper.GetReaderString(reader, "WaterMarkText");
                            model.DowlodedDateTime = dBConnectHelper.GetReaderString(reader, "DownLoadedDateTime");
                            model.LoggedUserName = dBConnectHelper.GetReaderString(reader, "loggedUserName");
                        }
                    }

                    dBConnectHelper.GetOutParameterValuesWithResponseModel(command, model);

                }
            }

            return model;

        }
    }

    internal class FieldValidationsDataAccess
    {
        #region "                FIELDS VALIDATIONS CUSTOMIZATION INFO  - LIST                   "

        /// <summary>
        /// *******PURPOSE              : THIS METHOD IS USED TO GET FIELDS VALIDATIONS CUSTOMIZATION INFO LIST
        ///*******CREATED BY            : sudheer.k
        ///*******CREATED DATE          : 07-30-2019
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>
        public EasyFormsFieldValidationsCustomizationContainerModel EasyFormsFieldsValidationCustomizationInfoList(EasyFormsFieldValidationsCustomizationContainerModel objEasyFormsFieldValidationsCustomizationContainerModel)
        {
            EasyFormsFieldValidationsCustomizationContainerModel containerModel = null; // THIS MODEL OBJECT TO HOLD THE RESPONSE AFTER EXECUTING THE QUERY
            EasyFormsFieldsValidationsCustomizationModel model = null; // HOLDs LIST OF REPORTS


            // CREATING THE INSTANCE FOR THE COMMON HELPER CLASS
            DBConnectHelper commonhelper = new DBConnectHelper();
            {
                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, objEasyFormsFieldValidationsCustomizationContainerModel)))
                {
                    // OPEN THE CONNECTION STATEMENT
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_Fields_Validation_Customization_List", con))
                    {
                        // command executed type
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************    

                        // logged user Id
                        command.Parameters.Add(new SqlParameter("@LoggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@LoggedUserID"], objEasyFormsFieldValidationsCustomizationContainerModel.LoggedUserID.ToString());

                        // THIS IS USED TO HOLD APPLICATION TYPE ID
                        // ApplicationType=1  ---->EHR
                        // ApplicationType=2  ---->PORTAL
                        // ApplicationType=3  ---->EHR & PORTAL
                        command.Parameters.Add(new SqlParameter("@Validation_ApplyType", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Validation_ApplyType"], objEasyFormsFieldValidationsCustomizationContainerModel.ApplicationType.ToString());


                        ///THIS IS USED TO HOLD THE USER SELECTED TEMPLATE ID INFORMATION
                        command.Parameters.Add(new SqlParameter("@Fillable_HTML_DocumentTemplateID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Fillable_HTML_DocumentTemplateID"], objEasyFormsFieldValidationsCustomizationContainerModel.FieldValidationInfo.FillableHTMLDocumentTemplateID.ToString());

                        /*******internal and external ip as input parameters end *****************/
                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, objEasyFormsFieldValidationsCustomizationContainerModel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************

                        using (IDataReader reader = command.ExecuteReader())
                        {
                            containerModel = new EasyFormsFieldValidationsCustomizationContainerModel();
                            containerModel.FieldValidationInfoList = new List<EasyFormsFieldsValidationsCustomizationModel>();

                            while (reader.Read())
                            {
                                model = new EasyFormsFieldsValidationsCustomizationModel()
                                {

                                    ValidationApplyType = commonhelper.GetReaderTinyInt(reader, "Validation_ApplyType"), // THIS IS USED TO HOLD THE USER SELECTED FIELD VALIDATION WHERE HAS TO APPLIED
                                    ValidationType = commonhelper.GetReaderTinyInt(reader, "Validation_Type"),  //   THIS IS USED TO HOLD THE USER SELECTED FIELD VALIDATION TYPE
                                    ValidationMessage = commonhelper.GetReaderString(reader, "ValidationMessage"),  // THIS IS USED TO HOLD THE USER SELECTED FIELD VALIDATION MESSAGE
                                    FillableHTMLDocumentTemplateFieldID = commonhelper.GetReaderInt32(reader, "Fillable_HTML_DocumentTemplate_FieldID"),//THIS IS USED HOLD THE USER SELECTED TEMPLATE FIELD ID
                                    FillableHTMLDocumentTemplateFieldName = commonhelper.GetReaderString(reader, "Fillable_HTML_DocumentTemplate_Field_Name"),
                                    FillableHTMLDocumentTemplateFieldDisplayName = commonhelper.GetReaderString(reader, "Fillable_HTML_DocumentTemplate_Field_DisplayName"), //FIELD DISPLAY NAME
                                };

                                // ADD ITEM TO THE CONTAINER MODEL
                                containerModel.FieldValidationInfoList.Add(model);
                            }
                        }

                        //WHEN NOTHING IS FROM DATA BASE THEN INITIALIZING WITH DEFAULTS
                        if (containerModel == null)
                            containerModel = new EasyFormsFieldValidationsCustomizationContainerModel();

                        // GETTING THE OUTPUT PARAMETERS
                        commonhelper.GetOutParameterValues(command, containerModel);

                        // CLOSE THE CONNECTION
                        if (con.State != ConnectionState.Closed)
                            con.Close();

                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        // EXCEPTION TRACE LOG
                        //emrwebexceptiontracelogmodel.AddToExecutionLog("EHR_EasyForms_DataAccess.HtmlTemplate", "FieldValidationsDataAccess", "EasyFormsFieldsValidationCustomizationInfoList", "EasyFormsFieldsValidationCustomizationInfoList END", ExecutionLogType.Detail);
                    }
                }
            }

            return containerModel;
        }
        #endregion
    }

    internal class GetNotesElectronicallySavedInfoDA
    {
        #region "               GET EASY FORM ELECTRONIC DETAILS                "
        /// <summary>
        /// *******PURPOSE              : THIS IS USED TO OPEN THE EXISTING PATIENTS HTML FORM AS NEW FORM OR TO EDIT THE EXISTING HTML FORM
        ///*******CREATED BY            : MALINI
        ///*******CREATED DATE          : 08/23/2014
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="htmltemplateinputmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>

        public EasyFormsElectronicallyCreatedInfoModel GetEasyFormElectronicallySavedInfo(HtmlTemplateSavingInputModel htmltemplateinputmodel)
        {
            EasyFormsElectronicallyCreatedInfoModel model = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplateinputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_ElectronicallySavedInfo_Get", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplateinputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplateinputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());


                        command.Parameters.Add(new SqlParameter("@Fillable_HTML_DocumentTemplateID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Fillable_HTML_DocumentTemplateID"], htmltemplateinputmodel.FillableHTMLDocumentTemplateID.ToString());


                        command.Parameters.Add(new SqlParameter("@UserType", SqlDbType.Int));
                        if (htmltemplateinputmodel.PortalUserType > 0 && htmltemplateinputmodel.ApplicationType == 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@UserType"], htmltemplateinputmodel.PortalUserType.ToString()); //user from portal
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@UserType"], "4");//4 means ehr provider

                        command.Parameters.Add(new SqlParameter("@ActionPerformedID", SqlDbType.Int));
                        //previously we are not given sign off for easyforms in portal,so other than the portal only if sign off action is done  we have assigning ButtonClickActionType is 11
                        //but we have give sign off  for consent forms in portal ,so we have checking the sign off action from portal or not code is removed
                        //if the sign off is done and ButtonClickActionType is SIGNOFFANDMOVETOUC then we are assigning 11 to SignOffActionTypePerformed sql parameter
                        // if (htmltemplateinputmodel.IsSignedOff == true && htmltemplateinputmodel.practicemodel.ApplicationType != 2 && htmltemplateinputmodel.ButtonClickActionType == (int)BtnClickType.SIGNOFFANDMOVETOUC)
                        if (htmltemplateinputmodel.IsSignedOff == true && htmltemplateinputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOUC)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ActionPerformedID"], "11"); // sign off and move to uc
                        else if (htmltemplateinputmodel.IsSignedOff == true && htmltemplateinputmodel.ApplicationType != 2 && htmltemplateinputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOBACKWARD)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ActionPerformedID"], "12"); // sign off and move to backward
                        else if (htmltemplateinputmodel.IsSignedOff == true && htmltemplateinputmodel.ApplicationType == 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ActionPerformedID"], "9"); // finalize in portal
                        else if (htmltemplateinputmodel.IsSignedOff == true && htmltemplateinputmodel.ApplicationType != 2)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ActionPerformedID"], "1"); //sign in ehr
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@ActionPerformedID"], "9"); //handling save as draft and edit as draft in ehr , save as draft in portal

                        command.Parameters.Add(new SqlParameter("@ShowSignoffButton", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@ShowSignoffButton"], htmltemplateinputmodel.showSignoffFinalizebutton);

                        command.Parameters.Add(new SqlParameter("@EasyForms_Electronically_Saved_OriginalUserID", SqlDbType.Int));
                        if (htmltemplateinputmodel.ehrImpersonateLoggedUserID > 0)
                        {
                            if (htmltemplateinputmodel.LoggedUserID == htmltemplateinputmodel.ehrImpersonateLoggedUserID)
                                command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"].Value = DBNull.Value;
                            else
                                commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"], htmltemplateinputmodel.ehrImpersonateLoggedUserID);
                        }
                        else
                            command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@DosFilledInForm", SqlDbType.VarChar, 32));
                        commonhelper.SetSqlParameterValue(command.Parameters["@DosFilledInForm"], htmltemplateinputmodel.DOSFiledinEasyForm);

                        // ASSIGNING EASY FORM BUTTON CLICKED TYPE 
                        // WHEN FORM FINALIZED FROM PORTAL WE HAVE TO PASS IsFinalizedInPortal
                        command.Parameters.Add(new SqlParameter("@IsFinalizedInPortal", SqlDbType.Bit));
                        if (htmltemplateinputmodel.ApplicationType == 2 &&
                            htmltemplateinputmodel.EasyFormButtonClickedType == 2)
                            commonhelper.SetSqlParameterBit(command.Parameters["@IsFinalizedInPortal"], true);
                        else
                            commonhelper.SetSqlParameterBit(command.Parameters["@IsFinalizedInPortal"], false);

                        //SENDING CLIENT IP ADDRESS AS INPUT FOR APPENDING TO THE SINATURE OF THE EASYFORM
                        command.Parameters.Add(new SqlParameter("@IpAddress", SqlDbType.VarChar, 32));
                        commonhelper.SetSqlParameterValue(command.Parameters["@IpAddress"], htmltemplateinputmodel.clientIP);

                        command.Parameters.Add(new SqlParameter("@PracticeTimeZoneShortName", SqlDbType.VarChar, 8));
                        if (!string.IsNullOrWhiteSpace(htmltemplateinputmodel.PracticeTimeZoneShortName))
                            commonhelper.SetSqlParameterValue(command.Parameters["@PracticeTimeZoneShortName"], htmltemplateinputmodel.PracticeTimeZoneShortName);
                        else
                            command.Parameters["@PracticeTimeZoneShortName"].Value = DBNull.Value;

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************
                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplateinputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        using (IDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                model = new EasyFormsElectronicallyCreatedInfoModel()
                                {
                                    EasyFormElectronicallyCreatedInfo = commonhelper.GetReaderString(reader, "CreatedInfo"),
                                    //FillableHTMLDocumentTemplateID = commonhelper.GetReaderInt32(reader, "ElectrinicallyCreatedInfoDisplayType"),
                                    //PatientID = commonhelper.GetReaderInt32(reader, "IsDocSigned"),
                                };
                            }
                        }

                        commonhelper.GetOutParameterValues(command, model);
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                    }
                }
            }

            return model;
        }


        #endregion
    }

    internal class SaveGTSessionAttendeeNotesLinkingForMultipleAttendeesDA
    {
        #region"        SAVE GROUP THERAPHY SESSION ATTENDEE NOTES FOR MULTIPLE ATTENDESS       "
        /// <summary>
        ///  *******PURPOSE:THIS IS USED FOR SAVING THE GROUP THERAPHY SESSION SAVING OR UPDATE IN THE DATA BASE
        ///*******CREATED BY:Jaya raju
        ///*******CREATED DATE: 04/24/2015(comments added date)
        ///*******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <param name="htmltemplatesavinginputmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>
        public ResponseModel SaveGroupTheraphySessionAttendeeNotesForMultipleAttendess(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel, DataTable dtAttendeesandBinaryFormat, DataTable dtAttendeeSpecificFieldValuesList, ref string strSupervisorIDs)
        {
            ResponseModel model = null;
            ConformationModel confirmationmodel = null;


            DBConnectHelper commonhelper = new DBConnectHelper();
            {
                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_Documents_Fillable_HTML_Templates_PatientData_Insert2", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            
                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@Fillable_HTML_DocumentTemplateID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.LinkSessionNotesAttendees == true)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Fillable_HTML_DocumentTemplateID"], htmltemplatesavinginputmodel.GroupTherapyLinkSessionNotesToAttendeesSelectedTemplateID.ToString());
                        else
                            commonhelper.SetSqlParameterInt32(command.Parameters["@Fillable_HTML_DocumentTemplateID"], htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID.ToString());

                        command.Parameters.Add(new SqlParameter("@AppointmentID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.patientchartmodel.AppointmentId != null && htmltemplatesavinginputmodel.patientchartmodel.AppointmentId > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@AppointmentID"], htmltemplatesavinginputmodel.patientchartmodel.AppointmentId.ToString());
                        else
                            command.Parameters["@AppointmentID"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@DOS", SqlDbType.DateTime));
                        commonhelper.SetSqlParameterDateTime(command.Parameters["@DOS"], htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime);

                        //command.Parameters.Add(new SqlParameter("@IsSignedOff", SqlDbType.Bit));
                        //commonhelper.SetSqlParameterBit(command.Parameters["@IsSignedOff"], htmltemplatesavinginputmodel.IsSignedOff);

                        command.Parameters.Add(new SqlParameter("@DocumentWasSignedByPatient_Guarantor", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@DocumentWasSignedByPatient_Guarantor"], htmltemplatesavinginputmodel.DocumentWasSignedByPatientGuarantor);


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_Binary_Formatting_Required_Status", SqlDbType.Int));
                        //  commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_Binary_Formatting_Required_Status"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataNotesNameFieldNamesData);
                        command.Parameters["@Documents_Fillable_HTML_Templates_Binary_Formatting_Required_Status"].Value = 1;

                        command.Parameters.Add(new SqlParameter("@InPat_GroupTherapy_Session_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], htmltemplatesavinginputmodel.InPatGroupTherapySessionInfoID.ToString());
                        else
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@InPat_GroupTherapy_Session_InfoID"], null);

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientData_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@Documents_Fillable_HTML_Templates_PatientData_DT"], dtAttendeesandBinaryFormat);

                        command.Parameters.Add(new SqlParameter("@SignOffActionTypePerformed", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType != 2 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOUC)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "11");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true && htmltemplatesavinginputmodel.ApplicationType != 2 && htmltemplatesavinginputmodel.ButtonClickActionType == (int)EasyFormSaveActionBTNClickEnum.BtnClickType.SIGNOFFANDMOVETOBACKWARD)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "12");
                        else if (htmltemplatesavinginputmodel.IsSignedOff == true)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SignOffActionTypePerformed"], "1");
                        else
                            command.Parameters["@SignOffActionTypePerformed"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@CreatedOrModifiedNavigationID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@CreatedOrModifiedNavigationID"], htmltemplatesavinginputmodel.EasyFormSavingModelNavigationFrom.ToString());
                        else
                            command.Parameters["@CreatedOrModifiedNavigationID"].Value = 0;

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_ClientInternalIP", SqlDbType.VarChar, 256));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.clientSystemLocalIP))
                            commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_ClientInternalIP"], htmltemplatesavinginputmodel.clientSystemLocalIP);
                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_ClientInternalIP"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_IPAddress", SqlDbType.VarChar, 256));
                        if (!string.IsNullOrWhiteSpace(htmltemplatesavinginputmodel.clientIP))
                            commonhelper.SetSqlParameterValue(command.Parameters["@Documents_Fillable_HTML_Templates_IPAddress"], htmltemplatesavinginputmodel.clientIP);
                        else
                            command.Parameters["@Documents_Fillable_HTML_Templates_IPAddress"].Value = DBNull.Value;

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataIDs", SqlDbType.VarChar, -1));
                        command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataIDs"].Direction = ParameterDirection.Output;

                        command.Parameters.Add(new SqlParameter("@SupervisorIDs", SqlDbType.VarChar, 1024));
                        command.Parameters["@SupervisorIDs"].Direction = ParameterDirection.Output;

                        command.Parameters.Add(new SqlParameter("@LoggedFacilityID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.LoggedFacilityID > 0)
                            commonhelper.SetSqlParameterInt32(command.Parameters["@LoggedFacilityID"], htmltemplatesavinginputmodel.LoggedFacilityID.ToString());
                        else
                            command.Parameters["@LoggedFacilityID"].Value = 0;

                        command.Parameters.Add(new SqlParameter("@Patient_PatientDataID", SqlDbType.VarChar, -1));
                        command.Parameters["@Patient_PatientDataID"].Direction = ParameterDirection.Output;


                        // ===================== ASSIGING IMPRESONATE DATA BLOCK START ==========================================

                        command.Parameters.Add(new SqlParameter("@EHR_User_Impersonate_Audit_InfoID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ehrIsImpersonatedUserAuditID > 0)
                            commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EHR_User_Impersonate_Audit_InfoID"], htmltemplatesavinginputmodel.ehrIsImpersonatedUserAuditID);
                        else
                            command.Parameters["@EHR_User_Impersonate_Audit_InfoID"].Value = DBNull.Value;


                        command.Parameters.Add(new SqlParameter("@EasyForms_Electronically_Saved_OriginalUserID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.ehrImpersonateLoggedUserID > 0)
                        {
                            if (htmltemplatesavinginputmodel.LoggedUserID == htmltemplatesavinginputmodel.ehrImpersonateLoggedUserID)
                                command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"].Value = DBNull.Value;
                            else
                                commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"], htmltemplatesavinginputmodel.ehrImpersonateLoggedUserID);
                        }
                        else
                            command.Parameters["@EasyForms_Electronically_Saved_OriginalUserID"].Value = DBNull.Value;

                        // ---------- ADDED BY SANJAY IDPUGANTI START  ----------

                        // THESE TWO PARAMETERS ARE ADDED DURING CHARGE CAPTURE TRIGGER CUSTOMIZATION CHANGES DONE BY SANJAY IDPUGANTI ON APRIL 4TH 2019
                        command.Parameters.Add(new SqlParameter("@IsTriggerCustomized", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@IsTriggerCustomized"], htmltemplatesavinginputmodel.IsTriggerCustomized);

                        command.Parameters.Add(new SqlParameter("@TriggerCustomizedEventID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32Nullable(command.Parameters["@TriggerCustomizedEventID"], htmltemplatesavinginputmodel.TriggerCustomizedEventID);

                        // ---------- ADDED BY SANJAY IDPUGANTI END  ----------

                        // FOLLOWING PARAMETER IS CURRENTLY SAVED SESSION NOTES PATIENT DATA ID TO INDICATE SESSION NOTES PATIENT DATA ID ADDED BY AJAY ON 27-04-2019 
                        command.Parameters.Add(new SqlParameter("@Parent_Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Parent_Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataIDCurrentlySaved.ToString());

                        // GROUP THERAPHY SESSION TYPE
                        command.Parameters.Add(new SqlParameter("@GroupTherapySessionType", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@GroupTherapySessionType"], htmltemplatesavinginputmodel.GroupTherapySessionType.ToString());

                        // GROUP THERAPHY MODE
                        // -- 1 - Session notes Link to Other Attendees; 2 - Attendee Notes Link to Other Attendees
                        command.Parameters.Add(new SqlParameter("@GroupTheraphyMode", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.LinkSessionNotesAttendees == true)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@GroupTheraphyMode"], "1");
                        }
                        else if (htmltemplatesavinginputmodel.LinkNotestoOtherAttendees == true)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@GroupTheraphyMode"], "2");
                        }
                        else
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@GroupTheraphyMode"], "0");
                        }

                        // SECRETARY SELECTED PHYSICIAN ID
                        command.Parameters.Add(new SqlParameter("@SecretarySelectedPhysicianID", SqlDbType.Int));
                        if (htmltemplatesavinginputmodel.patientchartmodel != null && htmltemplatesavinginputmodel.patientchartmodel.SecretarySelectedPhysicinID > 0)
                        {
                            commonhelper.SetSqlParameterInt32(command.Parameters["@SecretarySelectedPhysicianID"], htmltemplatesavinginputmodel.patientchartmodel.SecretarySelectedPhysicinID.ToString());
                        }
                        else
                        {
                            command.Parameters["@SecretarySelectedPhysicianID"].Value = DBNull.Value;
                        }

                        /// adding the Attendee specific field  values udt input param by using the command class instance
                        /// setting the Attendee specific field  values udt value by using the commonhelper class instance
                        /// this i/p param is used to hold customized attendee specified field values list
                        command.Parameters.Add(new SqlParameter("@EasyForms_SessionNotes_AttendeeSpecific_FieldsData_DT", SqlDbType.Structured));
                        commonhelper.SetSQLParameterStructureType(command.Parameters["@EasyForms_SessionNotes_AttendeeSpecific_FieldsData_DT"], dtAttendeeSpecificFieldValuesList);

                        /// passing the saved inputs info id is saved in the child gt pending table which indicaes the parent seesion noted saved inputs id
                        command.Parameters.Add(new SqlParameter("@EasyForms_Saved_Inputs_InfoID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_Saved_Inputs_InfoID"], htmltemplatesavinginputmodel.EasyFormsSavedInputsInfoID.ToString());

                        // ===================== ASSIGING IMPRESONATE DATA BLOCK END ==========================================

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();
                        model.confirmationModelList = new List<ConformationModel>();
                        confirmationmodel = new ConformationModel();

                        if (command.Parameters["@Patient_PatientDataID"].Value != null && command.Parameters["@Patient_PatientDataID"].Value != DBNull.Value)
                        {
                            confirmationmodel.ConformationMessage = command.Parameters["@Patient_PatientDataID"].Value.ToString();
                            model.confirmationModelList.Add(confirmationmodel);
                        }

                        if (command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataIDs"].Value != null && command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataIDs"].Value != DBNull.Value)
                        {
                            model.MultipleResponseID = command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataIDs"].Value.ToString();
                        }

                        if (command.Parameters["@SupervisorIDs"].Value != null && command.Parameters["@SupervisorIDs"].Value != DBNull.Value)
                        {
                            strSupervisorIDs = command.Parameters["@SupervisorIDs"].Value.ToString();
                        }
                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                        //assigning the output paramter 
                    }
                }
            }

            return model;
        }

        #endregion
    }

    internal class LettersSentStatusDetailsSaveUpdateDA
    {
        #region  " METHOD IS USED TO INSERT LETTER STATUS DATA  "
        /// *******PURPOSE:THIS METHOD IS USED TO METHOD IS USED TO INSERT LETTER STATUS DATA
        ///*******CREATED BY: PHANI KUMAR M
        ///*******CREATED DATE: 7 TH MARCH 2016
        /// *******MODIFIED DEVELOPER: DATE - NAME - WHAT IS MODIFIED; *************************

        public ResponseModel InsertLetterSentStatusDetails(LettersModel lettersmodel)
        {
            ResponseModel model = null;

            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, lettersmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("usp_EasyForms_PatientData_Status_Insert", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START***************** 

                        // assigning the logged user id
                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], lettersmodel.LoggedUserID.ToString());

                        // assigning the sent letter id
                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], lettersmodel.SentLetterID.ToString());

                        // assigning the letter status id
                        // ex : 1- saved, 6- emailed
                        command.Parameters.Add(new SqlParameter("@EasyForms_PatientData_Status_StatusID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_PatientData_Status_StatusID"], lettersmodel.LetterStatus.ToString());

                        // assigning the letter send navigation id
                        command.Parameters.Add(new SqlParameter("@EasyForms_PatientData_Status_NavigationID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@EasyForms_PatientData_Status_NavigationID"], lettersmodel.LetterActionDoneNavigationID.ToString());

                        // assinging the letter send to email ids
                        command.Parameters.Add(new SqlParameter("@EasyForms_PatientData_SentTo_EmailIDs", SqlDbType.VarChar, 1056));
                        commonhelper.SetSqlParameterValue(command.Parameters["@EasyForms_PatientData_SentTo_EmailIDs"], lettersmodel.letterTemplateSendToEmailIDs);

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************





                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, lettersmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                        //assigning the output paramter 


                    }

                }
            }

            return model;
        }

        #endregion
    }

    internal class PatientHighRiskForSuicideOrHomicideDA
    {
        #region "                GetEasyFormsNotesStartEndTimeofallnotesdocumentedbySameProvideronSameDateValidation                "

        /// <summary>
        /// *******PURPOSE              : THIS METHOD IS USED TO GET EASYFORMS NOTES START END TIME OF ALL NOTES DOCUMENTED BY SAME PROVIDER ON SAME DATE VALIDATION
        ///*******CREATED BY            : Durga Prasad V
        ///*******CREATED DATE          : JAN 23 2024
        ///*******MODIFIED DEVELOPER    : DATE - NAME - WHAT IS MODIFIED; *************************
        /// </summary>
        /// <returns></returns>
        public ResponseModel PatientHighRiskForSuicideOrHomicideInsert(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            ResponseModel model = null; // HOLDs LIST OF REPORTS


            // CREATING THE INSTANCE FOR THE COMMON HELPER CLASS
            DBConnectHelper commonhelper = new DBConnectHelper();
            {
                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    // OPEN THE CONNECTION STATEMENT
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("USP_Send_Mail_To_Recipient_If_Patient_HighRisk_For_Suicide_Or_Homicide_Insert", con))
                    {
                        // command executed type
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************    

                        // logged user Id
                        command.Parameters.Add(new SqlParameter("@LoggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@LoggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@PatientID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@PatientID"], htmltemplatesavinginputmodel.patientchartmodel.PatientID);

                        command.Parameters.Add(new SqlParameter("@EasyformInstanceID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@EasyformInstanceID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID);

                        command.Parameters.Add(new SqlParameter("@Fillable_HTML_DocumentTemplateID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Fillable_HTML_DocumentTemplateID"], htmltemplatesavinginputmodel.FillableHTMLDocumentTemplateID);

                        command.Parameters.Add(new SqlParameter("@DOS", SqlDbType.DateTime));

                        if (htmltemplatesavinginputmodel.patientchartmodel != null && htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime != null
                            && htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime.ToString().Trim().Length > 0)
                        {
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@DOS"], htmltemplatesavinginputmodel.patientchartmodel.AppointmentDateTime);
                        }
                        else if (htmltemplatesavinginputmodel.EasyFormApptDateTime != null && htmltemplatesavinginputmodel.EasyFormApptDateTime.ToString().Trim().Length > 0)
                        {
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@DOS"], htmltemplatesavinginputmodel.EasyFormApptDateTime);
                        }
                        else
                        {
                            commonhelper.SetSqlParameterDateTime(command.Parameters["@DOS"], DateTime.Now.ToString("yyyyMMddHHmm"));
                        }

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();


                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();

                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);
                        //assigning the output paramter 
                        //if (command.Parameters["@EasyForms_PatientData_StrikeOrUnStrike_InfoID"].Value != null && command.Parameters["@EasyForms_PatientData_StrikeOrUnStrike_InfoID"].Value != DBNull.Value)
                        //{
                        //    model.MultipleResponseID = command.Parameters["@EasyForms_PatientData_StrikeOrUnStrike_InfoID"].Value.ToString();
                        //}


                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********


                    }
                }
            }

            return model;
        }
        #endregion


    }


    internal class HtmlTemplateDataAccess
    {
        #region"               AUTO DISCHRAGE HCI LINKED PROGRAM OF ESAY FROM             "

        /// <summary>
        /// THIS FUCNCTION IS USED TO DISCHARGE HCI LINKED PROGRAM EPISODE BASED ON SELECTED ESAY FROM FROM ADMISSION SETTINGS
        /// CREATED BY: JAYARAJU S
        /// CREATED ON: 10/03/2017
        /// </summary>
        /// <param name="htmltemplatesavinginputmodel"></param>
        /// <param name="emrwebexceptiontracelogmodel"></param>
        /// <returns></returns>
        public ResponseModel AutoHCILinkedProgramEpisodeWhileSavingEasyForm(HtmlTemplateSavingInputModel htmltemplatesavinginputmodel)
        {
            ResponseModel model = null;

            DBConnectHelper commonhelper = new DBConnectHelper();
            {

                //********BLOCK START RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********
                using (SqlConnection con = new SqlConnection(commonhelper.GetDBConnectionString(DBConnectHelper.DBToConnect.EMR, htmltemplatesavinginputmodel)))
                {
                    commonhelper.OpenSqlConnection(con);

                    using (SqlCommand command = new SqlCommand("USP_AutoDischarge_BasedonHCI_Program_Name", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        //***********INPUT PARAMETERS BLOCK START*****************                            

                        command.Parameters.Add(new SqlParameter("@loggedUserID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@loggedUserID"], htmltemplatesavinginputmodel.LoggedUserID.ToString());

                        command.Parameters.Add(new SqlParameter("@Patientid", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Patientid"], htmltemplatesavinginputmodel.patientchartmodel.PatientID.ToString());

                        command.Parameters.Add(new SqlParameter("@Documents_Fillable_HTML_Templates_PatientDataID", SqlDbType.Int));
                        commonhelper.SetSqlParameterInt32(command.Parameters["@Documents_Fillable_HTML_Templates_PatientDataID"], htmltemplatesavinginputmodel.DocumentsFillableHTMLTemplatesPatientDataID.ToString());

                        command.Parameters.Add(new SqlParameter("@isSignoff", SqlDbType.Bit));
                        commonhelper.SetSqlParameterBit(command.Parameters["@isSignoff"], htmltemplatesavinginputmodel.IsSignedOff);

                        //***********INPUT PARAMETERS BLOCK END*****************

                        //**********OUT PUT PARAMETERS BLOCK START***************

                        command.Parameters.Add(new SqlParameter("@InPat_CareLevel_Event_Patient_InfoID", SqlDbType.Int));
                        command.Parameters["@InPat_CareLevel_Event_Patient_InfoID"].Direction = ParameterDirection.Output;

                        //**********REGULAR OUT PUT PARAMETERS BLOCK START***************
                        commonhelper.SetRegularOutputSqlParameters(command, htmltemplatesavinginputmodel);
                        //**********REGULAR OUT PUT PARAMETERS BLOCK END***************
                        //**********OUT PUT PARAMETERS BLOCK END***************

                        command.ExecuteNonQuery();

                        if (con.State != ConnectionState.Closed)
                            con.Close();
                        //********BLOCK END RELATED TO CALLING THE SP AND ASSIGN THE DATA WHICH IS GETTING FROM THE DATABASE TO THE USER DATA CLASS VARIABLES.**********

                        model = new ResponseModel();
                        if (command.Parameters["@InPat_CareLevel_Event_Patient_InfoID"].Value != null && command.Parameters["@InPat_CareLevel_Event_Patient_InfoID"].Value != DBNull.Value)
                        {
                            model.ResponseID = (int)command.Parameters["@InPat_CareLevel_Event_Patient_InfoID"].Value;
                        }
                        //Calling Method to assign the data related to the OutPut Parameters of the Base Class.
                        commonhelper.GetOutParameterValuesWithResponseModel(command, model);

                    }
                }
            }


            //RETURNING THE MODEL
            return model;

        }

        #endregion


    }


    #endregion


}