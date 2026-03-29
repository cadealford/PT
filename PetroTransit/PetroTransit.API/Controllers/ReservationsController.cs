using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetroTransit.API.Data;
using PetroTransit.API.Models;

namespace PetroTransit.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;

    public ReservationsController(AppDbContext context, UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // Helper to get the current user ID
    private string GetCurrentUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("nameid")?.Value ?? 
               User.FindFirst("sub")?.Value ?? 
               "";
    }

    // GET: api/Reservations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Reservation>>> GetReservations([FromQuery] DateTime? start, [FromQuery] DateTime? end)
    {
        var query = _context.Reservations
            .Include(r => r.Airplane)
            .Include(r => r.Personnel)
            .AsQueryable();

        if (start.HasValue)
        {
            query = query.Where(r => r.EndTime >= start.Value.ToUniversalTime());
        }

        if (end.HasValue)
        {
            query = query.Where(r => r.StartTime <= end.Value.ToUniversalTime());
        }

        return await query.ToListAsync();
    }

    // GET: api/Reservations/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Reservation>> GetReservation(int id)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Airplane)
            .Include(r => r.Personnel)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
        {
            return NotFound();
        }

        return reservation;
    }

    // POST: api/Reservations
    [HttpPost]
    public async Task<ActionResult<Reservation>> PostReservation(Reservation reservation)
    {
        // Set the current user as the creator
        reservation.CreatedByUserId = GetCurrentUserId();

        // Ensure IDs are correct (EF Core handles the saving, but validation is key)
        if (reservation.AirplaneId == 0 || reservation.PersonnelId == 0)
        {
            return BadRequest("Airplane and Personnel selections are required.");
        }
        
        // --- REMOVED ALL DAY VALIDATION/LOGIC ---

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Fetch the reservation back with navigation properties for the client
        var createdReservation = await _context.Reservations
            .Include(r => r.Airplane)
            .Include(r => r.Personnel)
            .FirstOrDefaultAsync(r => r.Id == reservation.Id);
            
        return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, createdReservation);
    }

    // PUT: api/Reservations/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutReservation(int id, Reservation reservation)
    {
        if (id != reservation.Id)
        {
            return BadRequest();
        }

        // 1. Get current reservation data from DB
        var existingReservation = await _context.Reservations.FindAsync(id);

        if (existingReservation == null)
        {
            return NotFound();
        }

        // 2. Check Permissions (Admin OR Owner)
        var isAdminClaim = User.FindFirst("isAdmin")?.Value;
        bool isOwner = existingReservation.CreatedByUserId == GetCurrentUserId();
        bool isAdmin = isAdminClaim == "True" || isAdminClaim == "true";

        if (!isOwner && !isAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You do not have permission to modify this reservation.");
        }

        // 3. Update all properties
        existingReservation.StartTime = reservation.StartTime;
        existingReservation.EndTime = reservation.EndTime;
        // existingReservation.IsAllDay = reservation.IsAllDay; <-- REMOVED
        existingReservation.AirplaneId = reservation.AirplaneId;
        existingReservation.PersonnelId = reservation.PersonnelId;
        existingReservation.Location = reservation.Location;
        existingReservation.FlightDetails = reservation.FlightDetails;
        existingReservation.PassengerCount = reservation.PassengerCount;
        existingReservation.Notes = reservation.Notes;

        _context.Entry(existingReservation).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Reservations.Any(e => e.Id == id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    // DELETE: api/Reservations/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReservation(int id)
    {
        var existingReservation = await _context.Reservations.FindAsync(id);
        if (existingReservation == null)
        {
            return NotFound();
        }

        var isAdminClaim = User.FindFirst("isAdmin")?.Value;
        bool isOwner = existingReservation.CreatedByUserId == GetCurrentUserId();
        bool isAdmin = isAdminClaim == "True" || isAdminClaim == "true";

        if (!isOwner && !isAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You do not have permission to delete this reservation.");
        }

        _context.Reservations.Remove(existingReservation);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
