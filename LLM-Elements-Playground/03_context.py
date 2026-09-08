from transformers import AutoTokenizer
from ollama import chat

tokenizer = AutoTokenizer.from_pretrained("gpt2")


messages = [
    {"role": "system", "content": "You are a helpful AI tutor."},

    {"role": "user", "content": "My name is Alice."},
    {"role": "assistant", "content": "Nice to meet you, Alice!"},

    {"role": "user", "content": "I am learning Python."},
    {"role": "assistant", "content": "Great! Python is useful for AI."},

    {"role": "user", "content": "I already understand variables and loops."},
    {"role": "assistant", "content": "Good. You can learn functions next."},

    {"role": "user", "content": "I learned how to define functions."},
    {"role": "assistant", "content": "Excellent. Try learning parameters and return values."},

    {"role": "user", "content": "I understand parameters now."},
    {"role": "assistant",
        "content": "Then you can start learning Python lists and dictionaries."},

    {"role": "user", "content": "I know lists but dictionaries are confusing."},
    {"role": "assistant", "content": "Dictionaries store data using key-value pairs."},

    {"role": "user", "content": "Can you explain dictionaries with an example?"}
]


def count_message_tokens(message):
    return len(tokenizer.tokenize(message["content"]))


def prepare_messages_for_summary(messages_to_summarize):
    summary_text = ""

    for message in messages_to_summarize:
        summary_text += (
            f"{message['role']}: "
            f"{message['content']}\n"
        )

    return summary_text


def summarize_messages(summary_text):
    prompt = (
        "Summarize the following conversation very briefly.\n"
        "Keep only important facts needed for future conversation.\n"
        "Do not include headings, bullet points, greetings, "
        "or explanations.\n"
        "Use at most 2 short sentences.\n\n"
        f"Conversation:\n{summary_text}"
    )

    response = chat(
        model="qwen3:0.6b",
        messages=[
            {"role": "user", "content": prompt}
        ],
        options={"num_ctx": 1024}
    )

    return response.message.content


def shorten_summary(summary, summary_token_budget):
    prompt = (
        f"Rewrite this summary using at most "
        f"{summary_token_budget} tokens.\n"
        "Keep only the most important information.\n"
        "Return only the shortened summary.\n\n"
        f"Summary:\n{summary}"
    )

    response = chat(
        model="qwen3:0.6b",
        messages=[
            {"role": "user", "content": prompt}
        ],
        options={"num_ctx": 1024}
    )

    return response.message.content


def truncate_to_token_budget(text, token_budget):
    token_ids = tokenizer.encode(text)

    token_ids = token_ids[:token_budget]

    return tokenizer.decode(token_ids)


def build_context(
    messages,
    max_context_tokens,
    summary_token_budget
):
    system_message = messages[0]
    current_message = messages[-1]

    # --------------------------------
    # 1. Check whether everything fits
    # --------------------------------

    total_tokens = sum(count_message_tokens(message) for message in messages)

    if total_tokens <= max_context_tokens:
        return messages, []

    # --------------------------------
    # 2. Reserve required token space
    # --------------------------------

    system_tokens = count_message_tokens(system_message)
    current_tokens = count_message_tokens(current_message)

    remaining_tokens = (max_context_tokens - system_tokens
                        - current_tokens - summary_token_budget)

    recent_messages = []
    messages_to_summarize = []

    # --------------------------------
    # 3. Keep newest messages first
    # --------------------------------

    middle_messages = messages[1:-1]

    for index in range(len(middle_messages) - 1, -1, -1):

        message = middle_messages[index]

        message_tokens = count_message_tokens(message)

        if message_tokens <= remaining_tokens:
            recent_messages.append(message)
            remaining_tokens -= message_tokens

        else:
            # This message AND everything before it
            # will be summarized.
            messages_to_summarize = middle_messages[:index + 1]
            break

    # Put recent messages back in chronological order
    recent_messages.reverse()

    # --------------------------------
    # 4. Build context
    # --------------------------------

    context = [system_message]

    # Only summarize if necessary
    if messages_to_summarize:

        summary_text = prepare_messages_for_summary(messages_to_summarize)

        summary = summarize_messages(summary_text)

        summary_message = {
            "role": "system",
            "content": summary
        }

        summary_tokens = count_message_tokens(summary_message)

        print("Initial summary tokens:", summary_tokens)

        # --------------------------------
        # 5. Try making summary shorter
        # --------------------------------

        if summary_tokens > summary_token_budget:

            print("Summary exceeds budget. " "Shortening...")

            summary = shorten_summary(summary, summary_token_budget)

            summary_message = {
                "role": "system",
                "content": summary
            }

            summary_tokens = count_message_tokens(summary_message)

            print("Shortened summary tokens:", summary_tokens)

        # --------------------------------
        # 6. Hard fallback
        # --------------------------------

        if summary_tokens > summary_token_budget:

            print("Summary still too large. " "Applying hard truncation...")

            summary = truncate_to_token_budget(summary, summary_token_budget)

            summary_message = {
                "role": "system",
                "content": summary
            }

        context.append(summary_message)

    context.extend(recent_messages)

    context.append(current_message)

    return context, messages_to_summarize

# ====================================
# Main
# ====================================


total_token_count = sum(
    count_message_tokens(message)
    for message in messages
)

print("Total tokens:", total_token_count)

max_context_tokens = 80
summary_token_budget = 20

print("Context limit:", max_context_tokens)
print("Summary token budget:", summary_token_budget)

context, messages_to_summarize = build_context(
    messages,
    max_context_tokens,
    summary_token_budget
)

tokens_used = sum(count_message_tokens(message) for message in context)

print("\nTokens used:", tokens_used)

print("\nMessages in context:")

for message in context:
    print(f"{message['role']}: " f"{message['content']}")

print("\nMessages summarized:")

for message in messages_to_summarize:
    print(f"{message['role']}: "f"{message['content']}")
