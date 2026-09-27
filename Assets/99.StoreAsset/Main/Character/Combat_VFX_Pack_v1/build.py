from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import numpy as np,math,json,csv,shutil
ROOT=Path(__file__).parent;OUT=ROOT/'Deliverables';PRE=OUT/'Previews';PRE.mkdir(parents=True,exist_ok=True)
G=json.loads((ROOT/'Generated_Sources.json').read_text());R=Image.Resampling;manifest=[]
def blank(size):return Image.new('RGBA',size)
def load(k):return Image.open(G[k]['path']).convert('RGBA')
def crop(im):return im.crop(im.getchannel('A').getbbox())
def clamp(v):return max(0,min(1,v))
def smooth(v):v=clamp(v);return v*v*(3-2*v)
def alpha(im,k):
 a=np.array(im);a[:,:,3]=np.uint8(a[:,:,3].astype(float)*np.clip(k,0,1));return Image.fromarray(a)
def save(im,p):p.parent.mkdir(parents=True,exist_ok=True);im.save(p,optimize=True)
def fit(im,size):
 im=im.copy();im.thumbnail(size,R.NEAREST);o=blank(size);o.alpha_composite(im,((size[0]-im.width)//2,(size[1]-im.height)//2));return o
def recolor(im,c):
 a=np.array(im);rgb=a[:,:,:3].astype(float)/255;v=rgb.max(2);w=rgb.min(2)/(v+1e-6);w=np.clip((w-.3)/.7,0,1)**1.5
 a[:,:,:3]=np.uint8(np.clip(v[:,:,None]*(np.array(c)[None,None,:]*(1-w[:,:,None])+255*w[:,:,None]),0,255));return Image.fromarray(a)
def gif(fs,name,fps=24,maxsize=(768,512)):
 imgs=[]
 for f in fs:
  q=min(maxsize[0]/f.width,maxsize[1]/f.height);sz=(round(f.width*q),round(f.height*q));im=f.resize(sz,R.NEAREST)
  bg=Image.new('RGB',sz,(27,30,39));bg.paste(im,(0,0),im);imgs.append(bg)
 times=[round((i+1)*100/fps)*10-round(i*100/fps)*10 for i in range(len(fs))]
 imgs[0].save(PRE/(name+'.gif'),save_all=True,append_images=imgs[1:],duration=times,loop=0,optimize=False)
def sheet(fs,name,cols=4,cell=(280,220)):
 ids=list(range(len(fs))) if len(fs)<=20 else np.linspace(0,len(fs)-1,12,dtype=int).tolist()
 s=Image.new('RGB',(cols*cell[0],math.ceil(len(ids)/cols)*cell[1]),(27,30,39));d=ImageDraw.Draw(s)
 for j,i in enumerate(ids):
  im=fit(fs[i],(cell[0]-12,cell[1]-30));x=(j%cols)*cell[0];y=(j//cols)*cell[1];s.paste(im,(x+6,y+25),im);d.text((x+6,y+6),f'{name} {i+1:03d}',fill='white')
 s.save(PRE/(name+'.jpg'),quality=95)
def export(name,fs,ppu,pivot=(.5,.5),fps=24,note='',preview=True):
 for i,f in enumerate(fs):save(f,OUT/name/f'{Path(name).name}_{i+1:03d}.png')
 manifest.append(dict(folder=name,frames=len(fs),size=fs[0].size,ppu=ppu,pivot=pivot,fps=fps,duration=len(fs)/fps,note=note))
 if preview:gif(fs,name.replace('/','_'),fps);sheet(fs,name.replace('/','_'))
def pulsate(im,phase,axis='x',amount=.2):
 a=np.array(im);y,x=np.mgrid[:im.height,:im.width];q=x if axis=='x' else y
 a[:,:,3]=np.uint8(a[:,:,3]*(1-amount/2+amount/2*np.sin(q*.065-phase*math.tau)));return Image.fromarray(a)

# Dog: an overlay aligned to the original head, with empty central space and rearward energy flow.
dogseed=crop(load('Dog')).resize((164,74),R.NEAREST);dogfx=[];combined=[]
old=ROOT.parent/'Slash_Shield_Boss_Animations_v1/Deliverables/Dog_Charge_Mouth/Frames'
for i in range(16):
 fx=blank((213,126));f=pulsate(dogseed,i/16,amount=.65);fx.alpha_composite(f,(38,26+round(math.sin(i/16*math.tau))))
 dogfx.append(fx);b=blank(fx.size);b.alpha_composite(Image.open(old/f'Dog_Charge_{i+1:02d}.png').convert('RGBA'),(32,16));b.alpha_composite(fx);combined.append(b)
export('DogCharge/FX_16',dogfx,100,note='LEFT-facing; same root center as original Dog; add behind original mouth animation')
export('DogCharge/Combined_MouthAndFX_16',combined,100,note='Ready combined mouth cycle + flow, original body pixel scale preserved')

# Open-edged black hole: no outer ring; radial travelling highlights move inward while filaments rotate.
src=load('BlackHole').resize((512,512),R.NEAREST);base=blank((512,512));base.alpha_composite(src,(0,-14))
y,x=np.mgrid[:512,:512];rad=np.hypot(x-255.5,y-255.5);outer=np.clip((245-rad)/16,0,1)
base=alpha(base,outer);bh=[]
for i in range(32):
 phase=i/32;im=base.rotate(-phase*360,R.NEAREST,center=(256,256))
 im=alpha(im,(.76+.24*np.sin(rad*.055+phase*math.tau*3))*outer)
 bh.append(im.resize((1254,1254),R.NEAREST))
export('BlackHole/Active_32',bh,100,note='Original 1254 canvas/PPU100; no outer circumference; loop')
export('BlackHole/Appear_12',[alpha(bh[i%32],smooth((i+1)/12)) for i in range(12)],100,note='Fixed-size opacity appearance; optional')
export('BlackHole/Disappear_12',[alpha(bh[(i+12)%32],1-smooth((i+1)/12)) for i in range(12)],100,note='Fixed-size opacity disappearance; optional')

# Big bullet: fixed geometry, separate core and peripheral discharge masks. No geometric scaling during charge.
src=fit(crop(load('BigBullet')),(488,488));base=blank((512,512));base.alpha_composite(src,(12,12));ar=np.array(base)
y,x=np.mgrid[:512,:512];r=np.hypot(x-256,y-256);mask=np.clip((145-r)/15,0,1)
core=alpha(base,mask);bolts=alpha(base,1-mask)
charge=[];cores=[];arcs=[];active=[]
for i in range(60):
 q=i/59;phase=i/12;bright=smooth(q);c=alpha(core,.025+.975*bright)
 # Definition comes from opacity/contrast and exposure of fine plasma filaments, not radius.
 ca=np.array(c);ca[:,:,:3]=np.uint8(ca[:,:,:3]*(.35+.65*bright));c=Image.fromarray(ca)
 a=bolts.rotate((i%12)*30,R.NEAREST,center=(256,256));a=pulsate(a,phase,amount=.6);a=alpha(a,.18+.82*min(q*2+.1,1))
 f=c.copy();f.alpha_composite(a);charge.append(f);cores.append(c);arcs.append(a)
for i in range(12):
 a=pulsate(bolts.rotate(i*30,R.NEAREST,center=(256,256)),i/12,amount=.55)
 f=core.copy();f.alpha_composite(a);active.append(f)
export('CruiserBigBullet/Charge_60',charge,236,note='2.5s at24FPS; fixed512 canvas/core radius; PPU236 keeps total nominal diameter near previous 31px/100*7')
export('CruiserBigBullet/Charge_CoreOnly_60',cores,236,preview=False,note='Optional separate core, use with LightningOnly')
export('CruiserBigBullet/Charge_LightningOnly_60',arcs,236,preview=False,note='Optional peripheral discharge; fixed pivot')
export('CruiserBigBullet/ActiveFlight_12',active,236,note='Loop during 1s hold and flight; scale remains1')

# Crack detonation anchors to the rupture point, not the texture center.
src=load('Crack').resize((768,384),R.NEAREST);origin=(round(768*.12),192);crack=[]
for i in range(16):
 q=i/15;f=pulsate(src,q*2,amount=.5)
 a=np.array(f);y,x=np.mgrid[:384,:768];progress=smooth(min(q*5,1));reveal=np.clip((progress*768-x)/70,0,1)
 a[:,:,3]=np.uint8(a[:,:,3]*reveal*(1-smooth((q-.3)/.7)));f=Image.fromarray(a);crack.append(f)
export('AlienResonance/CrackBurst_16',crack,94,(.12,.5),note='Rupture at12%width; +X normal; nominal width8.17world atscale1 matches existing main crack visible width8.19world. Existing scale6 alternative uses PPU564.')
# Animated replacement of the actual moving wave, same original aspect and fitting code.
wave=Image.open('C:/YSJ/SubPF/Assets/99.StoreAsset/Main/Bosses/Boss_Alien/AlienResonanceSprite_v1/Sprites/Alien_ResonanceWave_01.png').convert('RGBA').resize((256,768),R.NEAREST)
waves=[pulsate(wave,i/12,'y',.55) for i in range(12)]
export('AlienResonance/TravelWave_12',waves,100,note='Use current wave visual fitting: thickness0.4, width2; +X front')
# Final rectangle attack: one field per hit rectangle, never paint across the safe lane.
field=fit(crop(load('Field')),(500,500));b=blank((512,512));b.alpha_composite(field,(6,6));field=b
fields=[]
for i in range(12):
 q=i/11;f=pulsate(field,q*3,'y',.6)
 # Rearrange the existing field by quarter turns, preserving square aspect and center.
 f=f.rotate((i//3)*90,R.NEAREST);f=alpha(f,(.35+.65*smooth(min(q*6,1)))*(1-smooth((q-.55)/.45)));fields.append(f)
export('AlienResonance/FinalArea_12',fields,100,fps=30,note='0.4s matches FinalActiveDuration; apply separately to leftArea and rightArea, clip/tile inside each rectangle')

# Predictive leap warning: keep exact attack radius fixed while only internal details pulse.
w=load('Warning').resize((512,512),R.NEAREST);a=np.array(w);y,x=np.mgrid[:512,:512];r=np.hypot(x-255.5,y-255.5)
a[:,:,3]=np.uint8(a[:,:,3]*np.clip((237-r)/7,0,1));a[:,:,:3]=255;decor=Image.fromarray(a)
# Accurate fixed outer perimeter fills the supplied sprite diameter, matching the existing bounds-based scale calculation.
base=blank((512,512));base.alpha_composite(decor);d=ImageDraw.Draw(base);d.ellipse((1,1,510,510),outline=(255,255,255,240),width=3)
aim=[];locked=[]
for i in range(12):
 inner=alpha(decor,.36+.14*math.sin(i/12*math.tau));f=blank((512,512));f.alpha_composite(inner);d=ImageDraw.Draw(f);d.ellipse((1,1,510,510),outline=(255,255,255,210),width=3);aim.append(f)
for i in range(8):
 f=base.copy();d=ImageDraw.Draw(f);rr=24+round(5*math.sin(i/8*math.tau));d.polygon([(256,256-rr),(256+rr,256),(256,256+rr),(256-rr,256)],outline=(255,255,255,255),width=3);locked.append(f)
export('AlienLeapWarning/Aiming_White_12',aim,100,note='White tint-ready; current _aimingColor .8,.2,1,.25; outer radius stays fixed')
export('AlienLeapWarning/Locked_White_8',locked,100,note='White tint-ready; current _lockedColor1,.2,.65,.5; no growing radius')
save(base,OUT/'AlienLeapWarning/Static_White.png')
colored=[recolor(f,(204,51,255)) for f in aim]+[recolor(f,(255,51,166)) for f in locked];gif(colored,'LeapWarning_ColorPreview')

# Laser parts share the same vertical sampling, so the white filament stays aligned at all seams.
raw=load('Laser');cy=raw.height//2
tile=raw.crop((raw.width//4,cy-160,raw.width*3//4,cy+160)).resize((256,64),R.NEAREST)
start=raw.crop((0,cy-160,320,cy+160)).resize((64,64),R.NEAREST)
end=raw.crop((raw.width-320,cy-160,raw.width,cy+160)).resize((64,64),R.NEAREST)
ta=np.array(tile);ta[:,-1]=ta[:,0];tile=Image.fromarray(ta)
palette=[(50,190,255),(80,110,255),(160,65,255),(245,35,170),(255,30,25)];laserpreview=[];stage_table=[]
for stage in range(17):
 q=stage/16;z=q*4;idx=min(3,int(z));u=z-idx;c=tuple(round(palette[idx][k]*(1-u)+palette[idx+1][k]*u) for k in range(3))
 seq={'BodyTile':[],'StartCap':[],'EndCap':[],'FullBeam':[]}
 for i in range(8):
  # Roll a periodic body texture to show forward flow without changing thickness.
  ar=np.roll(ta,i*32,axis=1);ar[:,-1]=ar[:,0]
  t=recolor(Image.fromarray(ar),c);s=pulsate(recolor(start,c),i/8,amount=.12);e=pulsate(recolor(end,c),i/8,amount=.12)
  seq['BodyTile'].append(t);seq['StartCap'].append(s);seq['EndCap'].append(e)
  beam=blank((1152,64));beam.alpha_composite(s,(0,0))
  for k in range(4):beam.alpha_composite(t,(64+256*k,0))
  beam.alpha_composite(e,(1088,0));seq['FullBeam'].append(beam)
 for part,fs in seq.items():
  pivot=(0,.5) if part=='BodyTile' else (.25,.5) if part=='StartCap' else (.75,.5) if part=='EndCap' else (16/1152,.5)
  export(f'SoldierLaser/Stage_{stage:02d}/{part}',fs,100,pivot,note=f'Charge fraction{q:.4f}; color{c}; stage16 is max-damage VISUAL only',preview=False)
 stage_table.append([stage,q,*c,'MAX' if stage==16 else 'charging']);laserpreview+=seq['FullBeam'][:2]
gif(laserpreview,'SoldierLaser_CyanToRed',12,maxsize=(1152,128));sheet(laserpreview[::2],'SoldierLaser_17Stages',cols=1,cell=(1000,95))
gif([Image.open(OUT/f'SoldierLaser/Stage_16/FullBeam/FullBeam_{i+1:03d}.png').convert('RGBA') for i in range(8)],'SoldierLaser_MaxRed',24,maxsize=(1152,128))
with (OUT/'Laser_Charge_Stages.csv').open('w',newline='',encoding='utf-8-sig') as f:
 w=csv.writer(f);w.writerow(['stage','normalized_charge','red','green','blue','state']);w.writerows(stage_table)
(OUT/'Animation_Manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
shutil.copy2(ROOT/'Generated_Sources.json',OUT/'Generation_Prompts.json')
print('BUILD_COMPLETE',sum(m['frames'] for m in manifest),'frames',flush=True)
