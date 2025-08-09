namespace MVS_Project.Models
{
    public class GetRouteRequest
    {

        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public double EndLat { get; set; }
        public double EndLng { get; set; }
        public int VehicleId { get; set; }
    }
}
