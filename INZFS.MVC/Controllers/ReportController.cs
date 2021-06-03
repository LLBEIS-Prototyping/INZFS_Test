﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace INZFS.MVC.Controllers
{
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;
        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }
        [HttpGet]
        public FileContentResult GeneratePdf(string companyName, string applicationId)
        {
            byte[] bytes = _reportService.GeneratePdfReport(companyName, applicationId);
            string type = "application/pdf";
            string name = $"EEF_{ companyName.Trim() }_{ applicationId }.pdf";

            return File(bytes, type, name);
        }
    }
}