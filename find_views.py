with open(r'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml', 'r', encoding='utf-8') as f:
    for idx, line in enumerate(f):
        if 'x:Name="View' in line:
            print(f'{idx+1}: {line.strip()}')
