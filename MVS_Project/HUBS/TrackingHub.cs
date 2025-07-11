using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace MVS_Project.HUBS
{
    public class TrackingHub : Hub
    {
        // Track connected users and their watched cars
        private static readonly ConcurrentDictionary<string, List<int>> UserCarMap = new();

        public async Task SubscribeToCars(List<int> carIds)
        {
            var connectionId = Context.ConnectionId;
            UserCarMap[connectionId] = carIds;
            await Groups.AddToGroupAsync(connectionId, $"cars_{string.Join('_', carIds)}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            if (UserCarMap.TryRemove(connectionId, out var carIds))
            {
                await Groups.RemoveFromGroupAsync(connectionId, $"cars_{string.Join('_', carIds)}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Called by background service to push updates
        public async Task BroadcastCarUpdate(int carId, double lat, double lng)
        {
            await Clients.Group($"cars_{carId}").SendAsync("ReceiveCarUpdate", carId, lat, lng);
        }
    }
}
