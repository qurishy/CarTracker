// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using MVS_Project.Models;

namespace MVS_Project.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Cars> Car { get; set; }
        public DbSet<LocationHistory> LocationHistory { get; set; }
        public DbSet<RouteCar> Routes { get; set; }
        public DbSet<RouteWaypoint> RouteWaypoints { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure relationships
            modelBuilder.Entity<Cars>()
                .HasMany(c => c.LocationHistory)
                .WithOne(lh => lh.Car)
                .HasForeignKey(lh => lh.CarId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Cars>()
                .HasMany(c => c.Routes)
                .WithOne(r => r.Car)
                .HasForeignKey(r => r.CarId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RouteCar>()
                .HasMany(r => r.Waypoints)
                .WithOne(w => w.Route)
                .HasForeignKey(w => w.RouteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Create indexes for performance
            modelBuilder.Entity<LocationHistory>()
                .HasIndex(lh => lh.CarId);

            modelBuilder.Entity<LocationHistory>()
                .HasIndex(lh => lh.Timestamp);

            modelBuilder.Entity<RouteWaypoint>()
                .HasIndex(rw => rw.RouteId);

            modelBuilder.Entity<RouteWaypoint>()
                .HasIndex(rw => rw.Order);
        }
    }
}