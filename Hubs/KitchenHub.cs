using Microsoft.AspNetCore.SignalR;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Hubs
{
    public class KitchenHub : Hub
    {
        private readonly IBranchContext _branchContext;
        private readonly ILogger<KitchenHub> _logger;

        public KitchenHub(IBranchContext branchContext, ILogger<KitchenHub> logger)
        {
            _branchContext = branchContext ?? throw new ArgumentNullException(nameof(branchContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var context = await _branchContext.GetCurrentAsync(Context.ConnectionAborted);
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    KitchenHubGroups.Branch(context.CurrentBranch.Id),
                    Context.ConnectionAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KitchenHub branch group assignment skipped for connection {ConnectionId}", Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }
        
        public async Task SendMessage(string user, string message)
        {
            var context = await _branchContext.GetCurrentAsync(Context.ConnectionAborted);
            await Clients
                .Group(KitchenHubGroups.Branch(context.CurrentBranch.Id))
                .SendAsync(KitchenHubEvents.ReceiveMessage, user, message, Context.ConnectionAborted);
        }
    }
}
