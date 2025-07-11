namespace MVS_Project.Models
{
    public class LocationHistory
    {
        public int Id { get; set; }
        public int CarId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }
        public Cars Car { get; set; }
    }

}
