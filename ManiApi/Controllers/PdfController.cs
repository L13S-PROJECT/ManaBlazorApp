using Microsoft.AspNetCore.Mvc;
using ManiApi.Services.Pdf;

namespace ManiApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfController : ControllerBase
{
    private readonly PdfService _pdfService;

    public PdfController(PdfService pdfService)
    {
        _pdfService = pdfService;
    }

    [HttpPost("parse")]
    public async Task<IActionResult> ParsePdf(IFormFile file)
    {
        using var ms = new MemoryStream();

        await file.CopyToAsync(ms);

        var result = _pdfService.ParseDocument(ms.ToArray());

        return Ok(result);
    }
}