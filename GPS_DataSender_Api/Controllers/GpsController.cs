// Controllers/GpsController.cs
using Microsoft.AspNetCore.Mvc;
using MVS_Project.Models;
using MVS_Project.Services;

namespace MVS_Project.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GpsController : ControllerBase
    {
        private readonly IGpsDataService _gpsService;

        public GpsController(IGpsDataService gpsService)
        {
            _gpsService = gpsService;
        }

        /// <summary>
        /// Get all car positions for a specific country
        /// </summary>
        /// <param name="countryCode">ISO country code (e.g., AF, US, FR)</param>
        /// <returns>List of car positions</returns>
        [HttpGet("positions/{countryCode}")]
        public async Task<ActionResult<IEnumerable<CarPosition>>> GetPositions(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode))
                return BadRequest("Country code is required");

            var positions = await _gpsService.GetLatestPositionsAsync(countryCode.ToUpper());

            if (!positions.Any())
                return NotFound($"No positions found for country code: {countryCode}");

            return Ok(positions);
        }

        /// <summary>
        /// Get position for a specific car
        /// </summary>
        /// <param name="carId">Car ID</param>
        /// <param name="countryCode">ISO country code</param>
        /// <returns>Car position</returns>
        [HttpGet("car/{carId}/position")]
        public async Task<ActionResult<CarPosition>> GetCarPosition(int carId, [FromQuery] string countryCode = "AF")
        {
            if (carId <= 0)
                return BadRequest("Invalid car ID");

            var position = await _gpsService.GetCarPositionAsync(carId, countryCode.ToUpper());

            if (position == null)
                return NotFound($"Car with ID {carId} not found in {countryCode}");

            return Ok(position);
        }

        /// <summary>
        /// Get all available car IDs for a country
        /// </summary>
        /// <param name="countryCode">ISO country code</param>
        /// <returns>List of car IDs</returns>
        [HttpGet("cars/{countryCode}")]
        public async Task<ActionResult<IEnumerable<int>>> GetAvailableCars(string countryCode)
        {
            var positions = await _gpsService.GetLatestPositionsAsync(countryCode.ToUpper());
            var carIds = positions.Select(p => p.CarId).ToList();

            if (!carIds.Any())
                return NotFound($"No cars found for country code: {countryCode}");

            return Ok(carIds);
        }
    }
}