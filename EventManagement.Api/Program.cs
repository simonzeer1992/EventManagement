using EventManagement.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);
// All requests share one in-memory store until this API process stops.
builder.Services.AddSingleton<EventService>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
// Expected business failures keep their status and readable message.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (EventException error)
    {
        await Results.Problem(statusCode: error.StatusCode, detail: error.Message).ExecuteAsync(context);
    }
});
app.MapControllers();
app.Run();
