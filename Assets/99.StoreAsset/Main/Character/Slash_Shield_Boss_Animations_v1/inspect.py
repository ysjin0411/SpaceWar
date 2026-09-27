from pathlib import Path
from PIL import Image,ImageDraw
import re,json
ROOT=Path(__file__).parent;A=Path('C:/YSJ/SubPF/Assets')
mp={}
for p in A.rglob('*.meta'):
 t=p.read_text(errors='replace');m=re.search(r'^guid: (\w+)',t,re.M)
 if m:mp[m[1]]=p
groups={}
for name,glob in [('DogIdle','99.StoreAsset/Main/Bosses/Boss_Idle/Dog/*.png'),('DogDeath','99.StoreAsset/Main/Bosses/Boss_Dead/Dog/*x46.png'),('HeadIdle','99.StoreAsset/Main/Bosses/Boss_Idle/Head/*.png'),('HeadDeath','99.StoreAsset/Main/Bosses/Boss_Dead/Head/*x73.png'),('Cruiser','99.StoreAsset/Main/Bosses/Boss_Cruiser/Body/Boss*.png'),('Slash','99.StoreAsset/Main/Character/Warrior/Slash/*.png')]:
 rows=[]
 for p in sorted(A.glob(glob)):
  im=Image.open(p).convert('RGBA');mt=Path(str(p)+'.meta').read_text()
  rows.append(dict(path=str(p),size=im.size,bbox=im.getchannel('A').getbbox(),ppu=re.search(r'spritePixelsToUnits: (.*)',mt)[1],pivot=re.search(r'spritePivot: (.*)',mt)[1]))
 groups[name]=rows
 sheet=Image.new('RGB',(400*min(4,len(rows)),((len(rows)+3)//4)*340),(32,34,42));d=ImageDraw.Draw(sheet)
 for i,r in enumerate(rows):
  im=Image.open(r['path']).convert('RGBA');im.thumbnail((380,300),Image.Resampling.NEAREST)
  x=i%4*400;y=i//4*340;sheet.paste(im,(x+(400-im.width)//2,y+35),im);d.text((x+4,y+3),Path(r['path']).stem,fill='white');d.text((x+4,y+18),str(r['size'])+' '+str(r['bbox']),fill='white')
 sheet.save(ROOT/(name+'_Sources.jpg'))
resolved={}
for prefab in [A/'4. Prefabs/Enemy/Boss/Boss_Dog.prefab',A/'4. Prefabs/Enemy/Boss/Head/Boss_Head.prefab',A/'4. Prefabs/Enemy/Boss/Boss_Cruiser.prefab']:
 t=prefab.read_text();rr=[]
 for kind,g in re.findall(r'(m_Sprite|m_SpriteLibraryAsset): \{fileID: [^,]+, guid: (\w+)',t):
  if g in mp:rr.append([kind,str(mp[g])[:-5]])
 resolved[prefab.stem]=rr
(ROOT/'Source_Inventory.json').write_text(json.dumps(groups,indent=2))
(ROOT/'Prefab_References.json').write_text(json.dumps(resolved,indent=2))
print(json.dumps({'sources':groups,'prefabs':resolved},indent=2))
