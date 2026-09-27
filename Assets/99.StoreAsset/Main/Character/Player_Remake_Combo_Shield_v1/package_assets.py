from pathlib import Path
from PIL import Image, ImageDraw
import json, csv, hashlib, zipfile
import numpy as np

ROOT = Path(__file__).parent
OUT = ROOT / 'Deliverables'
inv = json.loads((ROOT / 'Source_Inventory.json').read_text())
records = []
errors = []
for p in sorted(OUT.rglob('*.png')):
    if 'Parts' in p.parts:
        continue
    im = Image.open(p)
    a = np.array(im.getchannel('A'))
    bb = im.getchannel('A').getbbox()
    rel = p.relative_to(OUT).as_posix()
    is_soldier = rel.startswith('Soldier/')
    hd = 'HD_1024' in rel
    expected = 1024 if hd else 64 if is_soldier else 3072
    if im.mode != 'RGBA' or im.size != (expected, expected):
        errors.append(rel + ': unexpected format/size')
    if a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any():
        errors.append(rel + ': touches canvas edge')
    if is_soldier and (bb[1] != (224 if hd else 14) or bb[3] != (800 if hd else 50)):
        errors.append(rel + ': inconsistent body vertical extent')
    records.append({'file': rel, 'width': im.width, 'height': im.height,
                    'ppu': 1600 if hd else 100, 'pivot_x': .5,
                    'pivot_y': .5 if is_soldier else .423828125,
                    'alpha_bbox': bb, 'blank': bb is None,
                    'sha256': hashlib.sha256(p.read_bytes()).hexdigest()})

counts = {
    'soldier_64': len(list((OUT/'Soldier'/'DropIn_64').rglob('*.png'))),
    'soldier_hd': len(list((OUT/'Soldier'/'HD_1024').rglob('*.png'))),
    'warrior_new_combo': sum(len(list((OUT/'Warrior'/w/f'Attack{s}').glob('*.png'))) for w in ['GiantSword','Hammer'] for s in [2,3]),
    'warrior_original_attack_reference': 8,
    'weapon_trails': len(list((OUT/'Warrior').glob('*/Trails_*/*.png'))),
    'shield_idle': len(list((OUT/'Warrior'/'Shield'/'Idle').glob('*.png'))),
    'shield_attack': len(list((OUT/'Warrior'/'Shield'/'Attack').glob('*.png'))),
    'shield_guard': 1,
    'shield_impact': len(list((OUT/'Warrior'/'Shield'/'Impact_8frames').glob('*.png')))
}
assert counts['soldier_64'] == 40 and counts['soldier_hd'] == 40
assert counts['warrior_new_combo'] == 16 and counts['weapon_trails'] == 24
assert not errors, errors
report = {'checks': 'PASS', 'scope': 'File-level dimensions, RGBA, transparent edges, counts, Soldier vertical anchors. Unity playback not tested.',
          'counts': counts, 'errors': errors, 'files': records}
(OUT/'Validation_Report.json').write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
with (OUT/'Import_Mapping.csv').open('w', newline='', encoding='utf-8-sig') as f:
    fields = ['file','width','height','ppu','pivot_x','pivot_y','blank']
    writer = csv.DictWriter(f, fieldnames=fields, extrasaction='ignore')
    writer.writeheader(); writer.writerows(records)

with (OUT/'Original_Replacement_Map.csv').open('w', newline='', encoding='utf-8-sig') as f:
    writer = csv.writer(f); writer.writerow(['new_file','current_source_file','note'])
    for weapon, key in [('Bullet','Soldier'),('Lazer','Soldier'),('Missile','Soldier_Missile'),('Grenade','Soldier_Grenade'),('Sniper','Soldier_Sniper')]:
        for state, offset in [('Idle',0),('Attack',3)]:
            for i in range(4):
                source = inv[key][offset+i]['path'] if i < 3 else ''
                note = '추가 프레임: 애니메이션/라이브러리 슬롯 추가 필요' if i == 3 else '기존 PNG 교체 시 .meta 유지'
                if weapon in ['Bullet','Lazer']:
                    note += '; Bullet/Lazer 원본 공유: 별도 외형에는 라이브러리 분리 필요'
                writer.writerow([f'Soldier/DropIn_64/{weapon}/{state}/Soldier_{weapon}_{state}_{i+1:02d}.png',source,note])

# Fixed-scale visual comparisons; never auto-fit individual actors to a different size.
bg = (28,31,40)
sheet = Image.new('RGB', (1152,460), bg); draw = ImageDraw.Draw(sheet)
paths = [Path(inv['Soldier'][0]['path']), OUT/'Soldier/DropIn_64/Bullet/Idle/Soldier_Bullet_Idle_01.png', OUT/'Soldier/HD_1024/Bullet/Idle/Soldier_Bullet_Idle_01.png']
for i,(p,label) in enumerate(zip(paths,['Original 64 / PPU 100','Remake 64 / PPU 100','Remake 1024 / PPU 1600'])):
    im = Image.open(p).convert('RGBA').resize((384,384),Image.Resampling.NEAREST)
    sheet.paste(im,(i*384,45),im)
    draw.text((i*384+10,15),label,fill='white')
    draw.line((i*384,45+300,i*384+383,45+300),fill=(50,160,150))
    draw.line((i*384+192,45,i*384+192,429),fill=(70,70,80))
sheet.save(OUT/'Previews/Soldier_Size_Comparison.jpg',quality=96)
sheet = Image.new('RGB',(1440,650),bg);draw=ImageDraw.Draw(sheet)
for i,(state,frame,label) in enumerate([('Idle',1,'Idle: small shield'),('Attack',2,'Attack: expanding'),('Attack',3,'Guard + forward impact')]):
    im=Image.open(OUT/f'Warrior/Shield/{state}/Warrior_Shield_{state}_{frame:02d}.png').convert('RGBA')
    if i==2:
        im.alpha_composite(Image.open(OUT/'Warrior/Shield/Impact_8frames/Shield_Impact_04.png'))
    im=im.crop((1250,1300,2650,2300));im.thumbnail((470,570),Image.Resampling.NEAREST)
    sheet.paste(im,(i*480,90),im);draw.text((i*480+10,20),label,fill='white')
sheet.save(OUT/'Previews/Warrior_Shield_Comparison.jpg',quality=96)
print(json.dumps(counts),flush=True)

for name,include in [('Soldier_Remake_40.zip',['Soldier']),('Warrior_Combo_Shield_FX.zip',['Warrior'])]:
    with zipfile.ZipFile(ROOT/name,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for p in OUT.rglob('*'):
            if not p.is_file(): continue
            rel=p.relative_to(OUT)
            if len(rel.parts)==1 or rel.parts[0] in include or (rel.parts[0]=='Previews' and rel.name.startswith(include[0])):
                z.write(p,rel.as_posix())
    print(name,(ROOT/name).stat().st_size,flush=True)
