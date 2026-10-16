using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

class Program
{
    static Dictionary<char, string> phone = new Dictionary<char, string>
    {
        { '2', "abc" },
        { '3', "def" },
        { '4', "ghi" },
        { '5', "jkl" },
        { '6', "mno" },
        { '7', "pqrs" },
        { '8', "tuv" },
        { '9', "wxyz" }
    };

    static List<string> LetterCombinations(string digits)
    {
        var result = new List<string>();
        if (digits == "")
            return result;

        void Build(int index, string current)
        {
            if (index == digits.Length)
            {
                result.Add(current);
                return;
            }

            string letters = phone[digits[index]];

            foreach (char letter in letters)
            {
                Build(index + 1, current + letter);
            }
                    
        }

        Build(0, "");
        return result;
    }

    static void Main()
    {
        string digits = "246";
        var combos = LetterCombinations(digits);
        Console.WriteLine(string.Join(", ", combos));
    }
}