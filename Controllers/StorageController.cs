using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FestiveGuestAPI.Services;

namespace FestiveGuestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StorageController : ControllerBase
{
    private readonly ISasTokenService _sasTokenService;

    public StorageController(ISasTokenService sasTokenService)
    {
        _sasTokenService = sasTokenService;
    }

    [HttpGet("get-sas-url/{fileName}")]
    [AllowAnonymous]
    public IActionResult GetSasUrl(string fileName)
    {
        try
        {
            var sasUrl = _sasTokenService.GenerateSasUrl(fileName, "logos");
            return Ok(new { url = sasUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("get-sas-url")]
    [AllowAnonymous]
    public IActionResult GetSasUrlFromQuery([FromQuery] string fileName, [FromQuery] string container = "logos")
    {
        try
        {
            var sasUrl = _sasTokenService.GenerateSasUrl(fileName, container);
            return Ok(new { url = sasUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
