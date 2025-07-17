using System.ComponentModel.DataAnnotations;

namespace MVS_Project.Models
{
    public class CarPosition
    {
        public int CarId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }

        public CarPosition(int carId, double latitude, double longitude)
        {
            CarId = carId;
            Latitude = latitude;
            Longitude = longitude;
            Timestamp = DateTime.UtcNow;
        }
    }
}