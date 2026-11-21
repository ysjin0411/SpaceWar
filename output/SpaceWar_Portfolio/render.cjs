// 원본을 변경하지 않고 장면별 컷과 자막을 별도 파일로 생성한다.
const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');
const dir = __dirname;
const ffmpeg = 'C:/Users/ysjin/AppData/Local/Programs/Insights Capture V3/resources/app.asar.unpacked/resources/ffmpeg/bin/ffmpeg.exe';
const source = 'C:/Users/ysjin/Desktop/잡동사니/Captures/SpaceWar_Edit.mp4';
const clips = [
  [2, 6, 1, 'SPACE WAR  /  UNITY · C#', ['저장부터 전투 보상까지 연결한 2D 액션 게임']],
  [8, 18, 1, '01  /  ID별 JSON 저장', ['사용자 ID마다 별도의 JSON 파일로 진행 정보를 관리합니다.', '임시 파일에 기록을 마친 뒤 교체해 기존 저장 파일을 보호합니다.']],
  [20, 26, 1, '01  /  저장된 프로필 불러오기', ['저장된 ID 목록에서 프로필을 선택하고 진행 정보를 불러옵니다.']],
  [42, 54, 1.5, '02  /  퀘스트 수락과 진행', ['NPC 대화에서 퀘스트를 수락하고, 퀘스트 창에서 목표를 확인합니다.', '수락 · 목표 달성 · 보상 수령을 서로 다른 상태로 관리합니다.']],
  [66, 75, 1.5, '02  /  조건 검사와 보상', ['진행 데이터를 목표 조건과 비교하고, 달성 후 NPC에게 보상을 받습니다.']],
  [78, 87, 1.5, '03  /  전투 준비', ['캐릭터별 무기 보유·장착 정보를 관리하고 전투 구성에 반영합니다.']],
  [133, 157, 2, '04  /  데이터 기반 적 스포너', ['스폰 패턴은 ScriptableObject로, 실행 중 쿨다운은 별도 상태로 분리합니다.', '그룹별 주기에 맞춰, 쿨다운이 끝난 패턴을 가중치로 선택합니다.']],
  [168, 174, 1, '05  /  전투 중 성장', ['레벨업 시 선택 창을 열어 무기와 능력 강화로 전투 구성을 바꿉니다.']],
  [192, 208, 2, '06  /  보스 전투', ['보스의 특수 패턴 중에는 일반 적 생성을 멈춰 패턴에 집중하게 합니다.', '경고 영역과 공격 연출로 회피 타이밍을 전달합니다.']],
  [219, 223, 1, '07  /  보스 처치 → 보상', ['보스 처치 후 드롭 처리와 보상 흐름을 이어갑니다.']],
  [224, 232, 1, '07  /  타격형 트레져박스', ['타격 누적 → 충전 → 추가 타격으로 승급 또는 개봉합니다.', '상자의 최종 등급은 등급별 가중치로 미리 결정합니다.']],
  [314, 326, 1.5, '08  /  두 번째 보스', ['보스마다 다른 패턴을 구성해 전투 경험을 구분합니다.']],
  [328, 334, 1, '08  /  정신 혼란 패턴', ['전투 중 입력 미션을 제시해 이동·공격 외의 대응을 요구합니다.']],
  [340, 344, 1, '09  /  최종 보스 처치', ['최종 보스 처치와 보상 처리를 스테이지 종료 흐름에 연결합니다.']],
  [344, 352, 1, '09  /  보물상자와 스테이지 결과', ['상자의 승급·개봉은 상태 전이로 제어하고 연출에 알립니다.', '보상 처리가 끝나면 스테이지 결과를 표시합니다.']],
  [354, 360, 1, '10  /  전투 결과와 퀘스트 연결', ['마을로 돌아와 전투로 달라진 퀘스트 진행 상황을 확인합니다.']],
  [30, 35, 1, 'SPACE WAR  /  개인 프로젝트', ['세이브 · 퀘스트 · 스포너 · 보스 · 보상을 하나의 흐름으로 연결했습니다.']]
];
const assHeader = `[Script Info]\nScriptType: v4.00+\nPlayResX: 1920\nPlayResY: 1080\nWrapStyle: 2\nScaledBorderAndShadow: yes\n\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\nStyle: Body,Malgun Gothic,34,&H00FFFFFF,&H00FFFFFF,&H00100B06,&H00000000,-1,0,0,0,100,100,0,0,1,0,0,5,70,70,0,1\nStyle: Label,Malgun Gothic,23,&H00E0D260,&H00FFFFFF,&H00000000,&H00000000,-1,0,0,0,100,100,1,0,1,0,0,4,0,0,0,1\nStyle: Speed,Arial,25,&H00C9B9A9,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,0,0,5,0,0,0,1\n\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n`;
// ASS 및 SRT에 사용할 시간을 각각 센티초와 밀리초로 표기한다.
function assTime(t) { const cs=Math.round(t*100); return `${Math.floor(cs/360000)}:${String(Math.floor(cs/6000)%60).padStart(2,'0')}:${String(Math.floor(cs/100)%60).padStart(2,'0')}.${String(cs%100).padStart(2,'0')}`; }
function srtTime(t) { const ms=Math.round(t*1000); return `${String(Math.floor(ms/3600000)).padStart(2,'0')}:${String(Math.floor(ms/60000)%60).padStart(2,'0')}:${String(Math.floor(ms/1000)%60).padStart(2,'0')},${String(ms%1000).padStart(3,'0')}`; }
let cursor=0;
const cues=[];
const shots=clips.map(([start,end,speed,label,texts],i)=>{
  const duration=(end-start)/speed;
  const id=String(i).padStart(2,'0');
  let ass=assHeader;
  ass+=`Dialogue: 0,${assTime(0)},${assTime(duration)},Label,,0,0,0,,{\\pos(96,968)}${label}\n`;
  if(speed!==1) ass+=`Dialogue: 0,${assTime(0)},${assTime(duration)},Speed,,0,0,0,,{\\pos(1848,42)}${speed.toFixed(1)}x\n`;
  texts.forEach((text,j)=>{
    const a=duration*j/texts.length, b=duration*(j+1)/texts.length;
    if(b-a<2) throw new Error('Subtitle shorter than 2 seconds');
    ass+=`Dialogue: 1,${assTime(a)},${assTime(b)},Body,,0,0,0,,{\\pos(960,1020)}${text}\n`;
    cues.push({start:cursor+a,end:cursor+b,text,label});
  });
  fs.writeFileSync(path.join(dir,`shot_${id}.ass`),ass,'utf8');
  const shot={id,start,end,speed,duration,label,outputStart:cursor}; cursor+=duration; return shot;
});
fs.writeFileSync(path.join(dir,'edit_plan.json'),JSON.stringify({source,duration:cursor,shots,cues},null,2));
fs.writeFileSync(path.join(dir,'SpaceWar_Portfolio_KO.srt'),cues.map((c,i)=>`${i+1}\n${srtTime(c.start)} --> ${srtTime(c.end)}\n${c.text}\n`).join('\n'),'utf8');
// FFmpeg의 오류는 로그로 남기고 실패한 작업을 즉시 중단한다.
function run(args,logname) { return new Promise((resolve,reject)=>{const log=fs.createWriteStream(path.join(dir,logname)); const p=spawn(ffmpeg,args,{cwd:dir,windowsHide:true,stdio:['ignore','ignore','pipe']}); p.stderr.pipe(log); p.on('error',reject); p.on('close',code=>{log.end();code===0?resolve():reject(new Error(`${logname}: exit ${code}`));});}); }
async function render(shot) {
  const fadeOut=shot.id==='16'?',fade=t=out:st=4.3:d=0.7':'';
  const vf=`crop=1578:888:172:100,setpts=(PTS-STARTPTS)/${shot.speed},fps=30,scale=1664:936:flags=lanczos,pad=1920:1080:128:0:color=0x07111d,setsar=1,drawbox=x=96:y=943:w=1728:h=2:color=0x35c7d7:t=fill,ass=shot_${shot.id}.ass${fadeOut}`;
  const af=`asetpts=PTS-STARTPTS,atempo=${shot.speed},volume=0.8,afade=t=in:d=0.08,afade=t=out:st=${shot.duration-0.12}:d=0.12`;
  await run(['-hide_banner','-y','-ss',String(shot.start),'-t',String(shot.end-shot.start),'-i',source,'-vf',vf,'-af',af,'-t',String(shot.duration),'-c:v','libx264','-preset','veryfast','-crf','19','-threads','4','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-ar','48000','-video_track_timescale','30000',`shot_${shot.id}.mp4`],`shot_${shot.id}.log`);
  console.log(`Rendered ${shot.id}: ${shot.label}`);
}
// 동시에 두 컷만 인코딩해 편집 중 메모리 사용량을 제한한다.
async function main(){
  let index=0;
  async function worker(){while(index<shots.length){const shot=shots[index++];await render(shot);}}
  await Promise.all([worker(),worker()]);
  fs.writeFileSync(path.join(dir,'concat.txt'),shots.map(s=>`file 'shot_${s.id}.mp4'`).join('\n'));
  await run(['-hide_banner','-y','-f','concat','-safe','0','-i','concat.txt','-c:v','copy','-af','aresample=async=1:first_pts=0','-c:a','aac','-b:a','192k','-t','115','-movflags','+faststart','SpaceWar_Portfolio_115s.mp4'],'final.log');
  console.log(`COMPLETE: ${cursor}s`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
