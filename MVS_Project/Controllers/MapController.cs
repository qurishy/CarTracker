using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVS_Project.Data;
using MVS_Project.Models;
using MVS_Project.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace MVS_Project.Controllers
{
    public class MapController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IGpsDataService _gpsService;
        private readonly ILogger<MapController> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _orsApiKey = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6Ijc4YzU1OGVlODQyNzQ5Yzc4MmE3MDc3ZjcwZGYyMzMyIiwiaCI6Im11cm11cjY0In0=";

        public MapController(AppDbContext context, IGpsDataService gpsService, ILogger<MapController> logger, HttpClient client)
        {
            _context = context;
            _gpsService = gpsService;
            _logger = logger;
            _httpClient = client;
        }

        #region View Actions

        /// <summary>
        /// Main map view - renders the map interface
        /// </summary>
        public IActionResult Index(int? id)
        {
            ViewBag.CarId = id;
            return View();
        }

        /// <summary>
        /// Dashboard view showing car statistics and controls
        /// </summary>
        [HttpGet]
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
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RefreshGpsPositions(string countryCode = "AF")
        {
            try
            {
                _logger.LogInformation("Manual GPS refresh requested for country: {CountryCode}", countryCode);

                var positions = await _gpsService.GetLatestPositionsAsync(countryCode);

                if (positions.Any())
                {
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
        /// </summary>
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

        [HttpGet]
        public async Task<IActionResult> GetCarLocations(int vehicleId)
        {
            try
            {
                _logger.LogInformation("Fetching locations for vehicle ID: {VehicleId}", vehicleId);

                var history = await _context.LocationHistory
                    .Where(l => l.CarId == vehicleId)
                    .OrderByDescending(l => l.Timestamp)
                    .Select(l => new
                    {
                        Latitude = l.Latitude,
                        Longitude = l.Longitude,
                        Timestamp = l.Timestamp
                    })
                    .ToListAsync();

                _logger.LogInformation("Found {Count} locations for vehicle {VehicleId}", history.Count, vehicleId);

                if (!history.Any())
                {
                    _logger.LogWarning("No location history found for vehicle {VehicleId}", vehicleId);
                    return Json(new List<object>());
                }

                return Json(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching car locations for vehicle {VehicleId}", vehicleId);
                return StatusCode(500, new
                {
                    error = "Error fetching locations",
                    message = ex.Message,
                    vehicleId = vehicleId
                });
            }
        }

        /// <summary>
        /// Get location history for a specific car
        /// </summary>
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
        [HttpGet]
        public async Task<IActionResult> GetAllCarsHistory(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
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
                        Locations = g.Select(l => new
                        {
                            l.Latitude,
                            l.Longitude,
                            l.Timestamp
                        }).ToList()
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

        #region Route Management APIs

        /// <summary>
        /// Get saved routes for a specific vehicle
        /// Matches frontend: loadSavedRoutes()
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSavedRoutes(int vehicleId)
        {
            if (vehicleId <= 0)
                return BadRequest(new { error = "Invalid vehicleId" });

            try
            {
                var routes = await _context.Routes
                    .Where(r => r.CarId == vehicleId && r.IsActive)
                    .Select(r => new
                    {
                        id = r.Id, // lowercase to match frontend
                        name = r.Name,
                        carId = r.CarId,
                        totalDistanceKm = r.TotalDistanceKm,
                        estimatedTimeMinutes = r.EstimatedTimeMinutes,
                        createdAt = r.CreatedAt
                    })
                    .OrderByDescending(r => r.createdAt)
                    .ToListAsync();

                return Json(routes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading saved routes for vehicle {VehicleId}", vehicleId);
                return StatusCode(500, new { error = "Failed to load saved routes" });
            }
        }

        /// <summary>
        /// Get a single saved route by ID
        /// Matches frontend: loadSavedRoute(id)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSavedRoute(int id)
        {
            try
            {
                var route = await _context.Routes
                    .Include(r => r.Waypoints)
                    .Where(r => r.Id == id && r.IsActive)
                    .FirstOrDefaultAsync();

                if (route == null)
                    return NotFound(new { error = "Route not found" });

                // Convert waypoints to coordinate array format expected by frontend
                var routeData = route.Waypoints
                    .OrderBy(w => w.Order)
                    .Select(w => new double[] { w.Latitude, w.Longitude })
                    .ToList();

                var result = new
                {
                    id = route.Id,
                    name = route.Name,
                    carId = route.CarId,
                    totalDistanceKm = route.TotalDistanceKm,
                    estimatedTimeMinutes = route.EstimatedTimeMinutes,
                    createdAt = route.CreatedAt,
                    routeData = routeData, // This matches what frontend expects
                    waypoints = route.Waypoints.OrderBy(w => w.Order).Select(w => new
                    {
                        latitude = w.Latitude,
                        longitude = w.Longitude,
                        order = w.Order
                    }).ToList()
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading saved route {RouteId}", id);
                return StatusCode(500, new { error = "Failed to load route" });
            }
        }

        /// <summary>
        /// Save a new route
        /// Matches frontend: saveCurrentRoute()
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveRoute([FromBody] SaveRouteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { error = "Name is required" });

            var carExists = await _context.Car.AnyAsync(c => c.Id == request.VehicleId);
            if (!carExists)
                return NotFound(new { error = "Car not found" });

            try
            {
                var coordinates = JsonConvert.DeserializeObject<List<double[]>>(request.RouteData);
                if (coordinates == null || !coordinates.Any())
                    return BadRequest(new { error = "Invalid route data" });

                var route = new RouteCar
                {
                    Name = request.Name,
                    CarId = request.VehicleId,
                    TotalDistanceKm = request.Distance / 1000.0, // Convert meters to km
                    EstimatedTimeMinutes = request.Duration / 60.0, // Convert seconds to minutes
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Waypoints = new List<RouteWaypoint>()
                };

                // Create waypoints from coordinates
                for (int i = 0; i < coordinates.Count; i++)
                {
                    var coord = coordinates[i];
                    route.Waypoints.Add(new RouteWaypoint
                    {
                        Latitude = coord[0], // Frontend sends [lat, lng]
                        Longitude = coord[1],
                        Order = i,
                        Route = route
                    });
                }

                _context.Routes.Add(route);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Route saved", routeId = route.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving route for vehicle {VehicleId}", request?.VehicleId);
                return StatusCode(500, new { error = "Save failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Delete a saved route (soft delete)
        /// Matches frontend: deleteSavedRoute(id)
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteSavedRoute(int id)
        {
            try
            {
                var route = await _context.Routes.FindAsync(id);
                if (route == null)
                    return NotFound(new { error = "Route not found" });

                route.IsActive = false;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Route deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting route {RouteId}", id);
                return StatusCode(500, new { error = "Failed to delete route" });
            }
        }

        #endregion

        #region Geocoding and Routing APIs

        /// <summary>
        /// Search locations for autocomplete suggestions
        /// Matches frontend: fetchSuggestions()
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchLocations(string query, string country = "AF")
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return BadRequest(new { error = "Query too short" });

            try
            {
                var url = $"https://api.openrouteservice.org/geocode/autocomplete?api_key={_orsApiKey}&text={Uri.EscapeDataString(query)}&boundary.country={country}";

                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("ORS Autocomplete failed: {Code}, {Error}", response.StatusCode, error);
                    return StatusCode((int)response.StatusCode, new { error = "Geocoding service error" });
                }

                var json = await response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in SearchLocations for query '{Query}'", query);
                return StatusCode(500, new { error = "Server error during search" });
            }
        }

        /// <summary>
        /// Geocode an address to coordinates
        /// Matches frontend: getRoute() geocoding fallback
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Geocode(string address, string country = "AF")
        {
            if (string.IsNullOrWhiteSpace(address) || address.Length < 3)
                return BadRequest(new { error = "Address is required" });

            try
            {
                var url = $"https://api.openrouteservice.org/geocode/search?api_key={_orsApiKey}&text={Uri.EscapeDataString(address)}&boundary.country={country}";

                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Geocode failed: {StatusCode}, {Error}", response.StatusCode, error);
                    return StatusCode((int)response.StatusCode, new { error = "Geocoding failed" });
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(json);

                var features = result["features"] as JArray;
                if (features == null || !features.Any())
                    return NotFound(new { error = "Location not found" });

                var first = features[0];
                var geometry = first["geometry"]?["coordinates"] as JArray;
                if (geometry == null || geometry.Count < 2)
                    return NotFound(new { error = "No coordinates found" });

                var lon = geometry[0].Value<double>();
                var lat = geometry[1].Value<double>();
                var displayName = first["properties"]?["label"]?.ToString() ?? address;

                return Json(new
                {
                    lat = lat,
                    lon = lon,
                    display_name = displayName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error geocoding address '{Address}'", address);
                return StatusCode(500, new { error = "Internal geocoding error" });
            }
        }

        /// <summary>
        /// Calculate route between two points
        /// Matches frontend: getRoute()
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetRoute([FromBody] GetRouteRequest request)
        {
            if (request == null)
                return BadRequest(new { error = "Invalid request body" });

            if (request.StartLat == 0 || request.StartLng == 0 ||
                request.EndLat == 0 || request.EndLng == 0)
            {
                return BadRequest(new { error = "Valid start and end coordinates required" });
            }

            try
            {
                // Build ORS Directions API URL with coordinates
                var start = $"{request.StartLng},{request.StartLat}"; // ORS expects [lng, lat]
                var end = $"{request.EndLng},{request.EndLat}";

                var url = $"https://api.openrouteservice.org/v2/directions/driving-car?api_key={_orsApiKey}&start={start}&end={end}";

                _logger.LogInformation("Calling ORS Directions API: {Url}", url);

                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("ORS Directions API error {Code}: {Error}", response.StatusCode, error);
                    return StatusCode((int)response.StatusCode, new { error = "Route calculation failed" });
                }

                var json = await response.Content.ReadAsStringAsync();
                var orsData = JObject.Parse(json);

                // Extract route features
                var features = orsData["features"] as JArray;
                if (features == null || !features.Any())
                {
                    return NotFound(new { error = "No route data returned from service" });
                }

                var firstFeature = features[0];
                var geometry = firstFeature["geometry"]?["coordinates"] as JArray;
                if (geometry == null || geometry.Count == 0)
                {
                    return NotFound(new { error = "No route geometry returned" });
                }

                // Extract distance and duration from properties
                var summary = firstFeature["properties"]?["summary"];
                var distance = (summary?["distance"]?.Value<double>() ?? 0) / 1000.0; // meters to km
                var duration = (summary?["duration"]?.Value<double>() ?? 0) / 60.0; // seconds to minutes

                // Convert coordinates: ORS returns [lng, lat], frontend expects [lat, lng]
                var coordinates = new List<double[]>();
                foreach (JToken coord in geometry)
                {
                    if (coord is JArray point && point.Count >= 2)
                    {
                        double lon = point[0].Value<double>();
                        double lat = point[1].Value<double>();
                        coordinates.Add(new[] { lat, lon }); // Convert to [lat, lng] for frontend
                    }
                }

                // Return format expected by frontend displayRoute()
                var routeData = new
                {
                    coordinates = coordinates,
                    distance = Math.Round(distance, 2),
                    duration = Math.Round(duration, 2),
                    geometry = new { coordinates = coordinates }, // Alternative format
                    origin = new { lat = request.StartLat, lng = request.StartLng },
                    destination = new { lat = request.EndLat, lng = request.EndLng }
                };

                return Json(routeData);
            }
            catch (JsonReaderException jex)
            {
                _logger.LogError(jex, "Invalid JSON received from ORS API");
                return StatusCode(502, new { error = "Invalid response from routing service", details = jex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating route from ({StartLat}, {StartLng}) to ({EndLat}, {EndLng})",
                    request.StartLat, request.StartLng, request.EndLat, request.EndLng);
                return StatusCode(500, new { error = "Failed to calculate route", details = ex.Message });
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
        [HttpGet]
        public async Task<IActionResult> CheckGpsApiStatus()
        {
            try
            {
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

        /// <summary>
        /// Test endpoint to verify ORS API connectivity
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> TestOrsApi()
        {
            try
            {
                _logger.LogInformation("Testing ORS API connectivity");

                if (string.IsNullOrWhiteSpace(_orsApiKey))
                {
                    return BadRequest(new { error = "API key not configured" });
                }

                using var httpClient = new HttpClient();
                var testUrl = $"https://api.openrouteservice.org/geocode/search?api_key={_orsApiKey}&text=Kabul&boundary.country=AF";

                _logger.LogInformation("Testing with URL: {Url}", testUrl);

                var response = await httpClient.GetAsync(testUrl);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Test response status: {StatusCode}", response.StatusCode);

                return Ok(new
                {
                    success = response.IsSuccessStatusCode,
                    statusCode = (int)response.StatusCode,
                    apiKeyConfigured = !string.IsNullOrWhiteSpace(_orsApiKey),
                    testUrl = testUrl,
                    responseContent = content
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing ORS API");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    details = ex.StackTrace
                });
            }
        }

        #endregion
    }
}