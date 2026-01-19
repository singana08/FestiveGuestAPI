using Azure.Data.Tables;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FestiveGuestAPI.Configuration;
using FestiveGuestAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FestiveGuestAPI.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.AddControllers();

// Configure CORS for React app - Allow all origins for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure JWT Authentication
var tempKey = Encoding.ASCII.GetBytes("temp-key-that-is-at-least-32-characters-long-for-hmac-sha256"); // Will be replaced with actual key from secrets
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(tempKey),
            ValidateIssuer = true,
            ValidIssuer = "FestiveGuestAPI",
            ValidateAudience = true,
            ValidAudience = "FestiveGuestApp",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Configure Key Vault
var keyVaultUrl = "https://kv-festive-guest.vault.azure.net/";
var secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

// Load all secrets
var secretService = new SecretService(secretClient);
var secrets = await secretService.LoadSecretsAsync();

// Configure Table Storage
var tableServiceClient = new TableServiceClient(secrets.TableStorageConnectionString);

// Update JWT configuration with actual secret key
var jwtKey = Encoding.ASCII.GetBytes(secrets.JwtSecretKey);
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(jwtKey);
    options.TokenValidationParameters.ValidIssuer = secrets.JwtIssuer;
    options.TokenValidationParameters.ValidAudience = secrets.JwtAudience;
});

builder.Services.AddSingleton(tableServiceClient);
builder.Services.AddSingleton(secrets);

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOTPRepository, OTPRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IGuestPostRepository, GuestPostRepository>();
builder.Services.AddScoped<IHostPostRepository, HostPostRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IReferralPointsService, ReferralPointsService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<ISasTokenService, SasTokenService>();
builder.Services.AddHostedService<FestiveGuestAPI.BackgroundServices.OTPCleanupService>();
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

// Comment out HTTPS redirect for testing
// app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

// Table discovery endpoints (commented out - not needed)
/*
app.MapGet("/tables", async (TableDiscoveryService service) =>
{
    var tables = await service.GetTableNamesAsync();
    return Results.Ok(tables);
})
.WithName("GetTables")
.WithOpenApi();

app.MapGet("/tables/{tableName}/schema", async (string tableName, TableDiscoveryService service) =>
{
    var schema = await service.GetTableSchemaAsync(tableName);
    return Results.Ok(schema);
})
.WithName("GetTableSchema")
.WithOpenApi();

app.MapGet("/tables/{tableName}/data", async (string tableName, TableDiscoveryService service, int maxRows = 10) =>
{
    var data = await service.GetTableDataAsync(tableName, maxRows);
    return Results.Ok(data);
})
.WithName("GetTableData")
.WithOpenApi();
*/

app.Run();
