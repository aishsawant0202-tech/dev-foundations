using System;
using System.Collections.Generic;
using System.ComponentModel;

class Program
{
    static List<List<int>> Permute(int[] nums)
    {
        List<List<int>> result = [];
        List<int> path = [];
        List<bool> used = new List<bool>(new bool[nums.Length]);

        void Backtrack()
        {
            if (path.Count == nums.Length)
            {
                result.Add(new List<int>(path));
                return;
            }

            for (int i = 0; i < nums.Length; i++)
            {
                if (used[i]) continue;
                used[i] = true;
                path.Add(nums[i]);
                Backtrack();
                path.RemoveAt(path.Count - 1);
                used[i] = false;
            }
        }

        Backtrack();
        return result;


    }
    static void Main()
    {
        int[] nums = { 1, 2, 3 };
        List<List<int>> result = Permute(nums);
        foreach (var combo in result)
        {
            Console.WriteLine("[" + string.Join(", ", combo) + "]");
        }
    }
}
