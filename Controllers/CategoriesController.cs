using GharAagan.Data;
using GharAagan.Dtos;
using GharAagan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db) => _db = db;

    // Public: anyone can browse categories.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceCategory>>> GetAll()
        => Ok(await _db.Categories.OrderBy(c => c.Name).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServiceCategory>> Get(int id)
    {
        var cat = await _db.Categories.FindAsync(id);
        return cat is null ? NotFound() : Ok(cat);
    }

    // Admin only: manage categories.
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ServiceCategory>> Create(CategoryRequest req)
    {
        var cat = new ServiceCategory { Name = req.Name.Trim(), Description = req.Description };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = cat.Id }, cat);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CategoryRequest req)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat is null) return NotFound();
        cat.Name = req.Name.Trim();
        cat.Description = req.Description;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat is null) return NotFound();
        if (await _db.Listings.AnyAsync(l => l.CategoryId == id))
            return Conflict("Cannot delete a category that still has listings.");
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
