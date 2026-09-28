from pathlib import Path
import json,csv,zipfile,shutil
import numpy as np
from PIL import Image,ImageDraw,ImageSequence
ROOT=Path(__file__).parent
OUT=ROOT/'HowlFX_18frames';OUT.mkdir(exist_ok=True)
FR=OUT/'Frames';FR.mkdir(exist_ok=True)
source=Path('C:/Users/ysjin/.codex/generated_images/01a0c917-f262-70d2-8607-7cd47db8cf48/exec-3ca824c1-c1d4-4040-8663-8987f0a8a12e.png')
im=Image.open(source).convert('RGBA');arr=np.array(im);arr[arr[:,:,3]<=3]=0
im=Image.fromarray(arr);box=im.getchannel('A').getbbox();im=im.crop(box)
shutil.copy2(source,OUT/'Source_HowlRing.png')
W=H=1254;FPS=24;COUNT=18;CENTER=(527,720)
frames=[];rows=[]
for i in range(COUNT):
    frame=Image.new('RGBA',(W,H));t=i/FPS
    for wave,delay in enumerate([0,.16]):
        p=(t-delay)/.52
        if not 0<p<1:continue
        eased=1-(1-p)**1.7
        size=round(68+660*eased)
        ring=im.resize((size,size),Image.Resampling.LANCZOS)
        strength=min(1,p/.09)*(1-p)**1.35*(.8 if wave else 1)
        ring.putalpha(ring.getchannel('A').point(lambda v:round(v*strength)))
        frame.alpha_composite(ring,(CENTER[0]-size//2,CENTER[1]-size//2))
    path=FR/f'Alien_HowlFX_{i+1:03d}.png';frame.save(path,optimize=True);frames.append(frame)
    a=np.asarray(frame)[:,:,3]
    assert max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())==0
    rows.append({'frame':i+1,'fx_time':round(t,7),'transition_time':round((101+i)/FPS,7),'file':path.name})
Image.new('RGBA',(W,H)).save(OUT/'Alien_HowlFX_Clear.png')
with (OUT/'timeline.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.DictWriter(f,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)
preview=[]
for i in range(120):
    body=Image.open(ROOT/'Final_120frames'/'Frames'/f'Alien_Transition_{i+1:03d}.png').convert('RGBA')
    if 101<=i<101+COUNT:body.alpha_composite(frames[i-101])
    sm=body.resize((627,627),Image.Resampling.LANCZOS)
    bg=Image.new('RGB',(627,627),(28,30,38));bg.paste(sm,(0,0),sm);preview.append(bg)
preview[0].save(OUT/'Preview_With_HowlFX.gif',save_all=True,append_images=preview[1:],duration=[50 if i%6==5 else 40 for i in range(120)],loop=0,optimize=False)
hero=Image.open(ROOT/'Final_120frames/Frames/Alien_Transition_109.png').convert('RGBA');hero.alpha_composite(frames[7])
bg=Image.new('RGB',(W,H),(28,30,38));bg.paste(hero,(0,0),hero);bg.crop((190,340,1000,1220)).save(OUT/'Preview_Howl_Closeup.jpg',quality=95)
report={'frame_count':18,'size':[1254,1254],'fps':24,'duration':.75,'start_at_transition_seconds':101/24,'effect_center_in_canvas':CENTER,'alpha_transparency':True,'clipped_frames':0,'first_last_frames_transparent':frames[0].getbbox() is None and frames[-1].getbbox() is None,'unity_tested':False}
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(report)
