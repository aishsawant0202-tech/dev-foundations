from transformers import AutoTokenizer

tokenizer = AutoTokenizer.from_pretrained("gpt2")

messages = [
    {"role": "system", "content": "You are a helpful AI tutor."},
    {"role": "user", "content": "My name is Alice."},
    {"role": "assistant", "content": "Nice to meet you, Alice!"},
    {"role": "user", "content": "I am learning Python."},
    {"role": "assistant", "content": "Great! Python is useful for AI."},
    {"role": "user", "content": "What should I learn next?"},
]


def build_context(messages, max_context_tokens):
    system_message = messages[0]
    current_message = messages[-1]

    system_tokens = count_message_tokens(system_message)
    current_tokens = count_message_tokens(current_message)

    remaining_tokens = max_context_tokens - system_tokens - current_tokens

    recent_messages = []

    for message in reversed(messages[1:-1]):
        message_tokens = count_message_tokens(message)
        if message_tokens <= remaining_tokens:
            recent_messages.append(message)
            remaining_tokens -= message_tokens
        else:
            break
    context = [system_message]
    for message in reversed(recent_messages):
        context.append(message)
    context.append(current_message)
    return context


def count_message_tokens(message):
    content = message["content"]
    tokens = tokenizer.tokenize(content)

    return len(tokens)


total_token_count = 0
for message in messages:
    token_count = count_message_tokens(message)
    total_token_count += token_count
    # print(message["role"], token_count, "tokens")


print("total tokens: ", total_token_count)

max_context_tokens = 30
print("Context limit: ", max_context_tokens)

context = build_context(messages, max_context_tokens)

tokens_used = sum(count_message_tokens(message) for message in context)
print("tokens used:", tokens_used)
print("tokens remaining:", max_context_tokens - tokens_used)
print("tokens removed:", total_token_count - tokens_used)
