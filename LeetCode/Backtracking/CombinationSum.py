def combinationSum(candidates, target):
    result = []
    path = []

    def backtrack(remaining, start):
        if remaining == 0:
            result.append(path[:])
            return

        if remaining < 0:
            return

        for i in range(start, len(candidates)):
            path.append(candidates[i])
            backtrack(remaining - candidates[i], start=i)
            path.pop()

    backtrack(remaining=target, start=0)
    return result


candidates = [2, 3, 6, 7]
target = 7

print(combinationSum(candidates, target))
