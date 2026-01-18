using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FestiveGuestAPI.Configuration;

public class AdminAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var userType = context.HttpContext.User.Claims
            .FirstOrDefault(c => c.Type == "userType")?.Value;

        if (string.IsNullOrEmpty(userType) || !userType.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                Success = false, 
                Message = "Admin access required" 
            });
        }
    }
}
