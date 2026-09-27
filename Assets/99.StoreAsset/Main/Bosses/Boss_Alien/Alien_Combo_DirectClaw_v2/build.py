from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,csv,math,zipfile
ROOT=Path(__file__).parent;OUT=ROOT/'Deliverables'
for d in ['Body_KeyPoses','Combo_69frames_20fps','Chase_8frames','FX_SlashA_9frames','FX_SlashB_9frames','FX_Slam_12frames','FX_Static']:
 (OUT/d).mkdir(parents=True,exist_ok=True)
M=json.loads((ROOT/'Sources.json').read_text())
BASE=Path('C:/YSJ/SubPF/Assets/99.StoreAsset/Main/Bosses/Boss_Alien')
SRC=BASE/'Alien_Phase2_Sprites/02_Combo'
R=Image.Resampling;W=2048;C=1024;PAD=397;FLOOR=1585
def empty(size=(W,W)):return Image.new('RGBA',size)
def visiblebox(im):
 a=np.array(im)[:,:,3];return Image.fromarray(np.uint8(a>32)*255).getbbox()
def padded(im):
 out=empty();out.paste(im,(PAD,PAD));return out
keys={'Idle':padded(Image.open(BASE/'Alien_IdleLeap_ExactOutline/Idle/Alien_Idle_01.png').convert('RGBA'))}
for p in sorted(SRC.glob('Combo_*.png')):
 name=p.stem.split('_',2)[2];keys[name]=padded(Image.open(p).convert('RGBA'))
transforms={}
for name,src,pelvis in [('ReachA','Afixed',770),('UppercutB','B',780),('SlamMid','SlamMid',770),('ChaseContact','RunA',720),('ChasePassing','RunB',735)]:
 im=Image.open(M[src]['path']).convert('RGBA');box=visiblebox(im)
 # Normalize the visible standing body height to original Idle's 1076px reference.
 scale=1076/(box[3]-box[1])
 im=im.resize((round(im.width*scale),round(im.height*scale)),R.LANCZOS)
 b=visiblebox(im);dx=round(1132-pelvis*scale);dy=FLOOR-(b[3]-1)
 out=empty();out.alpha_composite(im,(dx,dy));keys[name]=out
 transforms[name]={'source':src,'uniform_scale':scale,'translation':[dx,dy],'reference_height_pixels':1076,'visible_bbox':visiblebox(out)}
for n,im in keys.items():im.save(OUT/'Body_KeyPoses'/f'Alien_Combo_{n}.png',optimize=True)
def shift(im,y):
 out=empty();out.alpha_composite(im,(0,round(y)));return out
chase=[]
for i in range(8):
 name='ChaseContact' if i<4 else 'ChasePassing'
 im=shift(keys[name],-10*math.sin(math.pi*(i%4)/4))
 im.save(OUT/'Chase_8frames'/f'Alien_Chase_{i+1:02d}.png',optimize=True);chase.append(im)
# Match current configuration exactly at20fps; holds are intentional, not unique redrawn poses.
seq=[];events=[]
def add(names,phase,strike):
 for n in names:seq.append((n,phase,strike))
add(['PrepareA']*11,'AimLock',1)
events.append(len(seq))
add(['ReachA','SlashA','SlashA'],'Attack',1)
add(['RecoverA']*5,'Recover',1)
add(['PrepareB']*9,'AimLock',2)
events.append(len(seq))
add(['SlashB','UppercutB','UppercutB'],'Attack',2)
add(['RecoverB']*5,'Recover',2)
add(['PrepareSlam']*15,'AimLock',3)
events.append(len(seq))
add(['SlamMid','Slam','Slam','Slam'],'Attack',3)
add(['RecoverSlam']*10+['RecoverB']*2+['Idle']*2,'Recover',3)
rows=[];body=[]
for i,(n,phase,strike) in enumerate(seq):
 im=keys[n];fn=f'Alien_Combo_{i+1:03d}.png';im.save(OUT/'Combo_69frames_20fps'/fn,optimize=True);body.append(im)
 rows.append(dict(frame=i+1,time_seconds=i/20,pose=n,phase=phase,strike=strike,start_attack=i in events,file=fn))
with (OUT/'Combo_Timeline.csv').open('w',newline='',encoding='utf-8-sig') as f:
 wr=csv.DictWriter(f,fieldnames=rows[0]);wr.writeheader();wr.writerows(rows)
fxa=Image.open(M['FXSlash']['path']).convert('RGBA')
# Uniformly add margin before rotating the uppercut trail so its tips remain contained.
fxbase=Image.new('RGBA',(1536,1536));fxbase.alpha_composite(fxa,(141,141))
fxb=fxbase.rotate(-38,R.BICUBIC,center=(768,768))
fxa=fxbase
fxc=Image.new('RGBA',(1536,1536));fxc.alpha_composite(Image.open(M['FXSlam']['path']).convert('RGBA'),(141,141))
fxall={}
for name,im,N,vertical in [('SlashA',fxa,9,False),('SlashB',fxb,9,False),('Slam',fxc,12,True)]:
 im.save(OUT/'FX_Static'/f'Claw_{name}.png')
 arr=np.array(im);hh,ww=arr.shape[:2];yy,xx=np.mgrid[:hh,:ww]
 fxs=[]
 for j in range(N):
  progress=min(1,(j+1)/3);fade=1 if j<3 else max(0,1-(j-3)/(N-4))
  dist=yy/hh if vertical else 1-xx/ww
  mask=np.clip((progress*1.2-dist)/.16,0,1)*fade
  a=arr.copy();a[:,:,3]=np.uint8(a[:,:,3]*mask);fr=Image.fromarray(a)
  fr.save(OUT/f'FX_{name}_{N}frames'/f'Claw_{name}_{j+1:02d}.png',optimize=True);fxs.append(fr)
 fxall[name]=fxs
# Informational socket positions in delivered2048 canvas, origin top-left.
sockets={'ReachA':[380,950],'SlashA':[590,1190],'UppercutB':[500,700],'SlashB':[660,1080],'SlamMid':[640,1150],'Slam':[620,1490]}
(OUT/'HandAnchors.json').write_text(json.dumps({'coordinate_system':'top-left pixels on2048x2048 body canvas; approximate visual anchors, verify in Unity','anchors':sockets},indent=2),encoding='utf-8')
# Preview only: approach and short steps during preparation; lock movement for the actual swing.
preview=[];S=.36;floor=825
for i in range(20+len(body)):
 bg=Image.new('RGBA',(1920,1080),(24,27,36));d=ImageDraw.Draw(bg)
 d.line((70,floor,1850,floor),fill=(58,62,80),width=2)
 if i<20:
  im=chase[(i//2)%8];rx=1450-350*i/19;bi=-1;n='Chase';phase='Chase'
 else:
  bi=i-20;n,phase,strike=seq[bi];im=body[bi]
  # Face and move LEFT throughout; move only in preparation intervals.
  if bi<19:advance=70*min(1,bi/10)
  elif bi<36:advance=70+65*min(1,(bi-19)/8)
  else:advance=135+55*min(1,(bi-36)/14)
  rx=1100-advance
 ry=floor-(FLOOR-C)*S
 tx=565
 d.ellipse((tx-24,floor-80,tx+24,floor-32),outline=(150,206,225),width=3)
 d.text((tx-28,floor-105),'TARGET',fill=(160,207,223))
 sm=im.resize((round(W*S),round(W*S)),R.LANCZOS)
 bg.alpha_composite(sm,(round(rx-C*S),round(ry-C*S)))
 if bi>=0 and phase=='Attack':
  k=strike-1;j=bi-events[k];ename=['SlashA','SlashB','Slam'][k]
  fi=min(len(fxall[ename])-1,j*3+1)
  f=fxall[ename][fi].resize((470,470),R.LANCZOS)
  hx,hy=sockets.get(n,[620,1200]);ax=rx+(hx-C)*S;ay=ry+(hy-C)*S
  bg.alpha_composite(f,(round(ax-290),round(ay-235)))
 preview.append(bg.convert('RGB').resize((960,540),R.LANCZOS))
preview[0].save(OUT/'Preview_Combo.gif',save_all=True,append_images=preview[1:],duration=50,loop=0,optimize=False)
names=['Idle','ChaseContact','ChasePassing','PrepareA','ReachA','SlashA','RecoverA','PrepareB','SlashB','UppercutB','RecoverB','PrepareSlam','SlamMid','Slam','RecoverSlam']
sheet=Image.new('RGB',(1500,960),(24,27,36));d=ImageDraw.Draw(sheet)
for j,n in enumerate(names):
 x=j%5*300;y=j//5*320;sm=keys[n].resize((300,300),R.LANCZOS);sheet.paste(sm,(x,y+20),sm);d.text((x+10,y+5),n,fill='white')
sheet.save(OUT/'Body_ContactSheet.jpg',quality=95)
fs=Image.new('RGB',(1200,420),(24,27,36));d=ImageDraw.Draw(fs)
for j,(n,im) in enumerate([('SlashA',fxa),('SlashB',fxb),('Slam',fxc)]):
 sm=im.resize((400,400),R.LANCZOS);fs.paste(sm,(j*400,20),sm);d.text((j*400+10,5),n,fill='white')
fs.save(OUT/'FX_ContactSheet.jpg',quality=95)
edges=[]
for p in OUT.rglob('*.png'):
 a=np.array(Image.open(p))[:,:,3]
 if max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())>0:edges.append(str(p.relative_to(OUT)))
report={'body_key_poses':len(keys),'new_body_key_poses':5,'reused_original_combo_poses':9,'original_idle':1,'combo_timed_frames':len(body),'combo_fps':20,'combo_duration':len(body)/20,'chase_frames':8,'fx_frames':30,'body_canvas':[W,W],'ppu':100,'scale':.43,'padding_from1254':397,'ground_baseline_y':FLOOR,'new_body_normalization':transforms,'frames_touching_edge':edges,'attack_start_seconds':[i/20 for i in events],'alpha':'RGBA transparent','identity':'Generated new poses reference original, but fine details are not pixel-identical. Existing nine poses and Idle retain original pixels.','unity_tested':False}
(OUT/'Validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'Generation_prompts.json').write_text(json.dumps(M,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'사용안내.txt').write_text('에일리언 직접 할퀴기 콤보 v2\n\n구성\nBody_KeyPoses:15장. 기존9개 준비/공격/회수 자세와 원본Idle을 보존하고, 손을 길게 뻗는 ReachA, 반대손 올려치기 UppercutB, 내려찍기 중간 SlamMid, 추적자세2개를 새로 추가.\nCombo_69frames_20fps:현재설정 3.45초에 맞춘69개 재생프레임. 홀드 프레임을 포함하며69개를 모두 새로 그린 것은 아님.\nChase_8frames:2개 발걸음 자세를 기반으로 상하 움직임을 넣은8장,16fps 사용시0.5초 반복. 완전한 다방향 달리기 애니메이션은 아님.\nFX_SlashA/B:각9장,60fps=0.15초. FX_Slam:12장,60fps=0.2초. 마지막은투명.\nFX_Static:한장씩 사용하는대표이펙트3장.\n\n크기\n본체2048x2048 투명PNG. 원본1254이미지는397px여백만 추가해 원본 픽셀과크기 보존. 새5개자세는 원본Idle 가시높이1076px와 바닥선y1585에 맞춰정렬. 팔을높이들거나 몸을낮추는 기존자세는 동작상 높이가달라짐. 생성된 새자세의 세부무늬/해부학적비율까지 원본과 완전히동일하지는 않음.\nPPU100, PivotCenter(0.5,0.5), Scale0.43, SpriteModeSingle, FilterPoint, CompressionNone, MaxSize2048이상, AlphaIsTransparency켜기, MipMaps끄기.\n다른크기캔버스와혼용해도 원본중심에투명여백만 더한 파일은PPU와중앙Pivot유지시 원본표시크기동일.\n\n시간\n1타 준비0.55/공격0.15/회수0.25초\n2타 준비0.45/공격0.15/회수0.25초\n3타 준비0.75/공격0.2/회수0.7초\n공격시작:0.55,1.4,2.55초. 추적시간은별도.\n전체69프레임20fps로재생시 마지막68/20초 프레임다음 3.45초에Idle키를추가하면끝시간정확.\n\n현재코드와차이\nBossAlienComboPattern은 처음에만추적하며 각공격중에는제자리. 경고위치를고정한뒤 그위치에효과/히트박스를생성함. 새스프라이트를넣는것만으로 이동/손추적이 자동구현되지는않음.\n요청연출적용시: 접근/다음타준비동안플레이어추적 -> 잠금시방향확정 -> 손공격프레임과발톱효과/피해를동기화 -> 회수. 공격중방향을계속갱신하면회피한플레이어를따라때리므로 기존방향고정시점은유지.\n기존9개라벨외에중간자세를재생할프레임연결이필요. 이펙트는지면경고전체크기로늘리지말고손앞공중에배치. HandAnchors.json은시각적기준점이며Unity에서확인필요.\n본체기본방향왼쪽. 오른쪽공격시본체flipX와효과위치/방향도함께반전. 현재Effect는+X기준 angle회전이므로 이번왼쪽기준효과를기존슬롯에그대로넣으면방향불일치할수있음.\n미리보기의목표표식/이동은연출예시. 프로젝트와코드수정없음,Unity실행검증없음.\n제작:내장imagegen으로새자세/효과생성,승인된Python정렬/회전/프레임구성.프롬프트첨부.\n',encoding='utf-8-sig')
with zipfile.ZipFile(ROOT/'Alien_Combo_DirectClaw_v2.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in OUT.rglob('*'):
  if p.is_file():z.write(p,p.relative_to(OUT))
print(json.dumps(report,ensure_ascii=False))

