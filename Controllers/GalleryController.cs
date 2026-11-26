using System.Security.Claims;
using FestiveGuestAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FestiveGuestAPI.Controllers;

/// <summary>
/// A host's photo gallery. Upload is two steps: get a one-photo upload link, the browser PUTs the
/// (already resized, location-stripped) JPEG to storage, then confirm it here.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GalleryController : ControllerBase
{
    private readonly IGalleryService _gallery;

    public GalleryController(IGalleryService gallery) => _gallery = gallery;

    private string? Me => User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("{userId}")]
    public async Task<IActionResult> Get(string userId) => Ok(await _gallery.GetAsync(userId));

    [HttpPost("upload-url")]
    public async Task<IActionResult> UploadUrl([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] UploadUrlRequest? request)
    {
        if (Me is not { } me) return Unauthorized();
        var (photoId, uploadUrl, error) = await _gallery.StartUploadAsync(me, request?.City);
        return error != null ? BadRequest(new { message = error }) : Ok(new { photoId, uploadUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddPhotoRequest request)
    {
        if (Me is not { } me) return Unauthorized();
        var (photos, error) = await _gallery.AddAsync(me, request.PhotoId ?? "", request.Caption, request.City);
        return error != null ? BadRequest(new { message = error }) : Ok(photos);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateGalleryRequest request)
    {
        if (Me is not { } me) return Unauthorized();
        var (photos, error) = await _gallery.UpdateAsync(me, request.Photos ?? new());
        return error != null ? BadRequest(new { message = error }) : Ok(photos);
    }

    [HttpDelete("{photoId}")]
    public async Task<IActionResult> Remove(string photoId)
    {
        if (Me is not { } me) return Unauthorized();
        return await _gallery.RemoveAsync(me, photoId) ? NoContent() : NotFound(new { message = "Photo not found." });
    }
}

public class AddPhotoRequest
{
    public string? PhotoId { get; set; }
    public string? Caption { get; set; }
    public string? City { get; set; } // one of the host's cities; empty for "All places"
}

public class UpdateGalleryRequest
{
    public List<GalleryPhoto>? Photos { get; set; }
}

public class UploadUrlRequest
{
    public string? City { get; set; } // checked up front, so a full place is refused before the upload
}
