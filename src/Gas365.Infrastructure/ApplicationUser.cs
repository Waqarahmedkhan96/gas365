using Microsoft.AspNetCore.Identity;

namespace Gas365.Infrastructure;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
}
