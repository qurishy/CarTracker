using MVS_Project.Models;

namespace GPS_DataSender_Api.Services
{
    /// <summary>
    /// Interface for GPS tracking service
    /// Manages client tracking subscriptions and provides car position data
    /// </summary>
    public interface IGpsTrackingService
    {
        // Client tracking management
        Task StartTrackingCarAsync(string connectionId, int carId);
        Task StartTrackingAllCarsAsync(string connectionId);
        Task StopTrackingAsync(string connectionId);

        // Data retrieval
        Task<CarPosition?> GetCarPositionAsync(int carId);
        Task<IEnumerable<CarPosition>> GetAllCarPositionsAsync();
        Task<IEnumerable<int>> GetAvailableCarIdsAsync();

        // Service lifecycle
        Task StartContinuousUpdatesAsync();
        Task StopContinuousUpdatesAsync();
    }

}
