from pathlib import Path
import json,csv,math,shutil,zipfile
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).parent
OLD=ROOT.parent/'Alien_Leap_1p5s_v1'
OUT=ROOT/'Deliverables';OUT.mkdir(exist_ok=True)
BD=OUT/'Body_36frames';BD.mkdir(exist_ok=True)
KD=OUT/'KeyPoses';KD.mkdir(exist_ok=True)
M=json.loads((ROOT/'sources.json').read_text())
O=json.loads((OLD/'sources.json').read_text())
R=Image.Resampling;W=2048;C=1024;P=(1132,1147)
def blank():return Image.new('RGBA',(W,W))
def pose(path,scale,anchor,target=P):
 im=Image.open(path).convert('RGBA')
 im=im.resize((round(im.width*scale),round(im.height*scale)),R.LANCZOS)
 out=blank();out.alpha_composite(im,(round(target[0]-anchor[0]*scale),round(target[1]-anchor[1]*scale)))
 return out
idle=blank()
orig=Image.open('C:/YSJ/SubPF/Assets/99.StoreAsset/Main/Bosses/Boss_Alien/Alien_IdleLeap_ExactOutline/Idle/Alien_Idle_01.png').convert('RGBA')
idle.paste(orig,(397,397))
keys={'Idle':idle}
# Align torso landmarks instead of centering each frame on its lowest foot.
for name in ['Crouch','Takeoff','Fold','Tuck']:
 oldname={'Crouch':'crouch','Takeoff':'launch','Fold':'fold','Tuck':'tuck'}[name]
 src=Image.open(OLD/'Deliverables'/'KeyPoses'/f'Alien_Leap_{name}.png')
 out=blank();out.paste(src,(256,256));keys[name]=out
keys['HighKick']=pose(M['high']['path'],1.24,(675,790))
keys['Sweep']=pose(M['sweep']['path'],1.24,(785,690))
keys['DownKick']=pose(M['down']['path'],1.24,(780,635))
keys['Impact']=pose(M['impact']['path'],1.04,(625,790),(1132,1220))
for name,im in keys.items():im.save(KD/f'Alien_{name}.png')
def shift(im,dy):
 out=blank();out.alpha_composite(im,(0,round(dy)));return out
frames=[];rows=[]
for i in range(36):
 angle=0
 if i==0:n='Idle';im=keys[n]
 elif i<5:n='Crouch';im=shift(keys[n],(i-1)*9)
 elif i<7:n='Takeoff';im=keys[n]
 elif i==7:n='Fold';im=keys[n]
 elif i<20:
  n='Tuck';angle=(i-8)*30;im=keys[n].rotate(angle,R.BICUBIC,center=(1024,1024))
 elif i==20:n='Tuck';angle=360;im=keys[n]
 elif i<=23:n='HighKick';im=keys[n].rotate((23-i)*-3,R.BICUBIC,center=P)
 elif i==24:n='Sweep';im=keys[n]
 elif i<=26:n='DownKick';im=keys[n].rotate((26-i)*-5,R.BICUBIC,center=P)
 elif i<=30:n='Impact';im=shift(keys[n],12*math.sin((i-27)*math.pi/4))
 elif i<=33:n='Crouch';im=keys[n]
 else:n='Idle';im=keys[n]
 fn=f'Alien_AxeKick_{i+1:03d}.png';im.save(BD/fn,optimize=True);frames.append(im)
 rows.append(dict(frame=i+1,time_seconds=round(i/24,6),pose=n,rotation_degrees=angle,impact_event=i==27,file=fn))
with (OUT/'Timeline.csv').open('w',newline='',encoding='utf-8-sig') as f:
 w=csv.DictWriter(f,fieldnames=rows[0]);w.writeheader();w.writerows(rows)
for name in ['GroundCrack_12frames','ScreenCrack_12frames']:
 shutil.copytree(OLD/'Deliverables'/name,OUT/name,dirs_exist_ok=True)
for name in ['GroundCrack_Static.png','ScreenCrack_Static.png']:shutil.copy2(OLD/'Deliverables'/name,OUT/name)
# 1920x1080 preview: face LEFT and travel RIGHT -> LEFT, never reverse root movement.
preview=[];scale=.34
# Impact contact landmark is on the attacking front foot.
fx=1132+(315-625)*1.04;fy=1220+(1139-790)*1.04
targetx=580;groundy=865
endx=targetx-(fx-C)*scale
endY=groundy-(fy-C)*scale
for i in range(42):
 bg=Image.new('RGBA',(1920,1080),(23,26,35,255));d=ImageDraw.Draw(bg)
 d.line((70,groundy,1850,groundy),fill=(60,63,80),width=2)
 p=max(0,min(1,(i-6)/21))
 rx=1390+(endx-1390)*p
 ry=endY-300*math.sin(math.pi*p)
 if i<27:d.ellipse((targetx-120,groundy-35,targetx+120,groundy+35),outline=(203,84,255),width=4)
 fi=i-27
 if 0<=fi<12:
  g=Image.open(OUT/'GroundCrack_12frames'/f'Alien_GroundCrack_{fi+1:03d}.png').resize((520,520),R.LANCZOS)
  bg.alpha_composite(g,(targetx-260,groundy-260))
 im=frames[min(i,35)].resize((round(W*scale),round(W*scale)),R.LANCZOS)
 bg.alpha_composite(im,(round(rx-C*scale),round(ry-C*scale)))
 if 0<=fi<12:
  s=Image.open(OUT/'ScreenCrack_12frames'/f'Alien_ScreenCrack_{fi+1:03d}.png')
  bg.alpha_composite(s,(targetx-960,groundy-540))
 preview.append(bg.convert('RGB').resize((960,540),R.LANCZOS))
dur=[50 if i%6==5 else 40 for i in range(42)]
preview[0].save(OUT/'Preview_AxeKick.gif',save_all=True,append_images=preview[1:],duration=dur,loop=0,optimize=False)
sheet=Image.new('RGB',(1600,900),(23,26,35));d=ImageDraw.Draw(sheet)
idx=[0,4,6,7,8,11,14,17,20,21,23,24,25,26,27,29,31,33,34,35]
for j,i in enumerate(idx):
 x=j%5*320;y=j//5*225
 im=frames[i].resize((220,220),R.LANCZOS);sheet.paste(im,(x+50,y+5),im)
 d.text((x+8,y+8),f'{i+1:02d} {rows[i]["pose"]}',fill='white')
sheet.save(OUT/'ContactSheet.jpg',quality=95)
edges=[]
for i,im in enumerate(frames):
 a=np.array(im)[:,:,3]
 if max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())>0:edges.append(i+1)
report=dict(body_frames=36,fps=24,duration_seconds=1.5,canvas=[W,W],transparent_RGBA=True,edge_clipped_frames=edges,idle_pixel_exact=np.array_equal(np.array(frames[-1].crop((397,397,1651,1651))),np.array(orig)),ppu=100,scale=.43,impact_frame=28,impact_time_seconds=1.125,unity_tested=False,detail_identity='Generated action poses are not pixel-identical to original. Head-size normalization is approximate.')
(OUT/'Validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(OUT/'Generation_prompts.json').write_text(json.dumps(M,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'사용안내.txt').write_text('예측 도약 내려차기 v2\n\n36장 / 24fps / 1.5초 / Loop Time 끄기\n순서: 웅크리기 > 도약 > 무릎 감싸기 > 전방 회전 > 머리 위로 다리 올리기 > 전방 휘두르기 > 내려차기 > 낮은 착지 > 복귀\n타격: 28번 프레임, 1.125초. 균열 효과도 이때 시작.\n\nPNG: RGBA 투명, 2048x2048, PPU100, Pivot Center(0.5,0.5), Point Filter, Compression None, Max Size2048 이상.\n보스 Scale 0.43. 원본 Idle은 확대축소 없이 중앙에 여백만 추가. 새 공격 자세는 머리 크기를 참고해 정렬했으며 원본과 세부 픽셀까지 일치하지는 않음.\n회전은 이미지에 포함. Transform으로 다시 회전하지 않기.\nPNG에는 목표 지점으로의 이동과 점프 높이 궤적이 포함되지 않음. 미리보기만 오른쪽에서 왼쪽 목표로 이동. 미리보기의 카메라 크기와 높이는 연출 예시이며 Unity 검증 아님.\n기존 패턴은 네 개 포즈 라벨을 교체하므로 이 파일을 넣는 것만으로 36장 전체가 자동 재생되지는 않음. 프레임 재생 및 실제 발 접촉점과 경고/피해/효과 위치 동기화가 필요.\n지면 균열과 화면 균열은 별도 폴더. 이전 생성본을 함께 포함.\n미리보기는 공격1.5초 뒤 이펙트 잔여시간0.25초 포함.\n제작: 내장 imagegen으로 내려차기 핵심4자세 새 생성 + 승인된 Python 정렬/회전/프레임 구성. 프롬프트는 Generation_prompts.json.\n프로젝트는 수정하지 않음.\n',encoding='utf-8-sig')
zip_path=ROOT/'Alien_AxeKick_v2.zip'
with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED) as z:
 for p in OUT.rglob('*'):
  if p.is_file():z.write(p,p.relative_to(OUT))
print(json.dumps(report));print(zip_path)

