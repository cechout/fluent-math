using System;
using FluentMath.Engines;
using FluentMath.Models;

namespace FluentMath.Tests
{
    // drives MathInputManager the way the keypad does, so a test can be written as the run of keys that
    // produces the formula instead of as a hand-built token tree
    //
    // anything that is not a known key name goes in character by character as digits, which is what makes
    // "125" one argument instead of three
    internal static class Keys
    {
        // exactly the names Send knows, for the fuzz to draw from
        //
        // deliberately not Vocabulary.Commands: that is the keypads vocabulary, and feeding a cmd_ name
        // in here would fall through to the digit branch and spell it out letter by letter
        public static readonly string[] All =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", ".",
            "+", "-", "*", "/", "(", ")",
            "!", "inv", "%", "pi", "e", "ans",
            "frac", "pow", "powe", "sqrt", "root", "log", "logb", "exp",
            "left", "right", "up", "down", "back",
            "fn:sin", "fn:cos", "fn:tan", "fn:arcsin", "fn:arccos", "fn:arctan",
            "fn:sinh", "fn:cosh", "fn:tanh", "fn:arsinh", "fn:arcosh", "fn:artanh",
            "fn:ln", "fn:abs",
            "fn:sec", "fn:csc", "fn:cot", "fn:arcsec", "fn:arccsc", "fn:arccot",
            "fn:sech", "fn:csch", "fn:coth", "fn:arsech", "fn:arcsch", "fn:arcoth",
            "fn:floor", "fn:ceil", "fn:int", "fn:intg",
            "fn:gcd", "fn:lcm", "fn:ranint", "fn:rndfix", "fn:rnd",
            "npr", "ncr", "rand", "pre:kilo", "pre:micro",
            "mixed", "divr", "fn:pol", "fn:rec", "dms"
        };

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

                case "rand":
                    manager.AddRandom();
                    return;

                case "npr":
                    manager.AddOperator("P");
                    return;

                case "ncr":
                    manager.AddOperator("C");
                    return;

                case "divr":
                    manager.AddOperator("÷R");
                    return;

                // the °′″ key, which picks the marker itself
                case "dms":
                    manager.AddSexagesimalMarker();
                    return;

                case "frac":
                    manager.StartFraction();
                    return;

                case "mixed":
                    manager.StartMixedFraction();
                    return;

                case "pow":
                    manager.StartPower();
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

            // a decimal prefix by the name of the prefix, pre:kilo
            if (key.StartsWith("pre:", StringComparison.Ordinal))
            {
                manager.AddPostfix(key.Substring(4));
                return;
            }

            foreach (char digit in key)
            {
                manager.AddNumber(digit.ToString());
            }
        }
    }
}
