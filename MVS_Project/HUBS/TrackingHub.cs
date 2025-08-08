using Microsoft.AspNetCore.SignalR;
using MVS_Project.Models;
using System.Collections.Concurrent;

namespace MVS_Project.HUBS
{
    /// <summary>
    /// SignalR Hub for broadcasting real-time GPS updates to frontend clients
    /// This hub receives data from the background service and sends it to connected clients
    /// </summary>
    public class TrackingHub : Hub
    {
        private readonly ILogger<TrackingHub> _logger;

        // Track which cars each client is subscribed to
        private static readonly ConcurrentDictionary<string, HashSet<int>> UserCarMap = new();

        public TrackingHub(ILogger<TrackingHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Subscribe to updates for specific cars
        /// Frontend calls this method to specify which cars they want to track
        /// </summary>
        /// <param name="carIds">List of car IDs to track</param>
        public async Task SubscribeToCars(List<int> carIds)
        {
            var connectionId = Context.ConnectionId;

            // Store which cars this client wants to track
            UserCarMap[connectionId] = new HashSet<int>(carIds);

            // Add to groups for each car (for efficient broadcasting)
            foreach (var carId in carIds)
            {
                await Groups.AddToGroupAsync(connectionId, $"Car_{carId}");
            }

            _logger.LogInformation("Client {ConnectionId} subscribed to cars: {CarIds}",
                connectionId, string.Join(", ", carIds));
        }

        /// <summary>
        /// Subscribe to all car updates
        /// Frontend calls this to receive updates for all cars
        /// </summary>
        public async Task SubscribeToAllCars()
        {
            var connectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(connectionId, "AllCars");


            _logger.LogInformation("Client {ConnectionId} subscribed to all cars", connectionId);
        }

        /// <summary>
        /// Unsubscribe from specific cars
        /// </summary>
        /// <param name="carIds">Car IDs to unsubscribe from</param>
        public async Task UnsubscribeFromCars(List<int> carIds)
        {
            var connectionId = Context.ConnectionId;

            if (UserCarMap.TryGetValue(connectionId, out var currentCars))
            {
                foreach (var carId in carIds)
                {
                    currentCars.Remove(carId);
                    await Groups.RemoveFromGroupAsync(connectionId, $"Car_{carId}");
                }
            }

            _logger.LogInformation("Client {ConnectionId} unsubscribed from cars: {CarIds}",
                connectionId, string.Join(", ", carIds));
        }

        /// <summary>
        /// Called when client disconnects - cleanup subscriptions
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            UserCarMap.TryRemove(connectionId, out _);

            _logger.LogInformation("Client {ConnectionId} disconnected", connectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Called by background service to broadcast single car update
        /// This is called from the GpsDataService when new position data arrives
        /// </summary>
        /// <param name="position">Car position update</param>
        public async Task BroadcastCarPosition(CarPosition position)
        {
            // Send to clients tracking this specific car
            await Clients.Group($"Car_{position.CarId}")
                .SendAsync("CarPositionUpdate", position);

            // Send to clients tracking all cars
            await Clients.Group("AllCars")
                .SendAsync("CarPositionUpdate", position);
        }

        /// <summary>
        /// Called by background service to broadcast multiple car updates
        /// </summary>
        /// <param name="positions">List of car positions</param>
        public async Task BroadcastMultipleCarPositions(List<CarPosition> positions)
        {
            // Send individual updates to specific car trackers
            foreach (var position in positions)
            {
                await Clients.Group($"Car_{position.CarId}")
                    .SendAsync("CarPositionUpdate", position);
            }

            // Send all positions to clients tracking all cars
            await Clients.Group("AllCars")
                .SendAsync("MultipleCarPositions", positions);
        }

        /// <summary>
        /// Send initial car positions when client first connects
        /// </summary>
        /// <param name="positions">Current positions of all cars</param>
        public async Task SendInitialPositions(List<CarPosition> positions)
        {
            await Clients.Caller.SendAsync("InitialPositions", positions);
        }
    }

}
