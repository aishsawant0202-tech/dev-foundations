from transformers import AutoTokenizer

tokenizer = AutoTokenizer.from_pretrained("gpt2")


def tokenize_text(text):
    tokens = tokenizer.tokenize(text)
    token_ids = tokenizer.convert_tokens_to_ids(tokens)

    return tokens, token_ids


result = tokenize_text("Large Language Models use tokens to process text.")
print("Tokens:", result[0])
print("Token IDs:", result[1])
print("Number of Tokens:", len(result[0]))
