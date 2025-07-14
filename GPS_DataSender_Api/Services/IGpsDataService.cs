using MVS_Project.Models;

namespace MVS_Project.Services
{
    public interface IGpsDataService
    {
        Task<IEnumerable<CarPosition>> GetLatestPositionsAsync(string countryCode);
        Task<CarPosition?> GetCarPositionAsync(int carId, string countryCode);
    }
}