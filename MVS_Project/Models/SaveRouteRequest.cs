namespace MVS_Project.Models
{
    public class SaveRouteRequest
    {
        public string Name { get; set; }
        public int VehicleId { get; set; }
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public double EndLat { get; set; }
        public double EndLng { get; set; }
        public double Distance { get; set; }
        public double Duration { get; set; }
        public string RouteData { get; set; } // JSON string of coordinates
    }
}
