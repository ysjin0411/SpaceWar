'use strict';
const fs=require('fs'),path=require('path');
const ROOT=__dirname,SR=44100,TAU=2*Math.PI;
let seed=20261114;
const manifest=[],previewGroups={};
/** Deterministic noise keeps regenerated assets consistent. */
function random(){seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296*2-1;}
/** Allocate floating-point audio before final PCM encoding. */
function buf(sec,channels=1){return Array.from({length:channels},()=>new Float64Array(Math.round(sec*SR)));}
/** Add a sample with circular indexing for seamless musical tails. */
function put(b,i,v,pan=0){
 i=((i%b[0].length)+b[0].length)%b[0].length;
 if(b.length===1)b[0][i]+=v;
 else{b[0][i]+=v*Math.sqrt((1-pan)/2);b[1][i]+=v*Math.sqrt((1+pan)/2);}
}
/** Layer a pitched, enveloped oscillator; pad mode starts and ends smoothly. */
function tone(b,at,dur,f,amp=.5,o={}){
 const n=Math.round(dur*SR),start=Math.round(at*SR);
 for(let i=0;i<n;i++){
  const t=i/SR,p=i/n;
  const env=o.pad?Math.pow(Math.sin(Math.PI*p),o.shape||.7):(1-Math.exp(-t/(o.attack||.002)))*Math.exp(-t/(o.decay||dur/5))*Math.min(1,(dur-t)/.015);
  const ph=TAU*(f*t+.5*(o.sweep||0)*t*t);
  const v=(Math.sin(ph+(o.fm||0)*Math.sin(ph*(o.ratio||1.41)))+(o.harm||.06)*Math.sin(ph*2))*env*amp;
  put(b,start+i,v,o.pan||0);
 }
}
/** Add filtered noise; high-pass mode creates friction and metallic attacks. */
function noise(b,at,dur,amp=.5,o={}){
 let lp=0;const n=Math.round(dur*SR),start=Math.round(at*SR);
 for(let i=0;i<n;i++){
  const t=i/SR,x=random();lp+=(o.cut||.12)*(x-lp);
  const env=o.rise?Math.pow(Math.sin(Math.PI*i/n),1.5):(1-Math.exp(-t/(o.attack||.001)))*Math.exp(-t/(o.decay||dur/5))*Math.min(1,(dur-t)/.015);
  put(b,start+i,(o.high?x-lp:lp)*env*amp,o.pan||0);
 }
}
/** Add short reflections without changing the source timing. */
function echo(b,delay,gain,repeats=2){
 for(const c of b){const original=c.slice();for(let k=1;k<=repeats;k++){
  const off=Math.round(delay*k*SR),g=Math.pow(gain,k);
  for(let i=0;i+off<c.length;i++)c[i+off]+=original[i]*g;
 }}
}
/** Remove DC, normalize with headroom, encode WAV, and collect numeric checks. */
function save(file,b,peak=.4,meta={}){
 fs.mkdirSync(path.dirname(path.join(ROOT,file)),{recursive:true});
 const n=b[0].length,ch=b.length;
 for(const c of b){let mean=0;for(const x of c)mean+=x;mean/=n;for(let i=0;i<n;i++)c[i]-=mean;}
 if(!meta.loop){
  const edge=Math.min(Math.round(.004*SR),n>>2);
  for(const c of b)for(let i=0;i<edge;i++){const w=.5-.5*Math.cos(Math.PI*i/edge);c[i]*=w;c[n-1-i]*=w;}
 }
 let mx=0;for(const c of b)for(const x of c){if(!Number.isFinite(x))throw Error(file+' nonfinite');mx=Math.max(mx,Math.abs(x));}
 const scale=peak/mx,data=Buffer.alloc(n*ch*2);let sq=0,clip=0,jump=0,dc=0;
 for(let i=0;i<n;i++)for(let c=0;c<ch;c++){
  const v=b[c][i]*scale;b[c][i]=v;sq+=v*v;dc+=v;
  if(Math.abs(v)>=1)clip++;
  data.writeInt16LE(Math.round(v*32767),(i*ch+c)*2);
 }
 for(const c of b)jump=Math.max(jump,Math.abs(c[0]-c[n-1]));
 if(clip)throw Error(file+' clipping');
 const h=Buffer.alloc(44);h.write('RIFF');h.writeUInt32LE(36+data.length,4);h.write('WAVEfmt ',8);
 h.writeUInt32LE(16,16);h.writeUInt16LE(1,20);h.writeUInt16LE(ch,22);h.writeUInt32LE(SR,24);
 h.writeUInt32LE(SR*ch*2,28);h.writeUInt16LE(ch*2,32);h.writeUInt16LE(16,34);h.write('data',36);h.writeUInt32LE(data.length,40);
 fs.writeFileSync(path.join(ROOT,file),Buffer.concat([h,data]));
 manifest.push({file,seconds:+(n/SR).toFixed(5),sampleRate:SR,channels:ch,peakDb:+(20*Math.log10(peak)).toFixed(2),rmsDb:+(10*Math.log10(sq/n/ch)).toFixed(2),boundaryJump:+jump.toFixed(6),...meta});
 if(meta.preview){(previewGroups[meta.preview]??=[]).push({file,b});}
 console.log('Created '+file);
}
/** Produce a short musical UI response with optional inharmonic texture. */
function chime(file,notes,spacing=.08,tail=.28,label='',group='UI',peak=.28){
 const b=buf(spacing*(notes.length-1)+tail);
 notes.forEach((f,i)=>tone(b,i*spacing,tail,f,.6,{decay:tail/4,harm:.04}));
 save(file,b,peak,{use:label,preview:group});
}
/** Layer mechanical impact, pressure body, and debris into one-shot weapons. */
function impact(file,{dur=.3,low=100,weight=1,metal=0,crack=.4,label='',group='Weapons',peak=.43}={}){
 const b=buf(dur);
 tone(b,0,dur*.85,low,.9*weight,{decay:dur/6,sweep:-low/dur*.45,fm:.3});
 noise(b,0,dur*.9,.9*weight,{cut:.06,decay:dur/5});
 noise(b,0,Math.min(.09,dur),crack,{high:true,cut:.18,decay:.013});
 if(metal)[1,1.49,2.13,3.17].forEach((f,i)=>tone(b,.002,dur*.7,metal*f,.15/(i+1),{decay:dur/4,harm:0}));
 save(file,b,peak,{use:label,preview:group});
}
/** Make an airy attack sweep with a distinct pitched edge. */
function sweep(file,{dur=.3,f=350,delta=-650,amp=.5,label='',group='Weapons',peak=.3}={}){
 const b=buf(dur);noise(b,0,dur*.9,amp,{cut:.28,attack:.012,decay:dur/4});
 tone(b,.005,dur*.9,f,.35,{sweep:delta,decay:dur/3,attack:.008,fm:.35});
 save(file,b,peak,{use:label,preview:group});
}
/** Create a periodic sustained beam or energy texture. */
function energyLoop(file,base,seconds=2,label='',peak=.22){
 const b=buf(seconds);const n=b[0].length;
 for(let i=0;i<n;i++){const t=i/SR; b[0][i]=(.6*Math.sin(TAU*base*t+1.5*Math.sin(TAU*(base/2)*t))+.22*Math.sin(TAU*base*2*t))*(.8+.2*Math.sin(TAU*6*t));}
 save(file,b,peak,{use:label,loop:true});
}
/** Layer a synthetic creature voice from glottal harmonics, formants, and breath. */
function roar(b,at,dur,{pitch=66,intensity=1,attack=.09}={}){
 let phase=0,lp=0;
 const n=Math.round(dur*SR),off=Math.round(at*SR);
 for(let i=0;i<n;i++){
  const t=i/SR,p=t/dur;
  const f=pitch*(1+.55*Math.sin(Math.PI*Math.min(1,p*1.6))-.16*p)+3*Math.sin(TAU*7*t)+1.2*Math.sin(TAU*23*t);
  phase+=TAU*f/SR;
  const formant1=410+300*Math.sin(Math.PI*p),formant2=1100-400*p;
  let v=0;
  for(let h=1;h<=24;h++){
   const hz=f*h;
   const form=.2+2.4*Math.exp(-Math.pow((hz-formant1)/170,2))+1.6*Math.exp(-Math.pow((hz-formant2)/280,2));
   v+=Math.sin(phase*h+.16*Math.sin(phase*.47))*form/Math.pow(h,1.12);
  }
  const x=random();lp+=.19*(x-lp);
  const trem=.74+.26*Math.sin(TAU*(29-13*p)*t)*Math.sin(TAU*3.7*t);
  const env=Math.min(1,t/attack)*Math.pow(Math.sin(Math.PI*Math.min(.9999,p)),.32)*Math.min(1,(dur-t)/Math.min(.22,dur*.28));
  v=Math.tanh(v*1.5)*.65+lp*.5+Math.sin(phase*.5)*.25;
  put(b,off+i,v*env*trem*intensity);
 }
}

// Preserve the Town set and the user's already chosen menu effects.
const town=path.join(path.dirname(ROOT),'Town_SciFi_Audio');
const townLabels=fs.readFileSync(path.join(town,'README.txt'),'utf8');
for(const r of JSON.parse(fs.readFileSync(path.join(town,'audio_validation.json'),'utf8'))){
 if(r.file.startsWith('preview'))continue;
 const folder=r.file.startsWith('town_bgm')?'01_BGM':r.file.startsWith('town_ambience')?'02_Ambience':'03_Town_UI';
 fs.mkdirSync(path.join(ROOT,folder),{recursive:true});
 fs.copyFileSync(path.join(town,r.file),path.join(ROOT,folder,r.file));
 manifest.push({...r,file:folder+'/'+r.file,sampleRate:44100,use:'Town: '+r.file});
}
const menuSrc='C:/YSJ/SubPF/Assets/99.Music/Sound';
for(const name of ['ui_typing.wav','ui_login_success.wav','ui_login_failure.wav']){
 fs.mkdirSync(path.join(ROOT,'04_Menu_UI'),{recursive:true});
 fs.copyFileSync(path.join(menuSrc,name),path.join(ROOT,'04_Menu_UI',name));
 const d=fs.readFileSync(path.join(menuSrc,name));manifest.push({file:'04_Menu_UI/'+name,seconds:(d.length-44)/d.readUInt32LE(28),sampleRate:d.readUInt32LE(24),channels:d.readUInt16LE(22),use:'기존 MainMenu 효과음 재사용'});
}
chime('04_Menu_UI/ui_focus.wav',[660],.1,.055,'키보드 UI 선택 이동','UI',.15);
chime('04_Menu_UI/ui_confirm.wav',[523.25,783.99],.07,.22,'캐릭터·스테이지 선택 확정');
chime('04_Menu_UI/ui_back.wav',[659.25,440],.06,.18,'뒤로 이동');
chime('04_Menu_UI/ui_locked.wav',[246.94,233.08],.08,.18,'잠긴 항목·조건 미충족');
chime('04_Menu_UI/character_soldier_select.wav',[392,587.33,783.99],.065,.23,'병사 선택');
chime('04_Menu_UI/character_warrior_select.wav',[293.66,440,587.33],.09,.29,'전사 선택');
chime('04_Menu_UI/equipment_upgrade_success.wav',[523.25,659.25,1046.5],.08,.36,'장비 강화 성공');
chime('04_Menu_UI/achievement_unlock.wav',[587.33,739.99,880,1174.66],.085,.5,'업적 달성');
chime('04_Menu_UI/inventory_item_select.wav',[830.61],.08,.075,'인벤토리 아이템 선택','UI',.18);
chime('04_Menu_UI/stage_select_change.wav',[440,554.37],.045,.12,'스테이지 선택 변경','UI',.21);
chime('04_Menu_UI/pause_open.wav',[440,349.23],.055,.19,'일시정지');
chime('04_Menu_UI/pause_resume.wav',[349.23,440],.055,.19,'일시정지 해제');

// Distinct sound families for all nine weapon data assets.
for(let i=0;i<3;i++)impact('05_Weapons/Soldier/bullet_fire_0'+(i+1)+'.wav',{dur:.16+i*.01,low:135+i*12,weight:.7,metal:1300+i*95,crack:.95,label:'기본 총알 발사 변형 '+(i+1),peak:.35});
impact('05_Weapons/Soldier/bullet_hit.wav',{dur:.12,low:210,weight:.4,crack:.7,label:'총알 명중',peak:.25});
sweep('05_Weapons/Soldier/laser_start.wav',{dur:.25,f:260,delta:1800,label:'지속 레이저 시작',peak:.32});
energyLoop('05_Weapons/Soldier/laser_sustain_loop.wav',180,2,'지속 레이저 유지: 발사 중에만 Loop',.22);
energyLoop('05_Weapons/Soldier/laser_max_charge_loop.wav',240,2,'최대 출력 레이저 유지: 기본 루프와 교체',.25);
sweep('05_Weapons/Soldier/laser_stop.wav',{dur:.24,f:700,delta:-2200,label:'레이저 종료',peak:.24});
sweep('05_Weapons/Soldier/missile_launch.wav',{dur:.42,f:170,delta:900,amp:.9,label:'미사일 발사',peak:.38});
impact('05_Weapons/Soldier/missile_explosion.wav',{dur:.95,low:63,weight:1.3,metal:410,crack:.9,label:'미사일 폭발',peak:.48});
sweep('05_Weapons/Soldier/grenade_throw.wav',{dur:.21,f:220,delta:-500,label:'수류탄 투척',peak:.28});
impact('05_Weapons/Soldier/grenade_bounce.wav',{dur:.15,low:290,weight:.3,metal:980,crack:.22,label:'수류탄 바닥 접촉',peak:.24});
impact('05_Weapons/Soldier/grenade_explosion.wav',{dur:.72,low:84,weight:1.2,metal:530,crack:1.15,label:'수류탄 폭발',peak:.47});
impact('05_Weapons/Soldier/sniper_fire.wav',{dur:.58,low:105,weight:1.0,metal:1700,crack:1.5,label:'저격총 강한 단발',peak:.45});
impact('05_Weapons/Soldier/sniper_hit.wav',{dur:.2,low:145,weight:.7,metal:700,crack:.75,label:'저격탄 명중',peak:.32});
for(let i=0;i<3;i++)sweep('05_Weapons/Warrior/giant_sword_swing_0'+(i+1)+'.wav',{dur:.27+i*.045,f:320-i*45,delta:-550+i*70,amp:.7+i*.1,label:'대검 '+(i+1)+'타',peak:.32+i*.025});
impact('05_Weapons/Warrior/giant_sword_hit.wav',{dur:.26,low:135,weight:.8,metal:720,crack:.6,label:'대검 명중',peak:.36});
sweep('05_Weapons/Warrior/slash_wave_launch.wav',{dur:.38,f:500,delta:-1000,amp:.65,label:'참격 파동 발사',peak:.35});
impact('05_Weapons/Warrior/slash_wave_hit.wav',{dur:.22,low:230,weight:.5,metal:1020,crack:.5,label:'참격 파동 명중',peak:.30});
for(let i=0;i<3;i++)sweep('05_Weapons/Warrior/hammer_swing_0'+(i+1)+'.wav',{dur:.30+i*.04,f:180-i*22,delta:-200,amp:.9,label:'해머 '+(i+1)+'타 휘두름',peak:.30+i*.025});
impact('05_Weapons/Warrior/hammer_hit.wav',{dur:.4,low:72,weight:1.4,metal:370,crack:.65,label:'해머 일반 명중',peak:.42});
impact('05_Weapons/Warrior/hammer_finisher.wav',{dur:.7,low:53,weight:1.7,metal:280,crack:.95,label:'해머 마지막 타격',peak:.48});
chime('05_Weapons/Warrior/shield_raise.wav',[330,494],.035,.20,'방패 전개','Weapons',.27);
impact('05_Weapons/Warrior/shield_block.wav',{dur:.29,low:170,weight:.45,metal:860,crack:.5,label:'방패 방어 성공',peak:.34});
impact('05_Weapons/Warrior/shield_break.wav',{dur:.65,low:145,weight:.7,metal:1550,crack:1.1,label:'방패 파괴',peak:.40});
chime('05_Weapons/Warrior/shield_recover.wav',[330,440,660],.09,.26,'방패 회복 완료','Weapons',.26);
chime('05_Weapons/weapon_switch.wav',[260,390],.035,.12,'무기 교체','Weapons',.22);

// Player, enemy, pickups, progression, and end-of-stage events.
impact('06_Combat/player_hurt.wav',{dur:.23,low:145,weight:.7,crack:.35,label:'플레이어 피격: 음성 없음',group:'Combat',peak:.35});
sweep('06_Combat/player_death.wav',{dur:1.15,f:420,delta:-290,amp:.5,label:'플레이어 사망 에너지 소멸',group:'Combat',peak:.34});
for(let i=0;i<3;i++)impact('06_Combat/enemy_hit_0'+(i+1)+'.wav',{dur:.12+i*.02,low:175+i*35,weight:.5,crack:.4,label:'일반 적 피격 '+(i+1),group:'Combat',peak:.24});
impact('06_Combat/enemy_death.wav',{dur:.30,low:120,weight:.65,crack:.65,label:'일반 적 처치',group:'Combat',peak:.28});
impact('06_Combat/enemy_suicide_explosion.wav',{dur:.55,low:77,weight:1.0,crack:.8,label:'자폭 적 폭발',group:'Combat',peak:.39});
chime('06_Combat/pickup_gold.wav',[1200,1600],.035,.12,'골드 획득','Combat',.20);
chime('06_Combat/pickup_experience.wav',[988],.04,.085,'경험치 획득','Combat',.14);
chime('06_Combat/pickup_ultimate.wav',[523,784,1046],.045,.2,'궁극기 충전 아이템','Combat',.24);
chime('06_Combat/level_up.wav',[523,659,784,1046],.07,.38,'레벨업','Combat',.33);
chime('06_Combat/upgrade_select.wav',[659,988,1318],.06,.26,'업그레이드 선택 확정','Combat',.27);
chime('06_Combat/ultimate_ready.wav',[440,659,880],.1,.45,'궁극기 사용 가능','Combat',.30);
chime('06_Combat/treasure_appear.wav',[392,587,784],.07,.38,'보물상자 등장','Combat',.29);
sweep('06_Combat/treasure_charge.wav',{dur:1,f:280,delta:650,label:'상자 개봉 충전 1초 기준: 실제 진행 시간에 맞춰 조절',group:'Combat',peak:.26});
chime('06_Combat/treasure_open.wav',[523,659,784,1046,1318],.07,.55,'상자 개봉 성공','Combat',.35);
sweep('06_Combat/ultimate_warrior_cast.wav',{dur:.75,f:230,delta:620,amp:1,label:'전사 궁극기 시작',group:'Combat',peak:.42});
sweep('06_Combat/ultimate_sword_drop.wav',{dur:.36,f:700,delta:-1600,amp:.65,label:'궁극기 검 한 발 낙하: 재생 빈도 제한',group:'Combat',peak:.31});
impact('06_Combat/ultimate_sword_impact.wav',{dur:.52,low:63,weight:1.3,metal:620,crack:.75,label:'궁극기 검 착지',group:'Combat',peak:.40});
sweep('06_Combat/ultimate_soldier_launch.wav',{dur:1.2,f:120,delta:520,amp:1,label:'병사 궁극기 미사일 상승 1.2초 기본값',group:'Combat',peak:.40});
impact('06_Combat/ultimate_bombard_impact.wav',{dur:.68,low:55,weight:1.5,metal:320,crack:.85,label:'병사 궁극기 폭격 1발: 전체 30발 동시 재생 금지',group:'Combat',peak:.40});
chime('06_Combat/stage_clear.wav',[523.25,659.25,783.99,1046.5,1318.51],.18,.9,'스테이지 클리어','Combat',.38);
chime('06_Combat/stage_failed.wav',[440,349.23,293.66,220],.18,.75,'스테이지 실패','Combat',.32);
chime('06_Combat/boss_warning.wav',[220,440,220,440],.16,.18,'보스 등장 알림','Combat',.35);

// Boss Dog and Head.
let b=buf(1.25);roar(b,0,1.2,{pitch:95});save('07_Bosses/Dog/dog_intro_growl.wav',b,.42,{use:'Dog 등장: 합성 괴수 으르렁',preview:'Bosses'});
sweep('07_Bosses/Dog/dog_charge.wav',{dur:.65,f:130,delta:380,amp:1,label:'Dog 돌진 시작',group:'Bosses',peak:.4});
sweep('07_Bosses/Dog/dog_spiral_shot.wav',{dur:.16,f:640,delta:-1900,label:'Dog 나선탄 발사 묶음',group:'Bosses',peak:.29});
impact('07_Bosses/Dog/dog_impact.wav',{dur:.55,low:68,weight:1.2,crack:.65,label:'Dog 충돌',group:'Bosses',peak:.42});
sweep('07_Bosses/Head/head_laser_charge.wav',{dur:1.0,f:180,delta:600,amp:.55,label:'Head 레이저 예고: 실제 예고 시간에 맞춤',group:'Bosses',peak:.33});
energyLoop('07_Bosses/Head/head_laser_loop.wav',120,2,'Head 레이저 유지',.30);
sweep('07_Bosses/Head/head_confusion.wav',{dur:.8,f:420,delta:-170,amp:.35,label:'Head 혼란 발동',group:'Bosses',peak:.32});
energyLoop('07_Bosses/Head/head_blackhole_loop.wav',60,4,'Head 블랙홀 존재 중 유지',.24);
impact('07_Bosses/Head/head_blackhole_collapse.wav',{dur:.85,low:48,weight:1.2,metal:330,crack:.5,label:'블랙홀 소멸',group:'Bosses',peak:.39});

// Cruiser mechanical arsenal and its 3.5-second transition.
impact('07_Bosses/Cruiser/cruiser_turret_fire.wav',{dur:.28,low:120,weight:.8,metal:980,crack:.8,label:'Cruiser 포탑 발사',group:'Bosses',peak:.37});
sweep('07_Bosses/Cruiser/cruiser_bigshot_charge.wav',{dur:1.25,f:95,delta:460,amp:.7,label:'Cruiser 대형탄 충전: 실제 패턴 시간에 맞춤',group:'Bosses',peak:.35});
impact('07_Bosses/Cruiser/cruiser_bigshot_fire.wav',{dur:.85,low:55,weight:1.5,metal:480,crack:1,label:'Cruiser 대형탄 발사',group:'Bosses',peak:.48});
chime('07_Bosses/Cruiser/cruiser_summon.wav',[220,293.66,440],.12,.45,'Cruiser 소환 신호','Bosses',.31);
b=buf(3.5);for(let i=0;i<5;i++){noise(b,i*.55,.65,.5,{cut:.09,decay:.15});tone(b,i*.55,.7,75+i*12,.4,{fm:1.8,decay:.18});}tone(b,2.65,.8,55,.8,{sweep:-25,decay:.2});save('07_Bosses/Cruiser/cruiser_transition_3p5s.wav',b,.46,{use:'Cruiser 3.5초 전환 애니메이션용',preview:'Bosses'});
impact('07_Bosses/Cruiser/cruiser_death_explosion.wav',{dur:3.5,low:44,weight:1.7,metal:280,crack:1.2,label:'Cruiser 파괴: 3.5초 잔향',group:'Bosses',peak:.5});

// Alien biomachine textures and two-phase attacks.
sweep('07_Bosses/Alien/alien_missile_open.wav',{dur:.45,f:160,delta:140,amp:.8,label:'Alien 미사일 발사구 열림',group:'Bosses',peak:.31});
sweep('07_Bosses/Alien/alien_missile_launch.wav',{dur:.4,f:210,delta:850,amp:.75,label:'Alien 미사일 발사',group:'Bosses',peak:.34});
impact('07_Bosses/Alien/alien_barrage_blast.wav',{dur:.72,low:72,weight:1.2,metal:580,crack:.75,label:'Alien 탄막 폭발',group:'Bosses',peak:.40});
sweep('07_Bosses/Alien/alien_grab_prepare.wav',{dur:.75,f:125,delta:100,amp:.7,label:'Alien 잡기 준비',group:'Bosses',peak:.32});
impact('07_Bosses/Alien/alien_grab_catch.wav',{dur:.28,low:100,weight:.8,metal:440,crack:.3,label:'Alien 잡기 성공',group:'Bosses',peak:.35});
sweep('07_Bosses/Alien/alien_grab_throw.wav',{dur:.4,f:280,delta:-480,amp:.85,label:'Alien 던지기',group:'Bosses',peak:.36});
sweep('07_Bosses/Alien/alien_resonance_charge.wav',{dur:1.2,f:90,delta:260,amp:.55,label:'Alien 공명 준비: 실제 패턴 시간에 맞춤',group:'Bosses',peak:.32});
b=buf(1.3);tone(b,0,1.2,70,.8,{fm:2.2,ratio:1.7,decay:.28,sweep:80});tone(b,0,1.2,370,.3,{fm:1.5,decay:.3});noise(b,0,1.1,.5,{cut:.09,decay:.3});save('07_Bosses/Alien/alien_resonance_wave.wav',b,.40,{use:'Alien 공명 파동 1회',preview:'Bosses'});
sweep('07_Bosses/Alien/alien_p2_leap.wav',{dur:.6,f:130,delta:380,amp:1,label:'Alien 2페이즈 도약',group:'Bosses',peak:.37});
impact('07_Bosses/Alien/alien_p2_land.wav',{dur:.85,low:48,weight:1.7,metal:340,crack:.9,label:'Alien 2페이즈 도약 착지',group:'Bosses',peak:.47});
for(let i=0;i<3;i++)sweep('07_Bosses/Alien/alien_p2_claw_0'+(i+1)+'.wav',{dur:.26+i*.05,f:260-i*25,delta:-400,amp:.8+i*.12,label:'Alien 2페이즈 콤보 '+(i+1)+'타',group:'Bosses',peak:.34+i*.025});
impact('07_Bosses/Alien/alien_p2_trace_insert.wav',{dur:.45,low:95,weight:1.1,metal:490,crack:.7,label:'Alien 추적 발톱 삽입',group:'Bosses',peak:.37});
sweep('07_Bosses/Alien/alien_p2_trace_warning.wav',{dur:.6,f:350,delta:600,amp:.4,label:'Alien 추적 공격 예고',group:'Bosses',peak:.30});
impact('07_Bosses/Alien/alien_p2_trace_strike.wav',{dur:.48,low:85,weight:1.15,metal:640,crack:.85,label:'Alien 추적 발톱 타격',group:'Bosses',peak:.42});
sweep('07_Bosses/Alien/alien_p2_trace_withdraw.wav',{dur:.4,f:290,delta:-400,amp:.65,label:'Alien 추적 발톱 회수',group:'Bosses',peak:.30});
b=buf(2.3);roar(b,0,1.9,{pitch:56,intensity:.8,attack:.02});noise(b,.5,1.7,.8,{cut:.07,decay:.4});save('07_Bosses/Alien/alien_death.wav',b,.46,{use:'Alien 최종 사망',preview:'Bosses'});

// Exact source timing: HowlFX first non-clear sprite at 4.2083335, stop time 5.0.
const howlAt=101/24,burstDuration=5-howlAt;
b=buf(5);
tone(b,0,4.45,42,.3,{pad:true,shape:1.7,fm:1.3,harm:.08});
tone(b,1.1,3.8,100,.18,{pad:true,shape:1.5,sweep:25,fm:.9});
noise(b,.5,3.95,.38,{rise:true,cut:.045});
roar(b,howlAt,burstDuration,{pitch:72,intensity:1.6,attack:.018});
tone(b,howlAt,burstDuration,48,.55,{decay:.22,sweep:-18});
save('07_Bosses/Alien/alien_transition_full_5s.wav',b,.56,{use:'전환 시작에 1회. 4.208333초에 하울링 폭발, 5초 종료. Animator speed=1 기준',preview:'Howl',howlOnset:howlAt});
b=buf(burstDuration);roar(b,0,burstDuration,{pitch:72,intensity:1.6,attack:.018});tone(b,0,burstDuration,48,.55,{decay:.22,sweep:-18});
save('07_Bosses/Alien/alien_howl_sync_0p792s.wav',b,.56,{use:'HowlFX 첫 프레임에 재생하는 단독 하울링. full_5s와 중복 재생하지 않음',preview:'Howl'});
b=buf(2.4);roar(b,0,1.95,{pitch:66,intensity:1.4,attack:.035});tone(b,0,1.8,43,.4,{decay:.4});echo(b,.13,.23,3);
save('07_Bosses/Alien/alien_howl_extended_2p4s.wav',b,.56,{use:'긴 잔향 대안. 4.208초에 시작하면 전환 후까지 지속되므로 별도 유지 재생기 필요',preview:'Howl'});

// Musical loops share a harmonic palette but use different tempo and orchestration.
/** Compose 16-bar looped synth arrangements with restrained percussion. */
function music(file,bpm,mode,label){
 const beat=60/bpm,bars=16,dur=bars*4*beat,b=buf(dur,2);
 const base=mode==='alien2'?[36,37,32,31]:mode==='stage2'||mode==='alien1'?[38,34,33,37]:mode==='head'?[38,41,37,33]:[38,34,41,36];
 const combat=['stage1','stage2','dog','head','cruiser','alien1','alien2'].includes(mode);
 const busy=['dog','cruiser','alien2'].includes(mode);
 const hz=m=>440*Math.pow(2,(m-69)/12);
 for(let bar=0;bar<bars;bar++){
  const root=base[Math.floor(bar/4)%4],start=bar*4*beat;
  if(bar%4===0){
   [12,19,22,26].forEach((v,j)=>tone(b,start-.25,16*beat+.5,hz(root+v),combat?.055:.085,{pad:true,pan:(j-1.5)*.45,harm:.02}));
  }
  const bassSteps=combat?8:4;
  for(let k=0;k<bassSteps;k++){
   const at=start+k*4*beat/bassSteps;
   tone(b,at,beat*(combat?.46:.85),hz(root+(k===6?7:0)),combat?.24:.13,{decay:beat*.19,harm:combat?.22:.07,pan:0});
  }
  const pattern=busy?[12,19,24,22,12,26,19,22]:[24,19,22,26];
  pattern.forEach((v,j)=>{
   if(mode==='alien1'&&j%2===1)return;
   const at=start+(j*4/pattern.length+.25)*beat;
   tone(b,at,beat*(combat?1:1.8),hz(root+v),combat?.068:.10,{decay:beat*(combat?.18:.35),fm:mode.includes('alien')?.5:0,harm:.04,pan:j%2?.32:-.32});
   tone(b,at+beat*.5,beat*1.2,hz(root+v),.018,{decay:beat*.3,pan:j%2?-.4:.4});
  });
  if(combat){
   for(let k=0;k<4;k++){
    if(mode==='head'&&k===1)continue;
    const at=start+k*beat;
    tone(b,at,.23,51,.40,{decay:.055,sweep:-65,harm:.04});
    noise(b,at,.025,.09,{high:true,decay:.005,pan:0});
    if(k%2===1){noise(b,at,.16,.20,{high:true,decay:.035,pan:.1});tone(b,at,.1,170,.10,{decay:.025});}
   }
   for(let k=0;k<(busy?16:8);k++)noise(b,start+k*beat*(busy?.25:.5),.045,.055,{high:true,decay:.009,pan:k%2?.3:-.3});
   if(bar%4===3)for(let k=0;k<3;k++)tone(b,start+(3+k*.25)*beat,.13,150-k*25,.13,{decay:.035,pan:(k-1)*.2});
  }else if(mode==='select'||mode==='prep'){
   for(let k=0;k<4;k++)noise(b,start+(k+.5)*beat,.065,.035,{high:true,decay:.015,pan:k%2?.25:-.25});
  }
 }
 save(file,b,combat?.40:.35,{use:label,loop:true,bpm,bars});
}
music('01_BGM/mainmenu_signal_loop.wav',84,'menu','MainMenu 대체 BGM: 기존 음악과 택일');
music('01_BGM/preparation_terminal_loop.wav',96,'prep','EquipmentManage·CharacterSelect 단독 실행용. Town 추가 씬에서는 Town BGM 유지 권장');
music('01_BGM/stage_select_navigation_loop.wav',100,'select','StageSelect 단독 실행용. Town 위에서 열면 Town BGM 유지');
music('01_BGM/stage1_frontier_loop.wav',112,'stage1','Stage1 일반 전투');
music('01_BGM/stage2_biomechanical_loop.wav',120,'stage2','Stage2 일반 전투');
music('01_BGM/boss_dog_pursuit_loop.wav',128,'dog','Stage1 중간 보스 Dog');
music('01_BGM/boss_head_distortion_loop.wav',112,'head','Stage1 최종 보스 Head');
music('01_BGM/boss_cruiser_siege_loop.wav',120,'cruiser','Stage2 중간 보스 Cruiser');
music('01_BGM/boss_alien_phase1_loop.wav',108,'alien1','Stage2 Alien 1페이즈');
music('01_BGM/boss_alien_phase2_loop.wav',132,'alien2','Stage2 Alien 2페이즈: 하울링 후 시작');

// Category previews are explicitly separated from runtime assets.
for(const [group,list]of Object.entries(previewGroups)){
 const gap=Math.round(.35*SR),n=list.reduce((v,c)=>v+c.b[0].length+gap,0),b=[new Float64Array(n)];
 let at=0;for(const c of list){b[0].set(c.b[0],at);at+=c.b[0].length+gap;}
 save('00_Previews/'+group.toLowerCase()+'_preview.wav',b,group==='Howl'?.56:.4,{use:'청취용 모음: 게임 연결 금지',previewOnly:true});
}
fs.writeFileSync(path.join(ROOT,'manifest.json'),JSON.stringify(manifest,null,2));
fs.writeFileSync(path.join(ROOT,'Town_guide.txt'),townLabels,'utf8');
console.log('TOTAL '+manifest.length);
