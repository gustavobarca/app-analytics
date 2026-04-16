using HttpLogger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHttpLoggerSink(new Uri("https://webhook.site/c3fc0cd9-c1fb-457c-a667-7fbb2d110a9b"));

builder.Services.AddHttpClient("external", c =>
{
    c.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/");
}).AddHttpLogger();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCustomHttpLogger();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
