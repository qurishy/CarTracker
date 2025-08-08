using GPS_DataSender_Api.HUB;
using GPS_DataSender_Api.Services;
using GPS_DataSender_Api.Services.MVS_Project.Services;
using MVS_Project.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


//==================================================================
builder.Services.AddEndpointsApiExplorer();

// Configure SignalR for WebSocket-only communication
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// Register GPS tracking service as singleton to maintain state across connections
builder.Services.AddSingleton<IGpsTrackingService, GpsTrackingService>();

// Register GPS service as singleton to maintain state
builder.Services.AddSingleton<IGpsDataService, SimulatedGpsService>();

// Add CORS if needed for frontend access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});
//=======================================================================

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

//app.MapControllers();

// Map SignalR Hub - this is the only endpoint needed
app.MapHub<GpsHub>("/gps");

// Start continuous GPS updates when application starts
var gpsService = app.Services.GetRequiredService<IGpsTrackingService>();
await gpsService.StartContinuousUpdatesAsync();

app.Run();
