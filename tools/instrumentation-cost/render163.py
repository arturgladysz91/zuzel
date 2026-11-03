"""Render the frozen read-only report; requires matplotlib and markdown locally."""
from pathlib import Path
import base64, json
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import markdown
from analyze163 import DOC, read

a=read(DOC/'analysis.json');out=DOC/'plots';out.mkdir(exist_ok=True)
plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'figure.dpi':150})
colors={'Fine':'#40577b','Aggregate':'#17857b','Disabled':'#b78442'}
fig,axes=plt.subplots(1,2,figsize=(11,4.1))
for ax,metric,title in zip(axes,['ConditionalCpuSeconds','ReviewCpuSeconds'],['Rzeczywiste kontynuacje Rollout','Całe Choose (pełne 13/9)']):
 for j,m in enumerate(colors):
  for i,c in enumerate(a['Cases']):
   vals=c['Modes'][m][metric];x=i+(j-1)*.23
   ax.bar(x,sum(vals)/2,.21,color=colors[m],alpha=.8,label=m if i==0 else None)
   ax.scatter([x-.025,x+.025],vals,c='black',s=13,zorder=4)
 ax.set(xticks=range(3),xticklabels=['m1 nominalny','m2 ciasny','m3 Prepare'],ylabel='CPU [s]',title=title)
 ax.grid(axis='y',alpha=.2);ax.legend(fontsize=8)
fig.suptitle('#163 • dwie obserwacje / tryb • brak nowych wyścigów natywnych')
fig.tight_layout();fig.savefig(out/'cpu-163.png');plt.close(fig)
fig,axes=plt.subplots(1,2,figsize=(11,4.1))
axes[0].bar(['Zegar m1','Zegar m2','Zegar m3','Para Phase'],[c['CpuMicrosecondsPerRead'] for c in a['Calibration']]+[a['Calibration'][0]['CpuMicrosecondsPerScope']],color=['#40577b']*3+['#17857b'])
axes[0].set(ylabel='CPU netto [µs / operację]',title='Kontrolowane kalibracje, ten sam host');axes[0].grid(axis='y',alpha=.2)
for j,m in enumerate(colors):
 axes[1].bar([i+(j-1)*.23 for i in range(3)],[c['Modes'][m]['Profiler']['ClockReads'] for c in a['Cases']],.21,color=colors[m],label=m)
axes[1].set(xticks=range(3),xticklabels=['m1','m2','m3'],ylabel='Odczyty profilera / review',title='Zegary twardych Guard pozostają bez zmian');axes[1].legend(fontsize=8)
fig.suptitle('Mniej odczytów nie dowodzi ≥10% poprawy rzeczywistych kontynuacji')
fig.tight_layout();fig.savefig(out/'instrumentation-163.png');plt.close(fig)
body=markdown.markdown((DOC/'REPORT.md').read_text(encoding='utf-8'),extensions=['tables','fenced_code'])
for p in out.glob('*.png'):
 body=body.replace('plots/'+p.name,'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode())
html='<!doctype html><html lang="pl"><meta charset="utf-8"><title>Badanie163 — PARTIAL</title><style>body{max-width:1120px;margin:40px auto;padding:0 24px;color:#192b3c;font:16px/1.6 system-ui}h1,h2{line-height:1.25}h2{margin-top:2em}table{border-collapse:collapse;width:100%;font-size:14px}th,td{border-bottom:1px solid #cbd5df;padding:8px;text-align:left;overflow-wrap:anywhere}img{width:100%;height:auto}code{overflow-wrap:anywhere;font-size:13px}strong{color:#8b3f15}@media print{body{margin:0;max-width:none}h2{break-after:avoid}table,img{break-inside:avoid}}</style><body>'+body+'</body></html>'
(DOC/'report.html').write_text(html,encoding='utf-8')
print('Rendered report.html and two figures from existing measurements only')
