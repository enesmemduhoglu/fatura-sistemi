using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("manage")]
public class ManageController : Controller
{
    private readonly AppDbContext _db;

    public ManageController(AppDbContext db) => _db = db;

    [HttpGet("index")]
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var settings = await _db.CompanySettings.FirstOrDefaultAsync() ?? new CompanySettings();
        return View(settings);
    }

    [HttpPost("index")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CompanySettings model)
    {
        if (!ModelState.IsValid) return View(model);

        if (model.Id == 0) _db.CompanySettings.Add(model);
        else _db.Update(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Firma bilgileri kaydedildi.";
        return RedirectToAction(nameof(Index));
    }
}
