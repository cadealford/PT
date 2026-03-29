using Microsoft.AspNetCore.Identity;

namespace PetroTransit.API.Models;

public class AppUser : IdentityUser
{
    // IdentityUser already has Id, UserName, Email, PasswordHash, etc.
    public string FullName { get; set; } = string.Empty;

    public bool IsAdmin { get; set; } = false;

}