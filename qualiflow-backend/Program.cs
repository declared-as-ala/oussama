using System.Text;
using DocApi.Common;
using DocApi.Infrastructure;
using DocApi.Infrastructure.SignalR;
using DocApi.Repositories;
using DocApi.Repositories.Interfaces;
using DocApi.Services;
using DocApi.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RabbitMQ.Client;
using PdfSharpCore.Fonts;

// Load environment variables from .env files before builder.
LoadEnvironmentFiles();

// Register the custom cross-platform font resolver for PDF generation
GlobalFontSettings.FontResolver = new CustomFontResolver();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddInMemoryCollection(BuildEnvironmentConfiguration());

// Use console logging to avoid Windows EventLog permission issues in local dev.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// Configure JWT settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

// Configure Email settings
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMqSettings"));
builder.Services.Configure<FirebaseSettings>(builder.Configuration.GetSection("Firebase"));
builder.Services.Configure<OpenRouterSettings>(builder.Configuration.GetSection("OpenRouter"));
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? "")),
        ClockSkew = TimeSpan.Zero
    };

    // SignalR sends the JWT in query string (?access_token=...) for WebSocket/SSE.
    // Also allow query string JWT for secure file downloads/previews on native mobile platforms.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrWhiteSpace(accessToken) &&
                (path.StartsWithSegments("/hubs/notifications") ||
                 path.Value.Contains("/download") ||
                 path.Value.Contains("/preview") ||
                 path.Value.Contains("/logo") ||
                 path.Value.Contains("/photo") ||
                 path.Value.Contains("/attachment")))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

// Configure Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DocApi - Auth Module", Version = "v1" });
    c.CustomSchemaIds(type => type.FullName);
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
});

builder.Services.Configure<SupportAssistantSettings>(builder.Configuration.GetSection("SupportAssistant"));
builder.Services.Configure<SubscriptionMonitorSettings>(builder.Configuration.GetSection("SubscriptionMonitor"));
var rabbitMqSettings = builder.Configuration.GetSection("RabbitMqSettings").Get<RabbitMqSettings>() ?? new RabbitMqSettings();

// Register Infrastructure
builder.Services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();

// Register Repositories
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
builder.Services.AddScoped<ITypeDocumentRepository, TypeDocumentRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentVersionRepository, DocumentVersionRepository>();
builder.Services.AddScoped<IDocumentVersionRepository, DocumentVersionRepository>();
builder.Services.AddScoped<IDocumentActionLogRepository, DocumentActionLogRepository>();
builder.Services.AddScoped<IProcessActionLogRepository, ProcessActionLogRepository>();
builder.Services.AddScoped<IProcessRepository, ProcessRepository>();
builder.Services.AddScoped<IProcessActorRepository, ProcessActorRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IProcedureActionLogRepository, ProcedureActionLogRepository>();
builder.Services.AddScoped<IProcedureRepository, ProcedureRepository>();
builder.Services.AddScoped<IInstructionRepository, InstructionRepository>();
builder.Services.AddScoped<INonConformityRepository, NonConformityRepository>();
builder.Services.AddScoped<ICorrectiveActionRepository, CorrectiveActionRepository>();
builder.Services.AddScoped<ICorrectiveActionActionLogRepository, CorrectiveActionActionLogRepository>();
builder.Services.AddScoped<IIndicatorRepository, IndicatorRepository>();
builder.Services.AddScoped<IIndicatorValueRepository, IndicatorValueRepository>();
builder.Services.AddScoped<IIndicatorAlertRepository, IndicatorAlertRepository>();
builder.Services.AddScoped<IIndicatorActionLogRepository, IndicatorActionLogRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
builder.Services.AddScoped<IWebPushSubscriptionRepository, WebPushSubscriptionRepository>();
builder.Services.AddScoped<IDocumentNotificationRepository, DocumentNotificationRepository>();
builder.Services.AddScoped<INotificationRuleRepository, NotificationRuleRepository>();
builder.Services.AddScoped<IDocumentExpirationPolicyRepository, DocumentExpirationPolicyRepository>();
builder.Services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
builder.Services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
builder.Services.AddScoped<IActionLogRepository, ActionLogRepository>();

builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IPdfHeaderStampService, PdfHeaderStampService>();
builder.Services.AddScoped<IWordHeaderStampService, WordHeaderStampService>();
builder.Services.AddScoped<IExcelHeaderStampService, ExcelHeaderStampService>();
builder.Services.AddScoped<IOrganizationLogoStorageService, OrganizationLogoStorageService>();
builder.Services.AddScoped<IProfilePhotoStorageService, ProfilePhotoStorageService>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<ITypeDocumentService, TypeDocumentService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IProcessService, ProcessService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IProcedureService, ProcedureService>();
builder.Services.AddScoped<INonConformityService, NonConformityService>();
builder.Services.AddScoped<ICorrectiveActionService, CorrectiveActionService>();
builder.Services.AddScoped<IIndicatorService, IndicatorService>();
builder.Services.AddScoped<IActionLogger, ActionLogger>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDeviceTokenService, DeviceTokenService>();

builder.Services.AddScoped<INotificationRecipientService, NotificationRecipientService>();
builder.Services.AddScoped<INotificationEventPublisher, NotificationEventPublisher>();
builder.Services.AddScoped<IAlertRuleService, AlertRuleService>();
builder.Services.AddScoped<INotificationGeneratorService, NotificationGeneratorService>();
builder.Services.AddScoped<INotificationConsumerService, NotificationConsumerService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<ISupportService, SupportService>();
builder.Services.AddScoped<IPublicService, PublicService>();
builder.Services.AddHttpClient<IOpenRouterService, OpenRouterService>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<OpenRouterSettings>>().Value;
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    CheckCertificateRevocationList = false
});
builder.Services.AddScoped<IChatbotService, ChatbotService>();
if (rabbitMqSettings.Enabled)
{
    builder.Services.AddSingleton<ConnectionFactory>(sp =>
    {
        var settings = sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
        var factory = new ConnectionFactory
        {
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        if (!string.IsNullOrWhiteSpace(settings.Uri) &&
            (settings.Uri.StartsWith("amqp://", StringComparison.OrdinalIgnoreCase) || 
             settings.Uri.StartsWith("amqps://", StringComparison.OrdinalIgnoreCase)) &&
            Uri.TryCreate(settings.Uri, UriKind.Absolute, out var parsedUri))
        {
            factory.Uri = parsedUri;
        }
        else
        {
            factory.HostName = settings.HostName;
            factory.Port = settings.Port;
            factory.UserName = settings.UserName;
            factory.Password = settings.Password;
            factory.VirtualHost = settings.VirtualHost;
        }

        return factory;
    });

    builder.Services.AddScoped<INotificationPublisher, RabbitMqNotificationPublisher>();
}
else
{
    builder.Services.AddScoped<INotificationPublisher, InlineNotificationPublisher>();
}
builder.Services.AddSingleton<SignalRNotificationService>();
builder.Services.AddHostedService<NotificationBackgroundService>();
builder.Services.AddHostedService<OrganizationSubscriptionMonitorService>();
if (rabbitMqSettings.Enabled)
{
    builder.Services.AddHostedService<NotificationDispatcherService>();
}

var configuredCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();
var configuredCorsOriginPatterns = builder.Configuration
    .GetSection("Cors:AllowedOriginPatterns")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => IsAllowedFrontendOrigin(origin, configuredCorsOrigins, configuredCorsOriginPatterns))
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();
var isRenderEnvironment = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER"));

app.UseForwardedHeaders();

// Ensure database schema and local demo data are ready.
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration, startupLogger);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment() && !isRenderEnvironment)
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

// Required for SignalR WebSocket transport (skipNegotiation=true mode).
// Must be placed before UseAuthentication/UseAuthorization.
app.UseWebSockets();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

static void LoadEnvironmentFiles()
{
    var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var startDirectories = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    };

    foreach (var startDirectory in startDirectories)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory != null && visitedDirectories.Add(directory.FullName))
        {
            var envPath = Path.Combine(directory.FullName, ".env");
            if (File.Exists(envPath))
            {
                DotNetEnv.Env.Load(envPath);
            }

            directory = directory.Parent;
        }
    }
}

static Dictionary<string, string?> BuildEnvironmentConfiguration()
{
    var configuration = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    // JWT Settings
    AddIfPresent("JWT_SECRET", "JwtSettings:SecretKey");
    AddIfPresent("JWT_ISSUER", "JwtSettings:Issuer");
    AddIfPresent("JWT_AUDIENCE", "JwtSettings:Audience");
    AddIfPresent("JWT_EXPIRY", "JwtSettings:ExpirationInMinutes");

    // SMTP Settings
    AddIfPresent("SMTP_ENABLED", "SmtpSettings:Enabled");
    AddIfPresent("SMTP_HOST", "SmtpSettings:Host");
    AddIfPresent("SMTP_PORT", "SmtpSettings:Port");
    AddIfPresent("SMTP_USER", "SmtpSettings:Username");
    AddIfPresent("SMTP_PASS", "SmtpSettings:Password");
    AddIfPresent("SMTP_FROM", "SmtpSettings:FromEmail");

    // RabbitMQ Settings
    AddIfPresent("RABBITMQ_ENABLED", "RabbitMqSettings:Enabled");
    AddIfPresent("RABBITMQ_URI", "RabbitMqSettings:Uri");
    AddIfPresent("RABBITMQ_HOST", "RabbitMqSettings:HostName");
    AddIfPresent("RABBITMQ_PORT", "RabbitMqSettings:Port");
    AddIfPresent("RABBITMQ_USER", "RabbitMqSettings:UserName");
    AddIfPresent("RABBITMQ_PASS", "RabbitMqSettings:Password");
    AddIfPresent("RABBITMQ_VIRTUAL_HOST", "RabbitMqSettings:VirtualHost");
    AddIfPresent("RABBITMQ_QUEUE", "RabbitMqSettings:QueueName");

    // Firebase Settings
    AddIfPresent("FIREBASE_ENABLED", "Firebase:Enabled");
    AddIfPresent("FIREBASE_SERVICE_ACCOUNT_PATH", "Firebase:ServiceAccountPath");
    AddIfPresent("FIREBASE_SERVICE_ACCOUNT_JSON", "Firebase:ServiceAccountJson");
    AddIfPresent("FIREBASE_PROJECT_ID", "Firebase:ProjectId");

    // OpenRouter / Groq Settings
    AddIfPresent("OPENROUTER_API_KEY", "OpenRouter:ApiKey");
    AddIfPresent("OPENROUTER_MODEL", "OpenRouter:Model");
    AddIfPresent("OPENROUTER_URL", "OpenRouter:ApiBaseUrl");

    return configuration;

    void AddIfPresent(string environmentVariableName, string configurationKey)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName);
        if (value != null)
        {
            configuration[configurationKey] = value;
        }
    }
}

static bool IsAllowedFrontendOrigin(
    string? origin,
    IReadOnlyCollection<string> configuredOrigins,
    IReadOnlyCollection<string> configuredOriginPatterns)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return false;
    }

    if (configuredOrigins.Any(allowedOrigin =>
            string.Equals(allowedOrigin, origin, StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    if (IsOriginMatchingPattern(origin, configuredOriginPatterns))
    {
        return true;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
    {
        return false;
    }

    if (string.Equals(originUri.Scheme, "capacitor", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(originUri.Scheme, "ionic", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (string.Equals(originUri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(originUri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (System.Net.IPAddress.TryParse(originUri.Host, out var ipAddress))
    {
        return IsPrivateIpv4(ipAddress);
    }

    // Safe defaults for hosted frontends when env configuration is missing.
    if (originUri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase) ||
        originUri.Host.EndsWith(".onrender.com", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return false;
}

static bool IsOriginMatchingPattern(string origin, IReadOnlyCollection<string> patterns)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
    {
        return false;
    }

    foreach (var rawPattern in patterns)
    {
        var pattern = rawPattern?.Trim();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            continue;
        }

        if (pattern == "*")
        {
            return true;
        }

        if (string.Equals(pattern, origin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var scheme = "*";
        var hostPattern = pattern;
        var schemeSeparatorIndex = pattern.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparatorIndex >= 0)
        {
            scheme = pattern.Substring(0, schemeSeparatorIndex).Trim();
            hostPattern = pattern[(schemeSeparatorIndex + 3)..].Trim();
        }

        var slashIndex = hostPattern.IndexOf('/');
        if (slashIndex >= 0)
        {
            hostPattern = hostPattern.Substring(0, slashIndex).Trim();
        }

        if (string.IsNullOrWhiteSpace(hostPattern))
        {
            continue;
        }

        if (!string.Equals(scheme, "*", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scheme, originUri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (IsHostMatch(originUri.Host, hostPattern))
        {
            return true;
        }
    }

    return false;
}

static bool IsHostMatch(string originHost, string hostPattern)
{
    if (hostPattern.StartsWith("*.", StringComparison.Ordinal))
    {
        var suffix = hostPattern[1..];
        return originHost.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
               originHost.Length > suffix.Length;
    }

    return string.Equals(originHost, hostPattern, StringComparison.OrdinalIgnoreCase);
}

static bool IsPrivateIpv4(System.Net.IPAddress ipAddress)
{
    if (ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
    {
        return false;
    }

    var bytes = ipAddress.GetAddressBytes();
    return bytes[0] == 10 ||
           (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
           (bytes[0] == 192 && bytes[1] == 168);
}
