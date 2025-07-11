using Microsoft.EntityFrameworkCore;
using MVS_Project.Models;

namespace MVS_Project.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Cars> Car { get; set; }
        public DbSet<LocationHistory> LocationHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cars>()
                .HasMany(c => c.LocationHistory)
                .WithOne(l => l.Car)
                .HasForeignKey(l => l.CarId);
        }


    }
}
