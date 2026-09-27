from pathlib import Path
from PIL import Image,ImageSequence
import numpy as np,json,zipfile
root=Path(__file__).parent
out=root/'Final_120frames'
files=sorted((out/'Frames').glob('*.png'))
edges=[];opaque=[]
for p in files:
    a=np.array(Image.open(p).getchannel('A'))
    if max(a[0].max(),a[-1].max(),a[:,0].max(),a[:,-1].max())>0:edges.append(p.name)
    opaque.append(float(np.mean(a==0)))
g=Image.open(out/'Preview_5seconds.gif')
duration=sum(f.info.get('duration',0) for f in ImageSequence.Iterator(g))
report=json.loads((out/'validation.json').read_text())
report.update(frames_touching_canvas_edge=edges,gif_duration_ms=duration,minimum_fully_transparent_pixel_fraction=min(opaque))
(out/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
if edges:raise RuntimeError('Canvas edge clipping needs review')
assert len(files)==120 and duration==5000
assert report['last_frame_exactly_matches_transformed_source']
archive=root/'Alien_Transition_5s_120frames.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in out.rglob('*'):
        if p.is_file():z.write(p,p.relative_to(out))
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
print('ZIP',archive,'BYTES',archive.stat().st_size)
