from pathlib import Path

root = Path(__file__).resolve().parents[1]
program = root / 'src' / 'SuvidhaPremium' / 'Program.cs'
index = root / 'src' / 'SuvidhaPremium' / 'wwwroot' / 'index.html'

p = program.read_text(encoding='utf-8')
marker = 'SupportSettingsModule.Map(app);'
if marker not in p:
    needle = 'app.UseAuthorization();'
    if needle not in p:
        raise SystemExit('Program.cs mapping insertion point not found')
    p = p.replace(needle, needle + '\n' + marker, 1)
    program.write_text(p, encoding='utf-8', newline='\n')

h = index.read_text(encoding='utf-8')
script = '<script src="assets/js/support-settings.js?v=34"></script>'
if script not in h:
    if '</body>' not in h:
        raise SystemExit('index.html body close not found')
    h = h.replace('</body>', script + '</body>', 1)
    index.write_text(h, encoding='utf-8', newline='\n')

print('Support settings build wiring applied.')
