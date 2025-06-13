using CRM.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

//cookie settings
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // adjust to your login path
        options.AccessDeniedPath = "/Auth/AccessDenied";
    });

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<CallCenterContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//blazor dependencies
//app.MapBlazorHub();
//app.MapFallbackToPage("/_Host");
app.UseStaticFiles(); // Must be before routing!

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication(); // <== ADD THIS
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=login}/{id?}")
    .WithStaticAssets();


app.Run();
