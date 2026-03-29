using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetroTransit.API.Data;
using PetroTransit.API.Models;

namespace PetroTransit.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PersonnelController : ControllerBase
{
    private readonly AppDbContext _context;

    public PersonnelController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Personnel>>> GetPersonnel()
    {
        return await _context.Personnel.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Personnel>> PostPersonnel(Personnel personnel)
    {
        var isAdminClaim = User.FindFirst("isAdmin")?.Value;
        if (isAdminClaim != "True" && isAdminClaim != "true") 
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can add personnel."); 
        }

        if (!ModelState.IsValid) // <-- Added validation check
        {
            return BadRequest(ModelState);
        }
        
        _context.Personnel.Add(personnel);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPersonnel), new { id = personnel.Id }, personnel);
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> PutPersonnel(int id, Personnel personnel)
    {
        var isAdminClaim = User.FindFirst("isAdmin")?.Value;
        if (isAdminClaim != "True" && isAdminClaim != "true") 
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can edit personnel."); 
        }

        if (id != personnel.Id)
        {
            return BadRequest("Personnel ID mismatch.");
        }

        if (!ModelState.IsValid) // <-- Added validation check
        {
            return BadRequest(ModelState);
        }

        _context.Entry(personnel).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Personnel.Any(e => e.Id == id))
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
    public async Task<IActionResult> DeletePersonnel(int id)
    {
        var isAdminClaim = User.FindFirst("isAdmin")?.Value;
        if (isAdminClaim != "True" && isAdminClaim != "true") 
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can delete personnel."); 
        }

        var personnel = await _context.Personnel.FindAsync(id);
        if (personnel == null)
        {
            return NotFound();
        }

        _context.Personnel.Remove(personnel);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
