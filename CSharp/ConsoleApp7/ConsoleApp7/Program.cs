//Return array methods
using static System.Net.Mime.MediaTypeNames;

int target = 30;
int[] coins = new int[] { 5, 5, 50, 25, 25, 10, 5 };
int[,] result = TwoCoins(coins, target);
if (result.Length == 0)
{
    Console.WriteLine("No two coins make change");
}
else
{
    Console.WriteLine("Change found at positions:");
    for (int i = 0; i < result.GetLength(0); i++)
    {
        if (result[i, 0] == -1)
        {
            break;
        }
        Console.WriteLine($"{result[i, 0]},{result[i, 1]}");
    }
}
int[,] TwoCoins(int[] coins, int target)
{
    int[,] result = { { -1, -1 }, { -1, -1 }, { -1, -1 }, { -1, -1 }, { -1, -1 } };
    int count = 0;
    for (int i = 0; i < coins.Length; i++)
    {
        for (int j = i + 1; j < coins.Length; j++)
        {
            if (coins[i] + coins[j] == target)
            {
                result[count, 0] = i;
                result[count, 1] = j;
                count++;
            }

            if (count == result.GetLength(0))
            {
                return result;
            }
        }
    }

    return (count == 0) ? new int[0, 0] : result;
}

/*
//Return Booleans from methods
string[] words = { "racecar", "talented", "deified", "tent", "tenet" };

Console.WriteLine("Is it a palindrome?");
foreach (string word in words)
{
    Console.WriteLine($"{word}: {IsPalindrome(word)}");
}

bool IsPalindrome(string input)
{
    bool result = false;
    string reversed = "";
    for (int i = input.Length-1; i>=0; i--)
    {
        reversed += input[i];
    }
    if (input == reversed)
    {
        return true;
    }
    return result;
} */

/*
//Return strings from methods

string input = "there are snakes at the zoo";

Console.WriteLine(input);
//Console.WriteLine(ReverseWord(input));
Console.WriteLine(ReverseSentence(input));

string ReverseSentence(string input)
{
    string result = "";
    string[] words = input.Split(' ');
    foreach (string word in words)
    {
        result += ReverseWord(word) + " ";
    }
    return result;
}

string ReverseWord(string word)
{
    string result = "";
    for (int i = word.Length - 1; i >= 0; i--)
    {
        result += word[i];
    }

    return result;
} */


/*
//Return numbers from methods
double usd = 23.73;
int vnd = UsdToVnd(usd);

Console.WriteLine($"${usd} USD = ${vnd} VND");
Console.WriteLine($"${vnd} VND = ${VndToUsd(vnd)} USD");

int UsdToVnd(double usd)
{
    int rate = 23500;
    return (int)(rate * usd);
}
double VndToUsd(int vnd)
{
    double rate = 23500;
    return vnd / rate;

}*/
/*
// Understanding return type syntax
double total = 0;
double minimumSpend = 30.00;

double[] items = { 15.97, 3.50, 12.25, 22.99, 10.98 };
double[] discounts = { 0.30, 0.00, 0.10, 0.20, 0.50 };

for (int i = 0; i < items.Length; i++)
{
    total += GetDiscountedPrice(i);
}
if (TotalMeetsMinimum())
{
    total -= TotalMeetsMinimum() ? 5.00 : 0.00;
}
Console.WriteLine($"Total: ${FormatDecimal(total)}");

double GetDiscountedPrice(int itemIndex)
{
    // Calculate the discounted price of the item
    return items[itemIndex] * (1 - discounts[itemIndex]);
}

bool TotalMeetsMinimum()
{
    return total >= minimumSpend;
}

string FormatDecimal(double input)
{
    // Format the double so only 2 decimal places are displayed
    return input.ToString().Substring(0, 5);
}
*/