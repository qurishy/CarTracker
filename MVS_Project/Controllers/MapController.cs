using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVS_Project.Data;
using MVS_Project.Services;

namespace MVS_Project.Controllers
{
    public class MapController : Controller
    {

        private readonly AppDbContext _context;
        private readonly IGpsDataService _GpsData;

        public MapController(AppDbContext context, IGpsDataService gpsDataService)
        {
            _context = context;
            _GpsData = gpsDataService;

        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveCars()
        {
            // Get only cars in Afghanistan (for real implementation)
            var cars = await _context.Car
                .Where(c => c.LastTracked > DateTime.UtcNow.AddMinutes(-5))
                .ToListAsync();

            return Json(cars.Select(c => new {
                c.Id,
                c.LicensePlate,
                c.Make,
                c.Model,
                LastPosition = c.LocationHistory
                    .OrderByDescending(l => l.Timestamp)
                    .FirstOrDefault()
            }));
        }

    }
}
