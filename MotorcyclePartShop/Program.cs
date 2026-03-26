using MotorcyclePartShop.Data;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Services;
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddHostedService<MotorcyclePartShop.Services.AutoCancelUnpaidOrdersService>();
// ??ng ký DbContext ?úng tên (MotorcyclePartShopDbContext)
builder.Services.AddDbContext<MotorcyclePartShopDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// Thêm Session (dùng cho gi? hàng)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Session ph?i ??t tr??c UseAuthorization
app.UseSession();

app.UseAuthorization();

// Routing m?c ??nh
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
