using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ExportService>();

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "QLChiTieuSV API v1");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Tự động kiểm tra và khởi tạo bảng FinancialNotes nếu chưa có trong CSDL
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.ExecuteSqlRaw(@"
        IF OBJECT_ID('FinancialNotes', 'U') IS NULL
        BEGIN
            CREATE TABLE FinancialNotes (
                NoteId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                UserId INT NOT NULL,
                Title NVARCHAR(200) NOT NULL,
                Content NVARCHAR(MAX) NULL,
                EstimatedAmount DECIMAL(18,2) NULL,
                NoteType NVARCHAR(50) NOT NULL DEFAULT 'General',
                Priority NVARCHAR(20) NOT NULL DEFAULT 'Normal',
                IsCompleted BIT NOT NULL DEFAULT 0,
                DueDate DATE NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
                UpdatedAt DATETIME2 NULL,
                CONSTRAINT FK_FinancialNotes_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
            );
        END
    ");
}
catch
{
}

app.Run();
