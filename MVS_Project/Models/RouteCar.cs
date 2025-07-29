namespace MVS_Project.Models
{
    // Route definition
    public class RouteCar
    {
       
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int CarId { get; set; }
            public Cars Car { get; set; }
            public List<RouteWaypoint> Waypoints { get; set; } = new();
            public double TotalDistanceKm { get; set; }
            public double EstimatedTimeMinutes { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public bool IsActive { get; set; } = true;
       

    }

}
