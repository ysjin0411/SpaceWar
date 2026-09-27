from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import numpy as np,math,json,csv,zipfile,shutil
ROOT=Path(__file__).parent;OUT=ROOT/'Deliverables'
M=json.loads((ROOT/'Generated_Sources.json').read_text());INV=json.loads((ROOT/'Source_Inventory.json').read_text())
R=Image.Resampling
def folder(p):p.mkdir(parents=True,exist_ok=True);return p
def load(k):
 im=Image.open(M[k]['path']).convert('RGBA');a=np.array(im);a[a[:,:,3]<8]=0;return Image.fromarray(a)
def crop(im):return im.crop(im.getchannel('A').getbbox())
def blank(n):return Image.new('RGBA',(n,n))
def put_scaled(im,size,xy,n=1024):
 out=blank(n);im=im.resize(size,R.NEAREST);out.alpha_composite(im,xy);return out
def save(im,p):p.parent.mkdir(parents=True,exist_ok=True);im.save(p,optimize=True)
def preview(frames,p,ms=120,box=None,size=640):
 imgs=[]
 for fr in frames:
  if box:fr=fr.crop(box)
  fr=fr.copy();fr.thumbnail((size,size),R.NEAREST)
  bg=Image.new('RGB',(size,size),(28,31,40));bg.paste(fr,((size-fr.width)//2,(size-fr.height)//2),fr);imgs.append(bg)
 imgs[0].save(p,save_all=True,append_images=imgs[1:],duration=ms,loop=0,optimize=False)
# Soldier: exactly the same body at576px high (36px in drop-in64 version), same center and feet.
braw=load('SoldierBase');bb=braw.getchannel('A').getbbox();bc=braw.crop(bb)
bs=576/bc.height;bw=round(bc.width*bs);bx=round(29*16-bw/2);by=224
body=put_scaled(bc,(bw,576),(bx,by))
# Keep an independent front-hand layer so the gun is visibly gripped rather than pasted over the fists.
ha=np.array(braw);yy,xx=np.mgrid[:braw.height,:braw.width]
handmask=(xx>675)&(yy>480)&(yy<635);ha[~handmask]=0
handraw=Image.fromarray(ha).crop(bb);hands=put_scaled(handraw,(bw,576),(bx,by))
# Distinct action silhouettes while keeping helmet, torso and legs invariant.
gun_specs={'Bullet':(470,440,407),'Lazer':(470,440,395),'Missile':(500,418,380),'Sniper':(500,428,408)}
soldier={};body_files=[];maprows=[]
for weapon in ['Bullet','Lazer','Missile','Grenade','Sniper']:
 parts=folder(OUT/'Soldier'/'Parts')
 g=crop(load(weapon));save(g,parts/(weapon+'.png'))
 seqs={}
 for state in ['Idle','Attack']:
  seq=[]
  for i in range(4):
   fr=body.copy()
   # Breathing changes chest highlights only; root, head scale and foot baseline never change.
   ba=np.array(fr);Y,X=np.mgrid[:1024,:1024]
   region=(X>bx+80)&(X<bx+bw-50)&(Y>380)&(Y<550)&(ba[:,:,3]>0)
   delta=[0,3,6,3][i] if state=='Idle' else 0
   ba[:,:,:3][region]=np.uint8(np.clip(ba[:,:,:3][region].astype(int)+delta,0,255));fr=Image.fromarray(ba)
   if weapon!='Grenade':
    width,gx,gy=gun_specs[weapon];height=round(g.height*width/g.width)
    recoil=([0,0,0,0] if state=='Idle' else [0,-18,-9,0])[i]
    gun=g.resize((width,height),R.NEAREST)
    # Slight weapon kick without scaling the actor.
    layer=blank(1024);layer.alpha_composite(gun,(gx+recoil,gy))
    if state=='Attack' and i==1:layer=layer.rotate(2 if weapon!='Lazer' else 0,R.NEAREST,center=(620,500))
    fr.alpha_composite(layer);fr.alpha_composite(hands)
   else:
    # Articulate the free forearm about its elbow for the overarm throw.
    arm=blank(1024);aa=np.array(fr)
    region=(X>=575)&(Y>=420)&(Y<555)
    ar=aa.copy();ar[~region]=0;arm=Image.fromarray(ar)
    aa[region]=0;fr=Image.fromarray(aa)
    angle=([0,2,0,-2] if state=='Idle' else [65,28,-12,0])[i]
    arm=arm.rotate(angle,R.NEAREST,center=(577,495));fr.alpha_composite(arm)
    # The thrown projectile is not baked into the release frame.
    if state=='Idle' or i<2 or i==3:
     gr=g.resize((38,65),R.NEAREST);gl=blank(1024);gl.alpha_composite(gr,(627,452))
     gl=gl.rotate(angle,R.NEAREST,center=(577,495));fr.alpha_composite(gl)
   # Match the original actor's horizontal foot center, without changing the canvas pivot.
   aligned=blank(1024);aligned.alpha_composite(fr,(-96,0));fr=aligned
   name=f'Soldier_{weapon}_{state}_{i+1:02d}.png'
   hd=OUT/'Soldier'/'HD_1024'/weapon/state/name;lo=OUT/'Soldier'/'DropIn_64'/weapon/state/name
   save(fr,hd);save(fr.resize((64,64),R.NEAREST),lo);seq.append(fr);body_files.append(hd)
   maprows.append(['Soldier',weapon,state,i+1,str(lo.relative_to(OUT)),100,.5,.5,'64x64'])
  seqs[state]=seq
 soldier[weapon]=seqs
 folder(OUT/'Previews')
 preview(seqs['Idle']+seqs['Attack'],OUT/'Previews'/f'Soldier_{weapon}.gif',150)
aligned_body=blank(1024);aligned_body.alpha_composite(body,(-96,0))
save(aligned_body,OUT/'Soldier'/'Parts'/'Body_Fixed_1024.png')
# Warrior: use original attack2 actor pixels and original rigid weapons; all actor pixels are shared across combos.
W=3072;PIVOT=(.5,.423828125)
sword_src=Image.open(INV['Warrior_GiantSword'][5]['path']).convert('RGBA')
a=np.array(sword_src);Y,X=np.mgrid[:W,:W]
# Wrist joint at right edge of fist. Source weapon begins just beyond this joint.
wrist=(1705,1814)
bodya=a.copy();bodya[:,1710:]=0
warbody=Image.fromarray(bodya)
parts=folder(OUT/'Warrior'/'Parts');save(warbody,parts/'Original_Body_Fixed.png')
weapons={}
for name,inv in [('GiantSword','Warrior_GiantSword'),('Hammer','Warrior_GiantHammer')]:
 src=Image.open(INV[inv][5]['path']).convert('RGBA')
 ar=np.array(src);ar[:,:1703]=0
 wep=Image.fromarray(ar)
 weapons[name]=wep;save(wep,parts/(name+'_Original_Weapon.png'))
# Weapon angles, degrees counterclockwise from original forward horizontal.
angles={2:[-38,5,68,36],3:[102,48,-58,65]}
combos={};fxseqs={}
for weapon in weapons:
 combos[weapon]={}
 # Include original first attack for comparison/reference without overwriting user assets.
 inv='Warrior_GiantSword' if weapon=='GiantSword' else 'Warrior_GiantHammer'
 for i in range(4):save(Image.open(INV[inv][i+4]['path']).convert('RGBA'),OUT/'Warrior'/weapon/'Attack1_Existing'/f'{weapon}_Attack1_{i+1:02d}.png')
 for stage in [2,3]:
  frames=[];fxframes=[]
  for i,angle in enumerate(angles[stage]):
   fr=warbody.copy()
   wr=weapons[weapon].rotate(angle,R.NEAREST,center=wrist)
   # Draw original wrist/hand over the handle junction.
   fr.alpha_composite(wr)
   save(fr,OUT/'Warrior'/weapon/f'Attack{stage}'/f'{weapon}_Attack{stage}_{i+1:02d}.png')
   frames.append(fr)
   maprows.append(['Warrior',weapon,f'Attack{stage}',i+1,f'Warrior/{weapon}/Attack{stage}/{weapon}_Attack{stage}_{i+1:02d}.png',100,*PIVOT,'3072x3072'])
   # Trail geometry follows the actual weapon sweep; separate from character and collider sprite.
   fx=blank(W)
   prev=angles[stage][max(0,i-1)]
   for k in range(1,6):
    ang=prev+(angle-prev)*k/6
    wa=np.array(weapons[weapon].rotate(ang,R.NEAREST,center=wrist))
    # Exclude grip from trail. The visible blade/head alone leaves the afterimage.
    dist=np.sqrt((X-wrist[0])**2+(Y-wrist[1])**2)
    al=np.uint8(wa[:,:,3].astype(float)*(k/6)*.22*(dist>230))
    col=np.zeros_like(wa);col[:,:,0]=255;col[:,:,1]=125+15*k;col[:,:,2]=35;col[:,:,3]=al
    fx.alpha_composite(Image.fromarray(col))
   if i in [0,3]:fx=blank(W)
   save(fx,OUT/'Warrior'/weapon/f'Trails_Attack{stage}'/f'{weapon}_Trail{stage}_{i+1:02d}.png');fxframes.append(fx)
  combos[weapon][stage]=frames;fxseqs[(weapon,stage)]=fxframes
 # First attack trail from its own source weapon region would share whole body if unmasked; use explicit angles of original swing.
 fxframes=[]
 for i,ang in enumerate([130,0,-45,65]):
  fx=blank(W)
  if i in [1,2]:
   for k in range(5):
    wa=np.array(weapons[weapon].rotate(ang+12*k,R.NEAREST,center=wrist))
    dist=np.sqrt((X-wrist[0])**2+(Y-wrist[1])**2)
    wa[:,:,3]=np.uint8(wa[:,:,3]*.12*(dist>230));wa[:,:,:3]=[255,166,45]
    fx.alpha_composite(Image.fromarray(wa))
  save(fx,OUT/'Warrior'/weapon/'Trails_Attack1'/f'{weapon}_Trail1_{i+1:02d}.png');fxframes.append(fx)
 fxseqs[(weapon,1)]=fxframes
 allf=[]
 for st in [1,2,3]:
  fs=[Image.open(INV[inv][i+4]['path']).convert('RGBA') for i in range(4)] if st==1 else combos[weapon][st]
  for i,fr in enumerate(fs):
   fr=fr.copy();fr.alpha_composite(fxseqs[(weapon,st)][i]);allf.append(fr)
 preview(allf,OUT/'Previews'/f'Warrior_{weapon}_Combo123.gif',140,box=(450,400,3000,2800))
# Shield: same original warrior body, armor shield expands about a fixed held grip.
shield=crop(load('Shield'));save(shield,parts/'Shield_New.png')
shieldfx=crop(load('ShieldFX'));save(shieldfx,parts/'Shield_Impact_New.png')
shieldseq={}
for state,heights in [('Idle',[240,242,244,242]),('Attack',[280,440,740,380])]:
 fs=[]
 for i,height in enumerate(heights):
  sh=shield.resize((round(shield.width*height/shield.height),height),R.NEAREST)
  # Grip lies one-third of the way into the shield, at the same wrist for every size.
  x=round(wrist[0]-sh.width*.17);y=round(wrist[1]-height*.47)
  fr=warbody.copy();fr.alpha_composite(sh,(x,y))
  save(fr,OUT/'Warrior'/'Shield'/state/f'Warrior_Shield_{state}_{i+1:02d}.png');fs.append(fr)
 shieldseq[state]=fs
# Guard sprite is kept separate for existing Shield._guardSprite slot.
save(shieldseq['Attack'][2],OUT/'Warrior'/'Shield'/'Warrior_Shield_Guard.png')
sfx=[]
for i in range(8):
 fx=blank(W)
 if i<7:
  height=round(400+70*i);width=round(shieldfx.width*height/shieldfx.height)
  im=shieldfx.resize((width,height),R.NEAREST);aa=np.array(im);aa[:,:,3]=np.uint8(aa[:,:,3]*max(0,1-max(0,i-2)/5))
  fx.alpha_composite(Image.fromarray(aa),(1960+12*i,round(wrist[1]-height/2)))
 save(fx,OUT/'Warrior'/'Shield'/'Impact_8frames'/f'Shield_Impact_{i+1:02d}.png');sfx.append(fx)
prev=[]
for im in shieldseq['Idle']:prev.append(im)
for i,im in enumerate(shieldseq['Attack'][:3]):
 fr=im.copy();fr.alpha_composite(sfx[i]);prev.append(fr)
for i in range(3,8):
 fr=shieldseq['Attack'][2].copy();fr.alpha_composite(sfx[i]);prev.append(fr)
prev+=[shieldseq['Attack'][3],shieldseq['Idle'][0]]
preview(prev,OUT/'Previews'/'Warrior_Shield_Expand.gif',120,box=(1250,1300,2650,2300))
# Contact sheets use fixed windows, so comparisons do not secretly resize bodies per-frame.
sheet=Image.new('RGB',(1600,1000),(28,31,40));d=ImageDraw.Draw(sheet)
for row,weapon in enumerate(soldier):
 for j,im in enumerate(soldier[weapon]['Idle']+soldier[weapon]['Attack']):
  sm=im.resize((190,190),R.NEAREST);sheet.paste(sm,(j*200,row*200+10),sm)
  d.text((j*200+4,row*200+2),weapon+' '+('I' if j<4 else 'A')+str(j%4+1),fill='white')
sheet.save(OUT/'Previews'/'Soldier_40frames.jpg',quality=96)
sheet=Image.new('RGB',(1400,1400),(28,31,40));d=ImageDraw.Draw(sheet)
for row,(weapon,stage) in enumerate([('GiantSword',2),('GiantSword',3),('Hammer',2),('Hammer',3)]):
 for j,im in enumerate(combos[weapon][stage]):
  sm=im.resize((340,340),R.NEAREST);sheet.paste(sm,(j*350,row*350+10),sm);d.text((j*350+5,row*350+3),f'{weapon} {stage}-{j+1}',fill='white')
sheet.save(OUT/'Previews'/'Warrior_Combo16.jpg',quality=96)
with (OUT/'Import_Mapping.csv').open('w',newline='',encoding='utf-8-sig') as f:
 wr=csv.writer(f);wr.writerow(['character','weapon','state','frame','file','ppu','pivot_x','pivot_y','canvas']);wr.writerows(maprows)
shutil.copy2(ROOT/'Source_Inventory.json',OUT/'Source_Inventory.json')
shutil.copy2(ROOT/'Generated_Sources.json',OUT/'Generation_Prompts.json')
print('BUILD_COMPLETE',flush=True)
