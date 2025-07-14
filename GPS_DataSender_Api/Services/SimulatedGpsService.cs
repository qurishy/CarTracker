// Services/SimulatedGpsService.cs
using MVS_Project.Models;

namespace MVS_Project.Services
{
    public class SimulatedGpsService : IGpsDataService
    {
        private readonly Dictionary<string, (double minLat, double maxLat, double minLng, double maxLng)> _countryBounds;
        private readonly Dictionary<int, CarPosition> _carPositions;
        private readonly object _lock = new object();

        public SimulatedGpsService()
        {
            _carPositions = new Dictionary<int, CarPosition>();

            // Initialize country bounding boxes
            _countryBounds = new Dictionary<string, (double, double, double, double)>
            {
                ["AF"] = (29.3772, 38.4911, 60.5042, 74.9157), // Afghanistan
                ["US"] = (24.396308, 49.384358, -125.0, -66.93), // United States
           
                // Add more countries as needed
            };

            // Initialize some sample cars
            InitializeSampleCars();
        }

        private void InitializeSampleCars()
        {
            // Create initial positions for sample cars
            var bounds = _countryBounds["AF"]; // Default to Afghanistan

            for (int i = 1; i <= 5; i++)
            {
                var position = new CarPosition(
                    i,
                    bounds.minLat + (Random.Shared.NextDouble() * (bounds.maxLat - bounds.minLat)),
                    bounds.minLng + (Random.Shared.NextDouble() * (bounds.maxLng - bounds.minLng))
                );

                _carPositions[i] = position;
            }
        }

        public async Task<IEnumerable<CarPosition>> GetLatestPositionsAsync(string countryCode)
        {
            if (!_countryBounds.ContainsKey(countryCode))
                return Enumerable.Empty<CarPosition>();

            var bounds = _countryBounds[countryCode];
            var positions = new List<CarPosition>();

            lock (_lock)
            {
                // Update positions with slight movement simulation
                foreach (var carId in _carPositions.Keys.ToList())
                {
                    var currentPos = _carPositions[carId];

                    // Simulate small movement (within 0.01 degrees)
                    var newLat = Math.Max(bounds.minLat, Math.Min(bounds.maxLat,
                        currentPos.Latitude + (Random.Shared.NextDouble() - 0.5) * 0.02));
                    var newLng = Math.Max(bounds.minLng, Math.Min(bounds.maxLng,
                        currentPos.Longitude + (Random.Shared.NextDouble() - 0.5) * 0.02));

                    var newPosition = new CarPosition(carId, newLat, newLng);
                    _carPositions[carId] = newPosition;
                    positions.Add(newPosition);
                }
            }

            return await Task.FromResult(positions);
        }

        public async Task<CarPosition?> GetCarPositionAsync(int carId, string countryCode)
        {
            if (!_countryBounds.ContainsKey(countryCode))
                return null;

            lock (_lock)
            {
                if (_carPositions.TryGetValue(carId, out var position))
                {
                    return position;
                }
            }

            return await Task.FromResult<CarPosition?>(null);
        }
    }
}