using System;
using System.Globalization;

namespace Calculator_WinUI.Classes
{
    // the evaluator from the WPF version, carried over unchanged
    //
    // it works on the raw input string: it finds the innermost bracket, collapses the calculations inside
    // it in precedence order, writes the result back over that part of the string, and repeats
    // that only works because v1 had no structured input; the token tree cannot be expressed as a flat
    // string, so this class is currently unused and will be replaced by an evaluator that walks the tree
    class Calculate
    {
        public string PreCalculate(string inputTextBlockText)
        {
            int tempIndex1 = 0;

            int numberOfBrackets = 0;
            int currentBracketRank = 0;
            int highestBracketRank;
            int highestBracketPosition = 0;

            int numberOfPowerootCalculations;
            int numberOfPointCalculations;
            int numberOfLineCalculations;

            // how many brackets have to be collapsed in total
            while (tempIndex1 < inputTextBlockText.Length - 1)
            {
                if (inputTextBlockText[tempIndex1] == '(')
                {
                    numberOfBrackets++;
                }
                tempIndex1++;
            }

            // one pass per bracket, innermost first
            while (numberOfBrackets > 0 && currentBracketRank == 0)
            {
                tempIndex1 = 0;
                currentBracketRank = 0;
                highestBracketRank = 0;

                numberOfPowerootCalculations = 0;
                numberOfPointCalculations = 0;
                numberOfLineCalculations = 0;

                // the deepest nesting level is the one that has to be evaluated next, so the rank counts
                // up on every opening bracket and down on every closing one
                while (tempIndex1 <= inputTextBlockText.Length - 1)
                {
                    if (inputTextBlockText[tempIndex1] == '(')
                    {
                        currentBracketRank++;

                        if (currentBracketRank > highestBracketRank)
                        {
                            highestBracketRank = currentBracketRank;
                            highestBracketPosition = tempIndex1;
                        }
                    }
                    if (inputTextBlockText[tempIndex1] == ')')
                    {
                        currentBracketRank--;
                    }
                    tempIndex1++;
                }
                tempIndex1 = highestBracketPosition + 1;

                // count the operators inside that bracket, grouped by precedence
                while (!(inputTextBlockText[tempIndex1] == ')'))
                {
                    if (inputTextBlockText[tempIndex1] == '^' || inputTextBlockText[tempIndex1] == '√')
                    {
                        numberOfPowerootCalculations++;
                    }
                    if (inputTextBlockText[tempIndex1] == '*' || inputTextBlockText[tempIndex1] == '/')
                    {
                        numberOfPointCalculations++;
                    }
                    if (inputTextBlockText[tempIndex1] == '+')
                    {
                        numberOfLineCalculations++;
                    }
                    // a minus only counts as an operator when it follows a digit; otherwise it is the
                    // sign of the number behind it
                    if (tempIndex1 >= 1)
                    {
                        if ((inputTextBlockText[tempIndex1 - 1] == '0' || inputTextBlockText[tempIndex1 - 1] == '1' || inputTextBlockText[tempIndex1 - 1] == '2' || inputTextBlockText[tempIndex1 - 1] == '3' || inputTextBlockText[tempIndex1 - 1] == '4' || inputTextBlockText[tempIndex1 - 1] == '5' || inputTextBlockText[tempIndex1 - 1] == '6' || inputTextBlockText[tempIndex1 - 1] == '7' || inputTextBlockText[tempIndex1 - 1] == '8' || inputTextBlockText[tempIndex1 - 1] == '9') && inputTextBlockText[tempIndex1] == '-')
                        {
                            numberOfLineCalculations++;
                        }
                    }
                    tempIndex1++;
                }
                if (!((numberOfPowerootCalculations == 0 && numberOfPointCalculations == 0 && numberOfLineCalculations == 0) || inputTextBlockText == "0"))
                {
                    // strict precedence order; each call collapses its own operator class and hands the
                    // shortened string to the next one
                    inputTextBlockText = SaveNum1AndNum2(inputTextBlockText, highestBracketPosition, numberOfPowerootCalculations, numberOfPointCalculations, numberOfLineCalculations, '^', '√');

                    inputTextBlockText = SaveNum1AndNum2(inputTextBlockText, highestBracketPosition, numberOfPowerootCalculations, numberOfPointCalculations, numberOfLineCalculations, '*', '/');

                    inputTextBlockText = SaveNum1AndNum2(inputTextBlockText, highestBracketPosition, numberOfPowerootCalculations, numberOfPointCalculations, numberOfLineCalculations, '+', '-');
                }
                else return "Error";

                numberOfBrackets--;
            }
            return inputTextBlockText;
        }

        // collapses every occurrence of one operator pair inside the current bracket, left to right
        //
        // it reads the two operands out of the string around the operator, computes them, and splices the
        // result back in over the whole term, so the string gets shorter with every calculation
        static string SaveNum1AndNum2(string inputTextBlockText, int highestBracketPosition, int numberOfPowerootCalculations, int numberOfPointCalculations, int numberOfLineCalculations, char operationType1, char operationType2)
        {
            int numberOfCalculations = 0;
            int currentnumberOfCalculations = 0;
            int currentOperation = -1;

            int startingPointNumber1 = 0;
            int startingPointNumber2 = 0;
            int endPointNumber1 = 0;

            bool firstNumberSaved = false;
            bool secondNumberSaved = false;

            string tempNumberString = "";
            double[] currentNumbersForCalculation = new double[2];

            int tempIndex1 = highestBracketPosition + 1;

            // the caller counted all three classes up front, pick the one this call is responsible for
            if (operationType1 == '^') numberOfCalculations = numberOfPowerootCalculations;
            if (operationType1 == '*') numberOfCalculations = numberOfPointCalculations;
            if (operationType1 == '+') numberOfCalculations = numberOfLineCalculations;

            try
            {
                while (currentnumberOfCalculations < numberOfCalculations)
                {
                    // walk right until the next operator of this class shows up
                    while (startingPointNumber2 == 0)
                    {
                        if (inputTextBlockText[tempIndex1] == operationType1 || inputTextBlockText[tempIndex1] == operationType2)
                        {
                            // same rule as above: a minus after a bracket or another operator is a sign,
                            // not a subtraction
                            if (!(inputTextBlockText[tempIndex1] == operationType2 && operationType2 == '-' && (inputTextBlockText[tempIndex1 - 1] == '(' || inputTextBlockText[tempIndex1 - 1] == '+' || inputTextBlockText[tempIndex1 - 1] == '-' || inputTextBlockText[tempIndex1 - 1] == '*' || inputTextBlockText[tempIndex1 - 1] == '/')))
                            {
                                startingPointNumber2 = tempIndex1 + 1;
                                endPointNumber1 = tempIndex1 - 1;

                                if (inputTextBlockText[tempIndex1] == operationType1 && operationType1 == '*') currentOperation = 2;
                                if (inputTextBlockText[tempIndex1] == operationType2 && operationType2 == '/') currentOperation = 3;

                                if (inputTextBlockText[tempIndex1] == operationType1 && operationType1 == '+') currentOperation = 0;
                                if (inputTextBlockText[tempIndex1] == operationType2 && operationType2 == '-') currentOperation = 1;

                                if (inputTextBlockText[tempIndex1] == operationType1 && operationType1 == '^') currentOperation = 4;
                                if (inputTextBlockText[tempIndex1] == operationType2 && operationType2 == '√') currentOperation = 5;
                            }
                            else tempIndex1++;
                        }
                        else tempIndex1++;
                    }

                    while (firstNumberSaved == false || secondNumberSaved == false)
                    {
                        // left operand: walk back from the operator until something that cannot be part
                        // of a number appears
                        while (firstNumberSaved == false)
                        {
                            tempIndex1--;

                            if (inputTextBlockText[tempIndex1] == '(' || inputTextBlockText[tempIndex1] == '+' || inputTextBlockText[tempIndex1] == '-' || inputTextBlockText[tempIndex1] == '*' || inputTextBlockText[tempIndex1] == '/')
                            {
                                // a minus here belongs to the number, so it is kept
                                if (inputTextBlockText[tempIndex1] == '-') startingPointNumber1 = tempIndex1;
                                else startingPointNumber1 = tempIndex1 + 1;

                                int IndexStartingPointNumber1 = startingPointNumber1;

                                while (IndexStartingPointNumber1 <= endPointNumber1)
                                {
                                    tempNumberString += inputTextBlockText[IndexStartingPointNumber1];
                                    IndexStartingPointNumber1++;
                                }

                                // invariant culture, so the "." in the input stays the decimal point
                                currentNumbersForCalculation[0] = double.Parse(tempNumberString, CultureInfo.InvariantCulture);

                                firstNumberSaved = true;
                                tempNumberString = "";
                            }
                        }
                        tempIndex1 = startingPointNumber2;

                        // right operand: same walk in the other direction
                        while (secondNumberSaved == false)
                        {
                            tempIndex1++;

                            if (inputTextBlockText[tempIndex1] == ')' || inputTextBlockText[tempIndex1] == '+' || inputTextBlockText[tempIndex1] == '-' || inputTextBlockText[tempIndex1] == '*' || inputTextBlockText[tempIndex1] == '/')
                            {
                                // endPointNumber1 is reused as the end of the second number
                                endPointNumber1 = tempIndex1 - 1;

                                while (startingPointNumber2 <= endPointNumber1)
                                {
                                    tempNumberString += inputTextBlockText[startingPointNumber2];
                                    startingPointNumber2++;
                                }

                                currentNumbersForCalculation[1] = double.Parse(tempNumberString, CultureInfo.InvariantCulture);

                                secondNumberSaved = true;

                                double resultDouble = 0;
                                int decimalpoints = 3;

                                if (currentOperation == 0)
                                {
                                    resultDouble = currentNumbersForCalculation[0] + currentNumbersForCalculation[1];
                                    resultDouble = Math.Round(resultDouble, decimalpoints);
                                }
                                if (currentOperation == 1)
                                {
                                    resultDouble = currentNumbersForCalculation[0] - currentNumbersForCalculation[1];
                                    resultDouble = Math.Round(resultDouble, decimalpoints);
                                }
                                if (currentOperation == 2)
                                {
                                    resultDouble = currentNumbersForCalculation[0] * currentNumbersForCalculation[1];
                                    resultDouble = Math.Round(resultDouble, decimalpoints);
                                }
                                if (currentOperation == 3)
                                {
                                    resultDouble = currentNumbersForCalculation[0] / currentNumbersForCalculation[1];
                                    resultDouble = Math.Round(resultDouble, decimalpoints);
                                }
                                if (currentOperation == 4)
                                {
                                    resultDouble = Math.Pow(currentNumbersForCalculation[0], currentNumbersForCalculation[1]);
                                    resultDouble = Math.Round(resultDouble, decimalpoints);
                                }
                                if (currentOperation == 5)
                                {
                                    // num1 √ num2 is the same as num2^(1/num1)
                                    resultDouble = Math.Pow(currentNumbersForCalculation[1], 1 / currentNumbersForCalculation[0]);
                                    resultDouble = Math.Round(resultDouble, decimalpoints);
                                }

                                string resultString = Convert.ToString(resultDouble);

                                // ToString follows the system culture, which writes a comma in most regions
                                resultString = resultString.Replace(",", ".");

                                int resultStringLengh = resultString.Length;

                                // the surrounding brackets are only dropped once nothing inside them is
                                // left to calculate, otherwise the next pass would lose its boundaries
                                if ((numberOfLineCalculations == 0 || (currentnumberOfCalculations == numberOfLineCalculations - 1 && operationType1 == '+')) && currentnumberOfCalculations == numberOfCalculations - 1)
                                {
                                    startingPointNumber1--;
                                    endPointNumber1++;
                                }

                                int removeLengh = endPointNumber1 - (startingPointNumber1 + (resultStringLengh - 1));

                                // overwrite the term with its result, then delete whatever the shorter
                                // result left behind
                                inputTextBlockText = inputTextBlockText.Remove(startingPointNumber1, resultStringLengh).Insert(startingPointNumber1, resultString);

                                startingPointNumber1 += resultStringLengh;

                                inputTextBlockText = inputTextBlockText.Remove(startingPointNumber1, removeLengh);
                            }
                        }
                    }

                    // back to the start of the bracket for the next operator of the same class
                    startingPointNumber1 = 0;
                    startingPointNumber2 = 0;
                    endPointNumber1 = 0;

                    firstNumberSaved = false;
                    secondNumberSaved = false;

                    tempIndex1 = highestBracketPosition + 1;
                    tempNumberString = "";

                    currentnumberOfCalculations++;
                }
                return inputTextBlockText;
            }
            catch (Exception)
            {
                // any malformed input walks an index off the string; there is no partial result worth
                // salvaging, so the whole calculation reports as failed
                return "Error";
            }
        }
    }
}
