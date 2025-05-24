using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using System.Net; // <-- Add this line

public class LocalRequestsOnlyAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        // Allow access only from loopback addresses (localhost).
        var remoteIp = httpContext.Connection?.RemoteIpAddress;
        return remoteIp != null && IPAddress.IsLoopback(remoteIp);
    }
}