using System.IO;
using System.Text.Json;

namespace GameLoopOptimizer.Core;

public enum ControlInputType
{
    Button,
    CrossKey,
    MouseLClick,
    MouseRClick,
    MultiPoint
}

public class KeyMappingControl
{
    public string Action { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int AsciiCode { get; set; }
    public ControlInputType InputType { get; set; } = ControlInputType.Button;
    public ControlAnchorType Anchor { get; set; } = ControlAnchorType.Right;
    public double ReferenceX { get; set; }
    public double ReferenceY { get; set; }
    public double Offset { get; set; } // for WASD CrossKey radius
    public bool Enabled { get; set; } = true;
    public double SensitivityX { get; set; } = 1.0;
    public double SensitivityY { get; set; } = 1.0;
}

public class KeyMappingProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ReferenceWidth { get; set; } = 1920;
    public int ReferenceHeight { get; set; } = 1080;
    public bool IsBuiltIn { get; set; }
    public List<KeyMappingControl> Controls { get; set; } = new();
}

public static class MappingProfileManager
{
    private static readonly string ProfilesDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static MappingProfileManager()
    {
        ProfilesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLoopOptimizer", "profiles", "PUBG");
        try
        {
            if (!Directory.Exists(ProfilesDirectory))
            {
                Directory.CreateDirectory(ProfilesDirectory);
            }
        }
        catch { }
    }

    public static List<KeyMappingProfile> GetBuiltInProfiles()
    {
        return new List<KeyMappingProfile>
        {
            CreateClassicMouseKeyboardProfile(),
            CreateCompetitiveStretchedProfile(),
            CreateFastPeekerProfile()
        };
    }

    public static List<KeyMappingProfile> GetAllProfiles()
    {
        var profiles = new List<KeyMappingProfile>(GetBuiltInProfiles());

        if (Directory.Exists(ProfilesDirectory))
        {
            foreach (var file in Directory.GetFiles(ProfilesDirectory, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var p = JsonSerializer.Deserialize<KeyMappingProfile>(json);
                    if (p != null)
                    {
                        p.IsBuiltIn = false;
                        profiles.Add(p);
                    }
                }
                catch { }
            }
        }

        return profiles;
    }

    public static async Task<bool> SaveProfileAsync(KeyMappingProfile profile)
    {
        try
        {
            if (!Directory.Exists(ProfilesDirectory))
            {
                Directory.CreateDirectory(ProfilesDirectory);
            }

            profile.IsBuiltIn = false;
            string fileName = $"profile_{profile.Id}.json";
            string path = Path.Combine(ProfilesDirectory, fileName);
            string json = JsonSerializer.Serialize(profile, JsonOptions);
            await File.WriteAllTextAsync(path, json);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("MappingProfileManager", $"Failed saving profile '{profile.Name}': {ex.Message}");
            return false;
        }
    }

    private static KeyMappingProfile CreateClassicMouseKeyboardProfile()
    {
        return new KeyMappingProfile
        {
            Id = "classic_mk",
            Name = "Mouse + Keyboard Classic",
            Description = "Standard 1080p HUD layout matching default PUBG Mobile control layout.",
            IsBuiltIn = true,
            Controls = new List<KeyMappingControl>
            {
                new() { Action = "Move", Key = "WASD", AsciiCode = 87, InputType = ControlInputType.CrossKey, Anchor = ControlAnchorType.Left, ReferenceX = 0.158594, ReferenceY = 0.751389, Offset = 0.087805 },
                new() { Action = "Fire", Key = "LClick", AsciiCode = 1, InputType = ControlInputType.MouseLClick, Anchor = ControlAnchorType.Right, ReferenceX = 0.853906, ReferenceY = 0.743056 },
                new() { Action = "Scope", Key = "RClick", AsciiCode = 2, InputType = ControlInputType.MouseRClick, Anchor = ControlAnchorType.Right, ReferenceX = 0.792188, ReferenceY = 0.412500 },
                new() { Action = "Jump", Key = "Space", AsciiCode = 32, InputType = ControlInputType.Button, Anchor = ControlAnchorType.Right, ReferenceX = 0.958594, ReferenceY = 0.706944 },
                new() { Action = "Crouch", Key = "C", AsciiCode = 67, InputType = ControlInputType.Button, Anchor = ControlAnchorType.Right, ReferenceX = 0.846094, ReferenceY = 0.929167 },
                new() { Action = "Prone", Key = "Z", AsciiCode = 90, InputType = ControlInputType.Button, Anchor = ControlAnchorType.Right, ReferenceX = 0.944531, ReferenceY = 0.890278 },
                new() { Action = "Reload", Key = "R", AsciiCode = 82, InputType = ControlInputType.Button, Anchor = ControlAnchorType.Right, ReferenceX = 0.765625, ReferenceY = 0.938889 },
                new() { Action = "Interact", Key = "F", AsciiCode = 70, InputType = ControlInputType.Button, Anchor = ControlAnchorType.Right, ReferenceX = 0.653906, ReferenceY = 0.337500 },
                new() { Action = "PeekLeft", Key = "Q", AsciiCode = 81, InputType = ControlInputType.Button, Anchor = ControlAnchorType.Right, ReferenceX = 0.675781, ReferenceY = 0.680556 },
                new() { Action = "PeekRight", Key = "E", AsciiCode = 69, InputType = ControlInputType.Button, Anchor = ControlAnchorType.Right, ReferenceX = 0.959375, ReferenceY = 0.527778 },
                new() { Action = "Backpack", Key = "Tab", AsciiCode = 9, InputType = ControlInputType.Button, Anchor = ControlAnchorType.Left, ReferenceX = 0.071875, ReferenceY = 0.912500 },
                new() { Action = "Map", Key = "M", AsciiCode = 77, InputType = ControlInputType.Button, Anchor = ControlAnchorType.TopRight, ReferenceX = 0.976563, ReferenceY = 0.043056 },
                new() { Action = "Weapon1", Key = "1", AsciiCode = 49, InputType = ControlInputType.Button, Anchor = ControlAnchorType.BottomCenter, ReferenceX = 0.429688, ReferenceY = 0.902778 },
                new() { Action = "Weapon2", Key = "2", AsciiCode = 50, InputType = ControlInputType.Button, Anchor = ControlAnchorType.BottomCenter, ReferenceX = 0.551562, ReferenceY = 0.913889 },
                new() { Action = "Grenade", Key = "5", AsciiCode = 53, InputType = ControlInputType.Button, Anchor = ControlAnchorType.BottomCenter, ReferenceX = 0.681250, ReferenceY = 0.936111 },
                new() { Action = "Heal", Key = "9", AsciiCode = 57, InputType = ControlInputType.Button, Anchor = ControlAnchorType.Left, ReferenceX = 0.304688, ReferenceY = 0.938889 }
            }
        };
    }

    private static KeyMappingProfile CreateCompetitiveStretchedProfile()
    {
        var p = CreateClassicMouseKeyboardProfile();
        p.Id = "comp_stretched";
        p.Name = "Competitive Stretched (Optimized Spacing)";
        p.Description = "Calibrated for 4:3 (1440x1080) and 16:10 (1728x1080) competitive stretched viewports.";
        return p;
    }

    private static KeyMappingProfile CreateFastPeekerProfile()
    {
        var p = CreateClassicMouseKeyboardProfile();
        p.Id = "fast_peek";
        p.Name = "Fast Peeker & Flick ADS";
        p.Description = "Prioritizes rapid peek leaning (Q/E) and instant ADS thumb reach.";
        return p;
    }
}
