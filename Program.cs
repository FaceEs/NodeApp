using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NodeWorker;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8090");

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.WriteIndented = true;
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = string.Join(" ", context.ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "The request body is invalid."
                : error.ErrorMessage));
        return new BadRequestObjectResult(ProcessResponse.Failure("REQUEST_INVALID", message));
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddValidatorsFromAssemblyContaining<ProcessRequestValidator>();
builder.Services.AddSingleton<ProcessingService>();

var app = builder.Build();
var service = app.Services.GetRequiredService<ProcessingService>();
await service.EnsureSchemaAsync(app.Lifetime.ApplicationStopping);

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "NodeWorker API v1");
    options.RoutePrefix = "api/swagger";
});
app.MapControllers();
app.Run();