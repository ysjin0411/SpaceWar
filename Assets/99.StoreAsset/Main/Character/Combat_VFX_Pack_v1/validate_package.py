from pathlib import Path
from PIL import Image
import numpy as np,json,csv,hashlib,zipfile
ROOT=Path(__file__).parent;OUT=ROOT/'Deliverables'
ms=json.loads((OUT/'Animation_Manifest.json').read_text());rows=[];report={'status':'PASS','animations':[],'checks':{}}
for m in ms:
 ps=sorted((OUT/m['folder']).glob('*.png'));assert len(ps)==m['frames'],m['folder'];unique=set()
 for p in ps:
  im=Image.open(p);assert im.mode=='RGBA';assert list(im.size)==m['size'];a=np.array(im.getchannel('A'));assert np.any(a==0)
  h=hashlib.sha256(p.read_bytes()).hexdigest();unique.add(h)
  rows.append([p.relative_to(OUT).as_posix(),*im.size,m['ppu'],*m['pivot'],m['fps'],h])
 report['animations'].append({'folder':m['folder'],'frames':len(ps),'unique_files':len(unique),'duration':m['duration']})
# Charge core alpha must become monotonically clearer, at identical pixel coordinates.
prev=None
for p in sorted((OUT/'CruiserBigBullet/Charge_CoreOnly_60').glob('*.png')):
 ar=np.array(Image.open(p));a=ar[:,:,3].astype(int)
 if prev is not None:assert np.all(a>=prev)
 prev=a
report['checks']['BigBullet_core_alpha_monotonic_fixed_coordinates']='PASS'
for i in range(1,9):
 first=np.array(Image.open(OUT/f'SoldierLaser/Stage_00/BodyTile/BodyTile_{i:03d}.png'))
 for stage in range(17):
  arr=np.array(Image.open(OUT/f'SoldierLaser/Stage_{stage:02d}/BodyTile/BodyTile_{i:03d}.png'))
  assert np.array_equal(first[:,:,3],arr[:,:,3]);assert np.array_equal(arr[:,0],arr[:,-1])
report['checks']['Laser_all_stages_identical_alpha_width']='PASS'
report['checks']['Laser_body_tile_matching_edge_columns']='PASS'
bboxes=set()
for folder in ['Aiming_White_12','Locked_White_8']:
 for p in (OUT/'AlienLeapWarning'/folder).glob('*.png'):bboxes.add(Image.open(p).getchannel('A').getbbox())
assert len(bboxes)==1;report['checks']['Leap_warning_fixed_outer_bounds']=list(bboxes)[0]
for folder in ['BlackHole/Disappear_12','AlienResonance/CrackBurst_16','AlienResonance/FinalArea_12']:
 p=sorted((OUT/folder).glob('*.png'))[-1];assert Image.open(p).getchannel('A').getbbox() is None
report['checks']['Fade_sequences_end_transparent']='PASS'
report['checks']['Runtime']='File-level validation only; Unity playback and gameplay not tested'
report['total_animation_pngs']=len(rows)
static=Image.open(OUT/'AlienLeapWarning/Static_White.png');rows.append(['AlienLeapWarning/Static_White.png',*static.size,100,.5,.5,0,'static'])
with (OUT/'Import_Settings.csv').open('w',newline='',encoding='utf-8-sig') as f:
 w=csv.writer(f);w.writerow(['file','width','height','ppu','pivot_x','pivot_y','fps','sha256']);w.writerows(rows)
(OUT/'Validation_Report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
measurements={'Dog':{'source_canvas':[101,46],'source_ppu':100,'renderer_scale':3},'BlackHole':{'source_canvas':[1254,1254],'ppu':100,'root_scale':.6,'outer_radius_ratio':.47},'BigBullet':{'source_sprite_rect':[30,31],'ppu':100,'target_scale':7,'charge_seconds':2.5,'hold_seconds':1,'new_ppu':236},'Crack':{'source_canvas':[1254,1254],'ppu':600,'root_scale':6,'alpha32_bbox':[25,379,844,1111],'main_visible_width_world':8.19,'new_ppu_scale1':94,'new_ppu_scale6':564},'Resonance':{'wave_width':2,'wave_thickness':.4,'final_active_seconds':.4,'safe_lane_width':3},'Laser':{'source_glow_width':.25,'source_core_width':.08,'source_max_distance':20,'new_body_alpha32_height_pixels':25,'new_ppu':100}}
(OUT/'Source_Measurements.json').write_text(json.dumps(measurements,indent=2),encoding='utf-8')
for zipname,folders in [('Combat_VFX_All.zip',None),('Dog_BlackHole_BigBullet.zip',['DogCharge','BlackHole','CruiserBigBullet']),('Alien_Resonance_LeapWarning.zip',['AlienResonance','AlienLeapWarning']),('Soldier_Laser_17Stages.zip',['SoldierLaser'])]:
 with zipfile.ZipFile(ROOT/zipname,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  for p in OUT.rglob('*'):
   if not p.is_file():continue
   rel=p.relative_to(OUT)
   if folders is None or len(rel.parts)==1 or rel.parts[0] in folders or (rel.parts[0]=='Previews' and any(rel.name.startswith(x) for x in folders)) or (folders==['AlienResonance','AlienLeapWarning'] and rel.name.startswith('LeapWarning')):
    z.write(p,rel.as_posix())
 print(zipname,(ROOT/zipname).stat().st_size,flush=True)
print('VALIDATION_PASS',report['total_animation_pngs'],json.dumps(report['checks']),flush=True)
