using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Azure.Identity;
using Activity.Contracts;
using Activity.Implementation;
using Camps.Contracts;
using Camps.Implementation;
using Catering.Contracts;
using Catering.Implementation;
using FreizeitCockpit.ServiceDefaults;
using Identity.Contracts;
using Identity.Implementation;
using Knowledge.Contracts;
using Knowledge.Implementation;
using Logistics.Contracts;
using Logistics.Implementation;
using Files.Contracts;
using Files.Implementation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.IdentityModel.Tokens;
using Spiritual.Contracts;
using Spiritual.Implementation;

var builder = WebApplication.CreateBuilder(args);
var isOpenApiGeneration = Environment.GetCommandLineArgs().Any(argument =>
    string.Equals(argument, "--document-name", StringComparison.Ordinal));
builder.AddFreizeitServiceDefaults();
var configuredConnectionString = builder.Configuration.GetConnectionString("freizeit");

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");
builder.Services.AddHealthChecks();
if (builder.Environment.IsProduction() && !isOpenApiGeneration)
{
    var managedIdentityClientId = RequiredConfiguration("AZURE_CLIENT_ID");
    var blobServiceUri = new Uri(RequiredConfiguration("Storage:BlobServiceUri"));
    var blobContainer = RequiredConfiguration("DataProtection:BlobContainer");
    var keyIdentifier = new Uri(RequiredConfiguration("DataProtection:KeyIdentifier"));
    var keyRingUri = new Uri(blobServiceUri, $"{Uri.EscapeDataString(blobContainer)}/keys.xml");
    var credential = new ManagedIdentityCredential(
        ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId));

    builder.Services.AddDataProtection()
        .SetApplicationName("Freizeit-Cockpit")
        .PersistKeysToAzureBlobStorage(keyRingUri, credential)
        .ProtectKeysWithAzureKeyVault(keyIdentifier, credential);
}
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "freizeit_csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.HeaderName = "X-CSRF-TOKEN";
});
var jwtSigningMaterial = JwtSigningMaterialFactory.Create(
    builder.Configuration,
    builder.Environment,
    isOpenApiGeneration);
builder.Services.AddSingleton(jwtSigningMaterial);
builder.Services.AddSingleton<JwtAuthenticationTokenIssuer>();
builder.Services.AddSingleton<IAuthenticationTokenIssuer>(services =>
    services.GetRequiredService<JwtAuthenticationTokenIssuer>());
builder.Services.AddSingleton<IRefreshTokenReader>(services =>
    services.GetRequiredService<JwtAuthenticationTokenIssuer>());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Authentication:Jwt:Issuer"] ?? "freizeit-cockpit",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Authentication:Jwt:Audience"] ?? "freizeit-cockpit-api",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSigningMaterial.Key,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidTypes = ["at+jwt"],
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var validator = context.HttpContext.RequestServices
                    .GetRequiredService<IAuthenticationSessionValidator>();
                if (principal is null
                    || !Guid.TryParse(principal.FindFirstValue("sub"), out var userId)
                    || !Guid.TryParse(principal.FindFirstValue("sid"), out var sessionId)
                    || principal.FindFirstValue("sst") is not { } securityStamp
                    || !await validator.IsSessionActiveAsync(
                        new SessionValidationRequest(sessionId, userId, securityStamp),
                        context.HttpContext.RequestAborted))
                {
                    context.Fail("The authentication session is no longer active.");
                }
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton(TimeProvider.System);
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = TestingAuthenticationHandler.SchemeName;
            options.DefaultChallengeScheme = TestingAuthenticationHandler.SchemeName;
        })
        .AddScheme<AuthenticationSchemeOptions, TestingAuthenticationHandler>(
            TestingAuthenticationHandler.SchemeName,
            _ => { });
}
if (builder.Environment.IsEnvironment("Testing") || isOpenApiGeneration)
{
    builder.Services.AddScoped<IPasswordAuthentication>(_ =>
        throw new InvalidOperationException("Tests that use password authentication must supply IPasswordAuthentication."));
    builder.Services.AddScoped<IAuthenticationSessionValidator>(_ =>
        throw new InvalidOperationException("Tests that validate authentication sessions must supply IAuthenticationSessionValidator."));
    builder.Services.AddScoped<IAuthenticationSessionManagement>(_ =>
        throw new InvalidOperationException("Tests that use authentication sessions must supply IAuthenticationSessionManagement."));
    builder.Services.AddScoped<IInitialSuperAdminRegistration>(_ =>
        throw new InvalidOperationException("Tests that use First Login must supply IInitialSuperAdminRegistration."));
    builder.Services.AddScoped<IPasswordMaintenance>(_ =>
        throw new InvalidOperationException("Tests that maintain passwords must supply IPasswordMaintenance."));
    builder.Services.AddSingleton<IPasswordlessState>(_ =>
        throw new InvalidOperationException("Tests that use authentication must supply IPasswordlessState."));
    builder.Services.AddSingleton<ILoginCodeSender>(_ =>
        throw new InvalidOperationException("Tests that request codes must supply ILoginCodeSender."));
    builder.Services.AddSingleton<IEmailChangeCodeSender>(_ =>
        throw new InvalidOperationException("Tests that send email-change codes must supply IEmailChangeCodeSender."));
    builder.Services.AddSingleton<IInvitationSender>(_ =>
        throw new InvalidOperationException("Tests that send invitations must supply IInvitationSender."));
    builder.Services.AddSingleton<IPasswordResetSender>(_ =>
        throw new InvalidOperationException("Tests that send password resets must supply IPasswordResetSender."));
    builder.Services.AddSingleton<IPasswordlessLogin>(services =>
        CreatePasswordlessLogin(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<IInvitationLifecycle>(_ =>
        throw new InvalidOperationException("Tests that use invitation endpoints must supply IInvitationLifecycle."));
    builder.Services.AddScoped<ITransferableInvitationLinks>(_ =>
        throw new InvalidOperationException("Tests that use transferable invitations must supply ITransferableInvitationLinks."));
    builder.Services.AddScoped<IInvitationRegistration>(_ =>
        throw new InvalidOperationException("Tests that use invitation registration must supply IInvitationRegistration."));
    builder.Services.AddSingleton<IInvitationConfirmationSender>(_ =>
        throw new InvalidOperationException("Tests that send invitation confirmations must supply IInvitationConfirmationSender."));
    builder.Services.AddScoped<IAccountLifecycle>(_ =>
        throw new InvalidOperationException("Tests that use account endpoints must supply IAccountLifecycle."));
    builder.Services.AddScoped<IEmailChangeLifecycle>(_ =>
        throw new InvalidOperationException("Tests that use email-change endpoints must supply IEmailChangeLifecycle."));
    builder.Services.AddScoped<ITenantAccessControl>(_ =>
        throw new InvalidOperationException("Tests that use tenant authorization must supply ITenantAccessControl."));
    builder.Services.AddScoped<ITenantAdministration>(_ =>
        throw new InvalidOperationException("Tests that use tenant administration must supply ITenantAdministration."));
    builder.Services.AddScoped<ICampMemberDirectory>(_ =>
        throw new InvalidOperationException("Tests that use the camp member directory must supply ICampMemberDirectory."));
    builder.Services.AddScoped<IPlatformAdministration>(_ =>
        throw new InvalidOperationException("Tests that use platform administration must supply IPlatformAdministration."));
    builder.Services.AddScoped<ICampManagement>(_ =>
        throw new InvalidOperationException("Tests that use camp management must supply ICampManagement."));
    builder.Services.AddScoped<ICampPlanningDefaults>(_ =>
        throw new InvalidOperationException("Tests that use camp defaults must supply ICampPlanningDefaults."));
    builder.Services.AddScoped<ISchedulePlanning>(_ =>
        throw new InvalidOperationException("Tests that use schedule planning must supply ISchedulePlanning."));
    builder.Services.AddScoped<IScheduleReferenceAccess>(_ =>
        throw new InvalidOperationException("Tests that use schedule references must supply IScheduleReferenceAccess."));
    builder.Services.AddScoped<IOrganizationCateringLibrary>(_ =>
        throw new InvalidOperationException("Tests that use the catering library must supply IOrganizationCateringLibrary."));
    builder.Services.AddScoped<ICampMealPlanning>(_ =>
        throw new InvalidOperationException("Tests that use meal planning must supply ICampMealPlanning."));
    builder.Services.AddScoped<IMealShoppingSource>(_ =>
        throw new InvalidOperationException("Tests that use meal shopping must supply IMealShoppingSource."));
    builder.Services.AddScoped<IDevotionPlanning>(_ =>
        throw new InvalidOperationException("Tests that use devotion planning must supply IDevotionPlanning."));
    builder.Services.AddScoped<ICampNotebook>(_ =>
        throw new InvalidOperationException("Tests that use the camp notebook must supply ICampNotebook."));
    builder.Services.AddScoped<IMaterialPlanning>(_ =>
        throw new InvalidOperationException("Tests that use material planning must supply IMaterialPlanning."));
    builder.Services.AddScoped<IShoppingPlanning>(_ =>
        throw new InvalidOperationException("Tests that use shopping planning must supply IShoppingPlanning."));
    builder.Services.AddScoped<IShoppingTransfer>(_ =>
        throw new InvalidOperationException("Tests that use shopping transfer must supply IShoppingTransfer."));
    builder.Services.AddScoped<IShoppingAudit>(_ =>
        throw new InvalidOperationException("Tests that use shopping audit must supply IShoppingAudit."));
    builder.Services.AddScoped<IAttachmentCatalog>(_ =>
        throw new InvalidOperationException("Tests that use attachments must supply IAttachmentCatalog."));
    builder.Services.AddScoped<IAttachmentReader>(_ =>
        throw new InvalidOperationException("Tests that read attachments must supply IAttachmentReader."));
    builder.Services.AddScoped<IActivityJournal>(_ =>
        throw new InvalidOperationException("Tests that use activity must supply IActivityJournal."));
    builder.Services.AddScoped<ICampSearchIndex>(_ =>
        throw new InvalidOperationException("Tests that use search must supply ICampSearchIndex."));
    builder.Services.AddScoped<ICampExportFormatter>(_ =>
        throw new InvalidOperationException("Tests that use exports must supply ICampExportFormatter."));
}
else
{
    _ = configuredConnectionString
        ?? throw new InvalidOperationException("ConnectionStrings:freizeit must be configured.");
    var runtimeRole = builder.Configuration["Database:RuntimeRole"] ?? "freizeit_app";
    builder.Services.AddSingleton(services => FreizeitServiceDefaults.CreatePostgresDataSource(
        builder.Configuration,
        builder.Environment));
    builder.Services.AddHealthChecks().AddCheck<PostgresReadinessHealthCheck>(
        "postgresql",
        tags: ["ready"]);
    builder.Services.AddScoped(services =>
        services.GetRequiredService<NpgsqlDataSource>().CreateConnection());
    builder.Services.AddSingleton(new RuntimeRoleConnectionInterceptor(runtimeRole));
    builder.Services.AddDbContext<IdentityDbContext>((services, options) =>
        options
            .UseNpgsql(services.GetRequiredService<NpgsqlConnection>())
            .AddInterceptors(services.GetRequiredService<RuntimeRoleConnectionInterceptor>()));
    builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 15;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 10;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<IdentityDbContext>();
    builder.Services.AddScoped<PasswordAuthenticationService>(services =>
        CreatePasswordAuthentication(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<IPasswordAuthentication>(services =>
        services.GetRequiredService<PasswordAuthenticationService>());
    builder.Services.AddScoped<IAuthenticationSessionValidator>(services =>
        services.GetRequiredService<PasswordAuthenticationService>());
    builder.Services.AddScoped<IAuthenticationSessionManagement>(services =>
        services.GetRequiredService<PasswordAuthenticationService>());
    builder.Services.AddScoped<IInitialSuperAdminRegistration>(services =>
        CreateInitialSuperAdminRegistration(services, builder.Configuration, builder.Environment));
    builder.Services.AddSingleton<IPasswordResetSender, SmtpPasswordResetSender>();
    builder.Services.AddScoped<IPasswordMaintenance>(services =>
        CreatePasswordMaintenance(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<IPasswordlessState, EfPasswordlessState>();
    builder.Services.AddScoped<EfIdentityLifecycleState>();
    builder.Services.AddScoped<IIdentityLifecycleState>(services =>
        services.GetRequiredService<EfIdentityLifecycleState>());
    builder.Services.AddScoped<ITenantAuthorizationState>(services =>
        services.GetRequiredService<EfIdentityLifecycleState>());
    builder.Services.AddScoped<IEmailChangeState, EfEmailChangeState>();
    builder.Services.AddSingleton<ILoginCodeSender, SmtpLoginCodeSender>();
    builder.Services.AddSingleton<IEmailChangeCodeSender, SmtpEmailChangeCodeSender>();
    builder.Services.AddSingleton<IInvitationSender, SmtpInvitationSender>();
    builder.Services.AddScoped<IPasswordlessLogin>(services =>
        CreatePasswordlessLogin(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<IdentityLifecycleService>(services =>
        CreateIdentityLifecycle(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<IInvitationLifecycle>(services =>
        services.GetRequiredService<IdentityLifecycleService>());
    builder.Services.AddScoped<ITransferableInvitationLinks>(services =>
        CreateTransferableInvitationLinks(services, builder.Configuration, builder.Environment));
    builder.Services.AddSingleton<IInvitationConfirmationSender, SmtpInvitationConfirmationSender>();
    builder.Services.AddScoped<IInvitationRegistration>(services =>
        CreateInvitationRegistration(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<IAccountLifecycle>(services =>
        services.GetRequiredService<IdentityLifecycleService>());
    builder.Services.AddScoped<IEmailChangeLifecycle>(services =>
        CreateEmailChangeLifecycle(services, builder.Configuration, builder.Environment));
    builder.Services.AddScoped<TenantAuthorizationService>();
    builder.Services.AddScoped<ITenantAccessControl>(services =>
        services.GetRequiredService<TenantAuthorizationService>());
    builder.Services.AddScoped<ITenantAdministration>(services =>
        services.GetRequiredService<TenantAuthorizationService>());
    builder.Services.AddScoped<ICampMemberDirectory>(services =>
        services.GetRequiredService<TenantAuthorizationService>());
    builder.Services.AddScoped<IPlatformAdministration>(services =>
        services.GetRequiredService<TenantAuthorizationService>());
    builder.Services.AddDbContext<CampsDbContext>((services, options) =>
        options
            .UseNpgsql(services.GetRequiredService<NpgsqlConnection>())
            .AddInterceptors(services.GetRequiredService<RuntimeRoleConnectionInterceptor>()));
    builder.Services.AddScoped<ICampsState, EfCampsState>();
    builder.Services.AddScoped<CampPlanningService>();
    builder.Services.AddScoped<ICampManagement>(services =>
        services.GetRequiredService<CampPlanningService>());
    builder.Services.AddScoped<ICampPlanningDefaults>(services =>
        services.GetRequiredService<CampPlanningService>());
    builder.Services.AddScoped<SchedulePlanningService>();
    builder.Services.AddScoped<ISchedulePlanning>(services =>
        services.GetRequiredService<SchedulePlanningService>());
    builder.Services.AddScoped<IScheduleReferenceAccess>(services =>
        services.GetRequiredService<SchedulePlanningService>());
    builder.Services.AddDbContext<CateringDbContext>((services, options) =>
        options
            .UseNpgsql(services.GetRequiredService<NpgsqlConnection>())
            .AddInterceptors(services.GetRequiredService<RuntimeRoleConnectionInterceptor>()));
    builder.Services.AddScoped<ICampCateringContext, CampCateringContextAdapter>();
    builder.Services.AddScoped<CateringService>();
    builder.Services.AddScoped<IOrganizationCateringLibrary>(services =>
        services.GetRequiredService<CateringService>());
    builder.Services.AddScoped<ICampMealPlanning>(services =>
        services.GetRequiredService<CateringService>());
    builder.Services.AddScoped<IMealShoppingSource>(services =>
        services.GetRequiredService<CateringService>());
    builder.Services.AddDbContext<SpiritualDbContext>((services, options) =>
        options
            .UseNpgsql(services.GetRequiredService<NpgsqlConnection>())
            .AddInterceptors(services.GetRequiredService<RuntimeRoleConnectionInterceptor>()));
    builder.Services.AddScoped<IDevotionState, EfDevotionState>();
    builder.Services.AddScoped<IDevotionCampContext, DevotionCampContextAdapter>();
    builder.Services.AddHttpClient<IBiblePassageProvider, HttpBiblePassageProvider>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Bible:BaseUrl"] ?? "https://bible.helloao.org/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    builder.Services.AddScoped<IDevotionPlanning, DevotionPlanningService>();
    builder.Services.AddDbContext<KnowledgeDbContext>((services, options) =>
        options
            .UseNpgsql(services.GetRequiredService<NpgsqlConnection>())
            .AddInterceptors(services.GetRequiredService<RuntimeRoleConnectionInterceptor>()));
    builder.Services.AddScoped<IKnowledgeCampContext, KnowledgeCampContextAdapter>();
    builder.Services.AddScoped<INoteLinkTargetResolver, NoteLinkTargetResolver>();
    builder.Services.AddScoped<KnowledgeService>();
    builder.Services.AddScoped<ICampNotebook>(services => services.GetRequiredService<KnowledgeService>());
    builder.Services.AddScoped<INotebookRetention>(services => services.GetRequiredService<KnowledgeService>());
    builder.Services.AddDbContext<LogisticsDbContext>((services, options) =>
        options
            .UseNpgsql(services.GetRequiredService<NpgsqlConnection>())
            .AddInterceptors(services.GetRequiredService<RuntimeRoleConnectionInterceptor>()));
    builder.Services.AddScoped<ILogisticsState, EfLogisticsState>();
    builder.Services.AddScoped<LogisticsPlanningService>();
    builder.Services.AddScoped<IMaterialPlanning>(services => services.GetRequiredService<LogisticsPlanningService>());
    builder.Services.AddScoped<IShoppingPlanning>(services => services.GetRequiredService<LogisticsPlanningService>());
    builder.Services.AddScoped<IShoppingTransfer>(services => services.GetRequiredService<LogisticsPlanningService>());
    builder.Services.AddScoped<IShoppingAudit>(services => services.GetRequiredService<LogisticsPlanningService>());
    builder.Services.AddDbContext<FilesDbContext>((services, options) =>
        options
            .UseNpgsql(services.GetRequiredService<NpgsqlConnection>())
            .AddInterceptors(services.GetRequiredService<RuntimeRoleConnectionInterceptor>()));
    builder.Services.AddScoped<IAttachmentState, EfAttachmentState>();
    builder.Services.AddScoped<IAttachmentOwnerAuthorization, AttachmentOwnerAuthorizationAdapter>();
    builder.Services.AddSingleton<IPrivateBlobStorage>(_ =>
    {
        var connectionString = builder.Configuration.GetConnectionString("blobs");
        Azure.Storage.Blobs.BlobContainerClient container;
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            container = new Azure.Storage.Blobs.BlobContainerClient(connectionString, "attachments");
        }
        else
        {
            var serviceUri = builder.Configuration["Storage:BlobServiceUri"]
                ?? throw new InvalidOperationException("Storage:BlobServiceUri must be configured.");
            container = new Azure.Storage.Blobs.BlobContainerClient(
                new Uri(new Uri(serviceUri), "attachments"),
                new Azure.Identity.DefaultAzureCredential());
        }
        return new AzurePrivateBlobStorage(container);
    });
    builder.Services.AddScoped<AttachmentService>();
    builder.Services.AddScoped<IAttachmentCatalog>(services => services.GetRequiredService<AttachmentService>());
    builder.Services.AddScoped<IAttachmentReader>(services => services.GetRequiredService<AttachmentService>());
    builder.Services.AddScoped<IAttachmentMaintenance>(services => services.GetRequiredService<AttachmentService>());
    builder.Services.AddDbContext<ActivityDbContext>((services, options) =>
        options
            .UseNpgsql(services.GetRequiredService<NpgsqlConnection>())
            .AddInterceptors(services.GetRequiredService<RuntimeRoleConnectionInterceptor>()));
    builder.Services.AddScoped<ActivityService>();
    builder.Services.AddScoped<IActivityJournal>(services => services.GetRequiredService<ActivityService>());
    builder.Services.AddScoped<ICampSearchIndex>(services => services.GetRequiredService<ActivityService>());
    builder.Services.AddScoped<ICampExportFormatter, CampExportFormatter>();
}

builder.Services.AddScoped<PlanningActivityWriter>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});
app.UseAuthentication();
app.UseMiddleware<TenantDatabaseTransactionMiddleware>();
app.UseAuthorization();

app.MapGet("/api/v1", () => Results.Ok(new
{
    name = "Freizeit-Cockpit API",
    version = "v1",
    language = "de-DE"
}));
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapOpenApi("/api/v1/openapi.json");
app.MapIdentityEndpoints();
app.MapLifecycleEndpoints();
app.MapTenantAdministrationEndpoints();
app.MapCampPlanningEndpoints();
app.MapCateringEndpoints();
app.MapSpiritualEndpoints();
app.MapKnowledgeEndpoints();
app.MapLogisticsEndpoints();
app.MapFileEndpoints();
app.MapActivityEndpoints();
app.MapTrashEndpoints();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

static PasswordlessLoginService CreatePasswordlessLogin(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:LoginCodePepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException("Authentication:LoginCodePepper must be configured in production.");
    }

    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-login-code-pepper-do-not-use-in-production"));
    return new PasswordlessLoginService(
        services.GetRequiredService<IPasswordlessState>(),
        services.GetRequiredService<ILoginCodeSender>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

static PasswordAuthenticationService CreatePasswordAuthentication(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:RateLimitPepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException("Authentication:RateLimitPepper must be configured in production.");
    }
    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-authentication-rate-pepper-do-not-use-in-production"));
    return new PasswordAuthenticationService(
        services.GetRequiredService<IdentityDbContext>(),
        services.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
        services.GetRequiredService<IAuthenticationTokenIssuer>(),
        services.GetRequiredService<IRefreshTokenReader>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

static InitialSuperAdminRegistrationService CreateInitialSuperAdminRegistration(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:RateLimitPepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException("Authentication:RateLimitPepper must be configured in production.");
    }
    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-authentication-rate-pepper-do-not-use-in-production"));
    return new InitialSuperAdminRegistrationService(
        services.GetRequiredService<IdentityDbContext>(),
        services.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
        services.GetRequiredService<IAuthenticationTokenIssuer>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

static PasswordMaintenanceService CreatePasswordMaintenance(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:PasswordResetPepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException(
            "Authentication:PasswordResetPepper must be configured in production.");
    }
    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-password-reset-pepper-do-not-use-in-production"));
    return new PasswordMaintenanceService(
        services.GetRequiredService<IdentityDbContext>(),
        services.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
        services.GetRequiredService<IPasswordResetSender>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

static IdentityLifecycleService CreateIdentityLifecycle(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:InvitationTokenPepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException("Authentication:InvitationTokenPepper must be configured in production.");
    }

    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-invitation-token-pepper-do-not-use-in-production"));
    return new IdentityLifecycleService(
        services.GetRequiredService<IIdentityLifecycleState>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

static TransferableInvitationLinkService CreateTransferableInvitationLinks(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:InvitationTokenPepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException("Authentication:InvitationTokenPepper must be configured in production.");
    }

    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-invitation-token-pepper-do-not-use-in-production"));
    return new TransferableInvitationLinkService(
        services.GetRequiredService<IdentityDbContext>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

static InvitationRegistrationService CreateInvitationRegistration(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var invitationSecret = configuration["Authentication:InvitationTokenPepper"];
    var sessionSecret = configuration["Authentication:RateLimitPepper"];
    if (environment.IsProduction()
        && (string.IsNullOrWhiteSpace(invitationSecret) || string.IsNullOrWhiteSpace(sessionSecret)))
    {
        throw new InvalidOperationException(
            "Authentication:InvitationTokenPepper and Authentication:RateLimitPepper must be configured in production.");
    }
    var invitationPepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        invitationSecret ?? "development-only-invitation-token-pepper-do-not-use-in-production"));
    var sessionPepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        sessionSecret ?? "development-only-authentication-rate-pepper-do-not-use-in-production"));
    return new InvitationRegistrationService(
        services.GetRequiredService<IdentityDbContext>(),
        services.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
        services.GetRequiredService<IInvitationConfirmationSender>(),
        services.GetRequiredService<IAuthenticationTokenIssuer>(),
        services.GetRequiredService<TimeProvider>(),
        invitationPepper,
        sessionPepper);
}

static EmailChangeService CreateEmailChangeLifecycle(
    IServiceProvider services,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    var configuredPepper = configuration["Authentication:LoginCodePepper"];
    if (environment.IsProduction() && string.IsNullOrWhiteSpace(configuredPepper))
    {
        throw new InvalidOperationException("Authentication:LoginCodePepper must be configured in production.");
    }
    var pepper = SHA256.HashData(Encoding.UTF8.GetBytes(
        configuredPepper ?? "development-only-login-code-pepper-do-not-use-in-production"));
    return new EmailChangeService(
        services.GetRequiredService<IEmailChangeState>(),
        services.GetRequiredService<IEmailChangeCodeSender>(),
        services.GetRequiredService<TimeProvider>(),
        pepper);
}

app.Run();

string RequiredConfiguration(string key)
{
    var value = builder.Configuration[key];
    return !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException($"{key} must be configured in production.");
}

public partial class Program;
