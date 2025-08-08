// Services/IGpsDataService.cs
using MVS_Project.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MVS_Project.Services
{
    public interface IGpsDataService
    {
        Task<IEnumerable<CarPosition>> GetLatestPositionsAsync(string countryCode);
        Task<CarPosition?> GetCarPositionAsync(int carId, string countryCode);

        // New methods for real-time updates
        Task StartRealtimeUpdatesAsync();
        Task StopRealtimeUpdatesAsync();

        // Events for position updates
        event EventHandler<CarPosition>? CarPositionUpdated;
        event EventHandler<List<CarPosition>>? MultiplePositionsUpdated;
    }
}