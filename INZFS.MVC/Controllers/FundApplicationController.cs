﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using INZFS.MVC.ViewModels;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Routing;
using Microsoft.AspNetCore.Authorization;
using INZFS.MVC.Models;
using INZFS.MVC.Forms;
using Microsoft.AspNetCore.Http;
using OrchardCore.Media;
using Microsoft.Extensions.Logging;
using INZFS.MVC.Models.DynamicForm;
using INZFS.MVC.Services.FileUpload;
using INZFS.MVC.Services.VirusScan;
using System.Text.Json;
using ClosedXML.Excel;
using OrchardCore.FileStorage;
using System.IO;
using ClosedXML.Excel.CalcEngine.Exceptions;
using System.Globalization;

namespace INZFS.MVC.Controllers
{
    [Authorize]
    public class FundApplicationController : Controller
    {
        private readonly IContentManager _contentManager;
        private readonly IVirusScanService _virusScanService;
        private readonly IFileUploadService _fileUploadService;
        private readonly IMediaFileStore _mediaFileStore;
        private readonly dynamic New;
        private readonly INotifier _notifier;
        private readonly YesSql.ISession _session;
        private readonly IUpdateModelAccessor _updateModelAccessor;
        private readonly INavigation _navigation;
        private readonly ILogger _logger;
        private readonly IContentRepository _contentRepository;
        private readonly ApplicationDefinition _applicationDefinition;

        public FundApplicationController(ILogger<FundApplicationController> logger, IContentManager contentManager, IMediaFileStore mediaFileStore, IContentDefinitionManager contentDefinitionManager,
            IContentItemDisplayManager contentItemDisplayManager, IHtmlLocalizer<FundApplicationController> htmlLocalizer,
            INotifier notifier, YesSql.ISession session, IShapeFactory shapeFactory,
            IUpdateModelAccessor updateModelAccessor, INavigation navigation, IContentRepository contentRepository, IFileUploadService fileUploadService, IVirusScanService virusScanService, ApplicationDefinition applicationDefinition)
        {
            _contentManager = contentManager;
            _mediaFileStore = mediaFileStore;
            _notifier = notifier;
            _session = session;
            _updateModelAccessor = updateModelAccessor;
            _logger = logger;
            New = shapeFactory;
            _navigation = navigation;
            _contentRepository = contentRepository;
            _virusScanService = virusScanService;
            _fileUploadService = fileUploadService;
            _applicationDefinition = applicationDefinition;
        }

        [HttpGet]
        public async Task<IActionResult> Section(string pagename, string id)
        {
            if(string.IsNullOrEmpty(pagename))
            {
                return NotFound();
            }
            pagename = pagename.ToLower().Trim();


            // Page
            var currentPage = _applicationDefinition.Application.AllPages.FirstOrDefault(p => p.Name.ToLower().Equals(pagename));
            if(currentPage != null)
            {
                var content= await _contentRepository.GetApplicationContent(User.Identity.Name);
                var field = content?.Fields?.FirstOrDefault(f => f.Name.Equals(currentPage.FieldName));
                return GetViewModel(currentPage, field);
            }

            //Overview
            if (pagename.ToLower() == "application-overview")
            {
                var sections = _applicationDefinition.Application.Sections;
                var applicationOverviewContentModel = new ApplicationOverviewContent();

                var content = await _contentRepository.GetApplicationContent(User.Identity.Name);

                foreach (var section in sections)
                {
                    var sectionContentModel = GetSectionContent(content, section);
                    var applicationOverviewModel = new ApplicationOverviewModel();
                    applicationOverviewModel.SectionTag = section.Tag;
                    applicationOverviewModel.Title = sectionContentModel.OverviewTitle;
                    applicationOverviewModel.Url = sectionContentModel.Url;
                    applicationOverviewModel.SectionStatus = sectionContentModel.OverallStatus;

                    applicationOverviewContentModel.Sections.Add(applicationOverviewModel);
                }

                applicationOverviewContentModel.TotalSections = sections.Count;
                applicationOverviewContentModel.TotalSectionsCompleted = applicationOverviewContentModel.
                                                    Sections.Count(section => section.SectionStatus == SectionStatus.Completed);

                return View("ApplicationOverview", applicationOverviewContentModel);
            }

            // Section
            var currentSection = _applicationDefinition.Application.Sections.FirstOrDefault(section => section.Url.Equals(pagename));
            if (currentSection != null)
            {
                var content = await _contentRepository.GetApplicationContent(User.Identity.Name);
                var sectionContentModel = GetSectionContent(content, currentSection);
                return View(currentSection.RazorView, sectionContentModel);
            }


            return NotFound();

        }

        public async Task<bool> CreateDirectory(string directoryName)
        {
            if(directoryName == null)
            {
                return false;
            }
            await _mediaFileStore.TryCreateDirectoryAsync(directoryName);
            return true;
        }

        [Route("FundApplication/section/{pageName}")]
        [HttpPost, ActionName("save")]
        [FormValueRequired("submit.Publish")]
        public async Task<IActionResult> Save([Bind(Prefix = "submit.Publish")] string submitAction, string returnUrl, string pageName, IFormFile? file, BaseModel model)
        {
            var currentPage = _applicationDefinition.Application.AllPages.FirstOrDefault(p => p.Name.ToLower().Equals(pageName));
            if (currentPage.FieldType == FieldType.gdsFileUpload)
            {
                if (file != null || submitAction == "UploadFile")
                {
                    var errorMessage = await _fileUploadService.Validate(file);
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        //TODO - Handle validation Error
                        ModelState.AddModelError("DataInput", errorMessage);
                    }
                }
                //UploadFile
                //else
                //{
                //    //TODO - Handle validation Error
                //    if (submitAction != "DeleteFile")
                //    {
                //        ModelState.AddModelError("DataInput", "No file was uploaded.");
                //    }
                //}
            }
            if (ModelState.IsValid || submitAction == "DeleteFile")
            {
                var contentToSave = await _contentRepository.GetApplicationContent(User.Identity.Name);
                if (contentToSave == null)
                {
                    contentToSave = new ApplicationContent();
                    contentToSave.Application = new Application();
                    contentToSave.Author = User.Identity.Name;
                    contentToSave.CreatedUtc = DateTime.UtcNow;
                }

                contentToSave.ModifiedUtc = DateTime.UtcNow;

                
                string publicUrl = string.Empty;
                var additionalInformation = string.Empty;
                if (currentPage.FieldType == FieldType.gdsFileUpload)
                {
                    if (file != null || submitAction.ToLower() == "UploadFile".ToLower())
                    {
                        //var errorMessage = await _fileUploadService.Validate(file);
                        //if (!string.IsNullOrEmpty(errorMessage))
                        //{
                        //    //TODO - Handle validation Error
                        //    ModelState.AddModelError("DataInput", errorMessage);
                        //}

                        var directoryName = Guid.NewGuid().ToString();
                        publicUrl = await _fileUploadService.SaveFile(file, directoryName);
                        model.DataInput = file.FileName;

                        var uploadedFile = new UploadedFile()
                        {
                            FileLocation = publicUrl,
                            Name = file.FileName,
                            Size = (file.Length / (double)Math.Pow(1024, 2)).ToString("0.00")
                        };

                        if (file.FileName.ToLower().Contains(".xlsx") && currentPage.Name == "project-cost-breakdown")
                        {
                            // If env is Development, prepend local filepath to publicUrl to ensure functionality
                            if(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                            {
                                publicUrl = _mediaFileStore.NormalizePath("/App_Data/Sites/Default" + publicUrl);
                            }

                            try
                            {
                                XLWorkbook wb = new(publicUrl);

                                try
                                {
                                    IXLWorksheet ws = wb.Worksheet("A. Summary");
                                    //IXLCell totalGrantFunding = ws.Cell("A8");
                                    IXLCell totalGrantFunding = ws.Search("Total sum requested from BEIS").First<IXLCell>();
                                    IXLCell totalMatchFunding = ws.Search("Match funding contribution").First<IXLCell>();
                                    IXLCell totalProjectFunding = ws.Search("Total project costs").First<IXLCell>();

                                    bool spreadsheetValid = totalGrantFunding != null && totalMatchFunding != null && totalProjectFunding != null;

                                    if (spreadsheetValid)
                                    {
                                        try
                                        {
                                            ParsedExcelData parsedExcelData = new();
                                            parsedExcelData.ParsedTotalProjectCost = totalProjectFunding.CellRight().GetValue<double>().ToString("£0.00");
                                            parsedExcelData.ParsedTotalGrantFunding = totalGrantFunding.CellRight().GetValue<double>().ToString("£0.00");
                                            parsedExcelData.ParsedTotalGrantFundingPercentage = totalGrantFunding.CellRight().CellRight().GetValue<double>().ToString("0.00%");
                                            parsedExcelData.ParsedTotalMatchFunding = totalMatchFunding.CellRight().GetValue<double>().ToString("£0.00");
                                            parsedExcelData.ParsedTotalMatchFundingPercentage = totalMatchFunding.CellRight().CellRight().GetValue<double>().ToString("0.00%");
                                            uploadedFile.ParsedExcelData = parsedExcelData;
                                        }
                                        catch (DivisionByZeroException e)
                                        {
                                            ModelState.AddModelError("DataInput", "Template spreadsheet is incomplete.");
                                        }
                                        catch (FormatException e)
                                        {
                                            ModelState.AddModelError("DataInput", "Template spreadsheet is incomplete.");

                                        }
                                    }
                                    else
                                    {
                                        ModelState.AddModelError("DataInput", "Uploaded spreadsheet does not match the expected formatting. Please use the provided template.");
                                    }
                                }
                                catch (ArgumentException e)
                                {
                                    ModelState.AddModelError("DataInput", "Uploaded spreadsheet does not match the expected formatting. Please use the provided template.");
                                }
                            }
                            catch (InvalidDataException e)
                            {
                                ModelState.AddModelError("DataInput", "Invalid file uploaded");
                                return PopulateViewModel(currentPage, model);
                            }
                        }

                        additionalInformation = JsonSerializer.Serialize(uploadedFile);
                    }
                    else
                    {
                        var existingData = contentToSave.Fields.FirstOrDefault(f => f.Name.Equals(currentPage.FieldName));

                        //TODO - Handle validation Error
                        if (submitAction != "DeleteFile" && string.IsNullOrEmpty(existingData?.AdditionalInformation))
                        {
                            ModelState.AddModelError("DataInput", "No file was uploaded.");
                        }
                        else
                        {
                            additionalInformation = existingData.AdditionalInformation;
                        }

                    }
                }

                var existingFieldData = contentToSave.Fields.FirstOrDefault(f => f.Name.Equals(currentPage.FieldName));
                if(existingFieldData == null)
                {
                    contentToSave.Fields.Add(new Field {
                        Name = currentPage.FieldName,
                        Data = model.GetData(),
                        OtherOption = model.GetOtherSelected(),
                        MarkAsComplete = model.ShowMarkAsComplete ? model.MarkAsComplete : null,
                        AdditionalInformation = currentPage.FieldType == FieldType.gdsFileUpload ? additionalInformation : null
                    });
                }
                else
                {
                    if (currentPage.FieldType == FieldType.gdsFileUpload)
                    {
                        // TODO Delete  the old file

                        bool fileHasChanged = additionalInformation != existingFieldData?.AdditionalInformation;
                        if ((fileHasChanged && !string.IsNullOrEmpty(existingFieldData?.AdditionalInformation)) || submitAction == "DeleteFile")
                        {
                            var uploadedFile = JsonSerializer.Deserialize<UploadedFile>(existingFieldData.AdditionalInformation);
                            var deleteSucessful = await _fileUploadService.DeleteFile(uploadedFile.FileLocation);
                            if(submitAction == "DeleteFile")
                            {
                                additionalInformation = null;
                            }

                        }

                    }
                    existingFieldData.Data = model.GetData();
                    if(existingFieldData.Data == "Other")
                    {
                        existingFieldData.OtherOption = model.GetOtherSelected();
                    }
                    else
                    {
                        existingFieldData.OtherOption = null;
                    }
                    existingFieldData.MarkAsComplete = model.ShowMarkAsComplete ? model.MarkAsComplete : null;
                    existingFieldData.AdditionalInformation = currentPage.FieldType == FieldType.gdsFileUpload ? additionalInformation : null;
                }

                //Delete the data from dependants
                var datafieldForCurrentPage = contentToSave.Fields.FirstOrDefault(f => f.Name == currentPage.FieldName);
                //Get all pages that depends on the current field and its value
                var dependantPages = _applicationDefinition.Application.AllPages.Where(page => page.DependsOn?.FieldName == currentPage.FieldName);


                foreach (var dependantPage in dependantPages)
                {
                    if(dependantPage.DependsOn.Value != datafieldForCurrentPage.Data)
                    {
                        contentToSave.Fields.RemoveAll(field => field.Name == dependantPage.FieldName);
                    }
                }


                _session.Save(contentToSave);

                if (currentPage != null && currentPage.Actions != null && currentPage.Actions.Count > 0)
                {
                    var action = currentPage.Actions.FirstOrDefault(a => a.Value.ToLower().Equals(model.GetData()));
                    // action logic based on value
                    return RedirectToAction("section", new { pagename = action.PageName });
                }

                if (submitAction == "DeleteFile" || submitAction == "SaveProgress" || submitAction == "UploadFile")
                {
                    return RedirectToAction("section", new { pagename = pageName });
                }

                if (currentPage.NextPageName != null)
                {
                    return RedirectToAction("section", new { pagename = currentPage.NextPageName });
                }

                //TODO - replace all the references to AllPages with section.Pages
                var index = _applicationDefinition.Application.AllPages.FindIndex(p => p.Name.ToLower().Equals(pageName));
                var currentSection = _applicationDefinition.Application.Sections.Where(s => s.Pages.Any(c => c.Name == pageName.ToLower())).FirstOrDefault();

                //Dependant pages
                Page nextPage = null;
                while(true)
                {
                    nextPage = _applicationDefinition.Application.AllPages.ElementAtOrDefault(index + 1);
                    var dependsOn = nextPage?.DependsOn;
                    if (dependsOn == null)
                    {
                        break;
                    }

                    var dependantPageField = contentToSave.Fields.FirstOrDefault(field => field.Name.ToLower().Equals(dependsOn.FieldName));
                    
                    //TODO This will NOT work for all page types for now
                    if(dependantPageField.Data == dependsOn.Value)
                    {
                        break;
                    }

                    index++;
                }

                
                if (nextPage == null)
                {
                    //If there is no other page, then redirect back to the section page
                    return RedirectToAction("section", new { pagename = currentSection.ReturnUrl ?? currentSection.Url }); ;
                }

                
                var inSection = currentSection.Pages.Contains(nextPage);
                if (!inSection)
                {
                    // If next page exists, but it is optional or not applicable ( depending on the answer to the previous question),
                    // the also redirect back to section
                    return RedirectToAction("section", new { pagename = currentSection.ReturnUrl ?? currentSection.Url });
                     
                }
                
                //TODO: Check of non-existing pages
                // check for the last page
                return RedirectToAction("section", new { pagename = nextPage.Name });
            }
            else
            {
                await _session.CancelAsync();
                currentPage = _applicationDefinition.Application.AllPages.FirstOrDefault(p => p.Name.ToLower().Equals(pageName));
                return PopulateViewModel(currentPage, model);
            }
        }

        public async Task<IActionResult> Submit()
        {
            return View("ApplicationSubmit");
        }

        public async Task<IActionResult> Complete()
        {
            return Redirect("/complete/applicationcomplete");
        }

        private ViewResult GetViewModel(Page currentPage, Field field)
        {
            BaseModel model;
            switch (currentPage.FieldType)
            {
                case FieldType.gdsSingleRadioSelectOption:
                    model = new RadioSingleSelectModel();
                    return View("SingleRadioSelectInput", PopulateModel(currentPage, model, field));
                case FieldType.gdsTextBox:
                    model = new TextInputModel();
                    return View("TextInput", PopulateModel(currentPage, model, field));
                case FieldType.gdsCurrencyBox:
                    model = new CurrencyInputModel();
                    return View("CurrencyInput", PopulateModel(currentPage, model, field));
                case FieldType.gdsTextArea:
                    model = new TextAreaModel();
                    return View("TextArea", PopulateModel(currentPage, model, field));
                case FieldType.gdsDateBox:
                    model = PopulateModel(currentPage, new DateModel(), field);
                    var dateModel = (DateModel)model;
                    if (!string.IsNullOrEmpty(model.DataInput))
                    {
                        var inputDate = DateTime.Parse(model.DataInput, CultureInfo.GetCultureInfoByIetfLanguageTag("en-GB"));
                        dateModel.Day = inputDate.Day;
                        dateModel.Month = inputDate.Month;
                        dateModel.Year = inputDate.Year;
                    }
                    return View("DateInput", model);
                case FieldType.gdsMultiLineRadio:
                    model = new MultiRadioInputModel();
                    return View("MultiRadioInput", PopulateModel(currentPage, model, field));
                case FieldType.gdsYesorNoRadio:
                    model = new YesornoInputModel();
                    return View("YesornoInput", PopulateModel(currentPage, model, field));
                case FieldType.gdsMultiSelect:
                    model = PopulateModel(currentPage, new MultiSelectInputModel(), field);
                    var multiSelect = (MultiSelectInputModel)model;
                    if (!string.IsNullOrEmpty(model.DataInput))
                    {
                        var UserInputList = model.DataInput.Split(',').ToList();
                        multiSelect.UserInput = UserInputList;
                    }
                    return View("MultiSelectInput", PopulateModel(currentPage, model));
                case FieldType.gdsAddressTextBox:
                    model = PopulateModel(currentPage, new AddressInputModel(), field);
                    var addressInputModel = (AddressInputModel)model;
                    if (!string.IsNullOrEmpty(model.DataInput))
                    {
                        var userAddress = model.DataInput.Split(',').ToList();
                        addressInputModel.AddressLine1 = userAddress[0];
                        addressInputModel.AddressLine2 = userAddress[1];
                        addressInputModel.City = userAddress[2];
                        addressInputModel.County = userAddress[3];
                        addressInputModel.PostCode = userAddress[4];
                    }
                    return View("AddressInput", PopulateModel(currentPage, model));
                case FieldType.gdsFileUpload:
                    model = PopulateModel(currentPage, new FileUploadModel(), field);
                    var uploadmodel = (FileUploadModel)model;
                    if(!string.IsNullOrEmpty(field?.AdditionalInformation))
                    {
                        uploadmodel.UploadedFile = JsonSerializer.Deserialize<UploadedFile>(field.AdditionalInformation);
                    }
                    return View("FileUpload", uploadmodel);
                case FieldType.gdsStaticPage:
                    model = new StaticPageModel();
                    return View("_StaticPage", PopulateModel(currentPage, model, field));
                default:
                    throw new Exception("Invalid field type");
                
            }
        }

        protected ViewResult PopulateViewModel(Page currentPage, BaseModel currentModel)
        {
            switch (currentPage.FieldType)
            {
                case FieldType.gdsTextBox:
                    return View("TextInput", PopulateModel(currentPage, currentModel));
                case FieldType.gdsTextArea:
                    return View("TextArea", PopulateModel(currentPage, currentModel));
                case FieldType.gdsDateBox:
                    return View("DateInput", PopulateModel(currentPage, currentModel));
                case FieldType.gdsMultiLineRadio:
                    return View("MultiRadioInput", PopulateModel(currentPage, currentModel));
                case FieldType.gdsYesorNoRadio:
                    return View("YesornoInput", PopulateModel(currentPage, currentModel));
                case FieldType.gdsMultiSelect:
                    return View("MultiSelectInput", PopulateModel(currentPage, currentModel));
                case FieldType.gdsFileUpload:
                    return View("FileUpload", PopulateModel(currentPage, currentModel));
                case FieldType.gdsCurrencyBox:
                    return View("CurrencyInput", PopulateModel(currentPage, currentModel));
                case FieldType.gdsSingleRadioSelectOption:
                    return View("SingleRadioSelectInput", PopulateModel(currentPage, currentModel));
                case FieldType.gdsAddressTextBox:
                    return View("AddressInput", PopulateModel(currentPage, currentModel));
                case FieldType.gdsStaticPage:
                    return View("_StaticPage", PopulateModel(currentPage, currentModel));
                default:
                    throw new Exception("Invalid field type");
            }
        }

        private BaseModel PopulateModel(Page currentPage, BaseModel currentModel, Field field = null)
        {

            currentModel.Question = currentPage.Question;
            currentModel.PageName = currentPage.Name;
            currentModel.FieldName = currentPage.FieldName;
            currentModel.Hint = currentPage.Hint;
            currentModel.NextPageName = currentPage.NextPageName;
            currentModel.ReturnPageName = currentPage.ReturnPageName;
            currentModel.ShowMarkAsComplete = currentPage.ShowMarkComplete;
            currentModel.HasOtherOption = currentPage.HasOtherOption;
            currentModel.MaxLength = currentPage.MaxLength;
            currentModel.MaxLengthValidationType = currentPage.MaxLengthValidationType;
            currentModel.SelectedOptions = currentPage.SelectOptions;
            
            if(currentPage.Actions?.Count > 0)
            {
                currentModel.Actions = currentPage.Actions;
            }
            if (currentPage.ShowMarkComplete)
            {
                currentModel.MarkAsComplete = field?.MarkAsComplete != null ? field.MarkAsComplete.Value : false;
            }
            if (currentPage.PreviousPage != null)
            {
                currentModel.PreviousPage = currentPage.PreviousPage;
            }

            currentModel.FileToDownload = currentPage.FileToDownload;

            if (!string.IsNullOrEmpty(field ?.Data))
            {
                currentModel.DataInput = field?.Data;
            }
            if (!string.IsNullOrEmpty(field?.OtherOption))
            {
                currentModel.OtherOption = field?.OtherOption;
            }


            var currentSection = _applicationDefinition.Application.Sections.FirstOrDefault(section =>
                                         section.Pages.Any(page => page.Name == currentPage.Name));
            var index = currentSection.Pages.FindIndex(p => p.Name.ToLower().Equals(currentPage.Name));

            currentModel.QuestionNumber = index + 1;
            currentModel.TotalQuestions = currentSection.Pages.Count(p => !p.HideFromSummary);

            if (string.IsNullOrEmpty(currentPage.ContinueButtonText))
            {
                currentModel.ContinueButtonText = currentSection.ContinueButtonText;
            }else
            {
                currentModel.ContinueButtonText = currentPage.ContinueButtonText;
            }
            currentModel.ReturnToSummaryPageLinkText = currentSection.ReturnToSummaryPageLinkText;
            currentModel.SectionUrl = currentSection.Url;
            currentModel.SectionInfo = currentSection;

            var currentPageIndex = currentSection.Pages.FindIndex(p => p.Name == currentPage.Name);
            if (currentPageIndex >= 1)
            {
                currentModel.PreviousPageName = currentSection.Pages[currentPageIndex -1].Name;
            } 


            if (!string.IsNullOrEmpty(currentPage.Description))
            {
                currentModel.Description = currentPage.Description;
            }

            if (!string.IsNullOrEmpty(currentPage.UploadText))
            {
                currentModel.UploadText = currentPage.UploadText;
            }

            currentModel.DisplayQuestionCounter = currentPage.DisplayQuestionCounter;
            currentModel.GridDisplayType = currentPage.GridDisplayType;

            return currentModel;
        }

        private SectionContent GetSectionContent(ApplicationContent content, Section section)
        {
            var sectionContentModel = new SectionContent();
            sectionContentModel.TotalQuestions = section.Pages.Count(p => !p.HideFromSummary);
            sectionContentModel.Sections = new List<SectionModel>();
            sectionContentModel.Title = section.Title;
            sectionContentModel.OverviewTitle = section.OverviewTitle;
            sectionContentModel.Url = section.Url;
            sectionContentModel.ReturnUrl = section.ReturnUrl;

            foreach (var pageContent in section.Pages)
            {
                var dependsOn = pageContent.DependsOn;
                if (dependsOn != null)
                {
                    var datafieldForCurrentPage = content?.Fields?.FirstOrDefault(f => f.Name.Equals(dependsOn.FieldName));
                    if (datafieldForCurrentPage?.Data != dependsOn.Value)
                    {
                        continue;
                    }
                }

                var field = content?.Fields?.FirstOrDefault(f => f.Name.Equals(pageContent.FieldName));
                
                var sectionModel = new SectionModel();
                sectionModel.Title = pageContent.SectionTitle ?? pageContent.Question;
                sectionModel.Url = pageContent.Name;
                sectionModel.HideFromSummary = pageContent.HideFromSummary;

                if (string.IsNullOrEmpty(field?.Data) && string.IsNullOrEmpty(field?.AdditionalInformation))
                {
                    sectionModel.SectionStatus = SectionStatus.NotStarted;
                }
                else
                {
                    bool markAsComplete = true;
                    if (pageContent.ShowMarkComplete)
                    {
                        markAsComplete = field?.MarkAsComplete != null ? field.MarkAsComplete.Value : false;
                    }
                    
                    if (markAsComplete)
                    {
                        sectionModel.SectionStatus = SectionStatus.Completed;
                        sectionContentModel.TotalQuestionsCompleted++;
                    }
                    else
                    {
                        sectionModel.SectionStatus = SectionStatus.InProgress;
                    }

                }
                sectionContentModel.Sections.Add(sectionModel);
            }

            sectionContentModel.TotalQuestions = sectionContentModel.Sections.Count;

            return sectionContentModel;
        }
    }
}
