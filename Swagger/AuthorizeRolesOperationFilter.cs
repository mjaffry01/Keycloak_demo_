using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace Marketplace.Api.Swagger;

/// <summary>
/// Makes Swagger UI show "locks" only for endpoints that actually require auth,
/// and annotates endpoints with the roles required (based on [Authorize(Roles="...")]).
/// </summary>
public sealed class AuthorizeRolesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // If [AllowAnonymous] exists on action or controller => do nothing (no auth required)
        if (HasAllowAnonymous(context.MethodInfo))
            return;

        // Gather [Authorize] attributes from controller + action
        var authorizeAttrs = GetAuthorizeAttributes(context.MethodInfo);

        if (authorizeAttrs.Count == 0)
            return; // no [Authorize] => treat as public

        // Mark operation as requiring Bearer token
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Add readable note about roles
        var roles = authorizeAttrs
            .Select(a => a.Roles)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .SelectMany(r => r!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var note = roles.Count == 0
            ? "Auth required."
            : $"Auth required. Roles: {string.Join(", ", roles)}.";

        // Append to description (don’t overwrite user docs)
        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? note
            : $"{operation.Description}\n\n{note}";
    }

    private static bool HasAllowAnonymous(MethodInfo method)
    {
        // action
        if (method.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any())
            return true;

        // controller
        var controllerType = method.DeclaringType;
        if (controllerType != null && controllerType.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any())
            return true;

        return false;
    }

    private static List<AuthorizeAttribute> GetAuthorizeAttributes(MethodInfo method)
    {
        var result = new List<AuthorizeAttribute>();

        // controller-level
        var controllerType = method.DeclaringType;
        if (controllerType != null)
            result.AddRange(controllerType.GetCustomAttributes(true).OfType<AuthorizeAttribute>());

        // action-level
        result.AddRange(method.GetCustomAttributes(true).OfType<AuthorizeAttribute>());

        return result;
    }
}
