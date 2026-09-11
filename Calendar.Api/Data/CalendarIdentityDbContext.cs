using Calendar.Api.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Calendar.Api.Data;

/// <summary>
/// Stores local Calendar Hub users and roles.
/// </summary>
public sealed class CalendarIdentityDbContext : IdentityDbContext<CalendarUser>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarIdentityDbContext"/> class.
    /// </summary>
    /// <param name="options">The configured database options.</param>
    public CalendarIdentityDbContext(DbContextOptions<CalendarIdentityDbContext> options)
        : base(options)
    {
    }
}