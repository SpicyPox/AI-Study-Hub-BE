    using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.Api.Controllers;

/// <summary>
/// Development-only stub endpoints that replace real S3 and AI services.
/// Remove or protect in production.
/// </summary>
[ApiController]
[Route("api/stub")]
public class StubController : ControllerBase
{
    // Accepts the PUT file upload that would normally go to S3
    [HttpPut("upload/{**key}")]
    public IActionResult ReceiveUpload(string key) => Ok(new { key });
}
