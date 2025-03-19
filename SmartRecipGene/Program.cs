
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
//builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
//{
//    options.SignIn.RequireConfirmedAccount = false;
//})
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = true;

    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultUI()
.AddDefaultTokenProviders();
.AddEmailSender<EmailSender>();

builder.Services.Configure<IdentityOptions>(options => 
{
    options.Tokens.ProviderMap.Add("CustomEmailConfirmation",
        new TokenProviderDescriptor(
            typeof(CustomEmailConfirmationTokenProvider<ApplicationUser>)));
    options.Tokens.EmailConfirmationTokenProvider = "CustomEmailConfirmation";
});
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();


// Add services to the container
builder.Services.AddControllersWithViews();
// Add after AddIdentity configuration
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole",
         policy => policy.RequireRole("Admin"));
});
builder.Services.AddRazorPages(); // Add this line
builder.Services.Configure<SpoonacularSettings>(
builder.Configuration.GetSection("SpoonacularSettings"));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});// Required for Session
//builder.Services.AddSession(); // Add Session services
builder.Services.AddHttpContextAccessor(); // Required for accessing session in controllers
builder.Services.AddHttpClient();
builder.Services.AddScoped<SpoonacularService>();

var app = builder.Build();

// Ensure roles and admin user are created
//using (var scope = app.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;
//    await CreateRolesAndAdminUser(services);
//}
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate(); // This will create the database and apply migrations

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await CreateRolesAndAdminUser(roleManager, userManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or initializing the database.");
    }
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


async Task CreateRolesAndAdminUser(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
{
    // Add roles
    string[] roleNames = { "Admin", "User" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Add admin user
    var adminEmail = "diyadodhiawala@gmail.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            Name = "Admin User"
        };

        var result = await userManager.CreateAsync(admin, "Diya@123"); // Change this password in production
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            var roles = await userManager.GetRolesAsync(admin);
            if (!roles.Contains("Admin"))
            {
                throw new Exception("Failed to assign Admin role");
            }
        }
         else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"Failed to create admin user: {errors}");
        }
    }
    else if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        // Ensure existing admin user has Admin role
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
    }
