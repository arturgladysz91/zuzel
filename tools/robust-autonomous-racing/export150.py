"""Portable saved-evidence package; never evaluates a controller."""
import html,json,re,shutil,sys
from pathlib import Path
from analyze150 import R,D,read,save,charged
def render(text):
 def inline(t):
  t=html.escape(t);t=re.sub(r'!\[([^]]*)\]\(([^)]+)\)',r'<img alt="\1" src="\2">',t);t=re.sub(r'\[([^]]+)\]\(([^)]+)\)',r'<a href="\2">\1</a>',t);t=re.sub(r'\*\*(.+?)\*\*',r'<strong>\1</strong>',t);return re.sub(r'`([^`]+)`',r'<code>\1</code>',t)
 body=[];in_table=False
 for line in text.splitlines():
  if line.startswith('|'):
   if not in_table:body.append('<div class="scroll"><table>');in_table=True
   if set(line.replace('|','').replace('-','').replace(':','').strip()):body.append('<tr>'+''.join('<td>'+inline(v)+'</td>' for v in line.strip('|').split('|'))+'</tr>')
   continue
  if in_table:body.append('</table></div>');in_table=False
  if not line:continue
  if line.startswith('#'):level=len(line)-len(line.lstrip('#'));body.append(f'<h{level}>'+inline(line[level:].strip())+f'</h{level}>')
  else:body.append('<p>'+inline(line)+'</p>')
 return '<!doctype html><meta charset="utf-8"><title>Badanie150 · PARTIAL</title><style>body{font:16px/1.6 system-ui;max-width:1280px;margin:40px auto;padding:0 24px;color:#182633}h1,h2{line-height:1.2}h2{margin-top:2em}table{border-collapse:collapse;font-size:13px;width:100%}td{border:1px solid #ccd5dc;padding:7px}tr:first-child{font-weight:bold;background:#edf3f7}.scroll{overflow:auto}img{max-width:100%}a{color:#06628b}code{font-size:.9em}</style>'+''.join(body)
def main():
 out=Path(sys.argv[1]).resolve();label=sys.argv[2] if len(sys.argv)>2 else 'final';out.mkdir(parents=True,exist_ok=True)
 with charged('portable-export-'+label):
  shutil.copytree(D,out,dirs_exist_ok=True,ignore=shutil.ignore_patterns('*.lock'))
  shutil.copytree(R/'tools/robust-autonomous-racing',out/'source',dirs_exist_ok=True,ignore=shutil.ignore_patterns('bin','obj','__pycache__'))
  logs=out/'validation';logs.mkdir(exist_ok=True)
  for p in (R/'work').glob('*150*.log'):shutil.copyfile(p,logs/p.name)
  shutil.copytree(R/'work/discovery150',logs/'discovery',dirs_exist_ok=True)
  shutil.copytree(R/'work/test-results150',logs/'dotnet',dirs_exist_ok=True)
  for name in ['baseline149-final148','separated149-autonomous']:
   dest=out/'baseline149';dest.mkdir(exist_ok=True)
   for suffix in ['.json.gz','-result.json']:shutil.copyfile(R/'docs/experiments/tyre-physics-corner-limits/native'/(name+suffix),dest/(name+suffix))
 budget=read(D/'budget.json');summary=dict(CpuSeconds=sum(x['CpuSeconds'] for x in budget),NativeWorlds=sum(x['NativeWorlds'] for x in budget),AllComplete=all(x['Complete'] for x in budget),LimitCpuSeconds=1180,LimitWorlds=30,Scope='Experiments plus saved postprocessing/export; build/tests/CI excluded');save(D/'budget-summary.json',summary);save(out/'budget-summary.json',summary);shutil.copyfile(D/'budget.json',out/'budget.json')
 text=(D/'REPORT.md').read_text(encoding='utf-8');footer=f"\n\nBudżet końcowy (ledger z eksportem): **{summary['CpuSeconds']:.6f} CPU-s / {summary['NativeWorlds']} nowych światów**. Kompilacja, testy i CI są poza budżetem eksperymentów. Wszystkie rezerwacje zakończone; nie uruchamiano dalszych światów po negatywnych bramkach.\n"
 if 'Budżet końcowy (ledger z eksportem)' not in text:text+=footer;(D/'REPORT.md').write_text(text,encoding='utf-8',newline='\n')
 (out/'Raport150.md').write_text(text,encoding='utf-8',newline='\n');(out/'REPORT.md').write_text(text,encoding='utf-8',newline='\n');(out/'Raport150.html').write_text(render(text),encoding='utf-8',newline='\n');print(json.dumps(summary))
if __name__=='__main__':main()
