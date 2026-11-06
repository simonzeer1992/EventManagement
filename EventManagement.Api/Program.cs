using EventManagement.Api;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EventService>();
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (EventException error)
    {
        await Results.Problem(statusCode: error.StatusCode, detail: error.Message).ExecuteAsync(context);
    }
});
app.MapControllers();
app.Run();
