using System;
using System.Drawing;

namespace ClickLight.Windows
{
    internal static class TestHarness
    {
        private static int passed;
        private static int failed;

        public static int Main()
        {
            Run("settings defaults match spec", SettingsDefaultsMatchSpec);
            Run("settings sanitize clamps documented ranges", SettingsSanitizeClampsRanges);
            Run("settings clone is independent", SettingsCloneIsIndependent);
            Run("color preset metadata matches spec", ColorPresetMetadataMatchesSpec);
            Run("default kind colors match spec", DefaultKindColorsMatchSpec);
            Run("custom color is sanitized and applied", CustomColorIsSanitizedAndApplied);
            Run("numeric presets match spec", NumericPresetsMatchSpec);
            Run("click pulse progress clamps and expires", ClickPulseProgressClampsAndExpires);
            Run("laser cursor fade matches spec", LaserCursorFadeMatchesSpec);
            Run("laser stroke append and fade match spec", LaserStrokeAppendAndFadeMatchSpec);
            Run("startup command quotes unpackaged executable", StartupCommandQuotesUnpackagedExecutable);

            Console.WriteLine();
            Console.WriteLine("Passed: " + passed);
            Console.WriteLine("Failed: " + failed);

            return failed == 0 ? 0 : 1;
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                passed++;
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine("[FAIL] " + name);
                Console.WriteLine("       " + ex.Message);
            }
        }

        private static void SettingsDefaultsMatchSpec()
        {
            ClickSettings settings = ClickSettings.Defaults();
            AssertTrue(settings.isEnabled, "isEnabled");
            AssertTrue(settings.showPress, "showPress");
            AssertTrue(settings.showRelease, "showRelease");
            AssertTrue(settings.showRightClick, "showRightClick");
            AssertTrue(settings.showDrag, "showDrag");
            AssertFalse(settings.showLaserPointer, "showLaserPointer");
            AssertFalse(settings.showMenuBarText, "showMenuBarText");
            AssertClose(64.0, settings.size, "size");
            AssertClose(0.7, settings.intensity, "intensity");
            AssertClose(0.48, settings.duration, "duration");
            AssertEqual("default", settings.colorPreset, "colorPreset");
            AssertClose(0.0, settings.customColorRed, "customColorRed");
            AssertClose(0.74, settings.customColorGreen, "customColorGreen");
            AssertClose(1.0, settings.customColorBlue, "customColorBlue");
        }

        private static void SettingsSanitizeClampsRanges()
        {
            ClickSettings settings = ClickSettings.Defaults();
            settings.colorPreset = "unknown";
            settings.size = 999.0;
            settings.intensity = -1.0;
            settings.duration = Double.NaN;
            settings.customColorRed = -10.0;
            settings.customColorGreen = Double.PositiveInfinity;
            settings.customColorBlue = 3.0;

            settings.Sanitize();

            AssertEqual("default", settings.colorPreset, "colorPreset");
            AssertClose(240.0, settings.size, "size upper clamp");
            AssertClose(0.05, settings.intensity, "intensity lower clamp");
            AssertClose(0.48, settings.duration, "duration fallback");
            AssertClose(0.0, settings.customColorRed, "red clamp");
            AssertClose(0.0, settings.customColorGreen, "green fallback");
            AssertClose(1.0, settings.customColorBlue, "blue clamp");

            settings.size = 1.0;
            settings.intensity = 4.0;
            settings.duration = 0.01;
            settings.Sanitize();

            AssertClose(16.0, settings.size, "size lower clamp");
            AssertClose(2.0, settings.intensity, "intensity upper clamp");
            AssertClose(0.1, settings.duration, "duration lower clamp");
        }

        private static void SettingsCloneIsIndependent()
        {
            ClickSettings original = ClickSettings.Defaults();
            ClickSettings clone = original.Clone();
            clone.size = 116.0;
            clone.showLaserPointer = true;

            AssertClose(64.0, original.size, "original size");
            AssertFalse(original.showLaserPointer, "original laser");
        }

        private static void ColorPresetMetadataMatchesSpec()
        {
            AssertEqual(8, ClickColorPreset.All.Length, "preset count");
            AssertTrue(ClickColorPreset.IsKnown("default"), "default known");
            AssertTrue(ClickColorPreset.IsKnown("custom"), "custom known");
            AssertTrue(ClickColorPreset.IsKnown("blue"), "blue known");
            AssertTrue(ClickColorPreset.IsKnown("green"), "green known");
            AssertTrue(ClickColorPreset.IsKnown("purple"), "purple known");
            AssertTrue(ClickColorPreset.IsKnown("pink"), "pink known");
            AssertTrue(ClickColorPreset.IsKnown("orange"), "orange known");
            AssertTrue(ClickColorPreset.IsKnown("white"), "white known");
            AssertFalse(ClickColorPreset.IsKnown("yellow"), "unknown preset");
            AssertEqual("Default", ClickColorPreset.Title("default"), "default title");
            AssertEqual("Custom", ClickColorPreset.Title("custom"), "custom title");
        }

        private static void DefaultKindColorsMatchSpec()
        {
            ClickSettings settings = ClickSettings.Defaults();
            AssertColor(Color.FromArgb(255, 0, 189, 255), ClickColorPreset.ColorForKind(settings, ClickKind.LeftDown), "leftDown");
            AssertColor(Color.FromArgb(255, 102, 224, 255), ClickColorPreset.ColorForKind(settings, ClickKind.LeftUp), "leftUp");
            AssertColor(Color.FromArgb(255, 255, 117, 48), ClickColorPreset.ColorForKind(settings, ClickKind.RightDown), "rightDown");
            AssertColor(Color.FromArgb(255, 255, 117, 48), ClickColorPreset.ColorForKind(settings, ClickKind.RightUp), "rightUp");
            AssertColor(Color.FromArgb(255, 235, 214, 56), ClickColorPreset.ColorForKind(settings, ClickKind.Drag), "drag");
            AssertColor(Color.Transparent, ClickColorPreset.ColorForKind(settings, ClickKind.Move), "move");
        }

        private static void CustomColorIsSanitizedAndApplied()
        {
            ClickSettings settings = ClickSettings.Defaults();
            settings.colorPreset = "custom";
            settings.customColorRed = 2.0;
            settings.customColorGreen = 0.5;
            settings.customColorBlue = -1.0;
            settings.Sanitize();

            AssertColor(Color.FromArgb(255, 255, 128, 0), ClickColorPreset.ColorForKind(settings, ClickKind.LeftDown), "custom color");
        }

        private static void NumericPresetsMatchSpec()
        {
            AssertEqual(4, ClickSettingOptions.SizePresets.Length, "size preset count");
            AssertEqual("Small", ClickSettingOptions.SizePresets[0].Title, "size small title");
            AssertClose(44.0, ClickSettingOptions.SizePresets[0].Value, "size small value");
            AssertEqual("Medium", ClickSettingOptions.SizePresets[1].Title, "size medium title");
            AssertClose(64.0, ClickSettingOptions.SizePresets[1].Value, "size medium value");
            AssertEqual("Huge", ClickSettingOptions.SizePresets[3].Title, "size huge title");
            AssertClose(116.0, ClickSettingOptions.SizePresets[3].Value, "size huge value");

            AssertEqual("Subtle", ClickSettingOptions.IntensityPresets[0].Title, "intensity subtle title");
            AssertClose(0.28, ClickSettingOptions.IntensityPresets[0].Value, "intensity subtle value");
            AssertEqual("Beacon", ClickSettingOptions.IntensityPresets[3].Title, "intensity beacon title");
            AssertClose(1.35, ClickSettingOptions.IntensityPresets[3].Value, "intensity beacon value");

            AssertEqual("Snappy", ClickSettingOptions.DurationPresets[0].Title, "duration snappy title");
            AssertClose(0.28, ClickSettingOptions.DurationPresets[0].Value, "duration snappy value");
            AssertEqual("Very Slow", ClickSettingOptions.DurationPresets[3].Title, "duration very slow title");
            AssertClose(1.0, ClickSettingOptions.DurationPresets[3].Value, "duration very slow value");
        }

        private static void ClickPulseProgressClampsAndExpires()
        {
            ClickPulse pulse = new ClickPulse(ClickKind.LeftDown, new PointF(10.0f, 20.0f), 5.0, 0.5, 64.0, 0.7, Color.White);
            AssertClose(0.0, pulse.Progress(4.0), "progress before start");
            AssertClose(0.5, pulse.Progress(5.25), "progress halfway");
            AssertClose(1.0, pulse.Progress(5.5), "progress at end");
            AssertTrue(pulse.IsExpired(5.5), "expired at end");
        }

        private static void LaserCursorFadeMatchesSpec()
        {
            LaserCursor cursor = new LaserCursor(new PointF(1.0f, 2.0f), 10.0);
            AssertClose(1.0, cursor.Alpha(10.0), "alpha start");
            AssertClose(0.5, cursor.Alpha(10.21), "alpha half");
            AssertClose(0.0, cursor.Alpha(10.42), "alpha end");
            AssertFalse(cursor.IsExpired(10.41), "not expired before fade");
            AssertTrue(cursor.IsExpired(10.43), "expired after fade duration");
        }

        private static void LaserStrokeAppendAndFadeMatchSpec()
        {
            LaserStroke stroke = new LaserStroke();
            stroke.AddPoint(new PointF(0.0f, 0.0f));

            AssertFalse(stroke.ShouldAppend(new PointF(2.0f, 0.0f)), "do not append below 2.5px");
            AssertTrue(stroke.ShouldAppend(new PointF(2.5f, 0.0f)), "append at 2.5px");
            AssertClose(1.0, stroke.Alpha(10.0), "active alpha");
            AssertFalse(stroke.IsExpired(10.0), "active not expired");

            stroke.CompletedAt = 20.0;
            AssertClose(1.0, stroke.Alpha(20.0), "completed alpha start");
            AssertClose(0.5, stroke.Alpha(20.45), "completed alpha half");
            AssertClose(0.0, stroke.Alpha(20.9), "completed alpha end");
            AssertTrue(stroke.IsExpired(20.91), "completed expired");
        }

        private static void StartupCommandQuotesUnpackagedExecutable()
        {
            string command = LaunchAtLoginController.BuildRegistryStartupCommand(@"C:\Tools\ClickLight\ClickLight.exe");
            AssertEqual(@"""C:\Tools\ClickLight\ClickLight.exe""", command, "unpackaged startup command");
        }

        private static void AssertTrue(bool value, string message)
        {
            if (!value) throw new Exception(message + " should be true");
        }

        private static void AssertFalse(bool value, string message)
        {
            if (value) throw new Exception(message + " should be false");
        }

        private static void AssertEqual(string expected, string actual, string message)
        {
            if (!String.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new Exception(message + ": expected '" + expected + "', got '" + actual + "'");
            }
        }

        private static void AssertEqual(int expected, int actual, string message)
        {
            if (expected != actual)
            {
                throw new Exception(message + ": expected " + expected + ", got " + actual);
            }
        }

        private static void AssertClose(double expected, double actual, string message)
        {
            if (Math.Abs(expected - actual) > 0.0001)
            {
                throw new Exception(message + ": expected " + expected + ", got " + actual);
            }
        }

        private static void AssertColor(Color expected, Color actual, string message)
        {
            if (expected.ToArgb() != actual.ToArgb())
            {
                throw new Exception(message + ": expected " + expected + ", got " + actual);
            }
        }
    }
}
