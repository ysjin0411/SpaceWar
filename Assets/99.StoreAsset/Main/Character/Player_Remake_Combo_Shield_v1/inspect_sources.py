from pathlib import Path
from PIL import Image,ImageDraw
import re,json
ROOT=Path(__file__).parent;A=Path('C:/YSJ/SubPF/Assets');mp={}
for p in A.rglob('*.meta'):
 try:
  t=p.read_text(errors='replace');m=re.search(r'^guid: (\w+)',t,re.M)
  if m:mp[m[1]]=p
 except OSError:pass
out={}
for p in (A/'3. Animations/Player/SpriteLibrary').glob('*.spriteLib'):
 if not p.stem.startswith(('Warrior','Soldier')):continue
 t=p.read_text();rows=[]
 for g in re.findall(r'm_SpriteOverride: \{fileID: [^,]+, guid: (\w+)',t):
  meta=mp.get(g)
  if not meta:continue
  path=Path(str(meta)[:-5]);mt=meta.read_text(errors='replace')
  im=Image.open(path)
  rows.append(dict(path=str(path),size=im.size,bbox=im.convert('RGBA').getchannel('A').getbbox(),ppu=re.search(r'spritePixelsToUnits: (.*)',mt)[1],pivot=re.search(r'spritePivot: (.*)',mt)[1]))
 out[p.stem]=rows
(ROOT/'Source_Inventory.json').write_text(json.dumps(out,indent=2))
for name,rows in out.items():
 sheet=Image.new('RGB',(min(4,len(rows))*350,((len(rows)+3)//4)*370),(33,35,43));d=ImageDraw.Draw(sheet)
 for i,r in enumerate(rows):
  im=Image.open(r['path']).convert('RGBA');im.thumbnail((340,340))
  x=i%4*350;y=i//4*370
  sheet.paste(im,(x+(350-im.width)//2,y+25),im);d.text((x+5,y+3),str(i+1)+' '+str(r['size']),fill='white')
 sheet.save(ROOT/(name+'_Analysis.jpg'))
print(json.dumps(out,indent=2))

