using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVS_Project.Data;
using MVS_Project.Models;


namespace MVS_Project.Controllers;

public class CarsController : Controller
{
    private readonly AppDbContext _dbcontext;

    public CarsController(AppDbContext dbcontext)
    {
        _dbcontext = dbcontext;
    }

    public IActionResult Index()
    {
        var activeCars = GetActiveCars();
        return View(activeCars);
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveCars()
    {
        var activecars = await _dbcontext.Car
        .Select(car => new
        {
            car.Id,
            car.LicensePlate,
            car.Make,
            car.Model,
            car.LastTracked,

            Location = _dbcontext.LocationHistory
            .Where(loc => loc.CarId == car.Id)
            .OrderByDescending(loc => loc.Timestamp)
            .Select(loc => new { loc.Latitude, loc.Longitude })
            .FirstOrDefault()
        })
        .ToListAsync();

        return Json(activecars);
    }

    public async Task<IActionResult> GetCar(int id)
    {
        var car = await _dbcontext.Car
        .Where(c => c.Id == id)
        .Select(c => new
        {
            c.Id,
            c.LicensePlate,
            c.Make,
            c.Model,
            c.LastTracked,
            LocationHistory = c.LocationHistory
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new
            {
                l.Latitude,
                l.Longitude,
                l.Timestamp
            })
            .ToList(),
            LastPosition = c.LocationHistory
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new
            {
                l.Latitude,
                l.Longitude
            })
            .FirstOrDefault(),
            TotalLocation = c.LocationHistory.Count()
        })
        .FirstOrDefaultAsync();

        if (car == null)
        {
            return NotFound();
        }

        return Json(car);
    }

    
}
