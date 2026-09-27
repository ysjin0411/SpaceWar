from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import numpy as np, json, math, csv, shutil, random
ROOT=Path(__file__).parent;OUT=ROOT/'Deliverables';PRE=OUT/'Previews';PRE.mkdir(parents=True,exist_ok=True)
A=Path('C:/YSJ/SubPF/Assets/99.StoreAsset/Main')
OLD=ROOT.parent/'Player_Remake_Combo_Shield_v1/Deliverables'
R=Image.Resampling;FPS=24;N=84;manifest=[]
def load(p):return Image.open(p).convert('RGBA')
def blank(size):return Image.new('RGBA',size)
def clamp(x):return max(0,min(1,x))
def ease(x):x=clamp(x);return x*x*(3-2*x)
def alpha(im,a):
 ar=np.array(im);ar[:,:,3]=np.uint8(ar[:,:,3].astype(float)*clamp(a));return Image.fromarray(ar)
def tint(im,color,amount):
 ar=np.array(im);ar[:,:,:3]=np.uint8(ar[:,:,:3]*(1-amount)+np.array(color)*amount);return Image.fromarray(ar)
def save(im,p):p.parent.mkdir(parents=True,exist_ok=True);im.save(p,optimize=True)
def padded(im,pad):
 out=blank((im.width+2*pad,im.height+2*pad));out.alpha_composite(im,(pad,pad));return out
def shift(im,x,y):
 out=blank(im.size);out.alpha_composite(im,(round(x),round(y)));return out
def cropalpha(im):return im.crop(im.getchannel('A').getbbox())
def gif(fs,name,fps=24,scale=4):
 images=[]
 for im in fs:
  size=(im.width*scale,im.height*scale) if scale>=1 else (round(im.width*scale),round(im.height*scale))
  im=im.resize(size,R.NEAREST);bg=Image.new('RGB',size,(28,31,40));bg.paste(im,(0,0),im);images.append(bg)
 # GIF milliseconds are quantized to centiseconds. This pattern totals exactly 3.5 s for 84 frames.
 duration=[int(round((i+1)*100/fps)-round(i*100/fps))*10 for i in range(len(fs))]
 images[0].save(PRE/(name+'.gif'),save_all=True,append_images=images[1:],duration=duration,loop=0,optimize=False)
def contact(fs,name,indices=None,cell=(330,240)):
 indices=indices or list(range(len(fs)));cols=4;rows=(len(indices)+cols-1)//cols
 sheet=Image.new('RGB',(cols*cell[0],rows*cell[1]),(28,31,40));d=ImageDraw.Draw(sheet)
 for j,i in enumerate(indices):
  im=fs[i].copy();factor=min((cell[0]-16)/im.width,(cell[1]-34)/im.height);im=im.resize((round(im.width*factor),round(im.height*factor)),R.NEAREST)
  x=j%cols*cell[0];y=j//cols*cell[1];sheet.paste(im,(x+(cell[0]-im.width)//2,y+28+(cell[1]-34-im.height)//2),im);d.text((x+8,y+8),f'{name} {i+1:03d}',fill='white')
 sheet.save(PRE/(name+'.jpg'),quality=96)
def record(name,files,fps,ppu,pivot,notes):
 manifest.append(dict(name=name,frames=len(files),fps=fps,duration=len(files)/fps,ppu=ppu,pivot=pivot,canvas=files[0].size,notes=notes))

gen=json.loads((ROOT/'Generated_Sources.json').read_text())
blast=cropalpha(load(gen['Explosion']['path']))
block=cropalpha(load(gen['Block']['path']))
def burst(dst,center,age,duration,size,cyan=False):
 if not 0<=age<duration:return
 q=age/duration;s=round(size*(.18+.82*ease(min(q*3,1))))
 src=block if cyan else blast
 img=src.resize((max(1,s),max(1,round(s*src.height/src.width))),R.NEAREST)
 img=alpha(img,1-ease((q-.3)/.7));dst.alpha_composite(img,(round(center[0]-img.width/2),round(center[1]-img.height/2)))
def spark(dst,origin,t,seed,color=(255,170,40),amount=7,radius=12):
 d=ImageDraw.Draw(dst);rr=random.Random(seed)
 for i in range(amount):
  angle=rr.uniform(0,math.tau);speed=rr.uniform(.5,1.2);phase=(t*2.6+rr.random())%1
  r=radius*phase*speed;x=origin[0]+math.cos(angle)*r;y=origin[1]+math.sin(angle)*r
  d.line((round(x),round(y),round(x+math.cos(angle)*(1+2*phase)),round(y+math.sin(angle)*(1+2*phase))),fill=(*color,round(255*(1-phase))),width=1)
def smoke(dst,origin,age,duration=1.5,size=16,seed=0):
 if not 0<=age<duration:return
 q=age/duration;d=ImageDraw.Draw(dst);rr=random.Random(seed)
 for k in range(5):
  x=origin[0]+rr.uniform(-.5,.5)*size*(1+q);y=origin[1]-q*size*1.5+rr.uniform(-.3,.3)*size
  r=size*(.15+.22*q)*rr.uniform(.7,1.3);c=rr.choice([(54,58,66),(71,74,80),(88,86,83)])
  d.ellipse((round(x-r),round(y-r),round(x+r),round(y+r)),fill=(*c,round(170*(1-q))))
def split_parts(im,seeds):
 ar=np.array(im);yy,xx=np.mgrid[:im.height,:im.width]
 ds=np.array([(xx-x)**2+(yy-y)**2 for x,y in seeds]);labels=ds.argmin(axis=0);parts=[]
 for j,center in enumerate(seeds):
  a=ar.copy();a[labels!=j]=0;parts.append((Image.fromarray(a),center))
 return parts
def debris(parts,t,start,pad,center,spread=15,gravity=7,fade_start=2.65):
 size=(parts[0][0].width+pad*2,parts[0][0].height+pad*2);out=blank(size)
 for j,(part,(cx,cy)) in enumerate(parts):
  age=max(0,t-start-j*.024);r=math.hypot(cx-center[0],cy-center[1]) or 1
  vx=(cx-center[0])/r*spread*(.7+.15*(j%3));vy=(cy-center[1])/r*spread*.45-4
  layer=padded(part,pad);ang=age*((j%2)*2-1)*(12+j*2)
  layer=layer.rotate(ang,R.NEAREST,center=(cx+pad,cy+pad))
  layer=shift(layer,age*vx,age*vy+gravity*age*age)
  layer=alpha(layer,1-ease((t-fade_start)/(.8)))
  out.alpha_composite(layer)
 return out
def export_boss(name,bodies,fxs,pad,source):
 merged=[]
 for i,(b,fx) in enumerate(zip(bodies,fxs)):
  im=b.copy();im.alpha_composite(fx);merged.append(im)
  for sub,f in [('Combined',im),('Body_Only',b),('FX_Only',fx)]:save(f,OUT/name/sub/f'{name}_{i+1:03d}.png')
 gif(merged,name,24,3 if bodies[0].width>300 else 4)
 contact(merged,name,[0,9,19,29,39,49,59,69,75,79,81,83])
 record(name,merged,24,100,[.5,.5],f'Original pixel scale retained; symmetric padding {pad}px; source {source}')
 return merged

# Dog jaw animation: rigid lower jaw pivots at the original mechanical hinge.
dog=load(A/'Bosses/Boss_Idle/Dog/Doghead_boss1.png');da=np.array(dog)
mask=Image.new('L',dog.size);ImageDraw.Draw(mask).polygon([(0,30),(27,30),(35,24),(43,26),(42,40),(26,45),(0,46)],fill=255)
jaw_a=da.copy();jaw_a[np.array(mask)==0]=0;jaw=Image.fromarray(jaw_a)
upper_a=da.copy();upper_a[np.array(mask)>0]=0;upper=Image.fromarray(upper_a)
charge=[];pad=24
for i in range(16):
 # Close and open continuously, ending just before the loop's first pose.
 openness=(1-math.cos(i/16*math.tau))/2;angle=-19+32*openness
 fr=padded(upper,pad);j=padded(jaw,pad).rotate(angle,R.NEAREST,center=(38+pad,31+pad));fr.alpha_composite(j)
 # Preserve head size and animate only the existing rear exhaust pixels.
 flame=da.copy();flame[:,:84]=0;fl=Image.fromarray(flame)
 fl=tint(fl,(255,188,58),.16*(1+math.sin(i*.9)));fr.alpha_composite(padded(fl,pad))
 save(fr,OUT/'Dog_Charge_Mouth/Frames'/f'Dog_Charge_{i+1:02d}.png');charge.append(fr)
gif(charge,'Dog_Charge_Mouth',24,5);contact(charge,'Dog_Charge_Mouth');record('Dog_Charge_Mouth',charge,24,100,[.5,.5],'16-frame looping jaw motion; 24px symmetric padding; original head does not scale')

# Death sequences share exact original pixels, but use different mechanical disassembly and VFX choreography.
for kind in ['Dog','Head','Cruiser']:
 src=dog if kind=='Dog' else load(A/'Bosses/Boss_Idle/Head/Head_Boss_01.png') if kind=='Head' else load(A/'Bosses/Boss_Cruiser/Body/Boss_Cruiser_Body_Idle_01.png')
 pad=56 if kind!='Cruiser' else 96
 if kind=='Dog':seeds=[(14,11),(44,8),(61,17),(84,14),(14,36),(40,37),(67,37),(86,34)];center=(53,23);start=1.28;spread=16
 elif kind=='Head':seeds=[(15,9),(39,10),(15,29),(39,29),(12,49),(38,51),(25,67),(56,39)];center=(29,35);start=1.35;spread=14
 else:seeds=[(57,47),(58,82),(97,50),(102,79),(140,44),(151,78),(189,45),(195,79),(229,43),(228,84)];center=(147,65);start=1.6;spread=25
 parts=split_parts(src,seeds);bodies=[];fxs=[]
 for i in range(N):
  t=i/FPS;size=(src.width+pad*2,src.height+pad*2);fx=blank(size)
  b=padded(src,pad) if t<start else debris(parts,t,start,pad,center,spread,gravity=5 if kind=='Cruiser' else 7,fade_start=2.55)
  if 0<t<start:
   # Fixed-scale vibration is a small translation, never an expanding boss body.
   b=shift(b,round(math.sin(t*49)*min(2,t*2)),round(math.sin(t*38)*min(1,t)))
  if kind=='Dog':
   if t<start:
    pulse=.5+.5*math.sin(t*23);d=ImageDraw.Draw(fx);d.ellipse((pad+48,pad+11,pad+60,pad+24),fill=(255,68+round(100*pulse),30,round(70+100*pulse)))
    if t>.3:spark(fx,(pad+83,pad+28),t,5,radius=13)
    if t>.65:
     # Jaw loosens before the main breakup, while the upper head stays full scale.
     b=padded(upper,pad);j=padded(jaw,pad).rotate(18*ease((t-.65)/.6),R.NEAREST,center=(pad+38,pad+31));b.alpha_composite(j)
   burst(fx,(pad+84,pad+25),t-.72,.65,30)
   burst(fx,(pad+52,pad+22),t-1.25,.8,74)
   smoke(fx,(pad+53,pad+25),t-1.85,1.5,29,9)
  elif kind=='Head':
   if t<1.65:
    d=ImageDraw.Draw(fx);pulse=.5+.5*math.sin(t*24)
    d.arc((pad+4,pad+1,pad+49,pad+27),180,355,fill=(80,250,255,round(80+175*pulse)),width=2)
    if t>.3:
     points=[(pad+7,pad+20),(pad+20,pad+9),(pad+24,pad+17),(pad+39,pad+4),(pad+48,pad+14)]
     d.line(points,fill=(170,255,255,round(70+180*pulse)),width=1)
     spark(fx,(pad+29,pad+15),t,17,(80,230,255),10,24)
   burst(fx,(pad+27,pad+14),t-1.18,.85,78,True)
   burst(fx,(pad+27,pad+44),t-1.58,.55,39)
   smoke(fx,(pad+30,pad+42),t-1.9,1.5,25,7)
  else:
   if t<1.6:
    for p in [(133,48),(180,76),(93,65)]:
     if t>.3:spark(fx,(pad+p[0],pad+p[1]),t,round(p[0]),radius=13)
   for st,x,y,sz in [(.48,198,45,38),(.78,125,79,42),(1.04,88,60,40),(1.3,220,83,45),(1.58,145,62,100),(1.78,203,60,76)]:burst(fx,(pad+x,pad+y),t-st,.64,sz)
   for k,x in enumerate([90,145,202]):smoke(fx,(pad+x,pad+65),t-1.95-k*.07,1.4,42,k+20)
  if t>start+.15 and t<3.25:spark(fx,(pad+center[0],pad+center[1]),t,33 if kind=='Dog' else 48,(255,163,35),12,40 if kind=='Cruiser' else 27)
  if i>=80:fx=alpha(fx,(83-i)/3)
  if i==0:b=padded(src,pad);fx=blank(size)
  if i==83:b=blank(size);fx=blank(size)
  bodies.append(b);fxs.append(fx)
 export_boss(kind+'_Death_3p5s',bodies,fxs,pad,'original Idle 01')

# Cruiser transition: tower sockets discharge, armour vents lift, core charges, shockwave, settle to original Idle.
src=load(A/'Bosses/Boss_Cruiser/Body/Boss_Cruiser_Body_Idle_01.png');pad=96;size=(448,320);bodies=[];fxs=[]
sa=np.array(src);yy,xx=np.mgrid[:128,:256];ventmask=(yy<46)&(xx>101)&(xx<206)
vent=sa.copy();vent[~ventmask]=0;rest=sa.copy();rest[ventmask]=0
for i in range(N):
 t=i/FPS;b=padded(src,pad);fx=blank(size);d=ImageDraw.Draw(fx)
 if 0<t<3.25:
  openamount=ease((t-.65)/.7)*(1-ease((t-2.65)/.5))
  b=padded(Image.fromarray(rest),pad);b.alpha_composite(shift(padded(Image.fromarray(vent),pad),0,-round(openamount*5)))
  if openamount>0:
   # Warm exposed vent interiors, rendered behind the preserved armour pixels.
   under=blank(size);ud=ImageDraw.Draw(under)
   for x in [112,151,189]:ud.rectangle((pad+x-4,pad+37-round(openamount*4),pad+x+7,pad+47),fill=(255,80,30,round(220*openamount)))
   under.alpha_composite(b);b=under
  for k,x in enumerate([112,151,189]):
   burst(fx,(pad+x,pad+28),t-(.05+k*.13),.42,26)
   smoke(fx,(pad+x,pad+27),t-.2-k*.1,1.1,15,k)
  if .6<t<2.7:
   q=ease((t-.6)/1.45)*(1-ease((t-2.45)/.25));r=3+round(q*5);pulse=.75+.25*math.sin(t*35)
   d.ellipse((pad+74-r,pad+65-r,pad+74+r,pad+65+r),fill=(255,round(100+155*q),round(50+150*q),round(245*pulse)))
   spark(fx,(pad+74,pad+65),t,73,(255,140,30),12,18+q*10)
  # The failing barrier peels away early; full takeover shockwave follows later.
  if t<.85:
   q=t/.85;rx=112+q*13;ry=45+q*10
   for k in range(7):d.arc((pad+145-rx,pad+65-ry,pad+145+rx,pad+65+ry),k*55+int(q*60),k*55+35,fill=(80,200,255,round(170*(1-q))),width=2)
  if 2.05<t<2.85:
   q=(t-2.05)/.8;rx=12+q*125;ry=8+q*60
   d.ellipse((round(pad+125-rx),round(pad+64-ry),round(pad+125+rx),round(pad+64+ry)),outline=(255,130,45,round(245*(1-q))),width=2)
   burst(fx,(pad+74,pad+65),t-2.05,.36,45)
  if 1.2<t<2.6:b=shift(b,round(math.sin(t*50)*1.3),0)
 # The last frames return pixel-for-pixel to Idle to avoid a phase handover size jump.
 if i>=78:b=padded(src,pad);fx=blank(size)
 bodies.append(b);fxs.append(fx)
export_boss('Cruiser_PhaseTransition_3p5s',bodies,fxs,pad,'Boss_Cruiser_Body_Idle_01.png')

# Slash long sword: keep all eight existing body poses and replace only the short blade.
wep=cropalpha(load(OLD/'Warrior/Parts/GiantSword_Original_Weapon.png'))
wep=wep.resize((390,round(wep.height*390/wep.width)),R.NEAREST)
save(wep,OUT/'Slash_Longsword/Parts/Longsword.png')
slash=[];pad=256;size=(1536,1536)
for state in ['Idle','Attack']:
 fs=[]
 for i in range(4):
  src=load(A/f'Character/Warrior/Slash/Warrior_Slash_{state}_{i+1}.png');ar=np.array(src);yy,xx=np.mgrid[:1024,:1024]
  if state=='Idle' or i==3:
   # Only the blade beyond the hand; avoid the original leg and gauntlet.
   m=(xx>=628)&(yy>=625)&(yy<748)&(xx>628+(yy-640)*.29)
   joint=(633,639);angle=-18+[0,1,-1,0][i]
  elif i==0:
   m=(xx<406)&(yy>=402)&(yy<535);joint=(397,405);angle=108
  elif i==1:
   m=(xx>=677)&(yy>=631)&(yy<758);joint=(681,647);angle=-23
  else:
   m=xx>=815;joint=(807,539);angle=12
  ar[m]=0;body=Image.fromarray(ar);fr=padded(body,pad)
  # Align the original sword's held end to each pose's wrist.
  wl=blank(size);wl.alpha_composite(wep,(joint[0]+pad-23,joint[1]+pad-wep.height//2))
  wl=wl.rotate(angle,R.NEAREST,center=(joint[0]+pad,joint[1]+pad))
  # Put the sword behind the unchanged character to keep the fingers over the grip.
  wl.alpha_composite(fr);fr=wl
  save(fr,OUT/f'Slash_Longsword/{state}/Warrior_Slash_{state}_{i+1:02d}.png');fs.append(fr)
 slash+=fs;record('Slash_Longsword/'+state,fs,12,100,[.5,(454+256)/1536],'Original body poses, 256px padding; original short blade replaced by 390px long sword')
gif(slash,'Slash_Longsword',12,.5);contact(slash,'Slash_Longsword',cell=(360,360))

# Shield deployment remains a separate track. Block hit uses a compact impact-point centered track.
deploydir=OUT/'Shield/Deploy_8frames';deploydir.mkdir(parents=True,exist_ok=True)
deploy=[]
for i,p in enumerate(sorted((OLD/'Warrior/Shield/Impact_8frames').glob('*.png'))):
 im=load(p);save(im,deploydir/f'Shield_Deploy_{i+1:02d}.png');deploy.append(im)
record('Shield/Deploy_8frames',deploy,24,100,[.5,.423828125],'Character-aligned deployment wave; trigger only on shield opening')
blocks=[]
for i in range(12):
 fr=blank((512,512));q=i/11
 if i<11:
  s=round(110+300*ease(min(q*3,1)));im=block.resize((round(s*block.width/block.height),s),R.NEAREST)
  # The source burst is not symmetrical: anchor its luminous collision core, not its bbox centre.
  im=alpha(im,1-ease(max(0,(q-.12)/.88)));x=256-round(im.width*.51);y=256-round(im.height*.57)
  fr.alpha_composite(im,(x,y))
 save(fr,OUT/'Shield/BlockHit_12frames'/f'Shield_BlockHit_{i+1:02d}.png');blocks.append(fr)
gif(blocks,'Shield_BlockHit',24,1);record('Shield/BlockHit_12frames',blocks,24,1000,[.5,.5],'Impact-point-centered effect; 0.5 seconds; world prefab scale 1; peak height about 0.41 world units')
contact(blocks,'Shield_BlockHit')
shutil.copy2(ROOT/'Source_Inventory.json',OUT/'Source_Inventory.json')
shutil.copy2(ROOT/'Prefab_References.json',OUT/'Prefab_References.json')
shutil.copy2(ROOT/'Generated_Sources.json',OUT/'Generation_Prompts.json')
(OUT/'Animation_Manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('BUILD_COMPLETE',json.dumps([{x:m[x] for x in ['name','frames','canvas']} for m in manifest]),flush=True)
