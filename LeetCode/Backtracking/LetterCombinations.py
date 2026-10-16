phone = {
    "2": "abc",
    "3": "def",
    "4": "ghi",
    "5": "jkl",
    "6": "mno",
    "7": "pqrs",
    "8": "tuv",
    "9": "wxyz"
}

digits = "23"


def letterCombinations(digits):
    if digits == "":
        return []

    result = []

    def build(index, current):
        if index == len(digits):
            result.append(current)
            return

        letters = phone[digits[index]]

        for letter in letters:
            print(len(result))
            build(index + 1, current + letter)

    build(0, "")
    return result


letterCombinations(digits)
