using INZFS.MVC;
using INZFS.MVC.Models;
using iText.Html2pdf;
using iText.Kernel.Pdf;
using System;
using System.IO;
using System.Threading.Tasks;

public class ReportService : IReportService
{
    private string html;
    private string tableStyle = @"style=""margin-bottom:2rem; width:100%; border:1px solid grey;""";
    private string questionTableStyle = @"style=""background-color:rgb(248,241,220); width:100%; border:1px solid grey;""";
    private string questionHeaderStyle = @"style=""text-align:left;""";

    private readonly ApplicationDefinition _applicationDefinition;
    private readonly IContentRepository _contentRepository;
    private ApplicationContent _applicationContent;
    
    public ReportService(IContentRepository contentRepository, ApplicationDefinition applicationDefinition)
    {
        _contentRepository = contentRepository;
        _applicationDefinition = applicationDefinition;
    }

    public async Task<byte[]> GeneratePdfReport(string applicationAuthor)
    {
        _applicationContent = await _contentRepository.GetApplicationContent(applicationAuthor);

        OpenHtmlString();
        PopulateHtmlSections();
        CloseHtmlString();

        using (MemoryStream stream = new())
        using (PdfWriter writer = new(stream))
        {
            HtmlConverter.ConvertToPdf(html, writer);
            return stream.ToArray();
        }
    }

    private void OpenHtmlString()
    {
        html = $@"
           <!DOCTYPE html>
           <html lang=""en"">
           <head>
           </head>
          <body>
            <h1 style=""text-align:center;"">EEF 8A Application Form</h1>
          ";
    }

    private void CloseHtmlString()
    {
        html = html + "</body></html>";
    }

    private void PopulateHtmlSections()
    {
        foreach (var section in _applicationDefinition.Application.Sections)
        {
            string sectionHtml = $@"
                <h2>{ section.Title }</h2>
            ";
            html = html + sectionHtml;

            PopulateHtmlQuestions(section);
        }
    }

    private void PopulateHtmlQuestions(Section section)
    {
        foreach (var page in section.Pages)
        {
            String questionHtml = $@"
                <table { tableStyle }>
                  <tr { questionTableStyle }>
                    <th { questionHeaderStyle }>{ page.Question }</th>
                  </tr>
                  <tr>
                    <td>{ GetAnswer(page) }</td>
                  </tr>
                </table>
                ";
            html = html + questionHtml;
        }
    }

    private string GetAnswer(Page page)
    {
        var answer = _applicationContent?.Fields.Find(question => question.Name == page.Name);

        if (answer == null)
        {
            return @"<span style=""color:red;"">No response</span>";
        }
        else 
        {
            return answer.Data;
        }
    }
}
