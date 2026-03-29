using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PetroTransit.API.Models;

namespace PetroTransit.API.Data;

// CHANGE: Inherit from IdentityDbContext<AppUser> instead of just DbContext
public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // You don't need a DbSet for Users because IdentityDbContext already includes it!
    // public DbSet<AppUser> Users { get; set; } <-- REMOVE THIS if you have it
    
    public DbSet<Airplane> Airplanes { get; set; }
    public DbSet<Personnel> Personnel { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
}