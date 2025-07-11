using Microsoft.EntityFrameworkCore;
using MVS_Project.Data;
using MVS_Project.HUBS;
using MVS_Project.Models;
using MVS_Project.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSignalR();
//=================================================================================================

// Add services to the container.
builder.Services.AddControllersWithViews();


// Register services
// builder.Services.AddSingleton<IGpsDataService, RealGpsService>(); // Replace with your actual implementation>

builder.Services.AddSingleton<IGpsDataService, SimulatedGpsService>();
builder.Services.AddHostedService(provider =>
    (SimulatedGpsService)provider.GetRequiredService<IGpsDataService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Map}/{action=Index}/{id?}");

app.MapHub<TrackingHub>("/trackingHub");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Car.Any())
    {
        db.Car.AddRange(
            new Cars { LicensePlate = "AF-1234", Make = "Toyota", Model = "Corolla" },
            new Cars { LicensePlate = "AF-5678", Make = "Honda", Model = "Civic" }
        );
        db.SaveChanges();
    }
}



app.Run();
