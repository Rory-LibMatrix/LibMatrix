using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;
using LibMatrix.Extensions;
using LibMatrix.Federation;
using LibMatrix.FederationTest.Services;
using LibMatrix.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => {
        options.InvalidModelStateResponseFactory = context => {
            var problemDetails = new ValidationProblemDetails(context.ModelState) {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Detail = "See the errors property for more details.",
                Instance = context.HttpContext.Request.Path
            };

            Console.WriteLine("Model validation failed: " + problemDetails.ToJson());

            return new BadRequestObjectResult(problemDetails) {
                ContentTypes = { "application/problem+json", "application/problem+xml" }
            };
        };
    })
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
        // options.JsonSerializerOptions.DefaultBufferSize = ;
    }).AddMvcOptions(o => { o.SuppressOutputFormatterBuffering = true; });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpLogging(options => {
    options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    options.RequestHeaders.Add("X-Forwarded-Proto");
    options.RequestHeaders.Add("X-Forwarded-Host");
    options.RequestHeaders.Add("X-Forwarded-Port");
});
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRoryLibMatrixServices();
builder.Services.AddSingleton<FederationTestConfiguration>();
builder.Services.AddSingleton<FederationKeyStore>();
builder.Services.AddScoped<ServerAuthService>();

var app = builder.Build();
// Configure the HTTP request pipeline.
if (true || app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

// app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
// app.UseHttpLogging();

app.Run();