from transformers import AutoTokenizer

tokenizer = AutoTokenizer.from_pretrained("gpt2")

text = """
Large Language Models process text using tokens.
A context window determines how many tokens
the model can consider at one time.

Tokens are not necessarily complete words.
A word can be represented by multiple tokens.
Punctuation can also be represented as tokens.

When a conversation becomes very long,
the number of tokens in the context increases.
The model eventually reaches its context limit.
"""


def tokenize_text(text):
    return tokenizer.tokenize(text)


def context_window_check(text, max_context_tokens):
    tokens = tokenize_text(text)
    print("Original token count: ", len(tokens))
    if len(tokens) > max_context_tokens:
        truncated_tokens = tokens[-max_context_tokens:]
        print("Removed tokens: ", len(tokens) - len(truncated_tokens))
        print("Truncated Tokens: ", truncated_tokens)
        print("Final token count: ", len(truncated_tokens))
    else:
        print("Text fits inside context window.")


max_context_tokens = 30
context_window_check(text, max_context_tokens)
