//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using SmartRecipGene.Data;
//using SmartRecipGene.Services;
//using SmartRecipGene.Models; // Adjust namespace as per your project

//using Microsoft.AspNetCore.Http; // Add this for session access



//var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("dbcs")));


//builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
//    .AddEntityFrameworkStores<ApplicationDbContext>();
//// Add services to the container.
//builder.Services.AddControllersWithViews();
//builder.Services.AddDistributedMemoryCache(); // Required for Session
//builder.Services.AddSession(); // Add Session services
//builder.Services.AddHttpContextAccessor(); // Required for accessing session in controllers
//builder.Services.AddHttpClient();


////builder.Services.AddHttpClient<SpoonacularService>();
//builder.Services.AddScoped<SpoonacularService>();

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    app.UseHsts();
//}
//app.UseSession();

//app.UseHttpsRedirection();
//app.UseStaticFiles();

//app.UseRouting();
//app.UseAuthentication(); // ✅ Add this

//app.UseAuthorization();

////app.UseEndpoints(endpoints =>
////{
////    endpoints.MapControllerRoute(
////        name: "default",
////        pattern: "{controller=Home}/{action=Index}/{id?}");
////});

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

//app.MapRazorPages();
//app.Run();

// static async Task CreateAdminUser(IServiceProvider serviceProvider)
//{
//    var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
//    var adminEmail = "admin@exampl.com";  // Change this to your admin email
//    var adminPassword = "Admin@123";       // Change this password

//    var adminUser = await userManager.FindByEmailAsync(adminEmail);
//    if (adminUser == null)
//    {
//        var newUser = new IdentityUser { UserName = adminEmail, Email = adminEmail };
//        var result = await userManager.CreateAsync(newUser, adminPassword);
//        if (result.Succeeded)
//        {
//            await userManager.AddToRoleAsync(newUser, "Admin");
//        }
//    }
//}
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http; // Required for session access
using SmartRecipGene.Data;
using SmartRecipGene.Services;
using SmartRecipGene.Models; // Adjust namespace as per your project

var builder = WebApplication.CreateBuilder(args);

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("dbcs")));

// Add Identity with Roles
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>() // Add roles support
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache(); // Required for Session
builder.Services.AddSession(); // Add Session services
builder.Services.AddHttpContextAccessor(); // Required for accessing session in controllers
builder.Services.AddHttpClient();
builder.Services.AddScoped<SpoonacularService>();

var app = builder.Build();

// Ensure roles and admin user are created
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await CreateRolesAndAdminUser(services);
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSession();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.Run();

// ✅ Method to create roles and an admin user
static async Task CreateRolesAndAdminUser(IServiceProvider serviceProvider)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

    string[] roleNames = { "Admin", "User" };

    foreach (var roleName in roleNames)
    {
        var roleExists = await roleManager.RoleExistsAsync(roleName);
        if (!roleExists)
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Create Admin User
    string adminEmail = "admin@example.com"; // Change to your admin email
    string adminPassword = "Admin@123"; // Change to a secure password

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}
