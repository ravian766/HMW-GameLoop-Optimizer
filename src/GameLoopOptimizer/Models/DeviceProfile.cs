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
            Id = "rog2",
            DisplayName = "Asus ROG Phone 2 (Recommended 120 FPS)",
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
}
