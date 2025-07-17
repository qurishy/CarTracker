namespace MVS_Project.Models
{
    // Models/GpsApiResponse.cs (
    public class GpsApiResponse
    {
        public List<CarPosition> Positions { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
