using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 1. Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "super_secret_key_for_development_only_12345";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false, // In a real app, set to true and configure
            ValidateAudience = false, // In a real app, set to true and configure
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
builder.Services.AddAuthorization();

// 2. Configure Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("tokenBucket", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromSeconds(10);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

// 3. Configure YARP Reverse Proxy with Transforms
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(transformContext =>
        {
            // If the user is authenticated, extract claims and pass them to downstream
            var user = transformContext.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirst("userId")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var role = user.FindFirst("role")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
                }
                
                if (!string.IsNullOrEmpty(role))
                {
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Role", role);
                }
            }
            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapReverseProxy();

app.Run();
