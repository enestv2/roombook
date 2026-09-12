using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;
using Roombook.Api;
using Roombook.Api.Localization;
using Roombook.Infrastructure;
using Roombook.Reservations;
using Roombook.Rooms;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { new CultureInfo("en"), new CultureInfo("tr") };
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
});
builder.Services.AddRoombookSwagger();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var messages = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<ApiMessages>>();
        var code = ApiErrorCodes.ForStatus(context.ProblemDetails.Status ?? StatusCodes.Status500InternalServerError);
        context.ProblemDetails.Title = messages[code].Value;
        var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Extensions["code"] = code;
        if (context.ProblemDetails is ValidationProblemDetails validation
            && !context.ProblemDetails.Extensions.ContainsKey("errorCodes"))
        {
            context.ProblemDetails.Extensions["errorCodes"] = validation.Errors.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Select(_ => entry.Key).ToArray());
        }
        context.ProblemDetails.Extensions["correlationId"] = correlationId;
    };
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var messages = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<ApiMessages>>();
        return new BadRequestObjectResult(LocalizedProblemDetails.FromModelState(
            context.HttpContext, context.ModelState, messages));
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
    builder.Services.AddScoped<IdentityErrorDescriber, LocalizedIdentityErrorDescriber>();
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
app.UseRequestLocalization();
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
    var messages = statusContext.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<ApiMessages>>();
    var statusCode = response.StatusCode;
    var code = ApiErrorCodes.ForStatus(statusCode);
    await problemDetails.WriteAsync(new ProblemDetailsContext
    {
        HttpContext = statusContext.HttpContext,
        ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = messages[code].Value,
            Detail = messages[code].Value
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
