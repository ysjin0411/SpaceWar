
const counts={Shield_Deploy:12,Shield_Guard:6,Shield_Lower:12,Slash_Attack:6},images={},ref={};
const $=id=>document.getElementById(id);let playing=true,index=0,last=0;
function load(path){const img=new Image();img.src=path;img.onload=draw;return img}
for(const [name,n] of Object.entries(counts))for(let i=1;i<=n;i++){const key=name+'_'+String(i).padStart(2,'0');images[key]=load('Frames/'+name+'/'+key+'.png')}
ref.Shield=load('Reference/Shield_Idle_Full.png');ref.Slash=load('Reference/Slash_Idle_Full.png');
function timeline(){const s=$('seq').value;if(s!=='Shield_Cycle')return Array.from({length:counts[s]},(_,i)=>s+'_'+String(i+1).padStart(2,'0'));return [...Array.from({length:12},(_,i)=>'Shield_Deploy_'+String(i+1).padStart(2,'0')),...Array.from({length:12},(_,i)=>'Shield_Guard_'+String(i%6+1).padStart(2,'0')),...Array.from({length:12},(_,i)=>'Shield_Lower_'+String(i+1).padStart(2,'0'))]}
function paint(canvas,img,reference,overlay){const c=canvas.getContext('2d');const zoom=$('zoom').checked;canvas.width=750;canvas.height=zoom?500:911;c.imageSmoothingEnabled=false;const r=zoom?[850,750,750,500]:[650,150,1400,1700];c.clearRect(0,0,canvas.width,canvas.height);if(img&&img.complete)c.drawImage(img,...r,0,0,canvas.width,canvas.height);if(overlay&&reference.complete){c.globalAlpha=.3;c.drawImage(reference,...r,0,0,canvas.width,canvas.height);c.globalAlpha=1}const y=(1162-r[1])/r[3]*canvas.height;c.strokeStyle='#72e4a8';c.beginPath();c.moveTo(0,y);c.lineTo(canvas.width,y);c.stroke()}
function draw(){const list=timeline();index=Math.min(index,list.length-1);$('frame').max=list.length-1;$('frame').value=index;const kind=$('seq').value.startsWith('Slash')?'Slash':'Shield';const reference=ref[kind];if(!reference)return;paint($('current'),images[list[index]],reference,$('ghost').checked);paint($('idle'),reference,reference,false);$('status').textContent=(index+1)+' / '+list.length+' · '+list[index]}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'일시 정지':'재생'};$('seq').onchange=()=>{index=0;draw()};$('frame').oninput=()=>{index=+$('frame').value;playing=false;$('play').textContent='재생';draw()};$('zoom').onchange=draw;$('ghost').onchange=draw;
function tick(t){if(playing&&t-last>1000/Math.max(1,+$('fps').value)){index=(index+1)%timeline().length;draw();last=t}requestAnimationFrame(tick)}requestAnimationFrame(tick);

