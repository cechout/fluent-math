using System;
using System.Globalization;

namespace Calculator_WinUI.Classes
{
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

            //Detect how many Brackets there are
            while (tempIndex1 < inputTextBlockText.Length - 1)
            {
                if (inputTextBlockText[tempIndex1] == '(')
                {
                    numberOfBrackets++;
                }
                tempIndex1++;
            }

            //Calculating all brackets first
            while (numberOfBrackets > 0 && currentBracketRank == 0)
            {
                tempIndex1 = 0;
                currentBracketRank = 0;
                highestBracketRank = 0;

                numberOfPowerootCalculations = 0;
                numberOfPointCalculations = 0;
                numberOfLineCalculations = 0;

                //Detect highest Bracket
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

                while (!(inputTextBlockText[tempIndex1] == ')'))
                {
                    //Detect how many poweroot calculations are in the bracket
                    if (inputTextBlockText[tempIndex1] == '^' || inputTextBlockText[tempIndex1] == '√')
                    {
                        numberOfPowerootCalculations++;
                    }
                    //Detect how many point calculations are in the highest bracket
                    if (inputTextBlockText[tempIndex1] == '*' || inputTextBlockText[tempIndex1] == '/')
                    {
                        numberOfPointCalculations++;
                    }
                    //Detect how many line calculations are in the highest bracket
                    if (inputTextBlockText[tempIndex1] == '+')
                    {
                        numberOfLineCalculations++;
                    }
                    //check if the "-" sign belongs to the calcucaltion
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

                    //Calculate all poweroot calculations and get inputTextBlock.Text back
                    inputTextBlockText = SaveNum1AndNum2(inputTextBlockText, highestBracketPosition, numberOfPowerootCalculations, numberOfPointCalculations, numberOfLineCalculations, '^', '√');

                    //Calculate all point calculations and get inputTextBlock.Text back
                    inputTextBlockText = SaveNum1AndNum2(inputTextBlockText, highestBracketPosition, numberOfPowerootCalculations, numberOfPointCalculations, numberOfLineCalculations, '*', '/');

                    //Calculate all line calculations and get inputTextBlock.Text back
                    inputTextBlockText = SaveNum1AndNum2(inputTextBlockText, highestBracketPosition, numberOfPowerootCalculations, numberOfPointCalculations, numberOfLineCalculations, '+', '-');
                }
                else return "Error";

                numberOfBrackets--;
            }
            return inputTextBlockText;
        }

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

            if (operationType1 == '^') numberOfCalculations = numberOfPowerootCalculations;
            if (operationType1 == '*') numberOfCalculations = numberOfPointCalculations;
            if (operationType1 == '+') numberOfCalculations = numberOfLineCalculations;

            try
            {
                //detect and save the two numbers 
                while (currentnumberOfCalculations < numberOfCalculations)
                {
                    //detect the first point calculation in the bracket
                    while (startingPointNumber2 == 0)
                    {
                        if (inputTextBlockText[tempIndex1] == operationType1 || inputTextBlockText[tempIndex1] == operationType2)
                        {
                            //check if '-' belongs to calculation or to a presign of a number
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

                    //Save both numbers in currentNumbersForCalculation[]
                    while (firstNumberSaved == false || secondNumberSaved == false)
                    {
                        //save first number
                        while (firstNumberSaved == false)
                        {
                            tempIndex1--;

                            //detect start of the first number for Calculation
                            if (inputTextBlockText[tempIndex1] == '(' || inputTextBlockText[tempIndex1] == '+' || inputTextBlockText[tempIndex1] == '-' || inputTextBlockText[tempIndex1] == '*' || inputTextBlockText[tempIndex1] == '/')
                            {
                                if (inputTextBlockText[tempIndex1] == '-') startingPointNumber1 = tempIndex1;
                                else startingPointNumber1 = tempIndex1 + 1;

                                int IndexStartingPointNumber1 = startingPointNumber1;

                                //Save first number for multiplication/division
                                while (IndexStartingPointNumber1 <= endPointNumber1)
                                {
                                    tempNumberString += inputTextBlockText[IndexStartingPointNumber1];
                                    IndexStartingPointNumber1++;
                                }

                                //this method takes the "." as a decimal point
                                currentNumbersForCalculation[0] = double.Parse(tempNumberString, CultureInfo.InvariantCulture);

                                firstNumberSaved = true;
                                tempNumberString = "";
                            }
                        }
                        tempIndex1 = startingPointNumber2;

                        //save second number
                        while (secondNumberSaved == false)
                        {
                            tempIndex1++;

                            //detect end of the second number for pointCalculation/division
                            if (inputTextBlockText[tempIndex1] == ')' || inputTextBlockText[tempIndex1] == '+' || inputTextBlockText[tempIndex1] == '-' || inputTextBlockText[tempIndex1] == '*' || inputTextBlockText[tempIndex1] == '/')
                            {
                                ////endPointNumber1 is now the end point for number 2
                                endPointNumber1 = tempIndex1 - 1;

                                //Save second number for multiplication/division
                                while (startingPointNumber2 <= endPointNumber1)
                                {
                                    tempNumberString += inputTextBlockText[startingPointNumber2];
                                    startingPointNumber2++;
                                }

                                //this method takes the "." as a decimal point
                                currentNumbersForCalculation[1] = double.Parse(tempNumberString, CultureInfo.InvariantCulture);

                                secondNumberSaved = true;

                                //Calculate first number and second number
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
                                    //num1 √ num2 is the same as num2^1/num1
                                    resultDouble = Math.Pow(currentNumbersForCalculation[1], 1 / currentNumbersForCalculation[0]);
                                    resultDouble = Math.Round(resultDouble, decimalpoints);
                                }

                                string resultString = Convert.ToString(resultDouble);

                                //normally the decimal point would be "," so we replace it with "."
                                resultString = resultString.Replace(",", ".");

                                int resultStringLengh = resultString.Length;

                                //remove the brackets only if there is no line calculation or the last calculation was just calculated
                                if ((numberOfLineCalculations == 0 || (currentnumberOfCalculations == numberOfLineCalculations - 1 && operationType1 == '+')) && currentnumberOfCalculations == numberOfCalculations - 1)
                                {
                                    startingPointNumber1--;
                                    endPointNumber1++;
                                }

                                int removeLengh = endPointNumber1 - (startingPointNumber1 + (resultStringLengh - 1));

                                //putting the result into the inputTextBlock.Text
                                inputTextBlockText = inputTextBlockText.Remove(startingPointNumber1, resultStringLengh).Insert(startingPointNumber1, resultString);

                                //cutting the leftovers
                                startingPointNumber1 += resultStringLengh;

                                inputTextBlockText = inputTextBlockText.Remove(startingPointNumber1, removeLengh);
                            }
                        }
                    }

                    //resetting all the variables
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
                return "Error";
            }
        }
    }
}
