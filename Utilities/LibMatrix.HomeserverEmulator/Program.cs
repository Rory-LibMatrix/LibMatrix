using System.Net.Mime;
using System.Text.Json.Serialization;
using LibMatrix;
using LibMatrix.HomeserverEmulator.Services;
using LibMatrix.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddControllers(options => {
    options.MaxValidationDepth = null;
    options.MaxIAsyncEnumerableBufferLimit = 100;
}).AddJsonOptions(options => { options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull; });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo() {
        Version = "v1",
        Title = "Rory&::LibMatrix.HomeserverEmulator",
        Description = "Partial Matrix homeserver implementation"
    });
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "LibMatrix.HomeserverEmulator.xml"));
});

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<HSEConfiguration>();
builder.Services.AddSingleton<UserStore>();
builder.Services.AddSingleton<RoomStore>();
builder.Services.AddSingleton<MediaStore>();

builder.Services.AddScoped<TokenService>();

builder.Services.AddSingleton<HomeserverProviderService>();
builder.Services.AddSingleton<HomeserverResolverService>();
builder.Services.AddSingleton<PaginationTokenResolverService>();

builder.Services.AddRequestTimeouts(x => {
    x.DefaultPolicy = new RequestTimeoutPolicy {
        Timeout = TimeSpan.FromMinutes(10),
        WriteTimeoutResponse = async context => {
            context.Response.StatusCode = 504;
            context.Response.ContentType = "application/json";
            await context.Response.StartAsync();
            await context.Response.WriteAsJsonAsync(new MatrixException() {
                ErrorCode = "M_TIMEOUT",
                Error = "Request timed out"
            }.GetAsJson());
            await context.Response.CompleteAsync();
        }
    };
});
builder.Services.AddCors(options => {
    options.AddPolicy(
        "Open",
        policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Open");
app.UseExceptionHandler(exceptionHandlerApp => {
    exceptionHandlerApp.Run(async context => {
        var exceptionHandlerPathFeature =
            context.Features.Get<IExceptionHandlerPathFeature>();
        if (exceptionHandlerPathFeature?.Error is not null)
            Console.WriteLine(exceptionHandlerPathFeature.Error.ToString()!);

        if (exceptionHandlerPathFeature?.Error is MatrixException mxe) {
            context.Response.StatusCode = mxe.ErrorCode switch {
                "M_NOT_FOUND" => StatusCodes.Status404NotFound,
                "M_UNAUTHORIZED" => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsync(mxe.GetAsJson()!);
        }
        else {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsync(new MatrixException() {
                ErrorCode = "M_UNKNOWN",
                Error = exceptionHandlerPathFeature?.Error.ToString()
            }.GetAsJson());
        }
    });
});

app.UseAuthorization();

app.MapControllers();

app.Map("/_matrix/{*_}", (HttpContext ctx, ILogger<Program> logger) => {
    logger.LogWarning("Client hit non-existing route: {Method} {Path}{Query}", ctx.Request.Method, ctx.Request.Path, ctx.Request.Query);
    // Console.WriteLine($"Client hit non-existing route: {ctx.Request.Method} {ctx.Request.Path}");
    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
    ctx.Response.ContentType = MediaTypeNames.Application.Json;
    return ctx.Response.WriteAsJsonAsync(new MatrixException() {
        ErrorCode = MatrixException.ErrorCodes.M_UNRECOGNISED,
        Error = "Endpoint not implemented"
    });
});

app.Run();