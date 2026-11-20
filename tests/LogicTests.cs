using System;
using System.Collections.Generic;

namespace AutoShortcut.Tests
{
    // Logic tests: no windows, no keyboard. Run with tests\run-tests.ps1.
    // Each test is a method; Run() calls them all and returns the failure count.
    public static class LogicTests
    {
        private static int _passed;
        private static int _failed;

        public static int Run()
        {
            _passed = 0;
            _failed = 0;
            Test("KeyNames", KeyNames);

            Console.WriteLine(_passed + " passed, " + _failed + " failed");
            return _failed;
        }

        private static void Test(string name, Action body)
        {
            int before = _failed;
            try
            {
                body();
            }
            catch (Exception ex)
            {
                Fail(name, "threw " + ex.GetType().Name + ": " + ex.Message);
            }
            if (_failed == before)
            {
                _passed++;
                Console.WriteLine("PASS " + name);
            }
        }

        private static void Fail(string name, string detail)
        {
            _failed++;
            Console.WriteLine("FAIL " + name + ": " + detail);
        }

        // Records a failure (the test keeps running so all problems show at once).
        private static void Check(string name, bool ok, string detail)
        {
            if (!ok) Fail(name, detail);
        }

        private static void Equal(string name, object expected, object actual)
        {
            if (!object.Equals(expected, actual))
                Fail(name, "expected <" + expected + "> but got <" + actual + ">");
        }

        // ---- KeyCombo ----

        private static string Display(string text)
        {
            KeyCombo combo;
            if (!KeyCombo.TryParse(text, out combo)) return "(invalid)";
            return string.Join(",", combo.DisplayParts());
        }

        private static string Saved(string text)
        {
            KeyCombo combo;
            return KeyCombo.TryParse(text, out combo) ? combo.ToString() : "(invalid)";
        }

        private static void KeyNames()
        {
            Equal("Win+Plus display", "Win,+", Display("Win+Plus"));
            Equal("Win+Plus saved", "Win+Plus", Saved("win+plus"));
            Equal("Ctrl+NumPlus display", "Ctrl,Num +", Display("Ctrl+NumPlus"));
            Equal("Ctrl+NumPlus saved", "Ctrl+NumPlus", Saved("Ctrl+NumPlus"));
            Equal("Win+Minus display", "Win,-", Display("Win+Minus"));
            Equal("Win+Slash display", "Win,/", Display("Win+Slash"));
            Equal("Win+Slash saved", "Win+Slash", Saved("Win+Slash"));
            Equal("Win+Semicolon display", "Win,;", Display("Win+Semicolon"));
            Equal("Win+Semicolon saved", "Win+Semicolon", Saved("Win+Semicolon"));
            Equal("Win+Pause display", "Win,Pause", Display("Win+Pause"));
            Equal("Win+Comma saved", "Win+,", Saved("Win+Comma"));
            Equal("Win+Period saved", "Win+.", Saved("Win+Period"));
            Equal("Win+. still parses", "Win,.", Display("Win+."));
        }
    }
}
