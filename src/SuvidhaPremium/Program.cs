using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

var storeTypes = new[]
{
    "Retail Shop",
    "Pharmacy / Medical Store",
    "Agriculture Product Store",
    "Seeds & Fertilizer Store",
    "Pesticide / Crop Care Store",
    "General Store",
    "Grocery Store",
    "Supermarket",
    "Wholesale Store",
    "Distributor",
    "FMCG Store",
    "Cosmetics & Beauty Store",
    "Personal Care Store",
    "Stationery Store",
    "Hardware Store",
    "Electrical Store",
    "Electronics Store",
    "Mobile & Accessories Store",
    "Garments Store",
    "Footwear Store",
    "Hardware & Sanitary Store",
    "Auto Parts Store",
    "Pet / Veterinary Store",
    "Dairy Store",
    "Bakery",
    "Restaurant / Cafe",
    "Sweet Shop",
    "Department Store",
    "Jewellery Shop",
    "Other"
};
var graceDays = Math.Clamp(builder.Configuration.GetValue<int?>("Renewal:GraceDays") ?? 3, 0, 7);

var cookieName = builder.Configuration["Security:CookieName"] ?? "SuvidhaPremium.Auth";
var requireHttps = builder.Configuration.GetValue<bool>("Security:RequireHttps");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = cookieName;
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.SlidingExpiration = true;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect("/login.html");
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect("/login.html");
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", p => p.RequireRole("SuperAdmin"));
    options.AddPolicy("ManageOutlet", p => p.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("ManageLicense", p => p.RequireRole("SuperAdmin", "Admin", "RenewalManager"));
});
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<LicenseSigner>();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHsts();
if (requireHttps) app.UseHttpsRedirection();
app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    if (string.Equals(ctx.Request.Path.Value, "/index.html", StringComparison.OrdinalIgnoreCase) && ctx.User.Identity?.IsAuthenticated != true)
    {
        ctx.Response.Redirect("/login.html");
        return;
    }
    await next();
});
app.UseStaticFiles();
app.UseAuthorization();

app.MapGet("/", (HttpContext ctx) => Results.Redirect(ctx.User.Identity?.IsAuthenticated == true ? "/index.html" : "/login.html"));

// ---------- Setup / authentication ----------
app.MapGet("/api/setup/status", async (Db db) =>
{
    try { return Results.Ok(new { needsSetup = !await db.AnyAdminAsync(), setupKeyConfigured = !string.IsNullOrWhiteSpace(app.Configuration["Security:SetupKey"]) }); }
    catch { return Results.Problem("Database is not ready. Run Database/01_CreateDatabase.sql and verify the connection string.", statusCode: 503); }
});

app.MapPost("/api/setup/first-admin", async (FirstAdminRequest r, Db db, IConfiguration cfg, HttpContext ctx) =>
{
    if (await db.AnyAdminAsync()) return Results.Conflict(new { message = "Initial administrator already exists." });
    var expectedSetupKey = cfg["Security:SetupKey"] ?? "";
    if (string.IsNullOrWhiteSpace(expectedSetupKey) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedSetupKey), Encoding.UTF8.GetBytes(r.SetupKey ?? "")))
        return Results.Json(new { message = "Invalid setup key." }, statusCode: 403);
    if (string.IsNullOrWhiteSpace(r.FullName) || string.IsNullOrWhiteSpace(r.Email) || !PasswordHasher.IsStrong(r.Password))
        return Results.BadRequest(new { message = "Full name, valid email and a password of at least 10 characters are required." });
    var id = Guid.NewGuid();
    await db.CreateAdminAsync(id, r.FullName.Trim(), r.Email.Trim().ToLowerInvariant(), PasswordHasher.Hash(r.Password), "SuperAdmin");
    await db.AuditAsync(null, "INITIAL_ADMIN_CREATED", "Admin", id.ToString(), $"Initial admin {r.Email} created", ctx);
    return Results.Ok(new { message = "Administrator created. Please sign in." });
});

app.MapPost("/api/auth/login", async (LoginRequest r, Db db, HttpContext ctx) =>
{
    var admin = await db.GetAdminByEmailAsync(r.Email.Trim().ToLowerInvariant());
    if (admin is null || !admin.IsActive || !PasswordHasher.Verify(r.Password, admin.PasswordHash))
        return Results.Json(new { message = "Invalid email or password." }, statusCode: 401);

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, admin.AdminId.ToString()),
        new(ClaimTypes.Name, admin.FullName),
        new(ClaimTypes.Email, admin.Email),
        new(ClaimTypes.Role, admin.Role)
    };
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { IsPersistent = r.RememberMe, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(r.RememberMe ? 72 : 8) });
    await db.SetAdminLastLoginAsync(admin.AdminId);
    await db.AuditAsync(admin.AdminId, "ADMIN_LOGIN", "Admin", admin.AdminId.ToString(), "Administrator signed in", ctx);
    return Results.Ok(new { admin.FullName, admin.Email, admin.Role });
});

app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { message = "Signed out." });
}).RequireAuthorization();

app.MapGet("/api/auth/me", (HttpContext ctx) => Results.Ok(new
{
    id = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier),
    name = ctx.User.Identity?.Name,
    email = ctx.User.FindFirstValue(ClaimTypes.Email),
    role = ctx.User.FindFirstValue(ClaimTypes.Role)
})).RequireAuthorization();

// ---------- Admin APIs ----------
app.MapGet("/api/admin/dashboard", async (Db db) => Results.Ok(await db.GetDashboardAsync())).RequireAuthorization();
app.MapGet("/api/admin/outlets", async (Db db) => Results.Ok(await db.GetOutletsAsync())).RequireAuthorization();
app.MapGet("/api/admin/devices", async (Db db) => Results.Ok(await db.GetDevicesAsync())).RequireAuthorization();
app.MapGet("/api/admin/audit", async (Db db) => Results.Ok(await db.GetAuditAsync(150))).RequireAuthorization();
app.MapGet("/api/admin/admins", async (Db db) => Results.Ok(await db.GetAdminsAsync())).RequireAuthorization();

app.MapPost("/api/admin/admins", async (CreateAdminRequest r, Db db, HttpContext ctx) =>
{
    var allowedRoles = new[] { "SuperAdmin", "Admin", "RenewalManager", "Auditor" };
    if (string.IsNullOrWhiteSpace(r.FullName) || string.IsNullOrWhiteSpace(r.Email) || !PasswordHasher.IsStrong(r.Password) || !allowedRoles.Contains(r.Role, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "Name, email, valid role and password of at least 10 characters are required." });
    try
    {
        var id = Guid.NewGuid();
        var canonicalRole = allowedRoles.First(x => x.Equals(r.Role, StringComparison.OrdinalIgnoreCase));
        await db.CreateAdminAsync(id, r.FullName.Trim(), r.Email.Trim().ToLowerInvariant(), PasswordHasher.Hash(r.Password), canonicalRole);
        await db.AuditAsync(CurrentAdmin(ctx), "ADMIN_CREATED", "Admin", id.ToString(), $"Administrator {r.Email} created", ctx);
        return Results.Ok(new { adminId = id, message = "Administrator created." });
    }
    catch (SqlException ex) when (ex.Number is 2601 or 2627)
    {
        return Results.Conflict(new { message = "An administrator with this email already exists." });
    }
}).RequireAuthorization("SuperAdmin");

app.MapPost("/api/admin/outlets", async (CreateOutletRequest r, Db db, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(r.OutletName) || string.IsNullOrWhiteSpace(r.StoreType) || r.ValidityDays < 1 || r.ValidityDays > 3650)
        return Results.BadRequest(new { message = "Outlet name, store type and valid validity days are required." });
    if (!storeTypes.Contains(r.StoreType.Trim(), StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "Invalid Store Type. Select a Store Type from SuvidhaPremium master." });
    var activationCode = ActivationCode.NewCode();
    var result = await db.CreateOutletAsync(r, ActivationCode.Hash(activationCode), CurrentAdmin(ctx));
    await db.AuditAsync(CurrentAdmin(ctx), "OUTLET_CREATED", "Outlet", result.OutletId.ToString(), $"{result.OutletName} / {result.OutletCode}", ctx);
    return Results.Ok(new { outlet = result, activationCode, note = "Save this activation code now. Only its hash is stored on the server." });
}).RequireAuthorization("ManageOutlet");

app.MapPut("/api/admin/outlets/{id:guid}", async (Guid id, UpdateOutletRequest r, Db db, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(r.StoreType) || !storeTypes.Contains(r.StoreType.Trim(), StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "Invalid Store Type. Select a Store Type from SuvidhaPremium master." });
    var changed = await db.UpdateOutletAsync(id, r, CurrentAdmin(ctx));
    if (!changed) return Results.NotFound(new { message = "Outlet not found." });
    await db.AuditAsync(CurrentAdmin(ctx), "OUTLET_UPDATED", "Outlet", id.ToString(), "Outlet details/store type updated", ctx);
    return Results.Ok(new { message = "Outlet updated." });
}).RequireAuthorization("ManageOutlet");

app.MapPost("/api/admin/outlets/{id:guid}/renew", async (Guid id, RenewRequest r, Db db, HttpContext ctx) =>
{
    if (r.Days < 1 || r.Days > 3650) return Results.BadRequest(new { message = "Renewal must be between 1 and 3650 days." });
    var result = await db.RenewAsync(id, r.Days, CurrentAdmin(ctx));
    if (result is null) return Results.NotFound(new { message = "Outlet not found." });
    await db.AuditAsync(CurrentAdmin(ctx), "LICENSE_RENEWED", "Outlet", id.ToString(), $"Validity extended by {r.Days} days to {result.ValidUntilUtc:O}", ctx);
    return Results.Ok(result);
}).RequireAuthorization("ManageLicense");

app.MapPost("/api/admin/outlets/{id:guid}/block", async (Guid id, BlockRequest r, Db db, HttpContext ctx) =>
{
    if (!await db.SetOutletBlockedAsync(id, r.Blocked)) return Results.NotFound(new { message = "Outlet not found." });
    await db.AuditAsync(CurrentAdmin(ctx), r.Blocked ? "OUTLET_BLOCKED" : "OUTLET_UNBLOCKED", "Outlet", id.ToString(), r.Blocked ? "Outlet login/license blocked" : "Outlet unblocked", ctx);
    return Results.Ok(new { message = r.Blocked ? "Outlet blocked." : "Outlet unblocked." });
}).RequireAuthorization("ManageOutlet");

app.MapPost("/api/admin/outlets/{id:guid}/reset-activation", async (Guid id, Db db, HttpContext ctx) =>
{
    var code = ActivationCode.NewCode();
    if (!await db.ResetActivationAsync(id, ActivationCode.Hash(code))) return Results.NotFound(new { message = "Outlet not found." });
    await db.AuditAsync(CurrentAdmin(ctx), "ACTIVATION_CODE_RESET", "Outlet", id.ToString(), "Activation code reset", ctx);
    return Results.Ok(new { activationCode = code, note = "Save this activation code now. It cannot be recovered later." });
}).RequireAuthorization("ManageOutlet");

app.MapPost("/api/admin/devices/{id:guid}/block", async (Guid id, BlockRequest r, Db db, HttpContext ctx) =>
{
    if (!await db.SetDeviceBlockedAsync(id, r.Blocked)) return Results.NotFound(new { message = "Device not found." });
    await db.AuditAsync(CurrentAdmin(ctx), r.Blocked ? "DEVICE_BLOCKED" : "DEVICE_UNBLOCKED", "Device", id.ToString(), r.Blocked ? "Device blocked" : "Device unblocked", ctx);
    return Results.Ok(new { message = r.Blocked ? "Device blocked." : "Device unblocked." });
}).RequireAuthorization("ManageOutlet");

app.MapDelete("/api/admin/devices/{id:guid}", async (Guid id, Db db, HttpContext ctx) =>
{
    if (!await db.UnbindDeviceAsync(id)) return Results.NotFound(new { message = "Device not found." });
    await db.AuditAsync(CurrentAdmin(ctx), "DEVICE_UNBOUND", "Device", id.ToString(), "Device binding removed", ctx);
    return Results.Ok(new { message = "Device unbound. POS can be activated again." });
}).RequireAuthorization("ManageOutlet");

// ---------- POS License APIs (public HTTPS endpoints) ----------
app.MapGet("/api/pos/store-types", () => Results.Ok(storeTypes));

app.MapGet("/api/pos/public-key", (LicenseSigner signer) => Results.Ok(new
{
    algorithm = "PS256",
    keyId = signer.KeyId,
    publicKeyPem = signer.GetPublicKeyPem()
}));

app.MapPost("/api/pos/activate", async (PosActivateRequest r, Db db, LicenseSigner signer, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(r.OutletCode) || string.IsNullOrWhiteSpace(r.ActivationCode) || string.IsNullOrWhiteSpace(r.DeviceFingerprint))
        return Results.BadRequest(new { message = "Outlet code, activation code and device fingerprint are required." });
    var outlet = await db.GetOutletForActivationAsync(r.OutletCode.Trim().ToUpperInvariant());
    if (outlet is null || !ActivationCode.Verify(r.ActivationCode, outlet.ActivationCodeHash))
        return Results.Json(new { message = "Invalid outlet or activation code." }, statusCode: 401);
    if (outlet.IsBlocked) return Results.Json(new { status = "blocked", message = "Outlet is blocked by central admin." }, statusCode: 403);
    var activationDaysLapsed = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - DateOnly.FromDateTime(outlet.ValidUntilUtc).DayNumber;
    if (activationDaysLapsed > graceDays)
        return Results.Json(new { status = "expired", validUntilUtc = outlet.ValidUntilUtc, graceDays, message = "License grace period has ended. Renew to activate." }, statusCode: 403);

    var bind = await db.BindDeviceAsync(outlet.OutletId, r.DeviceFingerprint.Trim(), r.DeviceName?.Trim());
    if (!bind.Success) return Results.Json(new { status = "device_conflict", message = bind.Message }, statusCode: 409);
    var token = signer.Issue(outlet, r.DeviceFingerprint.Trim());
    await db.AuditAsync(null, "POS_ACTIVATED", "Outlet", outlet.OutletId.ToString(), $"Device {r.DeviceName ?? "POS"} activated", ctx);
    var activationStatus = activationDaysLapsed > 0 ? "grace" : "active";
    return Results.Ok(new LicenseResponse(activationStatus, token, outlet.ValidUntilUtc, outlet.StoreType, outlet.OutletCode, DateTime.UtcNow, signer.KeyId));
});

app.MapPost("/api/pos/check", async (PosCheckRequest r, Db db, LicenseSigner signer, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(r.OutletCode) || string.IsNullOrWhiteSpace(r.DeviceFingerprint))
        return Results.BadRequest(new { message = "Outlet code and device fingerprint are required." });
    var outlet = await db.GetOutletForDeviceAsync(r.OutletCode.Trim().ToUpperInvariant(), r.DeviceFingerprint.Trim());
    if (outlet is null) return Results.Json(new { status = "not_activated", message = "No active device binding found." }, statusCode: 401);
    if (outlet.DeviceBlocked) return Results.Json(new { status = "device_blocked", message = "This device is blocked." }, statusCode: 403);
    if (outlet.IsBlocked) return Results.Json(new { status = "blocked", message = "Outlet is blocked by central admin." }, statusCode: 403);
    await db.TouchDeviceAsync(outlet.DeviceId);
    var checkDaysLapsed = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - DateOnly.FromDateTime(outlet.ValidUntilUtc).DayNumber;
    if (checkDaysLapsed > graceDays)
        return Results.Ok(new { status = "expired", validUntilUtc = outlet.ValidUntilUtc, graceDays, serverTimeUtc = DateTime.UtcNow });
    var token = signer.Issue(outlet, r.DeviceFingerprint.Trim());
    return Results.Ok(new LicenseResponse(checkDaysLapsed > 0 ? "grace" : "active", token, outlet.ValidUntilUtc, outlet.StoreType, outlet.OutletCode, DateTime.UtcNow, signer.KeyId));
});

app.MapPost("/api/pos/profile", async (PosProfileRequest r, Db db, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(r.OutletCode) || string.IsNullOrWhiteSpace(r.DeviceFingerprint) || string.IsNullOrWhiteSpace(r.OutletName))
        return Results.BadRequest(new { message = "Outlet code, device fingerprint and outlet name are required." });
    var outlet = await db.GetOutletForDeviceAsync(r.OutletCode.Trim().ToUpperInvariant(), r.DeviceFingerprint.Trim());
    if (outlet is null || outlet.DeviceBlocked || outlet.IsBlocked)
        return Results.Json(new { message = "Device is not authorized." }, statusCode: 403);
    var ok = await db.UpdatePosProfileAsync(outlet.OutletId, r.OutletName.Trim(), r.Address, r.Mobile, r.GstNo);
    if (!ok) return Results.NotFound(new { message = "Outlet not found." });
    await db.AuditAsync(null, "POS_PROFILE_UPDATED", "Outlet", outlet.OutletId.ToString(), "Outlet name/contact/GST/address updated from POS. Store Type and Validity were not changed.", ctx);
    return Results.Ok(new { message = "Outlet profile updated. Store Type and Validity remain centrally controlled." });
});

app.MapGet("/renew/{outletCode}", (string outletCode, IConfiguration cfg) =>
{
    var rawNumber = cfg["Renewal:WhatsAppNumber"] ?? "";
    var number = new string(rawNumber.Where(char.IsDigit).ToArray());
    var text = Uri.EscapeDataString($"Hello SuvidhaPremium, I want to renew billing subscription for Outlet Code {outletCode.Trim().ToUpperInvariant()}.");
    var url = string.IsNullOrWhiteSpace(number)
        ? $"https://wa.me/?text={text}"
        : $"https://wa.me/{number}?text={text}";
    return Results.Redirect(url);
});

app.MapGet("/health", async (Db db) =>
{
    try { return Results.Ok(new { status = "ok", database = await db.PingAsync(), appVersion = "2.2.0", utc = DateTime.UtcNow }); }
    catch { return Results.Json(new { status = "degraded", database = false, appVersion = "2.0.0", utc = DateTime.UtcNow }, statusCode: 503); }
});
app.Run();

static Guid? CurrentAdmin(HttpContext ctx) => Guid.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

record FirstAdminRequest(string FullName, string Email, string Password, string SetupKey);
record LoginRequest(string Email, string Password, bool RememberMe = false);
record CreateAdminRequest(string FullName, string Email, string Password, string Role);
record CreateOutletRequest(string OutletName, string? Address, string? Mobile, string? GstNo, string StoreType, int ValidityDays);
record UpdateOutletRequest(string OutletName, string? Address, string? Mobile, string? GstNo, string StoreType);
record RenewRequest(int Days);
record BlockRequest(bool Blocked);
record PosActivateRequest(string OutletCode, string ActivationCode, string DeviceFingerprint, string? DeviceName);
record PosCheckRequest(string OutletCode, string DeviceFingerprint);
record PosProfileRequest(string OutletCode, string DeviceFingerprint, string OutletName, string? Address, string? Mobile, string? GstNo);
record LicenseResponse(string Status, string Token, DateTime ValidUntilUtc, string StoreType, string OutletCode, DateTime ServerTimeUtc, string KeyId);

sealed class Db
{
    private readonly string _cs;
    public Db(IConfiguration cfg) => _cs = cfg.GetConnectionString("CentralDb") ?? throw new InvalidOperationException("CentralDb connection string missing.");
    private SqlConnection Conn() => new(_cs);

    public async Task<bool> AnyAdminAsync()
    {
        await using var c = Conn(); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.AdminUsers) THEN 1 ELSE 0 END", c);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
    }

    public async Task CreateAdminAsync(Guid id, string name, string email, string hash, string role)
    {
        await using var c = Conn(); await c.OpenAsync();
        await using var cmd = new SqlCommand("INSERT dbo.AdminUsers(AdminId,FullName,Email,PasswordHash,Role) VALUES(@id,@n,@e,@p,@r)", c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@n", name); cmd.Parameters.AddWithValue("@e", email); cmd.Parameters.AddWithValue("@p", hash); cmd.Parameters.AddWithValue("@r", role);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<AdminRow?> GetAdminByEmailAsync(string email)
    {
        await using var c=Conn(); await c.OpenAsync();
        await using var cmd=new SqlCommand("SELECT TOP 1 AdminId,FullName,Email,PasswordHash,Role,IsActive FROM dbo.AdminUsers WHERE Email=@e",c); cmd.Parameters.AddWithValue("@e",email);
        await using var r=await cmd.ExecuteReaderAsync(); if(!await r.ReadAsync()) return null;
        return new((Guid)r[0],r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetBoolean(5));
    }
    public async Task SetAdminLastLoginAsync(Guid id){ await ExecAsync("UPDATE dbo.AdminUsers SET LastLoginAtUtc=SYSUTCDATETIME() WHERE AdminId=@id",("@id",id)); }

    public async Task<object> GetDashboardAsync()
    {
        await using var c=Conn(); await c.OpenAsync();
        var sql=@"SELECT
COUNT(*) TotalOutlets,
SUM(CASE WHEN IsBlocked=0 AND ValidUntilUtc>=CAST(SYSUTCDATETIME() AS date) THEN 1 ELSE 0 END) ActiveLicenses,
SUM(CASE WHEN IsBlocked=0 AND ValidUntilUtc>=CAST(SYSUTCDATETIME() AS date) AND ValidUntilUtc<DATEADD(day,11,SYSUTCDATETIME()) THEN 1 ELSE 0 END) ExpiringSoon,
SUM(CASE WHEN IsBlocked=1 OR ValidUntilUtc<DATEADD(day,-3,CAST(SYSUTCDATETIME() AS date)) THEN 1 ELSE 0 END) BlockedExpired
FROM dbo.Outlets;
SELECT COUNT(*) FROM dbo.Devices WHERE IsBlocked=0;";
        await using var cmd=new SqlCommand(sql,c); await using var r=await cmd.ExecuteReaderAsync();
        int total=0,active=0,soon=0,bad=0,devices=0;
        if(await r.ReadAsync()){total=I(r,0);active=I(r,1);soon=I(r,2);bad=I(r,3);} await r.NextResultAsync(); if(await r.ReadAsync()) devices=I(r,0);
        return new { totalOutlets=total, activeLicenses=active, expiringSoon=soon, blockedExpired=bad, activeDevices=devices };
    }

    public async Task<List<object>> GetOutletsAsync()
    {
        var list=new List<object>(); await using var c=Conn(); await c.OpenAsync();
        var sql=@"SELECT o.OutletId,o.OutletCode,o.LicenseCode,o.OutletName,o.Address,o.Mobile,o.GstNo,o.StoreType,o.ValidFromUtc,o.ValidUntilUtc,o.IsBlocked,o.LicenseVersion,
(SELECT TOP 1 d.DeviceName FROM dbo.Devices d WHERE d.OutletId=o.OutletId ORDER BY d.BoundAtUtc DESC) DeviceName
FROM dbo.Outlets o ORDER BY o.CreatedAtUtc DESC";
        await using var cmd=new SqlCommand(sql,c); await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync()) list.Add(new { outletId=r.GetGuid(0),outletCode=r.GetString(1),licenseCode=r.GetString(2),outletName=r.GetString(3),address=N(r,4),mobile=N(r,5),gstNo=N(r,6),storeType=r.GetString(7),validFromUtc=r.GetDateTime(8),validUntilUtc=r.GetDateTime(9),isBlocked=r.GetBoolean(10),licenseVersion=r.GetInt32(11),deviceName=N(r,12),status=Status(r.GetBoolean(10),r.GetDateTime(9)) });
        return list;
    }

    public async Task<CreateOutletResult> CreateOutletAsync(CreateOutletRequest r, string activationHash, Guid? adminId)
    {
        await using var c=Conn(); await c.OpenAsync(); await using var tx=await c.BeginTransactionAsync();
        try
        {
            long n; await using(var seq=new SqlCommand("SELECT NEXT VALUE FOR dbo.OutletNumberSequence",c,(SqlTransaction)tx)){n=Convert.ToInt64(await seq.ExecuteScalarAsync());}
            var id=Guid.NewGuid(); var code=$"OUT-{n:000000}"; var lic=$"LIC-{DateTime.UtcNow:yy}-{n:000000}"; var until=DateTime.UtcNow.Date.AddDays(r.ValidityDays).AddSeconds(-1);
            var sql=@"INSERT dbo.Outlets(OutletId,OutletCode,LicenseCode,OutletName,Address,Mobile,GstNo,StoreType,ValidFromUtc,ValidUntilUtc,ActivationCodeHash,CreatedByAdminId)
VALUES(@id,@code,@lic,@name,@addr,@mobile,@gst,@type,SYSUTCDATETIME(),@until,@act,@admin)";
            await using var cmd=new SqlCommand(sql,c,(SqlTransaction)tx); P(cmd,"@id",id);P(cmd,"@code",code);P(cmd,"@lic",lic);P(cmd,"@name",r.OutletName.Trim());P(cmd,"@addr",r.Address);P(cmd,"@mobile",r.Mobile);P(cmd,"@gst",r.GstNo);P(cmd,"@type",r.StoreType.Trim());P(cmd,"@until",until);P(cmd,"@act",activationHash);P(cmd,"@admin",adminId);
            await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync();
            return new(id,code,lic,r.OutletName.Trim(),r.StoreType.Trim(),until);
        } catch { await tx.RollbackAsync(); throw; }
    }

    public async Task<bool> UpdateOutletAsync(Guid id, UpdateOutletRequest r, Guid? adminId)
    {
        await using var c=Conn(); await c.OpenAsync(); await using var tx=await c.BeginTransactionAsync();
        var read=new SqlCommand("SELECT StoreType,ValidUntilUtc,LicenseVersion FROM dbo.Outlets WITH (UPDLOCK,ROWLOCK) WHERE OutletId=@id",c,(SqlTransaction)tx);P(read,"@id",id);
        string oldType; DateTime validUntil; int oldVersion;
        await using(var rr=await read.ExecuteReaderAsync())
        {
            if(!await rr.ReadAsync()){await tx.RollbackAsync();return false;}
            oldType=rr.GetString(0); validUntil=rr.GetDateTime(1); oldVersion=rr.GetInt32(2);
        }
        var typeChanged=!string.Equals(oldType,r.StoreType,StringComparison.OrdinalIgnoreCase);
        var newVersion=typeChanged?oldVersion+1:oldVersion;
        var sql=@"UPDATE dbo.Outlets SET OutletName=@name,Address=@addr,Mobile=@mobile,GstNo=@gst,
StoreType=@type,LicenseVersion=@ver,UpdatedAtUtc=SYSUTCDATETIME() WHERE OutletId=@id";
        await using var cmd=new SqlCommand(sql,c,(SqlTransaction)tx);P(cmd,"@name",r.OutletName);P(cmd,"@addr",r.Address);P(cmd,"@mobile",r.Mobile);P(cmd,"@gst",r.GstNo);P(cmd,"@type",r.StoreType);P(cmd,"@ver",newVersion);P(cmd,"@id",id);
        await cmd.ExecuteNonQueryAsync();
        if(typeChanged)
        {
            var hist=new SqlCommand("INSERT dbo.LicenseHistory(OutletId,OldValidUntilUtc,NewValidUntilUtc,OldStoreType,NewStoreType,Action,AdminId,TokenVersion) VALUES(@id,@vu,@vu,@old,@new,'STORE_TYPE_CHANGE',@a,@v)",c,(SqlTransaction)tx);
            P(hist,"@id",id);P(hist,"@vu",validUntil);P(hist,"@old",oldType);P(hist,"@new",r.StoreType);P(hist,"@a",adminId);P(hist,"@v",newVersion);await hist.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync(); return true;
    }

    public async Task<RenewResult?> RenewAsync(Guid id,int days,Guid? admin)
    {
        await using var c=Conn(); await c.OpenAsync(); await using var tx=await c.BeginTransactionAsync();
        var oldCmd=new SqlCommand("SELECT ValidUntilUtc,StoreType,LicenseVersion FROM dbo.Outlets WITH (UPDLOCK,ROWLOCK) WHERE OutletId=@id",c,(SqlTransaction)tx);P(oldCmd,"@id",id);
        DateTime old; string type; int ver;
        await using(var rr=await oldCmd.ExecuteReaderAsync()){if(!await rr.ReadAsync()){await tx.RollbackAsync();return null;}old=rr.GetDateTime(0);type=rr.GetString(1);ver=rr.GetInt32(2);}
        var now=DateTime.UtcNow; var nu=old>now?old.AddDays(days):now.Date.AddDays(days).AddSeconds(-1); var newVer=ver+1;
        var up=new SqlCommand("UPDATE dbo.Outlets SET ValidUntilUtc=@nu,LicenseVersion=@v,LastRenewedAtUtc=SYSUTCDATETIME(),UpdatedAtUtc=SYSUTCDATETIME() WHERE OutletId=@id",c,(SqlTransaction)tx);P(up,"@nu",nu);P(up,"@v",newVer);P(up,"@id",id);await up.ExecuteNonQueryAsync();
        var hist=new SqlCommand("INSERT dbo.LicenseHistory(OutletId,OldValidUntilUtc,NewValidUntilUtc,OldStoreType,NewStoreType,Action,AdminId,TokenVersion) VALUES(@id,@old,@nu,@t,@t,'RENEW',@a,@v)",c,(SqlTransaction)tx);P(hist,"@id",id);P(hist,"@old",old);P(hist,"@nu",nu);P(hist,"@t",type);P(hist,"@a",admin);P(hist,"@v",newVer);await hist.ExecuteNonQueryAsync();
        await tx.CommitAsync(); return new(nu,newVer);
    }

    public Task<bool> SetOutletBlockedAsync(Guid id,bool blocked)=>ExecBoolAsync("UPDATE dbo.Outlets SET IsBlocked=@b,LicenseVersion=LicenseVersion+1,UpdatedAtUtc=SYSUTCDATETIME() WHERE OutletId=@id",("@b",blocked),("@id",id));
    public Task<bool> ResetActivationAsync(Guid id,string hash)=>ExecBoolAsync("UPDATE dbo.Outlets SET ActivationCodeHash=@h,UpdatedAtUtc=SYSUTCDATETIME() WHERE OutletId=@id",("@h",hash),("@id",id));
    public Task<bool> SetDeviceBlockedAsync(Guid id,bool blocked)=>ExecBoolAsync("UPDATE dbo.Devices SET IsBlocked=@b WHERE DeviceId=@id",("@b",blocked),("@id",id));
    public Task<bool> UnbindDeviceAsync(Guid id)=>ExecBoolAsync("DELETE dbo.Devices WHERE DeviceId=@id",("@id",id));
    public Task TouchDeviceAsync(Guid id)=>ExecAsync("UPDATE dbo.Devices SET LastSeenAtUtc=SYSUTCDATETIME() WHERE DeviceId=@id",("@id",id));

    public Task<bool> UpdatePosProfileAsync(Guid id,string name,string? address,string? mobile,string? gstNo)
        => ExecBoolAsync("UPDATE dbo.Outlets SET OutletName=@n,Address=@a,Mobile=@m,GstNo=@g,UpdatedAtUtc=SYSUTCDATETIME() WHERE OutletId=@id",("@n",name),("@a",address),("@m",mobile),("@g",gstNo),("@id",id));

    public async Task<List<object>> GetDevicesAsync()
    {
        var list=new List<object>();await using var c=Conn();await c.OpenAsync();var sql=@"SELECT d.DeviceId,d.OutletId,o.OutletCode,o.OutletName,d.DeviceName,d.DeviceFingerprint,d.IsBlocked,d.BoundAtUtc,d.LastSeenAtUtc FROM dbo.Devices d JOIN dbo.Outlets o ON o.OutletId=d.OutletId ORDER BY d.BoundAtUtc DESC";await using var cmd=new SqlCommand(sql,c);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(new{deviceId=r.GetGuid(0),outletId=r.GetGuid(1),outletCode=r.GetString(2),outletName=r.GetString(3),deviceName=N(r,4),fingerprint=MaskFingerprint(r.GetString(5)),isBlocked=r.GetBoolean(6),boundAtUtc=r.GetDateTime(7),lastSeenAtUtc=r.IsDBNull(8)?(DateTime?)null:r.GetDateTime(8)});return list;
    }
    public async Task<List<object>> GetAuditAsync(int take)
    {
        var list=new List<object>();await using var c=Conn();await c.OpenAsync();var sql=@"SELECT TOP (@take) a.AuditId,a.Action,a.EntityType,a.EntityId,a.Details,a.IpAddress,a.CreatedAtUtc,u.FullName FROM dbo.AuditLogs a LEFT JOIN dbo.AdminUsers u ON u.AdminId=a.AdminId ORDER BY a.AuditId DESC";await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@take",take);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(new{auditId=r.GetInt64(0),action=r.GetString(1),entityType=N(r,2),entityId=N(r,3),details=N(r,4),ipAddress=N(r,5),createdAtUtc=r.GetDateTime(6),adminName=N(r,7)??"System/POS"});return list;
    }
    public async Task<List<object>> GetAdminsAsync(){var list=new List<object>();await using var c=Conn();await c.OpenAsync();await using var cmd=new SqlCommand("SELECT AdminId,FullName,Email,Role,IsActive,CreatedAtUtc,LastLoginAtUtc FROM dbo.AdminUsers ORDER BY CreatedAtUtc",c);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(new{adminId=r.GetGuid(0),fullName=r.GetString(1),email=r.GetString(2),role=r.GetString(3),isActive=r.GetBoolean(4),createdAtUtc=r.GetDateTime(5),lastLoginAtUtc=r.IsDBNull(6)?(DateTime?)null:r.GetDateTime(6)});return list;}

    public async Task<ActivationOutlet?> GetOutletForActivationAsync(string code)
    {
        await using var c=Conn();await c.OpenAsync();await using var cmd=new SqlCommand("SELECT OutletId,OutletCode,LicenseCode,OutletName,StoreType,ValidFromUtc,ValidUntilUtc,IsBlocked,LicenseVersion,ActivationCodeHash FROM dbo.Outlets WHERE OutletCode=@c",c);P(cmd,"@c",code);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;return new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetDateTime(5),r.GetDateTime(6),r.GetBoolean(7),r.GetInt32(8),r.GetString(9));
    }
    public async Task<DeviceOutlet?> GetOutletForDeviceAsync(string code,string fp)
    {
        await using var c=Conn();await c.OpenAsync();var sql=@"SELECT o.OutletId,o.OutletCode,o.LicenseCode,o.OutletName,o.StoreType,o.ValidFromUtc,o.ValidUntilUtc,o.IsBlocked,o.LicenseVersion,o.ActivationCodeHash,d.DeviceId,d.IsBlocked FROM dbo.Outlets o JOIN dbo.Devices d ON d.OutletId=o.OutletId WHERE o.OutletCode=@c AND d.DeviceFingerprint=@f";await using var cmd=new SqlCommand(sql,c);P(cmd,"@c",code);P(cmd,"@f",fp);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;return new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetDateTime(5),r.GetDateTime(6),r.GetBoolean(7),r.GetInt32(8),r.GetString(9),r.GetGuid(10),r.GetBoolean(11));
    }
    public async Task<BindResult> BindDeviceAsync(Guid outletId,string fp,string? name)
    {
        await using var c=Conn();await c.OpenAsync();
        var exists=new SqlCommand("SELECT TOP 1 DeviceId,DeviceFingerprint,IsBlocked FROM dbo.Devices WHERE OutletId=@id",c);P(exists,"@id",outletId);await using(var r=await exists.ExecuteReaderAsync()){if(await r.ReadAsync()){var same=FixedEquals(r.GetString(1),fp);var blocked=r.GetBoolean(2);if(same&&!blocked)return new(true,"Already bound");return new(false,"Outlet is already bound to another device. Unbind it from Central Admin first.");}}
        try{var cmd=new SqlCommand("INSERT dbo.Devices(DeviceId,OutletId,DeviceFingerprint,DeviceName,LastSeenAtUtc) VALUES(@d,@o,@f,@n,SYSUTCDATETIME())",c);P(cmd,"@d",Guid.NewGuid());P(cmd,"@o",outletId);P(cmd,"@f",fp);P(cmd,"@n",name);await cmd.ExecuteNonQueryAsync();return new(true,"Bound");}catch(SqlException ex) when(ex.Number is 2601 or 2627){return new(false,"Outlet is already bound to another device. Unbind it from Central Admin first.");}
    }

    public async Task AuditAsync(Guid? admin,string action,string? entityType,string? entityId,string? details,HttpContext ctx)
    {
        try{await using var c=Conn();await c.OpenAsync();var cmd=new SqlCommand("INSERT dbo.AuditLogs(AdminId,Action,EntityType,EntityId,Details,IpAddress,UserAgent) VALUES(@a,@ac,@et,@ei,@d,@ip,@ua)",c);P(cmd,"@a",admin);P(cmd,"@ac",action);P(cmd,"@et",entityType);P(cmd,"@ei",entityId);P(cmd,"@d",details);P(cmd,"@ip",ctx.Connection.RemoteIpAddress?.ToString());P(cmd,"@ua",ctx.Request.Headers["User-Agent"].ToString());await cmd.ExecuteNonQueryAsync();}catch{}
    }

    public async Task<bool> PingAsync(){await using var c=Conn();await c.OpenAsync();await using var cmd=new SqlCommand("SELECT 1",c);return Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;}

    private async Task ExecAsync(string sql,params (string,object?)[] ps){await using var c=Conn();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);foreach(var p in ps)P(cmd,p.Item1,p.Item2);await cmd.ExecuteNonQueryAsync();}
    private async Task<bool> ExecBoolAsync(string sql,params (string,object?)[] ps){await using var c=Conn();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);foreach(var p in ps)P(cmd,p.Item1,p.Item2);return await cmd.ExecuteNonQueryAsync()>0;}
    private static void P(SqlCommand c,string n,object? v)=>c.Parameters.AddWithValue(n,v??DBNull.Value);
    private static string? N(SqlDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i);
    private static int I(SqlDataReader r,int i)=>r.IsDBNull(i)?0:Convert.ToInt32(r.GetValue(i));
    private static string Status(bool blocked,DateTime until)
    {
        if(blocked) return "Blocked";
        var days = DateOnly.FromDateTime(until).DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
        if(days < -3) return "Expired";
        if(days < 0) return "Grace";
        return days <= 10 ? "Expiring" : "Active";
    }
    private static string MaskFingerprint(string x)=>x.Length<=12?x:$"{x[..8]}…{x[^4..]}";
    private static bool FixedEquals(string a,string b)=>CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a),Encoding.UTF8.GetBytes(b));
}

record AdminRow(Guid AdminId,string FullName,string Email,string PasswordHash,string Role,bool IsActive);
record CreateOutletResult(Guid OutletId,string OutletCode,string LicenseCode,string OutletName,string StoreType,DateTime ValidUntilUtc);
record RenewResult(DateTime ValidUntilUtc,int LicenseVersion);
record ActivationOutlet(Guid OutletId,string OutletCode,string LicenseCode,string OutletName,string StoreType,DateTime ValidFromUtc,DateTime ValidUntilUtc,bool IsBlocked,int LicenseVersion,string ActivationCodeHash);
record DeviceOutlet(Guid OutletId,string OutletCode,string LicenseCode,string OutletName,string StoreType,DateTime ValidFromUtc,DateTime ValidUntilUtc,bool IsBlocked,int LicenseVersion,string ActivationCodeHash,Guid DeviceId,bool DeviceBlocked)
    : ActivationOutlet(OutletId,OutletCode,LicenseCode,OutletName,StoreType,ValidFromUtc,ValidUntilUtc,IsBlocked,LicenseVersion,ActivationCodeHash);
record BindResult(bool Success,string Message);

static class PasswordHasher
{
    const int Iterations=210000;
    public static bool IsStrong(string? p)=>!string.IsNullOrWhiteSpace(p)&&p.Length>=10;
    public static string Hash(string password){var salt=RandomNumberGenerator.GetBytes(16);var hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,Iterations,HashAlgorithmName.SHA256,32);return $"PBKDF2-SHA256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";}
    public static bool Verify(string password,string encoded){try{var p=encoded.Split('$');if(p.Length!=4)return false;var iter=int.Parse(p[1]);var salt=Convert.FromBase64String(p[2]);var expected=Convert.FromBase64String(p[3]);var actual=Rfc2898DeriveBytes.Pbkdf2(password,salt,iter,HashAlgorithmName.SHA256,expected.Length);return CryptographicOperations.FixedTimeEquals(actual,expected);}catch{return false;}}
}

static class ActivationCode
{
    public static string NewCode(){const string chars="ABCDEFGHJKLMNPQRSTUVWXYZ23456789";Span<byte>b=stackalloc byte[12];RandomNumberGenerator.Fill(b);var s=new char[12];for(int i=0;i<s.Length;i++)s[i]=chars[b[i]%chars.Length];return $"SUV-{new string(s,0,4)}-{new string(s,4,4)}-{new string(s,8,4)}";}
    public static string Hash(string code)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(N(code))));
    public static bool Verify(string code,string hash)=>CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(code)),Encoding.ASCII.GetBytes(hash.ToUpperInvariant()));
    static string N(string x)=>x.Trim().ToUpperInvariant();
}

sealed class LicenseSigner
{
    private readonly IConfiguration _cfg; private readonly string _priv; private readonly string _pub; public string KeyId {get;}
    public LicenseSigner(IConfiguration cfg)
    {
        _cfg=cfg; KeyId=cfg["License:KeyId"]??"main-v1";
        _priv=Path.GetFullPath(cfg["License:PrivateKeyPath"]??"App_Data/license-private.pem",AppContext.BaseDirectory);
        _pub=Path.GetFullPath(cfg["License:PublicKeyPath"]??"App_Data/license-public.pem",AppContext.BaseDirectory);
        EnsureKeys();
    }
    void EnsureKeys(){if(File.Exists(_priv)&&File.Exists(_pub))return;Directory.CreateDirectory(Path.GetDirectoryName(_priv)!);using var rsa=RSA.Create(3072);File.WriteAllText(_priv,rsa.ExportPkcs8PrivateKeyPem());File.WriteAllText(_pub,rsa.ExportSubjectPublicKeyInfoPem());}
    public string GetPublicKeyPem()=>File.ReadAllText(_pub);
    public string Issue(ActivationOutlet o,string fingerprint)
    {
        var now=DateTime.UtcNow; var header=new{alg="PS256",typ="SLT",kid=KeyId};
        var fromUtc=DateTime.SpecifyKind(o.ValidFromUtc,DateTimeKind.Utc);
        var untilUtc=DateTime.SpecifyKind(o.ValidUntilUtc,DateTimeKind.Utc);
        var payload=new{
            iss=_cfg["License:Issuer"]??"SuvidhaPremium",
            licenseId=o.LicenseCode,
            outletId=o.OutletId,
            outletCode=o.OutletCode,
            outletName=o.OutletName,
            storeType=o.StoreType,
            deviceHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))),
            issuedAtUtc=now.ToString("O"),
            validFromUtc=fromUtc.ToString("O"),
            validUntilUtc=untilUtc.ToString("O"),
            status="Active",
            plan="Premium",
            graceDays=Math.Clamp(_cfg.GetValue<int?>("Renewal:GraceDays")??3,0,7),
            renewalUrl=(_cfg["Renewal:PublicBaseUrl"]??"http://suvidhapremium.suvidhapos.in").TrimEnd('/')+"/renew/"+Uri.EscapeDataString(o.OutletCode),
            tokenVersion=o.LicenseVersion,
            nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(12))
        };
        var h=B64(JsonSerializer.SerializeToUtf8Bytes(header));var p=B64(JsonSerializer.SerializeToUtf8Bytes(payload));var input=Encoding.ASCII.GetBytes(h+"."+p);using var rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(_priv));var sig=rsa.SignData(input,HashAlgorithmName.SHA256,RSASignaturePadding.Pss);return h+"."+p+"."+B64(sig);
    }
    static string B64(byte[] b)=>Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');
}
