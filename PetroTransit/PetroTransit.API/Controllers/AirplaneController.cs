using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetroTransit.API.Data;
using PetroTransit.API.Models;

namespace PetroTransit.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AirplanesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AirplanesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Airplane>>> GetAirplanes()
    {
        return await _context.Airplanes.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Airplane>> PostAirplane(Airplane airplane)
    {
        var isAdminClaim = User.FindFirst("isAdmin")?.Value;
        if (isAdminClaim != "True" && isAdminClaim != "true") 
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can add airplanes."); 
        }
        
        // Check for model state validity (Fixes "Failed to add plane" error)
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _context.Airplanes.Add(airplane);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAirplanes), new { id = airplane.Id }, airplane);
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> PutAirplane(int id, Airplane airplane)
    {
        var isAdminClaim = User.FindFirst("isAdmin")?.Value;
        if (isAdminClaim != "True" && isAdminClaim != "true") 
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can edit airplanes."); 
        }

        if (id != airplane.Id)
        {
            return BadRequest("Airplane ID mismatch.");
        }
        
        // Check for model state validity (Ensures PUT also validates)
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _context.Entry(airplane).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Airplanes.Any(e => e.Id == id))
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAirplane(int id)
    {
        var isAdminClaim = User.FindFirst("isAdmin")?.Value;
        if (isAdminClaim != "True" && isAdminClaim != "true") 
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can delete airplanes."); 
        }

        var airplane = await _context.Airplanes.FindAsync(id);
        if (airplane == null)
        {
            return NotFound();
        }

        _context.Airplanes.Remove(airplane);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
