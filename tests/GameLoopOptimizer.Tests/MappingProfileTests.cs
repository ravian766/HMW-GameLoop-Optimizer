using System.Text.Json;
using GameLoopOptimizer.Core;
using Xunit;

namespace GameLoopOptimizer.Tests;

public class MappingProfileTests
{
    [Fact]
    public void GetBuiltInProfiles_ReturnsThreeDefaultProfiles()
    {
        var profiles = MappingProfileManager.GetBuiltInProfiles();

        Assert.Equal(3, profiles.Count);
        Assert.Contains(profiles, p => p.Id == "classic_mk");
        Assert.Contains(profiles, p => p.Id == "comp_stretched");
        Assert.Contains(profiles, p => p.Id == "fast_peek");
    }

    [Fact]
    public void ClassicProfile_ContainsEssentialPubgControls()
    {
        var classic = MappingProfileManager.GetBuiltInProfiles().First(p => p.Id == "classic_mk");

        Assert.True(classic.IsBuiltIn);
        Assert.Equal(1920, classic.ReferenceWidth);
        Assert.Equal(1080, classic.ReferenceHeight);
        Assert.NotEmpty(classic.Controls);

        // Verify key gameplay controls exist
        var wasd = classic.Controls.FirstOrDefault(c => c.Action == "Move");
        Assert.NotNull(wasd);
        Assert.Equal(ControlInputType.CrossKey, wasd.InputType);
        Assert.Equal(ControlAnchorType.Left, wasd.Anchor);
        Assert.True(wasd.Offset > 0);

        var fire = classic.Controls.FirstOrDefault(c => c.Action == "Fire");
        Assert.NotNull(fire);
        Assert.Equal(ControlInputType.MouseLClick, fire.InputType);

        var scope = classic.Controls.FirstOrDefault(c => c.Action == "Scope");
        Assert.NotNull(scope);
        Assert.Equal(ControlInputType.MouseRClick, scope.InputType);
        Assert.Equal(ControlAnchorType.Right, scope.Anchor);

        var peekLeft = classic.Controls.FirstOrDefault(c => c.Action == "PeekLeft");
        var peekRight = classic.Controls.FirstOrDefault(c => c.Action == "PeekRight");
        Assert.NotNull(peekLeft);
        Assert.NotNull(peekRight);
    }

    [Fact]
    public void KeyMappingProfile_JsonSerialization_RoundTripsAccurately()
    {
        var original = new KeyMappingProfile
        {
            Id = "custom_test_profile",
            Name = "Test Custom Profile",
            Description = "A test profile for serialization",
            ReferenceWidth = 1440,
            ReferenceHeight = 1080,
            IsBuiltIn = false,
            Controls = new List<KeyMappingControl>
            {
                new()
                {
                    Action = "Jump",
                    Key = "Space",
                    AsciiCode = 32,
                    InputType = ControlInputType.Button,
                    Anchor = ControlAnchorType.Right,
                    ReferenceX = 0.95,
                    ReferenceY = 0.70,
                    SensitivityX = 1.2,
                    SensitivityY = 1.2
                }
            }
        };

        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<KeyMappingProfile>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.ReferenceWidth, deserialized.ReferenceWidth);
        Assert.Single(deserialized.Controls);

        var ctrl = deserialized.Controls[0];
        Assert.Equal("Jump", ctrl.Action);
        Assert.Equal("Space", ctrl.Key);
        Assert.Equal(32, ctrl.AsciiCode);
        Assert.Equal(ControlAnchorType.Right, ctrl.Anchor);
        Assert.Equal(0.95, ctrl.ReferenceX);
        Assert.Equal(0.70, ctrl.ReferenceY);
        Assert.Equal(1.2, ctrl.SensitivityX);
    }
}
