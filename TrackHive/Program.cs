using Microsoft.EntityFrameworkCore;
using TrackHive.Data;

var builder = WebApplication.CreateBuilder(args);

// EF Core (LocalDB)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure()
    ));

// MVC + Razor
builder.Services.AddControllersWithViews();

// App services
builder.Services.AddSingleton<TrackHive.Services.Email.IEmailSender, TrackHive.Services.Email.ConsoleEmailSender>();

var app = builder.Build();

// Auto-create DB + apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); // creates TrackHiveDb if missing, applies migrations
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Future proof for role-based auth
app.UseAuthentication();
app.UseAuthorization();

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
