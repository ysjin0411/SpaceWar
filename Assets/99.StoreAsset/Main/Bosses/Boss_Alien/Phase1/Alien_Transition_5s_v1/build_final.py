from pathlib import Path
import math, json, csv, hashlib, zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT=Path(__file__).parent
OUT=ROOT/'Final_120frames'
OUT.mkdir(exist_ok=True)
FRAMES=OUT/'Frames'; FRAMES.mkdir(exist_ok=True)
ASSET=Path('C:/YSJ/SubPF/Assets/99.StoreAsset/Main/Bosses/Boss_Alien')
ROBOT=Image.open(ASSET/'AlienTransition/Alien_Transition_01.png').convert('RGBA')
# Remove almost-invisible source background specks before splitting rigid pieces.
_robot_array=np.array(ROBOT);_robot_array[_robot_array[:,:,3]<=3]=0
ROBOT=Image.fromarray(_robot_array)
ALIEN=Image.open(ASSET/'Alien_IdleLeap_ExactOutline/Idle/Alien_Idle_01.png').convert('RGBA')
HOWL=Image.open('C:/Users/ysjin/.codex/generated_images/01a0c917-f262-70d2-8607-7cd47db8cf48/exec-73da3f04-c6ba-42fe-91c9-6161d2c283a5.png').convert('RGBA')
W=H=1254; GROUND=1150; AX=627; FOOT=1188; FPS=24; COUNT=120
R=Image.Resampling
def blank():return Image.new('RGBA',(W,H))
def clamp(x):return max(0.,min(1.,x))
def smooth(x):x=clamp(x);return x*x*(3-2*x)
def segment(t,a,b):return clamp((t-a)/(b-a))
def opacity(im,a):
    im=im.copy();im.putalpha(im.getchannel('A').point(lambda v:round(v*clamp(a))));return im
def maskpoly(poly):
    m=Image.new('L',(W,H));ImageDraw.Draw(m).polygon(poly,fill=255);return m
def masked(im,m):
    z=im.copy();z.putalpha(ImageChops.multiply(im.getchannel('A'),m));return z
def paste_at_foot(im,scale,x,y):
    # Uniform source scaling, with source foot row mapped to the fixed ground line.
    inv=1/scale
    return im.transform((W,H),Image.Transform.AFFINE,(inv,0,AX-x*inv,0,inv,FOOT-y*inv),R.BICUBIC)

# Corresponding landmarks allow a coherent pose interpolation, rather than a double-image fade.
S=np.array([(0,0),(627,0),(1253,0),(0,627),(1253,627),(0,1253),(627,1253),(1253,1253),
 (345,548),(299,499),(277,431),(281,359),(330,274),(450,179),(590,126),(704,122),
 (777,150),(799,196),(783,231),(715,249),(632,293),(561,351),(494,411),(439,467),(391,455),
 (565,406),(603,465),(683,479),(722,365),(809,424),(850,533),
 (465,584),(586,622),(683,665),(351,797),(559,929),(300,1153),(491,1178),
 (796,790),(1045,955),(1056,1136),(1179,1169),(207,767),(181,879),(1083,772),(1147,856)],float)
D=S.copy()
D[8:25]=np.array([(410,176),(398,157),(400,119),(416,88),(456,65),(518,62),(600,82),(732,183),
 (844,306),(884,384),(855,414),(762,355),(657,279),(557,213),(498,186),(439,176),(494,131)])
D[25:34]=np.array([(556,282),(568,357),(660,410),(703,382),(804,441),(853,542),(457,534),(585,586),(683,665)])

def triangulate(points):
    # Bowyer-Watson triangulation; self-contained to keep the deliverable reproducible.
    pts=list(map(tuple,points))+[(-10000,-10000),(20000,-10000),(0,20000)]
    tris=[(len(points),len(points)+1,len(points)+2)]
    def inside(tr,p):
        a,b,c=[np.array(pts[i],float) for i in tr]
        mat=2*np.array([b-a,c-a]);rhs=np.array([b@b-a@a,c@c-a@a])
        try:center=np.linalg.solve(mat,rhs)
        except np.linalg.LinAlgError:return False
        return np.sum((np.array(p)-center)**2)<np.sum((a-center)**2)-1e-7
    for i,p in enumerate(points):
        bad=[tr for tr in tris if inside(tr,p)];edges={}
        for tr in bad:
            for a,b in [(tr[0],tr[1]),(tr[1],tr[2]),(tr[2],tr[0])]:
                e=tuple(sorted((a,b)));edges[e]=edges.get(e,0)+1
        tris=[tr for tr in tris if tr not in bad]
        tris += [(a,b,i) for (a,b),n in edges.items() if n==1]
    return [tr for tr in tris if max(tr)<len(points)]
TRIS=triangulate((S+D)/2)
def warp(im,src,dst):
    out=blank()
    for tri in TRIS:
        st=src[list(tri)];dt=dst[list(tri)]
        x0=max(0,int(np.floor(dt[:,0].min()))-1);y0=max(0,int(np.floor(dt[:,1].min()))-1)
        x1=min(W,int(np.ceil(dt[:,0].max()))+2);y1=min(H,int(np.ceil(dt[:,1].max()))+2)
        if x1<=x0 or y1<=y0:continue
        mat=np.column_stack([dt,np.ones(3)])
        try:coef=np.linalg.solve(mat,st)
        except np.linalg.LinAlgError:continue
        a,b,c=coef[:,0];d,e,f=coef[:,1]
        piece=im.transform((x1-x0,y1-y0),Image.Transform.AFFINE,(a,b,c+a*x0+b*y0,d,e,f+d*x0+e*y0),R.BICUBIC)
        mask=Image.new('L',piece.size);dr=ImageDraw.Draw(mask)
        polygon=[(float(x-x0),float(y-y0)) for x,y in dt]
        dr.polygon(polygon,fill=255);dr.line(polygon+[polygon[0]],fill=255,width=2)
        out.paste(piece,(x0,y0),mask)
    return out
HEAD_MASK=maskpoly([(345,555),(304,525),(278,468),(271,410),(288,340),(330,274),(450,179),(590,116),(704,112),(777,143),(807,183),(802,229),(742,248),(654,284),(580,344),(515,405),(439,477),(384,536)])
HEAD_MASK=HEAD_MASK.filter(ImageFilter.MaxFilter(7))
HEAD=masked(ALIEN,HEAD_MASK)
JAW_MASK=maskpoly([(337,536),(350,550),(369,539),(398,512),(408,505),(398,531),(372,553),(353,558)])
JAW=masked(HEAD,JAW_MASK)
BODY=masked(ALIEN,ImageChops.invert(HEAD_MASK))
NECK=masked(HOWL,maskpoly([(490,205),(556,250),(619,296),(682,337),(710,400),(681,489),(602,540),(453,524),(452,386)]))
POSE_CACHE={}
def pose(q):
    q=round(clamp(q),4)
    if q==0:return ALIEN.copy()
    if q not in POSE_CACHE:
        # Rigid original-pixel head articulation avoids folding or stretching its surface details.
        result=blank()
        neck=NECK.transform((W,H),Image.Transform.AFFINE,(1,0,0,0,1/(.55+.45*q),540-540/(.55+.45*q)),R.BICUBIC)
        result.alpha_composite(neck)
        result.alpha_composite(BODY)
        head=HEAD.copy()
        if q>.18:
            opening=smooth(segment(q,.18,1))
            head=masked(head,ImageChops.invert(JAW_MASK))
            mouth=blank();md=ImageDraw.Draw(mouth)
            md.polygon([(339,536),(398,508),(408,519+12*opening),(367,555+12*opening),(350,553+12*opening)],fill=(28,6,30,255))
            mouth.alpha_composite(head)
            jaw=JAW.transform((W,H),Image.Transform.AFFINE,(1,0,-3*opening,0,1,-13*opening),R.BICUBIC)
            mouth.alpha_composite(jaw);head=mouth
        articulated=head.rotate(-65*q,R.BICUBIC,center=(580,360),translate=(90*q,-50*q))
        result.alpha_composite(articulated)
        POSE_CACHE[q]=result
    return POSE_CACHE[q].copy()

HATCH=[(678,423),(784,422),(819,456),(820,591),(699,593),(677,557)]
HM=maskpoly(HATCH)
LEFT=masked(ROBOT,maskpoly([(678,423),(735,424),(736,592),(699,593),(677,557)]))
RIGHT=masked(ROBOT,maskpoly([(735,424),(784,422),(819,456),(820,591),(736,592)]))
def hatch_robot(t):
    robot=ROBOT.copy();o=smooth(segment(t,.48,1.08))
    if o>0:
        cavity=blank();dr=ImageDraw.Draw(cavity)
        dr.polygon(HATCH,fill=(16,11,24,255),outline=(86,58,103,255),width=5)
        dr.line([(686,438),(686,555),(705,580),(808,580)],fill=(158,39,212,255),width=3)
        dr.line([(689,443),(689,551)],fill=(241,156,255,255),width=2)
        robot.paste(cavity,(0,0),HM)
        if t<1.2:
            child=paste_at_foot(ALIEN,.14,743,585)
            robot.alpha_composite(masked(child,HM))
        for panel,hinge,sign in [(LEFT,680,-1),(RIGHT,817,1)]:
            scale=max(.13,math.cos(o*math.pi*.465))
            dx=sign*o*25
            door=panel.transform((W,H),Image.Transform.AFFINE,(1/scale,0,hinge-(hinge+dx)/scale,0,1,0),R.BICUBIC)
            robot.alpha_composite(door)
    return robot

# Partition the source robot into stable rigid pieces for collapse. No frame is independently redrawn.
yy,xx=np.mgrid[:H,:W]
seeds=np.array([(795,279),(852,381),(611,425),(1064,416),(752,505),(889,564),
 (572,632),(361,646),(264,675),(1116,686),(1151,797),(779,685),
 (635,782),(729,932),(634,1095),(1003,839),(1048,1017),(1096,1130),
 (916,415),(884,713)],float)
labels=np.argmin(np.stack([(xx-x)**2+(yy-y)**2 for x,y in seeds]),axis=0)
rng=np.random.default_rng(240923)
piece_params=[(rng.uniform(-125,125),rng.uniform(-35,35),rng.uniform(-90,20)) for _ in seeds]
def broken_robot(t):
    base=hatch_robot(min(t,1.2))
    p=segment(t,1.86,2.6)
    if p<=0:return base
    result=blank()
    for i,(sx,sy) in enumerate(seeds):
        m=Image.fromarray(np.uint8(labels==i)*255)
        part=masked(base,m);box=part.getbbox()
        if not box:continue
        piece=part.crop(box);cx=(box[0]+box[2])/2;cy=(box[1]+box[3])/2
        dx,rot,vy=piece_params[i]
        piece=piece.rotate(rot*smooth(p),R.BICUBIC,expand=True)
        nx=cx+dx*p;ny=cy+vy*p+650*p*p
        nx=max(16+piece.width/2,min(W-16-piece.width/2,nx))
        ny=min(GROUND-piece.height/2,ny)
        piece=opacity(piece,1-smooth(segment(t,2.52,3.12)))
        result.alpha_composite(piece,(round(nx-piece.width/2),round(ny-piece.height/2)))
    return result

def electrical(t):
    im=blank();dr=ImageDraw.Draw(im)
    power=(.25+.75*smooth(segment(t,0,.45)))*(1-smooth(segment(t,.65,1.35)))
    if power<=0:return im
    rng=np.random.default_rng(int(t*24)+100)
    for x,y in [(722,440),(785,495),(876,379),(987,490),(565,619),(636,799),(993,860)]:
        pts=[(x,y)]
        for j in range(4):pts.append((x+int(rng.integers(-14,15)),y+12*(j+1)))
        dr.line(pts,fill=(218,91,255,int(210*power)),width=5)
        dr.line(pts,fill=(255,236,254,int(250*power)),width=2)
    return im

def illuminate(im,strength):
    if strength<=0:return im
    a=np.array(im);alpha=a[:,:,3]
    rgb=a[:,:,:3].astype(float);target=np.array([255,223,255])
    # Retain the input alpha; light comes from the body's existing silhouette.
    rgb=rgb*(1-strength*.78)+target*(strength*.78)
    a[:,:,:3]=np.uint8(np.clip(rgb,0,255));core=Image.fromarray(a)
    halo=Image.new('RGBA',(W,H),(189,41,255,0))
    halo.putalpha(Image.fromarray(alpha).filter(ImageFilter.GaussianBlur(9)).point(lambda x:int(x*strength*.65)))
    halo.alpha_composite(core);return halo

def stage(t):
    for end,name in [(.5,'Break'),(1.2,'Hatch'),(1.75,'Drop'),(2.,'Land'),(2.5,'SmallHowl'),(3.7,'Grow'),(4.,'Reveal'),(4.6,'AdultHowl'),(5.01,'Settle')]:
        if t<end:return name

def render(t):
    canvas=blank()
    if t<3.12:
        canvas.alpha_composite(broken_robot(t));canvas.alpha_composite(electrical(t))
    meta={'time':round(t,6),'stage':stage(t),'source_scale':0,'feet_y':None,'pose':0,'glow':0}
    if t<1.2:return canvas,meta
    scale=.14;x=743;y=585;q=0;light=0
    if t<1.75:
        p=segment(t,1.2,1.75);y=585+(GROUND-585)*p*p;x=743+(AX-743)*smooth(p)
    else:
        x=AX;y=GROUND
        if t<2.:
            p=segment(t,1.75,2);scale=.14*(1-.1*math.sin(math.pi*p))
        elif t<2.5:
            q=math.sin(math.pi*segment(t,2.,2.5))**2
            light=smooth(segment(t,2.2,2.48))
        elif t<3.7:
            p=smooth(segment(t,2.5,3.7));scale=.14+(.43-.14)*p
            light=.72+.24*math.sin(segment(t,2.5,3.7)*math.pi)**2
        else:
            scale=.43
            if t<4.:light=.72*(1-smooth(segment(t,3.7,4.)))
            elif t<4.22:q=smooth(segment(t,4.,4.22))
            elif t<4.6:q=1.;light=.1+.08*math.sin(t*40)**2
            elif t<4.94:q=1-smooth(segment(t,4.6,4.94))
    char=paste_at_foot(pose(q),scale,x,y)
    if t>=4.94:
        meta.update(source_scale=.43,feet_y=GROUND,pose=0,glow=0)
        return char,meta
    canvas.alpha_composite(illuminate(char,light))
    meta.update(source_scale=round(scale,8),feet_y=round(y,5),pose=round(q,5),glow=round(light,5))
    return canvas,meta

rows=[];previews=[];contacts=[]
for i in range(COUNT):
    t=i/FPS
    im,meta=render(t)
    filename=f'Alien_Transition_{i+1:03d}.png';im.save(FRAMES/filename,optimize=True)
    alpha=im.getchannel('A');bbox=alpha.point(lambda v:255 if v>32 else 0).getbbox()
    rows.append({'frame':i+1,**meta,'file':filename,'alpha_bbox':str(bbox)})
    prev=Image.new('RGB',(627,627),(28,30,38));sm=im.resize((627,627),R.LANCZOS);prev.paste(sm,(0,0),sm)
    previews.append(prev)
    if i in [0,10,20,28,34,40,44,50,56,62,70,78,86,94,102,110,116,119]:contacts.append((i,im.copy()))
    if i%12==0:print(f'Rendered {i+1}/{COUNT}',flush=True)

with (OUT/'timeline.csv').open('w',newline='',encoding='utf-8-sig') as f:
    writer=csv.DictWriter(f,fieldnames=rows[0].keys());writer.writeheader();writer.writerows(rows)
previews[0].save(OUT/'Preview_5seconds.gif',save_all=True,append_images=previews[1:],duration=[50 if i%6==5 else 40 for i in range(COUNT)],loop=0,optimize=False)
sheet=Image.new('RGB',(6*260,3*294),(29,31,39));dr=ImageDraw.Draw(sheet)
for j,(i,im) in enumerate(contacts):
    sm=im.resize((250,250),R.LANCZOS);x=(j%6)*260+5;y=(j//6)*294+28
    sheet.paste(sm,(x,y),sm);dr.text((x,y-20),f'{i+1:03d}  {i/FPS:.2f}s  {stage(i/FPS)}',fill=(240,240,245))
sheet.save(OUT/'ContactSheet.jpg',quality=94)
expected=paste_at_foot(ALIEN,.43,AX,GROUND)
last=Image.open(FRAMES/'Alien_Transition_120.png').convert('RGBA')
report={'frame_count':COUNT,'fps':FPS,'duration_seconds':5,'canvas':[W,H],
 'ppu':100,'transition_renderer_world_scale_assumed':1,'phase2_scale':.43,
 'ground_row':GROUND,'source_foot_row':FOOT,
 'phase2_spawn_offset_y_units':-(GROUND-H/2-.43*(FOOT-H/2))/100,
 'last_frame_exactly_matches_transformed_source':bool(np.array_equal(np.array(last),np.array(expected))),
 'all_frames_rgba':all(Image.open(p).mode=='RGBA' for p in FRAMES.glob('*.png')),
 'all_frames_same_size':all(Image.open(p).size==(W,H) for p in FRAMES.glob('*.png')),
 'source_alien_sha256':hashlib.sha256((ASSET/'Alien_IdleLeap_ExactOutline/Idle/Alien_Idle_01.png').read_bytes()).hexdigest(),
 'howl_note':'Original head pixels articulated as a rigid cutout, original body and feet retained; newly exposed neck uses a generated supporting insert. Resampling changes pixels in moving regions.',
 'runtime_validation':'Not imported or tested in Unity. Project not modified.'}
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2),flush=True)
