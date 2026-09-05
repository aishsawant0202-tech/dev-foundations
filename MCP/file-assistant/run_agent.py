#!/usr/bin/env python3
"""
Run a Tiny Agent with local configuration
"""
import json
import os
import subprocess
import sys
from pathlib import Path

def main():
    # Check for OpenAI API key
    api_key = os.getenv("OPENAI_API_KEY")
    if not api_key:
        print("Error: OPENAI_API_KEY environment variable not set!")
        print("Please set it with: $env:OPENAI_API_KEY = 'your-key-here'")
        return
    
    # Load the agent configuration
    agent_config_path = Path("agent.json")
    
    if not agent_config_path.exists():
        print(f"Error: {agent_config_path} not found!")
        return
    
    with open(agent_config_path, "r") as f:
        config = json.load(f)
    
    print("Agent Configuration Loaded!")
    print(f"Model: {config.get('model')}")
    print(f"Endpoint: {config.get('endpointUrl')}")
    print(f"MCP Servers: {len(config.get('servers', []))}")
    print()
    
    # Build the tiny-agents command
    # Since tiny-agents CLI tries to load from HF hub, let's try with a placeholder
    # Actually, let's just tell the user to run it with the CLI directly
    print("To run the agent via CLI, use:")
    print("  tiny-agents run .")
    print()
    print("Or for interactive chat, use a tool like:")
    print("  huggingface-cli chat")
    print()
    print("For now, let's test the setup by checking if the config is valid...")
    print()
    
    # Validate config
    required_keys = ["model", "endpointUrl", "servers"]
    for key in required_keys:
        if key not in config:
            print(f"✗ Missing required key: {key}")
            return
        print(f"✓ {key}: OK")
    
    print()
    print("Configuration is valid!")
    print()
    print("Next steps:")
    print("1. Install Ollama or use the OpenAI API directly")
    print("2. Run: tiny-agents run .")
    print("3. Interact with your agent!")

if __name__ == "__main__":
    main()
