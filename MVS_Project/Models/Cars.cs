namespace MVS_Project.Models
{
    public class Cars
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public DateTime LastTracked { get; set; }
        public List<LocationHistory> LocationHistory { get; set; } = new();
    }
}
