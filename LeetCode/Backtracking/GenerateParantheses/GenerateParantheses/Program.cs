using System;
using System.Collections.Generic;

class Program
{
    static List<string> Generate_parantheses(int n)
    {
        var result = new List<string>();

        void Backtrack(string current, int open_count, int close_count)
        {
            if (open_count ==n && close_count == n)
            {
                result.Add(current);
                return;
            }

            if (open_count < n)
            {
                Backtrack(current + "(", open_count + 1, close_count);
            }

            if (close_count < open_count)
            {
                Backtrack(current + ")", open_count, close_count + 1);
            }

        }

        Backtrack("", 0, 0);
        return result;

    }
    static void Main()
    {
        Console.WriteLine(string.Join(", ", Generate_parantheses(3)));
    }
}
