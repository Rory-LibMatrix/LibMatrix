using LibMatrix.FederationTest.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpLogging(options => {
    options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
    options.RequestHeaders.Add("X-Forwarded-Proto");
    options.RequestHeaders.Add("X-Forwarded-Host");
    options.RequestHeaders.Add("X-Forwarded-Port");
});

builder.Services.AddSingleton<FederationTestConfiguration>();
builder.Services.AddSingleton<FederationKeyStore>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (true || app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();
// app.UseHttpLogging();


app.Run();