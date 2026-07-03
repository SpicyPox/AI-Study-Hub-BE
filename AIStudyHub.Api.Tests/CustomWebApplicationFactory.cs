using AIStudyHub.Api.Data;
using AIStudyHub.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace AIStudyHub.Api.Tests
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // 1. Force environment to "Testing"
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // 2. Seed database with required seed data (like default roles)
                var sp = services.BuildServiceProvider();
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<AppDbContext>();

                    db.Database.EnsureDeleted();
                    db.Database.EnsureCreated();

                    // Seed default role "user"
                    if (!db.Roles.Any(r => r.Name == "user"))
                    {
                        db.Roles.Add(new Role
                        {
                            Id = Guid.NewGuid(),
                            Name = "user",
                            Description = "Default User Role",
                            CreatedAt = DateTime.UtcNow
                        });
                        db.SaveChanges();
                    }
                }
            });
        }
    }
}
