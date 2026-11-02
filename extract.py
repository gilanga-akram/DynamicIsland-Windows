import json

input_path = r'C:\Users\PC\.gemini\antigravity\brain\2cac9276-16d9-4723-8202-32b2ff4964de\.system_generated\logs\transcript_full.jsonl'
output_path = r'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\prev_xaml.txt'

with open(input_path, 'r', encoding='utf-8') as f:
    for line in f:
        try:
            entry = json.loads(line)
            tool_calls = entry.get('tool_calls', [])
            for tc in tool_calls:
                if tc.get('name') == 'write_to_file':
                    args = tc.get('args', {})
                    if args.get('TargetFile', '').endswith('MainWindow.xaml') and 'TabTimer' in args.get('CodeContent', ''):
                        with open(output_path, 'w', encoding='utf-8') as out:
                            out.write(args['CodeContent'])
                        print('Found and saved!')
                        break
        except Exception:
            pass
