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

builder.Services.AddHttpClient<RealGpsService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GpsApi:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
});

// Register GPS services
builder.Services.AddScoped<IGpsDataService, RealGpsService>();

// Register background service for periodic GPS updates
builder.Services.AddHostedService<GpsBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Map}/{action=Dashboard}/{id?}");

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
