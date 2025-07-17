namespace MVS_Project.Models
{
    public record CarPosition(int CarId, double Latitude, double Longitude)
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

}
