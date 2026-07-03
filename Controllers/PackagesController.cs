using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Payment;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/packages")]
public class PackagesController(AppDbContext db) : ControllerBase
{
    [HttpGet("storage")]
    public async Task<ActionResult<IEnumerable<PackageDto>>> GetStoragePackages()
    {
        var packages = await db.StoragePackages
            .Where(p => p.IsActive == true)
            .OrderBy(p => p.Price)
            .Select(p => new PackageDto(
                p.Id,
                p.Name,
                p.Price,
                p.CapacityBytes,
                null,
                "storage",
                null
            ))
            .ToListAsync();

        return Ok(packages);
    }

    [HttpGet("subscription")]
    public async Task<ActionResult<IEnumerable<PackageDto>>> GetSubscriptionPackages()
    {
        var packages = await db.SubscriptionPackages
            .Where(p => p.IsActive == true)
            .OrderBy(p => p.Price)
            .Select(p => new PackageDto(
                p.Id,
                p.Name,
                p.Price,
                p.BaseStorageBytes,
                p.Description,
                "subscription",
                p.DurationDays
            ))
            .ToListAsync();

        return Ok(packages);
    }
}
