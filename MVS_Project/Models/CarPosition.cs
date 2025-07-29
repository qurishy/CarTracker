namespace MVS_Project.Models
{

    public record CarPosition
    {
        public int CarId { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        public CarPosition(int carId, double latitude, double longitude)
        {
            CarId = carId;
            Latitude = latitude;
            Longitude = longitude;
        }
    }

}
