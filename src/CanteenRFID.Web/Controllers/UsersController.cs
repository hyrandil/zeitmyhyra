using CanteenRFID.Core.Models;
using CanteenRFID.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _db;

    public UsersController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? search = null, string? location = null)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                u.PersonnelNo.Contains(search) ||
                (u.Uid != null && u.Uid.Contains(search)) ||
                (u.TokenId != null && u.TokenId.Contains(search)) ||
                (u.Location != null && u.Location.Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(u => u.Location != null && u.Location.Contains(location));
        }
        var users = await query.OrderBy(u => u.LastName).ToListAsync();
        return View(users);
    }

    public IActionResult Create()
    {
        return View(new User());
    }

    [HttpPost]
    public async Task<IActionResult> Create(User user)
    {
        if (await _db.Users.AnyAsync(u => u.PersonnelNo == user.PersonnelNo))
        {
            ModelState.AddModelError(nameof(user.PersonnelNo), "Personalnummer bereits vergeben");
        }
        if (!string.IsNullOrWhiteSpace(user.Uid) && await _db.Users.AnyAsync(u => u.Uid == user.Uid))
        {
            ModelState.AddModelError(nameof(user.Uid), "UID bereits verknüpft");
        }
        if (!ModelState.IsValid)
        {
            return View(user);
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, User user)
    {
        var existing = await _db.Users.FindAsync(id);
        if (existing == null) return NotFound();

        if (await _db.Users.AnyAsync(u => u.PersonnelNo == user.PersonnelNo && u.Id != id))
        {
            ModelState.AddModelError(nameof(user.PersonnelNo), "Personalnummer bereits vergeben");
        }
        if (!string.IsNullOrWhiteSpace(user.Uid) && await _db.Users.AnyAsync(u => u.Uid == user.Uid && u.Id != id))
        {
            ModelState.AddModelError(nameof(user.Uid), "UID bereits verknüpft");
        }
        if (!ModelState.IsValid)
        {
            return View(user);
        }

        existing.FirstName = user.FirstName;
        existing.LastName = user.LastName;
        existing.PersonnelNo = user.PersonnelNo;
        existing.Uid = user.Uid;
        existing.TokenId = user.TokenId;
        existing.Location = user.Location;
        existing.IsActive = user.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var stamps = _db.Stamps.Where(s => s.UserId == id);
        await stamps.ForEachAsync(s => s.UserId = null);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        TempData["Info"] = "Mitarbeiter gelöscht.";
        return RedirectToAction(nameof(Index));
    }
}
