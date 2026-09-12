using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roombook.Api;
using Roombook.Infrastructure;
using Roombook.Reservations;
using Roombook.Rooms;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddRoombookSwagger();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Extensions["correlationId"] = correlationId;
    };
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.HttpContext.TraceIdentifier;
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed."
        };
        problem.Extensions["correlationId"] = correlationId;
        return new BadRequestObjectResult(problem);
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
    allowedOrigins = new[] { "http://localhost:5173", "http://127.0.0.1:5173" };
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Test") && allowedOrigins.Length == 0)
    throw new InvalidOperationException("Cors:AllowedOrigins must contain the deployed client origin.");
builder.Services.AddCors(options => options.AddPolicy("Client", policy =>
    policy.WithOrigins(allowedOrigins)
        .WithHeaders("Content-Type", "Authorization", "X-Development-Member-Id", "X-Correlation-Id")
        .WithMethods("GET", "POST", "OPTIONS")
        .AllowCredentials()));

var connectionString = builder.Configuration.GetConnectionString("Roombook");
var identityEnabled = !string.IsNullOrWhiteSpace(connectionString);
if (identityEnabled)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
    builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, MemberClaimsPrincipalFactory>();
}
else
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Development";
        options.DefaultChallengeScheme = "Development";
    }).AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>("Development", _ => { });
}
builder.Services.AddAuthorization();
var roomId = Guid.TryParse(builder.Configuration["DefaultRoom:Id"], out var configuredId)
    ? configuredId
    : Guid.Parse("00000000-0000-0000-0000-000000000001");
var room = new Room(new RoomId(roomId), "Default room", TimeZoneInfo.Utc,
    new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) });
builder.Services.AddSingleton<IRoomAvailability>(new InMemoryRoomAvailability(new[] { room }));
if (string.IsNullOrWhiteSpace(connectionString))
{
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Test"))
        throw new InvalidOperationException("A Roombook PostgreSQL connection string is required outside Development and Test.");
    builder.Services.AddSingleton<IBookingRepository, InMemoryBookingRepository>();
}
else
{
    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
    builder.Services.AddSingleton<IBookingRepository, PostgresBookingRepository>();
}
builder.Services.AddSingleton<BookingService>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Roombook API v1"));
}
var skipPostgresInitialization =
    (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test")) &&
    builder.Configuration.GetValue<bool>("Roombook:SkipPostgresInitialization");
if (!string.IsNullOrWhiteSpace(connectionString) && !skipPostgresInitialization)
{
    await PostgresSchemaInitializer.InitializeAsync(app.Services.GetRequiredService<NpgsqlDataSource>());
}
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    if (response.StatusCode < StatusCodes.Status400BadRequest ||
        response.ContentLength.HasValue || response.ContentType is not null)
        return;

    var problemDetails = statusContext.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problemDetails.WriteAsync(new ProblemDetailsContext
    {
        HttpContext = statusContext.HttpContext,
        ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = response.StatusCode,
            Title = response.StatusCode switch
            {
                StatusCodes.Status401Unauthorized => "Authentication is required.",
                StatusCodes.Status403Forbidden => "You are not authorized to perform this action.",
                StatusCodes.Status404NotFound => "The requested resource was not found.",
                _ => "The request could not be completed."
            }
        }
    });
});
app.UseCors("Client");
app.UseAuthentication();
app.UseAuthorization();
if (identityEnabled)
    app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();
app.MapControllers();
app.Run();

public partial class Program;
