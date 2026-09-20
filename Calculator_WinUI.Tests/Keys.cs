using System;
using Calculator_WinUI.Engines;
using Calculator_WinUI.Models;

namespace Calculator_WinUI.Tests
{
    // drives MathInputManager the way the keypad does, so a test can be written as the run of keys that
    // produces the formula instead of as a hand-built token tree
    //
    // anything that is not a known key name goes in character by character as digits, which is what makes
    // "125" one argument instead of three
    internal static class Keys
    {
        public static MathInputManager Press(params string[] keys)
        {
            var manager = new MathInputManager();
            foreach (string key in keys)
            {
                Send(manager, key);
            }

            return manager;
        }

        private static void Send(MathInputManager manager, string key)
        {
            switch (key)
            {
                case "+":
                case "-":
                case "*":
                case "/":
                    manager.AddOperator(key);
                    return;

                case "(":
                    manager.AddBracket(open: true);
                    return;

                case ")":
                    manager.AddBracket(open: false);
                    return;

                case "!":
                case "inv":
                case "%":
                    manager.AddPostfix(key);
                    return;

                case "pi":
                case "e":
                    manager.AddConstant(key);
                    return;

                case "ans":
                    manager.AddAns();
                    return;

                case "frac":
                    manager.StartFraction();
                    return;

                case "pow":
                    manager.StartPower();
                    return;

                case "pow10":
                    manager.StartPowerOfTen();
                    return;

                case "powe":
                    manager.StartPowerOfE();
                    return;

                case "sqrt":
                    manager.StartRoot(customIndex: false);
                    return;

                case "root":
                    manager.StartRoot(customIndex: true);
                    return;

                case "log":
                    manager.StartLogarithm(customBase: false);
                    return;

                case "logb":
                    manager.StartLogarithm(customBase: true);
                    return;

                case "exp":
                    manager.StartScientific();
                    return;

                case "left":
                    manager.Move(NavDirection.Left);
                    return;

                case "right":
                    manager.Move(NavDirection.Right);
                    return;

                case "up":
                    manager.Move(NavDirection.Up);
                    return;

                case "down":
                    manager.Move(NavDirection.Down);
                    return;

                case "back":
                    manager.Backspace();
                    return;
            }

            if (key.StartsWith("fn:", StringComparison.Ordinal))
            {
                manager.StartFunction(key.Substring(3));
                return;
            }

            foreach (char digit in key)
            {
                manager.AddNumber(digit.ToString());
            }
        }
    }
}
