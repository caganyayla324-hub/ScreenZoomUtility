using System.Windows.Input;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Engines.Keyboard;
using ScreenZoomUtility.Models;
using Xunit;

namespace ScreenZoomUtility.Tests;

public class HotkeyAndModelTests
{
    [Fact]
    public void DetectConflicts_FindsDuplicateEnabledShortcutsAndIgnoresDisabled()
    {
        using var manager = new HotkeyManager();
        var bindings = new List<HotkeyBinding>
        {
            new() { Id = "a", ActionName = "Action A", Ctrl = true, Alt = true, VirtualKey = 0x5A, KeyDisplay = "Z", Enabled = true },
            new() { Id = "b", ActionName = "Action B", Ctrl = true, Alt = true, VirtualKey = 0x5A, KeyDisplay = "Z", Enabled = true },
            new() { Id = "c", ActionName = "Action C", Ctrl = true, Alt = true, VirtualKey = 0x5A, KeyDisplay = "Z", Enabled = false },
            new() { Id = "d", ActionName = "Unassigned", VirtualKey = 0, Enabled = true }
        };

        var conflicts = manager.DetectConflicts(bindings);

        Assert.Single(conflicts);
        Assert.Contains("Action A", conflicts[0]);
        Assert.Contains("Action B", conflicts[0]);
        Assert.DoesNotContain("Action C", conflicts[0]);
    }

    [Fact]
    public void KeyboardSequenceClone_CreatesIndependentDeepCopy()
    {
        var orig = new KeyboardSequence
        {
            Name = "Orig",
            Steps = [new SequenceStep { Label = "S1", VirtualKey = 0x43, KeyDisplay = "C", DelayAfterMs = 120 }]
        };

        var copy = orig.Clone("Copy");
        copy.Steps[0].DelayAfterMs = 500;

        Assert.NotEqual(orig.Id, copy.Id);
        Assert.Equal(120, orig.Steps[0].DelayAfterMs);
        Assert.Equal(500, copy.Steps[0].DelayAfterMs);
    }

    [Fact]
    public void KeyMapper_IdentifiesModifiersAndFormatsKeys()
    {
        Assert.True(KeyMapper.IsModifierKey(Key.LeftCtrl));
        Assert.True(KeyMapper.IsModifierKey(Key.RightShift));
        Assert.False(KeyMapper.IsModifierKey(Key.Z));
        Assert.Equal("0", KeyMapper.ToDisplayName(Key.D0));
        Assert.Equal("Esc", KeyMapper.ToDisplayName(Key.Escape));
    }
}
