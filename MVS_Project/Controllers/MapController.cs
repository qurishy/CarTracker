using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVS_Project.Data;
using MVS_Project.Models;
using MVS_Project.Services;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace MVS_Project.Controllers
{
    public class MapController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IGpsDataService _gpsService;
        private readonly ILogger<MapController> _logger;
        //private readonly string _orsApiKey = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6Ijc4YzU1OGVlODQyNzQ5Yzc4MmE3MDc3ZjcwZGYyMzMyIiwiaCI6Im11cm11cjY0In0=";

        public MapController(AppDbContext context, IGpsDataService gpsService, ILogger<MapController> logger)
        {
            _context = context;
            _gpsService = gpsService;
            _logger = logger;
        }

        #region View Actions

        /// <summary>
        /// Main map view - renders the map interface
        /// </summary>
        /// <returns>Map view with initial data</returns>
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get initial car data to pass to the view
                var cars = await GetActiveCarData();
                ViewBag.InitialCars = cars;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading map index page");
                ViewBag.ErrorMessage = "Unable to load map data. Please try again.";
                return View();
            }
        }

        /// <summary>
        /// Dashboard view showing car statistics and controls
        /// </summary>
        /// <returns>Dashboard view</returns>
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var stats = await GetDashboardStats();
                return View(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                ViewBag.ErrorMessage = "Unable to load dashboard data.";
                return View();
            }
        }

        #endregion

        #region GPS API Integration

        /// <summary>
        /// Manually refresh GPS positions from external API
        /// This calls your GPS API and saves the latest positions to database
        /// </summary>
        /// <param name="countryCode">Country code for GPS data (default: AF)</param>
        /// <returns>JSON response with updated positions</returns>
        [HttpPost]
        public async Task<IActionResult> RefreshGpsPositions(string countryCode = "AF")
        {
            try
            {
                _logger.LogInformation("Manual GPS refresh requested for country: {CountryCode}", countryCode);

                // Call the GPS API to get latest positions
                var positions = await _gpsService.GetLatestPositionsAsync(countryCode);

                if (positions.Any())
                {
                    // The GPS service automatically saves to database
                    var result = positions.Select(p => new
                    {
                        CarId = p.CarId,
                        Latitude = p.Latitude,
                        Longitude = p.Longitude,
                        Timestamp = p.Timestamp
                    }).ToList();

                    _logger.LogInformation("Successfully refreshed {Count} GPS positions", positions.Count());

                    return Json(new
                    {
                        success = true,
                        message = $"Refreshed {positions.Count()} car positions",
                        data = result,
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = "No GPS positions available for the specified country",
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing GPS positions for country: {CountryCode}", countryCode);
                return Json(new
                {
                    success = false,
                    message = "Failed to refresh GPS positions: " + ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Get real-time position for a specific car from GPS API
        /// </summary>
        /// <param name="carId">Car ID to track</param>
        /// <param name="countryCode">Country code</param>
        /// <returns>JSON response with car position</returns>
        [HttpGet]
        public async Task<IActionResult> GetRealTimeCarPosition(int carId, string countryCode = "AF")
        {
            try
            {
                var position = await _gpsService.GetCarPositionAsync(carId, countryCode);

                if (position != null)
                {
                    return Json(new
                    {
                        success = true,
                        carId = position.CarId,
                        latitude = position.Latitude,
                        longitude = position.Longitude,
                        timestamp = position.Timestamp
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Car {carId} not found in {countryCode}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting real-time position for car {CarId}", carId);
                return Json(new
                {
                    success = false,
                    message = "Failed to get car position: " + ex.Message
                });
            }
        }

        #endregion

        #region Database Query APIs

        /// <summary>
        /// Get all active cars with their latest positions from database
        /// This queries your local database, not the external GPS API
        /// </summary>
        /// <returns>JSON array of cars with their last known positions</returns>
        [HttpGet]
        public async Task<IActionResult> GetActiveCars()
        {
            try
            {
                var cars = await GetActiveCarData();
                return Json(cars);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active cars from database");
                return BadRequest($"Error fetching car data: {ex.Message}");
            }
        }

        /// <summary>
        /// Get detailed information for a specific car from database
        /// </summary>
        /// <param name="id">Car ID</param>
        /// <returns>JSON object with car details and last position</returns>
        [HttpGet]
        public async Task<IActionResult> GetCar(int id)
        {
            try
            {
                var car = await _context.Car
                    .Include(c => c.LocationHistory)
                    .Where(c => c.Id == id)
                    .Select(c => new
                    {
                        c.Id,
                        c.LicensePlate,
                        c.Make,
                        c.Model,
                        c.LastTracked,
                        LastPosition = c.LocationHistory
                            .OrderByDescending(l => l.Timestamp)
                            .Select(l => new
                            {
                                l.Latitude,
                                l.Longitude,
                                l.Timestamp
                            })
                            .FirstOrDefault(),
                        TotalLocations = c.LocationHistory.Count(),
                        FirstTracked = c.LocationHistory
                            .OrderBy(l => l.Timestamp)
                            .Select(l => l.Timestamp)
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

                if (car == null)
                {
                    return NotFound($"Car with ID {id} not found");
                }

                return Json(car);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching car {CarId} from database", id);
                return BadRequest($"Error fetching car data: {ex.Message}");
            }
        }

        /// <summary>
        /// Get location history for a specific car
        /// </summary>
        /// <param name="id">Car ID</param>
        /// <param name="take">Number of records to return (default: 100)</param>
        /// <param name="skip">Number of records to skip (for pagination)</param>
        /// <returns>JSON array of location history</returns>
        [HttpGet]
        public async Task<IActionResult> GetCarHistory(int id, int take = 100, int skip = 0)
        {
            try
            {
                var history = await _context.LocationHistory
                    .Where(l => l.CarId == id)
                    .OrderByDescending(l => l.Timestamp)
                    .Skip(skip)
                    .Take(take)
                    .Select(l => new
                    {
                        l.Latitude,
                        l.Longitude,
                        l.Timestamp
                    })
                    .ToListAsync();

                var totalCount = await _context.LocationHistory
                    .Where(l => l.CarId == id)
                    .CountAsync();

                return Json(new
                {
                    data = history,
                    totalCount = totalCount,
                    hasMore = (skip + take) < totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching location history for car {CarId}", id);
                return BadRequest($"Error fetching location history: {ex.Message}");
            }
        }

        /// <summary>
        /// Get location history for all cars within a time range
        /// </summary>
        /// <param name="startDate">Start date for history</param>
        /// <param name="endDate">End date for history</param>
        /// <returns>JSON array of location history grouped by car</returns>
        [HttpGet]
        public async Task<IActionResult> GetAllCarsHistory(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                // Default to last 24 hours if no dates provided
                var start = startDate ?? DateTime.UtcNow.AddHours(-24);
                var end = endDate ?? DateTime.UtcNow;

                var history = await _context.LocationHistory
        .Include(l => l.Car)
        .Where(l => l.Timestamp >= start && l.Timestamp <= end)
        .OrderByDescending(l => l.Timestamp)
        .GroupBy(l => l.CarId)
        .Select(g => new
        {
            CarId = g.Key,
            LicensePlate = g.First().Car.LicensePlate,
            Make = g.First().Car.Make,
            Model = g.First().Car.Model,
            LocationCount = g.Count(),
            Locations = g.Select(l => new  // Ensure this is always populated
            {
                l.Latitude,
                l.Longitude,
                l.Timestamp
            }).ToList() // Convert to list to avoid null
        })
        .ToListAsync();

                return Json(new
                {
                    startDate = start,
                    endDate = end,
                    totalCars = history.Count,
                    data = history
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all cars history");
                return BadRequest($"Error fetching history data: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get dashboard statistics
        /// </summary>
        private async Task<object> GetDashboardStats()
        {
            var totalCars = await _context.Car.CountAsync();
            var activeCars = await _context.Car
                .Where(c => c.LastTracked > DateTime.UtcNow.AddHours(-1))
                .CountAsync();
            var totalLocations = await _context.LocationHistory.CountAsync();
            var todayLocations = await _context.LocationHistory
                .Where(l => l.Timestamp >= DateTime.Today)
                .CountAsync();

            return new
            {
                TotalCars = totalCars,
                ActiveCars = activeCars,
                InactiveCars = totalCars - activeCars,
                TotalLocations = totalLocations,
                TodayLocations = todayLocations,
                LastUpdate = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Helper method to get active car data
        /// </summary>
        private async Task<object> GetActiveCarData()
        {
            return await _context.Car
        .Include(c => c.LocationHistory)
        .Select(c => new
        {
            // Ensure these match your JSON structure
            c.Id,
            c.LicensePlate,
            c.Make,
            c.Model,
            c.LastTracked,
            IsActive = c.LastTracked > DateTime.UtcNow.AddHours(-1),
            LastPosition = c.LocationHistory
                .OrderByDescending(l => l.Timestamp)
                .Select(l => new
                {
                    l.Latitude,
                    l.Longitude
                })
                .FirstOrDefault(),
            LocationCount = c.LocationHistory.Count()
        })
        .ToListAsync();
        }

        #endregion

        #region Status and Health Checks

        /// <summary>
        /// Check the status of GPS API connectivity
        /// </summary>
        /// <returns>JSON response with API status</returns>
        [HttpGet]
        public async Task<IActionResult> CheckGpsApiStatus()
        {
            try
            {
                // Try to get positions to test API connectivity
                var positions = await _gpsService.GetLatestPositionsAsync("AF");

                return Json(new
                {
                    success = true,
                    apiStatus = "Connected",
                    lastCheck = DateTime.UtcNow,
                    availablePositions = positions.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GPS API connectivity check failed");
                return Json(new
                {
                    success = false,
                    apiStatus = "Disconnected",
                    lastCheck = DateTime.UtcNow,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get system health status
        /// </summary>
        /// <returns>JSON response with system health</returns>
        [HttpGet]
        public async Task<IActionResult> GetSystemHealth()
        {
            try
            {
                var dbConnected = await _context.Database.CanConnectAsync();
                var recentActivity = await _context.LocationHistory
                    .Where(l => l.Timestamp > DateTime.UtcNow.AddMinutes(-5))
                    .CountAsync();

                return Json(new
                {
                    databaseConnected = dbConnected,
                    recentActivity = recentActivity,
                    lastCheck = DateTime.UtcNow,
                    status = dbConnected ? "Healthy" : "Unhealthy"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system health");
                return Json(new
                {
                    databaseConnected = false,
                    status = "Unhealthy",
                    error = ex.Message,
                    lastCheck = DateTime.UtcNow
                });
            }
        }

        #endregion

        #region Route and geocoding


        //[HttpGet]
        //public async Task<IActionResult> Geocode(string query)
        //{
        //    try
        //    {
        //        using var client = new HttpClient();
        //        var response = await client.GetAsync(
        //            $"https://api.openrouteservice.org/geocode/search?api_key={_orsApiKey}&text={Uri.EscapeDataString(query)}");

        //        response.EnsureSuccessStatusCode();
        //        return Content(await response.Content.ReadAsStringAsync(), "application/json");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Geocoding error for query: {Query}", query);
        //        return StatusCode(500, $"Geocoding failed: {ex.Message}");
        //    }
        //}

        //[HttpGet]
        //public async Task<IActionResult> CalculateRoute(string origin, string destination)
        //{
        //    try
        //    {
        //        // Geocode origin
        //        var originResponse = await GeocodeLocation(origin);
        //        if (!originResponse.features.Any())
        //            return BadRequest("Origin location not found");

        //        // Geocode destination
        //        var destResponse = await GeocodeLocation(destination);
        //        if (!destResponse.features.Any())
        //            return BadRequest("Destination location not found");

        //        // Prepare route request
        //        var fromCoord = originResponse.features[0].geometry.coordinates;
        //        var toCoord = destResponse.features[0].geometry.coordinates;

        //        var routeRequest = new
        //        {
        //            coordinates = new[] { fromCoord, toCoord },
        //            instructions = false
        //        };

        //        // Calculate route
        //        using var client = new HttpClient();
        //        client.DefaultRequestHeaders.Accept.Clear();
        //        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_orsApiKey);

        //        var response = await client.PostAsJsonAsync(
        //            "https://api.openrouteservice.org/v2/directions/driving-car/geojson",
        //            routeRequest);

        //        response.EnsureSuccessStatusCode();
        //        return Content(await response.Content.ReadAsStringAsync(), "application/json");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Route calculation error: {Origin} to {Destination}", origin, destination);
        //        return StatusCode(500, $"Route calculation failed: {ex.Message}");
        //    }
        //}

        //private async Task<dynamic> GeocodeLocation(string query)
        //{
        //    using var client = new HttpClient();
        //    var response = await client.GetAsync(
        //        $"https://api.openrouteservice.org/geocode/search?api_key={_orsApiKey}&text={Uri.EscapeDataString(query)}");

        //    response.EnsureSuccessStatusCode();
        //    return JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
        //}

        #endregion


    }
}