import re
import json

with open(r'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\pin.html', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

urls = set(re.findall(r'https://[^\s"\'<>]+\.(?:gif|mp4|webm|jpg|jpeg|png)', text))
for u in sorted(urls):
    if 'pinimg' in u:
        print(u)
