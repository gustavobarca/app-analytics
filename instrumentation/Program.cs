using HttpLogger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHttpLoggerSink(new Uri("http://127.0.0.1:7878/"));

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
