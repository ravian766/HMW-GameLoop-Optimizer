namespace GameLoopOptimizer.Models;

public class DeviceProfile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string DevicePhoneString { get; set; } = string.Empty;
    public int MaxSupportedFps { get; set; } = 120;
    public string Description { get; set; } = string.Empty;

    public static readonly List<DeviceProfile> Profiles = new()
    {
        new DeviceProfile
        {
            Id = "tabs9ultra",
            DisplayName = "Samsung Galaxy Tab S9 Ultra (SM-X910 - Official 120 FPS)",
            Manufacturer = "samsung",
            Model = "SM-X910",
            DevicePhoneString = "Samsung Tab S9 Ultra",
            MaxSupportedFps = 120,
            Description = "Officially certified 120 FPS ultra-high framerate device string for PUBG Mobile 3.x+."
        },
        new DeviceProfile
        {
            Id = "rog6",
            DisplayName = "Asus ROG Phone 6 Pro (120 FPS High Refresh)",
            Manufacturer = "asus",
            Model = "ASUS_AI2201_D",
            DevicePhoneString = "ROG Phone 6 Pro",
            MaxSupportedFps = 120,
            Description = "Premier 120 FPS high-refresh whitelist device for PUBG Mobile 3.x+."
        },
        new DeviceProfile
        {
            Id = "rog2",
            DisplayName = "Asus ROG Phone 2 (Legacy 90/120 FPS)",
            Manufacturer = "Asus",
            Model = "ASUS_I001DA",
            DevicePhoneString = "Asus ROG 2",
            MaxSupportedFps = 120,
            Description = "Most widely compatible 90/120 FPS profile for PUBG Mobile."
        },
        new DeviceProfile
        {
            Id = "redmagic9",
            DisplayName = "Nubia Red Magic 9 Pro (120 FPS Low Latency)",
            Manufacturer = "Nubia",
            Model = "NX769J",
            DevicePhoneString = "Nubia NX769J",
            MaxSupportedFps = 120,
            Description = "Optimized for high-refresh Vulkan/DirectX render dispatch."
        },
        new DeviceProfile
        {
            Id = "s24ultra",
            DisplayName = "Samsung Galaxy S24 Ultra (HDR Extreme)",
            Manufacturer = "Samsung",
            Model = "SM-S928B",
            DevicePhoneString = "Samsung S24 Ultra",
            MaxSupportedFps = 120,
            Description = "Enables Ultra HDR / Extreme graphic fidelity rendering."
        },
        new DeviceProfile
        {
            Id = "blackshark5",
            DisplayName = "Xiaomi Black Shark 5 Pro (High Refresh)",
            Manufacturer = "Xiaomi",
            Model = "SHARK KTUS-H0",
            DevicePhoneString = "Black Shark 5 Pro",
            MaxSupportedFps = 120,
            Description = "High-responsiveness gaming fingerprint."
        },
        new DeviceProfile
        {
            Id = "ipadpro",
            DisplayName = "Apple iPad Pro 11 (Tablet 4:3 Wide FOV)",
            Manufacturer = "Apple",
            Model = "iPad13,4",
            DevicePhoneString = "iPad Pro 11",
            MaxSupportedFps = 90,
            Description = "Enables wide tablet perspective aspect ratio."
        }
    };

    /// <summary>
    /// Resolves or synthesizes a DeviceProfile matching the GameLoop Settings Center configuration.
    /// Ensures In-VM device properties match GameLoop's VMDeviceModel and VMDeviceManufacturer.
    /// </summary>
    public static DeviceProfile ResolveFromGameLoop(string model, string manufacturer = "")
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return Profiles.First();
        }

        string cleanModel = model.Trim();
        string cleanManuf = manufacturer.Trim();

        // 1. Exact match by Model or DevicePhoneString
        var matched = Profiles.FirstOrDefault(p =>
            p.Model.Equals(cleanModel, StringComparison.OrdinalIgnoreCase) ||
            p.DevicePhoneString.Equals(cleanModel, StringComparison.OrdinalIgnoreCase));

        if (matched != null)
        {
            return matched;
        }

        // 2. Partial match (e.g. S948B vs S928B, or ROG Phone 2 vs Asus ROG 2)
        var partial = Profiles.FirstOrDefault(p =>
            p.Model.Contains(cleanModel, StringComparison.OrdinalIgnoreCase) ||
            cleanModel.Contains(p.Model, StringComparison.OrdinalIgnoreCase) ||
            p.DevicePhoneString.Contains(cleanModel, StringComparison.OrdinalIgnoreCase) ||
            cleanModel.Contains(p.DevicePhoneString, StringComparison.OrdinalIgnoreCase));

        if (partial != null)
        {
            return new DeviceProfile
            {
                Id = partial.Id,
                DisplayName = $"{partial.DisplayName} ({cleanModel})",
                Manufacturer = !string.IsNullOrWhiteSpace(cleanManuf) ? cleanManuf : partial.Manufacturer,
                Model = cleanModel,
                DevicePhoneString = partial.DevicePhoneString,
                MaxSupportedFps = partial.MaxSupportedFps,
                Description = partial.Description
            };
        }

        // 3. Dynamic profile matching the exact custom/preset model configured in GameLoop settings
        string brand = !string.IsNullOrWhiteSpace(cleanManuf) ? cleanManuf : "Samsung";
        return new DeviceProfile
        {
            Id = $"gameloop_{cleanModel.ToLowerInvariant().Replace(' ', '_')}",
            DisplayName = $"{brand} {cleanModel} (GameLoop Settings)",
            Manufacturer = brand,
            Model = cleanModel,
            DevicePhoneString = $"{brand} {cleanModel}",
            MaxSupportedFps = 120,
            Description = "Synchronized directly from GameLoop Settings Center."
        };
    }
}
