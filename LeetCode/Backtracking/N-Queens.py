def solveNQueens(n):
    result = []
    path = []
    used = [False] * n
    diag1 = set()
    diag2 = set()
    board = []

    def backtrack(row):
        if row == n:
            for col in path:
                board.append("." * col + "Q" + "." * (n-col-1))
            result.append(board)
            return

        for col in range(n):
            if used[col] or (row-col) in diag1 or (row+col) in diag2:
                continue
            used[col] = True
            diag1.add(row-col)
            diag2.add(row+col)
            path.append(col)
            backtrack(row+1)
            path.pop()
            diag2.remove(row + col)
            diag1.remove(row-col)
            used[col] = False

    backtrack(0)
    return result


print(solveNQueens(4))
