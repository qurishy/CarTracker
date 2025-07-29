using System.ComponentModel.DataAnnotations.Schema;

namespace MVS_Project.Models
{
    // Route waypoints
    public class RouteWaypoint
    {
        public int Id { get; set; }
        public int RouteId { get; set; }
        public RouteCar Route { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Order { get; set; }
        public DateTime? EstimatedArrival { get; set; }

        [NotMapped]
        public GeoCoordinate Position => new(Latitude, Longitude);
    }
}
