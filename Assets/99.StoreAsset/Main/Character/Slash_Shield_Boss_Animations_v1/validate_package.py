from pathlib import Path
from PIL import Image
import numpy as np,json,csv,hashlib,zipfile
ROOT=Path(__file__).parent;OUT=ROOT/'Deliverables';A=Path('C:/YSJ/SubPF/Assets/99.StoreAsset/Main')
manifest=json.loads((OUT/'Animation_Manifest.json').read_text());report={'status':'PASS','file_count':0,'checks':{},'animations':[]};imports=[];timing=[]
def visible_equal(a,b):
 x=np.array(a);y=np.array(b)
 # Fully transparent RGB values do not affect rendering; alpha and all visible RGB must match.
 return np.array_equal(x[:,:,3],y[:,:,3]) and np.array_equal(x[:,:,:3][x[:,:,3]>0],y[:,:,:3][x[:,:,3]>0])
for m in manifest:
 folder=OUT/m['name']
 variants=['Combined','Body_Only','FX_Only'] if m['frames']==84 else ['Frames'] if m['name']=='Dog_Charge_Mouth' else ['']
 for variant in variants:
  fs=sorted((folder/variant).glob('*.png'));assert len(fs)==m['frames'],(m['name'],variant,len(fs))
  unique=set()
  for i,p in enumerate(fs):
   im=Image.open(p);assert im.mode=='RGBA';assert list(im.size)==m['canvas']
   a=np.array(im.getchannel('A'));assert not (a[0].any() or a[-1].any() or a[:,0].any() or a[:,-1].any()),str(p)
   unique.add(hashlib.sha256(im.tobytes()).hexdigest())
   rel=p.relative_to(OUT).as_posix();imports.append([rel,*im.size,m['ppu'],*m['pivot'],im.getchannel('A').getbbox() is None])
   timing.append([m['name'],variant or 'Frames',i+1,rel,round(i/m['fps'],9),round((i+1)/m['fps'],9),'frame'])
   report['file_count']+=1
  timing.append([m['name'],variant or 'Frames',m['frames'],fs[-1].relative_to(OUT).as_posix(),m['duration'],m['duration'],'terminal key: repeat last image'])
  report['animations'].append({'name':m['name'],'variant':variant,'frames':len(fs),'distinct_pixel_frames':len(unique),'duration':m['duration']})

for name,path,pad in [('Dog',A/'Bosses/Boss_Idle/Dog/Doghead_boss1.png',56),('Head',A/'Bosses/Boss_Idle/Head/Head_Boss_01.png',56),('Cruiser',A/'Bosses/Boss_Cruiser/Body/Boss_Cruiser_Body_Idle_01.png',96)]:
 src=Image.open(path).convert('RGBA');first=Image.open(OUT/f'{name}_Death_3p5s/Combined/{name}_Death_3p5s_001.png')
 assert visible_equal(src,first.crop((pad,pad,pad+src.width,pad+src.height))),name
 last=Image.open(OUT/f'{name}_Death_3p5s/Combined/{name}_Death_3p5s_084.png');assert last.getchannel('A').getbbox() is None
 report['checks'][name+'_first_original_pixels']='PASS';report['checks'][name+'_last_transparent']='PASS'
src=Image.open(A/'Bosses/Boss_Cruiser/Body/Boss_Cruiser_Body_Idle_01.png').convert('RGBA')
for frame in ['001','084']:
 im=Image.open(OUT/f'Cruiser_PhaseTransition_3p5s/Combined/Cruiser_PhaseTransition_3p5s_{frame}.png')
 assert visible_equal(src,im.crop((96,96,352,224)))
report['checks']['Cruiser_transition_start_end_original_pixels']='PASS'
for state in ['Idle','Attack']:
 for i in range(4):
  src=Image.open(A/f'Character/Warrior/Slash/Warrior_Slash_{state}_{i+1}.png').convert('RGBA')
  new=Image.open(OUT/f'Slash_Longsword/{state}/Warrior_Slash_{state}_{i+1:02d}.png')
  # Compare both feet and lower legs away from the replaced weapon.
  assert visible_equal(src.crop((340,760,680,805)),new.crop((596,1016,936,1061)))
report['checks']['Slash_original_feet_all_8_frames']='PASS'
report['checks']['transparent_canvas_edges_all_frames']='PASS'
report['checks']['image_only_validation']='Unity runtime has not been tested.'
(OUT/'Validation_Report.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
for name,head,rows in [('Import_Settings.csv',['file','width','height','ppu','pivot_x','pivot_y','transparent_frame'],imports),('Frame_Timing.csv',['animation','variant','frame','file','start_seconds','end_seconds','kind'],timing)]:
 with (OUT/name).open('w',newline='',encoding='utf-8-sig') as f:
  w=csv.writer(f);w.writerow(head);w.writerows(rows)
groups={'All_Animations.zip':None,'Slash_and_Shield_FX.zip':['Slash_Longsword','Shield'],'Dog_Death_and_Charge.zip':['Dog_Death_3p5s','Dog_Charge_Mouth'],'Head_Death.zip':['Head_Death_3p5s'],'Cruiser_Death_and_Transition.zip':['Cruiser_Death_3p5s','Cruiser_PhaseTransition_3p5s']}
for name,include in groups.items():
 with zipfile.ZipFile(ROOT/name,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  for p in OUT.rglob('*'):
   if not p.is_file():continue
   rel=p.relative_to(OUT)
   if include is None or len(rel.parts)==1 or rel.parts[0] in include or (rel.parts[0]=='Previews' and any(rel.name.startswith(x.split('_')[0]) for x in include)):
    z.write(p,rel.as_posix())
 print(name,(ROOT/name).stat().st_size,flush=True)
print('VALIDATION_PASS',report['file_count'],json.dumps(report['checks']),flush=True)
