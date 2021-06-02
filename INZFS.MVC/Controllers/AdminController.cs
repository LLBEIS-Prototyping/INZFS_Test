﻿using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Settings;
using YesSql;
using INZFS.MVC.Models;
using INZFS.MVC.Forms;
using OrchardCore.Flows.Models;
using System.Collections.Generic;
using System.Linq.Expressions;
using System;

namespace INZFS.MVC.Controllers
{
    public class AdminController : Controller
    {

        private readonly IContentRepository _contentRepository;

        public AdminController(IContentRepository contentRepository)
        {
            _contentRepository = contentRepository;
        }


        [HttpGet]
        public async Task<IActionResult> GetApplicationsSearch(string companyName)
        {

            var applications = string.IsNullOrEmpty(companyName) ? new Dictionary<string, ContentItem>() : await GetContentItemListFromBagPart(companyName);

            var model = new FundManagerApplicationsModel
            {
                Applications = applications
            };

            return View(model);
        }

        private async Task<Dictionary<string, ContentItem>> GetContentItemListFromBagPart(string companyName)
        {
            var applicationListResult = new Dictionary<string, ContentItem>();

            Expression<Func<ContentItemIndex, bool>> expression = index => index.ContentType == ContentTypes.INZFSApplicationContainer;
            var applications = await _contentRepository.GetContentItems(expression, string.Empty);

            foreach (var application in applications)
            {
                var applicationContainer = application?.ContentItem.As<BagPart>();

                var contentItem = applicationContainer.ContentItems.FirstOrDefault(item => item.ContentType == ContentTypes.CompanyDetails);
                if (contentItem != null)
                {
                    var companyDetailsPart = contentItem?.ContentItem.As<CompanyDetailsPart>();

                    if (companyDetailsPart.CompanyName.ToLower().Contains(companyName.ToLower()))
                    {
                        applicationListResult.Add(companyDetailsPart.CompanyName, application);
                    }
                }

            }


            return applicationListResult;
        }

        [HttpGet]
        public async Task<IActionResult> Application(string id)
        {
            var application = await _contentRepository.GetContentItemById(id);
            var bagPart = application?.ContentItem?.As<BagPart>();
            var contents = bagPart?.ContentItems;

            return View(contents);
        }


    }

}


