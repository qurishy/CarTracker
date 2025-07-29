namespace MVS_Project.Models
{
    // Main vehicle model
    public class Cars
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime LastTracked { get; set; }
        public List<LocationHistory> LocationHistory { get; set; } = new();
        public List<RouteCar> Routes { get; set; } = new();
    }
}
