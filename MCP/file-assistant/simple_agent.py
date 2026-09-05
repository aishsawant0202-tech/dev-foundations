#!/usr/bin/env python3
"""
Simple File Assistant Agent using OpenAI
"""
import os
import json
from pathlib import Path
from openai import OpenAI

def read_file(path):
    """Read a file and return its contents"""
    try:
        with open(path, 'r', encoding='utf-8') as f:
            return f.read()
    except Exception as e:
        return f"Error reading file: {e}"

def list_directory(path):
    """List contents of a directory"""
    try:
        items = []
        for item in Path(path).iterdir():
            items.append(str(item.name))
        return "\n".join(items)
    except Exception as e:
        return f"Error listing directory: {e}"

def write_file(path, content):
    """Write content to a file"""
    try:
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        return f"Successfully wrote to {path}"
    except Exception as e:
        return f"Error writing file: {e}"

def process_tool_call(tool_name, tool_input):
    """Process tool calls from OpenAI"""
    if tool_name == "read_file":
        return read_file(tool_input.get("path", ""))
    elif tool_name == "list_directory":
        return list_directory(tool_input.get("path", "."))
    elif tool_name == "write_file":
        return write_file(tool_input.get("path", ""), tool_input.get("content", ""))
    else:
        return f"Unknown tool: {tool_name}"

def main():
    api_key = os.getenv("OPENAI_API_KEY")
    if not api_key:
        print("Error: OPENAI_API_KEY environment variable not set!")
        return
    
    client = OpenAI(api_key=api_key)
    
    # Define available tools
    tools = [
        {
            "type": "function",
            "function": {
                "name": "read_file",
                "description": "Read the contents of a file",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "path": {
                            "type": "string",
                            "description": "Path to the file to read"
                        }
                    },
                    "required": ["path"]
                }
            }
        },
        {
            "type": "function",
            "function": {
                "name": "list_directory",
                "description": "List the contents of a directory",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "path": {
                            "type": "string",
                            "description": "Path to the directory (default: current directory)"
                        }
                    }
                }
            }
        },
        {
            "type": "function",
            "function": {
                "name": "write_file",
                "description": "Write content to a file",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "path": {
                            "type": "string",
                            "description": "Path to the file to write"
                        },
                        "content": {
                            "type": "string",
                            "description": "Content to write to the file"
                        }
                    },
                    "required": ["path", "content"]
                }
            }
        }
    ]
    
    print("=" * 60)
    print("File Assistant Agent (OpenAI)")
    print("=" * 60)
    print("Available commands:")
    print("  - Read files")
    print("  - List directories")
    print("  - Write/Create files")
    print("  - Process and analyze content")
    print()
    print("Type 'exit' to quit\n")
    
    messages = []
    system_prompt = """You are a helpful file assistant. You can:
- Read files from the file system
- List directory contents
- Write files to the file system

Use the available tools to help the user manage their files. 
Be conversational and helpful in your responses."""
    
    while True:
        try:
            user_input = input("» ").strip()
            
            if user_input.lower() in ["exit", "quit", "q"]:
                print("Goodbye!")
                break
            
            if not user_input:
                continue
            
            messages.append({
                "role": "user",
                "content": user_input
            })
            
            # Call OpenAI with tools
            response = client.chat.completions.create(
                model="gpt-4o-mini",
                messages=[{"role": "system", "content": system_prompt}] + messages,
                tools=tools,
                tool_choice="auto"
            )
            
            # Process the response
            assistant_message = {"role": "assistant", "content": ""}
            
            while response.stop_reason == "tool_calls":
                # Process tool calls
                tool_results = []
                for tool_call in response.message.tool_calls:
                    tool_name = tool_call.function.name
                    tool_input = json.loads(tool_call.function.arguments)
                    
                    result = process_tool_call(tool_name, tool_input)
                    tool_results.append({
                        "type": "tool_result",
                        "tool_use_id": tool_call.id,
                        "content": result
                    })
                
                # Add assistant message with tool calls
                if response.message.content:
                    assistant_message["content"] = response.message.content
                assistant_message["tool_calls"] = [
                    {
                        "id": tc.id,
                        "type": "function",
                        "function": {
                            "name": tc.function.name,
                            "arguments": tc.function.arguments
                        }
                    }
                    for tc in response.message.tool_calls
                ]
                
                messages.append(assistant_message)
                messages.extend(tool_results)
                
                # Get next response
                response = client.chat.completions.create(
                    model="gpt-4o-mini",
                    messages=[{"role": "system", "content": system_prompt}] + messages,
                    tools=tools,
                    tool_choice="auto"
                )
            
            # Final response
            final_response = response.message.content
            if final_response:
                print(final_response)
                messages.append({
                    "role": "assistant",
                    "content": final_response
                })
            print()
            
        except KeyboardInterrupt:
            print("\n\nGoodbye!")
            break
        except Exception as e:
            print(f"Error: {e}\n")

if __name__ == "__main__":
    main()
