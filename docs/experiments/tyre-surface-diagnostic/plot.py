"""Plot original-contact query results. Does not implement/evaluate tyre physics."""
import json
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

HERE=Path(__file__).resolve().parent
grid=json.loads((HERE/"static-grid.json").read_text())
state=json.loads((HERE/"saved-state.json").read_text())
mid=[r for r in grid if r["SourceSeconds"]==.5 and not r.get("Tail")]
saved=state["Original"][1]
colors=["#176b8f","#d26c22","#20866a","#8a569e","#607486"]
plt.rcParams.update({"font.family":"DejaVu Sans","font.size":11,"axes.spines.top":False,
                     "axes.spines.right":False,"axes.labelcolor":"#233444","text.color":"#233444",
                     "axes.titlesize":14,"axes.titleweight":"bold","figure.facecolor":"white",
                     "axes.edgecolor":"#becbd3","grid.color":"#e4eaee","grid.alpha":1,
                     "savefig.facecolor":"white"})
fig,axes=plt.subplots(1,2,figsize=(13,5.8),layout="constrained")
fig.suptitle("Oryginalny kontakt tylnej opony: wzrost do plateau",fontsize=18,fontweight="bold")
for a,c in zip([0,10,40],colors):
    rows=sorted([r for r in mid if r["AskedAlpha"]==a],key=lambda r:r["AskedKappa"])
    axes[0].plot([r["Kappa"] for r in rows],[r["Fx"] for r in rows],"o-",color=c,label=f"α = {a}°",lw=2)
axes[0].axhline(saved["Br"],ls="--",color="#a0a9b1",lw=1,label="B tyłu = 997,4 N")
axes[0].set(xlabel="Poślizg wzdłużny κ [–]",ylabel="Fx tyłu [N]",title="Fx(κ): brak własnej gałęzi spadku",xlim=(-.04,3.05),ylim=(0,1060))
for k,c in zip([0,.3,1,3],colors):
    rows=sorted([r for r in mid if r["AskedKappa"]==k],key=lambda r:r["AskedAlpha"])
    axes[1].plot([r["AlphaRearDegrees"] for r in rows],[-r["Fy"] for r in rows],"o-",color=c,label=f"κ = {k:g}",lw=2)
axes[1].axhline(saved["Br"],ls="--",color="#a0a9b1",lw=1)
axes[1].set(xlabel="Lokalny kąt tylnego kontaktu α [°]",ylabel="Przeciwdziałająca siła |Fy| tyłu [N]",
            title="Fy(α): większe κ zmniejsza odpowiedź",xlim=(-.5,40.5),ylim=(0,1060))
for ax in axes:
    ax.grid();ax.legend(frameon=False,loc="lower right")
fig.supxlabel("u tyłu = 20,230 m/s; m = 147 kg; µ i obciążenia z #116 bez zmian; h = 0.\n"
               "Punkty: zapytania 5 × 5. Linie łączą punkty. α = atan2(w tyłu, u tyłu); α ≠ β.",fontsize=10)
fig.savefig(HERE/"forces-vs-slip.png",dpi=160)
plt.close(fig)

fig,ax=plt.subplots(figsize=(10.2,7.4),layout="constrained")
fig.suptitle("Podział tylnego budżetu między napęd i skręcanie",fontsize=18,fontweight="bold")
br=saved["Br"];x=np.linspace(0,br,400)
ax.plot(x,np.sqrt(np.maximum(0,br*br-x*x)),"--",color="#495965",lw=1.6,label="Granica koła sił √(Br² − Fx²)")
for a,c in zip([5,10,20,40],colors):
    rows=sorted([r for r in mid if r["AskedAlpha"]==a],key=lambda r:r["AskedKappa"])
    ax.plot([r["Fx"] for r in rows],[-r["Fy"] for r in rows],"o-",lw=1.7,color=c,label=f"Stałe α = {a}°; rosnące κ")
relaxed=[r["RelaxedWheel"] for r in state["Throttle"]]
ax.plot([r["Fx"] for r in relaxed],[r["Fy"] for r in relaxed],"D:",color="#ce4e4e",lw=2,
        label="Stan 0,5 s: równowaga samego koła")
for r,d in zip(relaxed,[0,.25,.5,.75,1]):
    offset={0:(8,-18),.25:(8,8),.5:(8,8),.75:(-65,-24),1:(14,-25)}[d]
    ax.annotate(f"D={d:g}",(r["Fx"],r["Fy"]),xytext=offset,textcoords="offset points",fontsize=9,color="#b93e3e",
                arrowprops={"arrowstyle":"-","color":"#b93e3e","lw":.7} if d in [.75,1] else None)
ax.scatter([saved["Fx"]],[saved["Fy"]],marker="*",s=190,color="#142e45",zorder=5,label="Zapisany stan #116 (h=10 ms)")
ax.annotate("Fx 924 N, Fy 319 N\nkoło sił dopuszcza najwyżej 376 N",
            (saved["Fx"],saved["Fy"]),xytext=(460,160),arrowprops={"arrowstyle":"->","color":"#142e45"},fontsize=10)
ax.set(xlabel="Siła napędowa Fx tyłu [N]",ylabel="Siła boczna |Fy| tyłu [N]",xlim=(-35,1040),ylim=(-15,1060))
ax.grid();ax.legend(frameon=True,facecolor="white",edgecolor="#d6dfe5",loc="upper right",fontsize=9)
fig.supxlabel("Br = 997,4 N. Zmniejszenie gazu działa przez zmianę ω i κ; przy zamrożonym ω oraz h=0 siły są identyczne.\n"
               "Czerwone punkty zamrażają nadwozie i kierownicę: równowaga koła nie dowodzi stabilności motocykla.",fontsize=10)
fig.savefig(HERE/"lateral-vs-drive.png",dpi=160)
plt.close(fig)
