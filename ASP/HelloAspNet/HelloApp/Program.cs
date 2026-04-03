using System.Reflection;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Register services into the DI container
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();