from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,csv,zipfile,math
ROOT=Path(__file__).parent;OUT=ROOT/'Deliverables';M=json.loads((ROOT/'Sources.json').read_text())
for d in ['01_Insert_12frames','02_Hold_8frames','03_Withdraw_12frames','Body_KeyPoses','Compatible_3labels','FX_Rims_12frames']:(OUT/d).mkdir(parents=True,exist_ok=True)
R=Image.Resampling;W=2048;C=1024;FLOOR=1585;S=.97
plant=Image.open(M['Plant']['path']).convert('RGBA');ready=Image.open(M['Ready']['path']).convert('RGBA')
def empty():return Image.new('RGBA',(W,W))
def bbox(im):
 return Image.fromarray(np.uint8(np.array(im)[:,:,3]>32)*255).getbbox()
# The crouched dome remains at its reference size; do not stretch crouch to standing height.
dx=round(1132-660*S);dy=FLOOR-round(1138*S)
def place(im):
 out=empty();im=im.resize((round(im.width*S),round(im.height*S)),R.LANCZOS);out.alpha_composite(im,(dx,dy));return out
orig=Image.open('C:/YSJ/SubPF/Assets/99.StoreAsset/Main/Bosses/Boss_Alien/Alien_IdleLeap_ExactOutline/Idle/Alien_Idle_01.png').convert('RGBA')
idle=empty();idle.paste(orig,(397,397))
def deform(im,depth):
 h=im.height;w=im.width;sy=np.arange(h,dtype=float)
 p=np.clip((sy-700)/(1138-700),0,1);smooth=p*p*(3-2*p)
 dest=sy+depth*(1-smooth);inv=np.interp(sy,dest,sy);mesh=[]
 for y in range(0,h,8):
  y1=min(y+8,h);a=float(inv[y]);b=float(inv[min(h-1,y1)])
  mesh.append(((0,y,w,y1),(0,a,0,b,w,b,w,a)))
 return im.transform((w,h),Image.Transform.MESH,mesh,R.BICUBIC)
def buried(amount,glow=0):
 im=deform(plant,80*amount);a=np.array(im);yy,xx=np.mgrid[:im.height,:im.width]
 # Only the two hands disappear below their own ground contact plane; the feet stay intact.
 cut=1180-200*amount
 handregion=((xx>421)&(xx<615))|((xx>679)&(xx<866))
 mask=np.where(handregion,np.clip((cut-yy)/2,0,1),1)
 a[:,:,3]=np.uint8(a[:,:,3]*mask)
 rib=np.exp(-((xx-620)/160)**2-((yy-650)/180)**2)
 strong=np.clip((a[:,:,2].astype(float)-160)/80,0,1)*rib*glow
 a[:,:,:3]=np.uint8(np.clip(a[:,:,:3].astype(float)+strong[:,:,None]*np.array([7,4,12]),0,255))
 return place(Image.fromarray(a))
rim=Image.open(M['Rim']['path']).convert('RGBA')
rim=rim.crop(rim.getchannel('A').getbbox()).resize((205,78),R.LANCZOS)
anchors=[(round(dx+x*S),round(dy+980*S)) for x in [500,785]]
rimlayer=empty()
for x,y in anchors:rimlayer.alpha_composite(rim,(x-102,y-39))
rimlayer.save(OUT/'Trace_WristRims_Aligned.png')
rim.save(OUT/'Trace_SingleRim.png')
keys={'Observe':idle,'Prepare':place(ready),'Contact':place(plant),'Embedded':buried(1),'Withdraw':place(ready),'Idle':idle}
for n,im in keys.items():im.save(OUT/'Body_KeyPoses'/f'Trace_{n}.png',optimize=True)
# Old three-label compatibility keeps hands buried while the remote trace claws emerge.
keys['Observe'].save(OUT/'Compatible_3labels'/'Trace_01_Observe.png')
keys['Prepare'].save(OUT/'Compatible_3labels'/'Trace_02_Invoke.png')
keys['Embedded'].save(OUT/'Compatible_3labels'/'Trace_03_Release.png')
insert=[];hold=[];withdraw=[];rows=[]
for i in range(12):
 if i<5:im=place(deform(ready,6*i));amount=0
 elif i<7:im=buried(0);amount=0
 else:amount=(i-6)/5;im=buried(amount)
 insert.append(im);im.save(OUT/'01_Insert_12frames'/f'Trace_Insert_{i+1:02d}.png',optimize=True)
 rows.append(['Insert',i+1,i/24,amount])
for i in range(8):
 im=buried(1,.5-.5*math.cos(2*math.pi*i/8));hold.append(im)
 im.save(OUT/'02_Hold_8frames'/f'Trace_Hold_{i+1:02d}.png',optimize=True)
 rows.append(['Hold',i+1,i/12,1])
for i in range(12):
 if i<6:amount=1-i/5;im=buried(amount)
 elif i<10:amount=0;im=place(ready)
 else:amount=0;im=idle
 withdraw.append(im);im.save(OUT/'03_Withdraw_12frames'/f'Trace_Withdraw_{i+1:02d}.png',optimize=True)
 rows.append(['Withdraw',i+1,i/24,amount])
for i in range(12):
 a=np.array(rimlayer);a[:,:,3]=np.uint8(a[:,:,3]*min(1,(i+1)/3));Image.fromarray(a).save(OUT/'FX_Rims_12frames'/f'Trace_Rims_{i+1:02d}.png',optimize=True)
with (OUT/'Timeline.csv').open('w',newline='',encoding='utf-8-sig') as f:
 wr=csv.writer(f);wr.writerow(['phase','frame','time_within_phase','buried_amount']);wr.writerows(rows)
# Preview demonstrates modular stages; hold duration here is just an example.
seq=[(idle,0)]*12+[(im,max(0,(i-6)/5)) for i,im in enumerate(insert)]+[(hold[(i//2)%8],1) for i in range(32)]+[(im,max(0,1-i/5)) for i,im in enumerate(withdraw)]+[(idle,0)]*8
preview=[]
for im,opacity in seq:
 bg=Image.new('RGBA',(W,W),(27,30,39))
 bg.alpha_composite(im)
 if opacity:
  a=np.array(rimlayer);a[:,:,3]=np.uint8(a[:,:,3]*opacity);bg.alpha_composite(Image.fromarray(a))
 preview.append(bg.convert('RGB').resize((720,720),R.LANCZOS))
preview[0].save(OUT/'Preview_Trace.gif',save_all=True,append_images=preview[1:],duration=[50 if i%6==5 else 40 for i in range(len(preview))],loop=0,optimize=False)
sheet=Image.new('RGB',(1500,620),(27,30,39));d=ImageDraw.Draw(sheet)
examples=[('Observe',idle,0),('Prepare',place(ready),0),('Contact',place(plant),0),('Insert50',buried(.5),.5),('Embedded',buried(1),1),('HoldGlow',hold[4],1),('Withdraw50',buried(.5),.5),('HandsOut',place(plant),0),('Recover',place(ready),0),('Idle',idle,0)]
for j,(name,im,o) in enumerate(examples):
 x=j%5*300;y=j//5*310;view=im.copy()
 if o:view.alpha_composite(rimlayer)
 sm=view.resize((300,300),R.LANCZOS);sheet.paste(sm,(x,y+10),sm);d.text((x+10,y+5),name,fill='white')
sheet.save(OUT/'ContactSheet.jpg',quality=95)
edges=[]
for p in OUT.rglob('*.png'):
 a=np.array(Image.open(p))[:,:,3]
 if p.name=='Trace_SingleRim.png':continue
 if max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())>0:edges.append(str(p.relative_to(OUT)))
report={'body_canvas':[W,W],'ppu':100,'pivot':[.5,.5],'scale':.43,'insert_frames':12,'insert_fps':24,'hold_frames':8,'hold_fps':12,'withdraw_frames':12,'withdraw_fps':24,'rim_anchors_top_left_pixels':anchors,'edge_clipped_frames':edges,'original_idle_pixel_exact':True,'new_pose_uniform_scale':S,'identity':'Generated new arm poses preserve reference concept, not pixel-exact details. Crouch retains head scale instead of scaling crouched height up to Idle.','unity_tested':False}
(OUT/'Validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(OUT/'Generation_prompts.json').write_text(json.dumps(M,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'사용안내.txt').write_text('Trace 양손 지면 삽입\n\n본체32장:삽입12장24fps(0.5초),유지8장12fps(0.667초 반복),손빼기12장24fps(0.5초).\n손가락이 아래로향하며지면에들어가는부분을프레임마다가림.발은가리지않음.유지중에는손을박은채흉곽발광.손목균열은별도이미지로본체앞에표시.\n본체2048x2048 투명PNG,PPU100,Pivot중앙,Scale0.43,Point,CompressionNone,MaxSize2048이상.원본Idle은397px여백만추가해동일크기.새자세는머리크기를기준으로정렬했으며웅크린전체높이를Idle높이로늘리지않음.세부픽셀까지원본과같지는않음.\nTrace_WristRims_Aligned.png는본체와같은위치/중앙Pivot/PPU100으로표시하면손목위치에맞음.별도Transform위치이동불필요.정렬레이어는본체앞.지면유지동안표시,손빼기종료전에없애기.한개만쓸경우Trace_SingleRim.png와Validation의좌표참고.\n\n현재Trace코드의Observe는위치기록,Invoke는0.6초잠금,Release는순차발톱발생동안유지.Compatible_3labels는이구조의빠른교체용으로Observe=Idle,Invoke=준비,Release=손박은상태.이세장만교체하면삽입/손빼기프레임은재생되지않음.\n완전연출:관찰후Invoke구간에Insert0.5초,남은잠금시간과발톱발생동안Hold반복,발톱소멸뒤Recovery0.6초중Withdraw0.5초재생후Idle복귀.현재코드에는별도Withdraw자세전환이없으므로연결작업필요.\n프레임수에맞게각구간끝에다음자세또는루프첫장키를넣어길이확정.유지반복만Loop켜기.\n현재원격지면발톱이펙트는유지하고,이번패키지는보스본체동작과양손삽입구멍만추가.프로젝트수정없음,Unity실행검증없음.\n내장imagegen새자세2장/균열1장생성+승인된Python정렬,지면가림,프레임구성.프롬프트첨부.\n',encoding='utf-8-sig')
with zipfile.ZipFile(ROOT/'Alien_Trace_GroundHands_v1.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in OUT.rglob('*'):
  if p.is_file():z.write(p,p.relative_to(OUT))
print(json.dumps(report))

