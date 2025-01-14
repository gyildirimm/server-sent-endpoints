using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseInMemoryDatabase("Notifications"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Bildirim ekleme endpoint'i
app.MapPost("/notifications", async (NotificationDbContext db, Notification notification) =>
{
    notification.CreatedAt = DateTime.UtcNow;
    db.Notifications.Add(notification);
    await db.SaveChangesAsync();
    return Results.Created($"/notifications/{notification.Id}", notification);
});

// SSE ile kullanıcıya özel bildirimleri yayınlama endpoint'i
app.MapGet("/notifications/stream/{userId}", async (HttpContext context, NotificationDbContext db, string userId) =>
{
    context.Response.Headers.Add("Content-Type", "text/event-stream");

    // Yeni bildirimleri sürekli olarak kontrol et
    var lastNotificationId = 0;

    while (!context.RequestAborted.IsCancellationRequested)
    {
        // Kullanıcıya özel ve yeni bildirimleri kontrol et
        var newNotifications = await db.Notifications
            .Where(n => n.UserId == userId && n.Id > lastNotificationId)
            .ToListAsync();

        if (newNotifications.Any())
        {
            foreach (var notification in newNotifications)
            {
                await context.Response.WriteAsync(
                    $"data: {System.Text.Json.JsonSerializer.Serialize(notification)}\n\n");
                await context.Response.Body.FlushAsync();
                lastNotificationId = notification.Id;
            }
        }

        // 1 saniye bekle ve tekrar kontrol et
        await Task.Delay(1000);
    }
});

app.Run();