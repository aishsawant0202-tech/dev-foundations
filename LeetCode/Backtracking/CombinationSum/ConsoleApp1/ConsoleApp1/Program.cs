using System;
using System.Collections.Generic;

class Program
{
    static List<List<int>> Combination_Sum(List<int> candidates, int target)
    {
        List<List<int>> result = [];
        List<int> path = [];

        void backtrack(int remaining, int start)
        {
            if (remaining == 0)
            {
                result.Add(new List<int>(path));
                return;
            }

            if (remaining < 0)
                return;

            for (int i = start; i < candidates.Count; i++)
            {
                path.Add(candidates[i]);
                backtrack(remaining - candidates[i], start = i);
                path.RemoveAt(path.Count - 1);
            }
        }
                
        backtrack(target, 0);
        return result;
    }

    static void Main()
    {
        List<int> candidates = new List<int> { 2, 3, 6, 7 };
        int target = 7;
        List<List<int>> result = Combination_Sum(candidates, target);
        foreach (var combo in result)
        {
            Console.WriteLine("[" + string.Join(", ", combo) + "]");
        }

    }
}