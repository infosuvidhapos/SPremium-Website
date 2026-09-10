using System.Text.Json;
using System.Text.RegularExpressions;

public static class SupportSettingsModule
{
    const string DefaultSupportWhatsApp = "918271718844";
    static readonly SemaphoreSlim Gate = new(1, 1);
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/admin/settings/support", async () =>
            Results.Ok(new { supportWhatsApp = await ReadSupportNumberAsync() }))
            .RequireAuthorization();

        app.MapPut("/api/admin/settings/support", async (SupportSettingsRequest request) =>
        {
            var number = NormalizeNumber(request.SupportWhatsApp);
            if (number is null)
                return Results.BadRequest(new { message = "Enter a valid WhatsApp number with country code, e.g. 918271718844." });

            await WriteSupportNumberAsync(number);
            return Results.Ok(new { supportWhatsApp = number, message = "Support WhatsApp number updated." });
        }).RequireAuthorization("ManageOutlet");

        app.MapGet("/api/pos/support", async () =>
            Results.Ok(new { supportWhatsApp = await ReadSupportNumberAsync() }));

        app.MapGet("/support/whatsapp", async (string? outletCode) =>
        {
            var number = await ReadSupportNumberAsync();
            var code = NormalizeOutletCode(outletCode);
            var message = $"Hello SuvidhaPremium, I want to renew billing subscription for Outlet Code {code}.";
            var url = $"https://wa.me/{number}?text={Uri.EscapeDataString(message)}";
            return Results.Redirect(url);
        });
    }

    static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "App_Data", "support-settings.json");

    static async Task<string> ReadSupportNumberAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (!File.Exists(SettingsPath)) return DefaultSupportWhatsApp;
            var json = await File.ReadAllTextAsync(SettingsPath);
            var data = JsonSerializer.Deserialize<SupportSettingsFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return NormalizeNumber(data?.SupportWhatsApp) ?? DefaultSupportWhatsApp;
        }
        catch
        {
            return DefaultSupportWhatsApp;
        }
        finally
        {
            Gate.Release();
        }
    }

    static async Task WriteSupportNumberAsync(string number)
    {
        await Gate.WaitAsync();
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            var temp = SettingsPath + ".tmp";
            var json = JsonSerializer.Serialize(new SupportSettingsFile(number, DateTime.UtcNow), JsonOptions);
            await File.WriteAllTextAsync(temp, json);
            File.Move(temp, SettingsPath, true);
        }
        finally
        {
            Gate.Release();
        }
    }

    static string? NormalizeNumber(string? value)
    {
        var digits = Regex.Replace(value ?? "", "[^0-9]", "");
        if (digits.Length == 10) digits = "91" + digits;
        return digits.Length is >= 11 and <= 15 ? digits : null;
    }

    static string NormalizeOutletCode(string? value)
    {
        var code = Regex.Replace((value ?? "").Trim().ToUpperInvariant(), "[^A-Z0-9-]", "");
        return string.IsNullOrWhiteSpace(code) ? "UNKNOWN" : code;
    }

    public sealed record SupportSettingsRequest(string? SupportWhatsApp);
    sealed record SupportSettingsFile(string SupportWhatsApp, DateTime UpdatedAtUtc = default);
}
