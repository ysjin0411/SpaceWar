# Boss Alien Missile Blast v2
전용 미사일 폭발 애니메이션용 개별 PNG 4개. 프로젝트 파일은 수정하지 않았습니다.
내장 image_gen으로 각각 생성했고 원본 RGBA를 그대로 복사했습니다. 크기변경/배경제거 후처리는 하지 않았습니다.
generation_prompts.json에 실제 생성 프롬프트가 있습니다. 요청한 1024 대신 출력은 모두 1254x1254이며 기존 에셋과 동일한 캔버스입니다.

## Unity Import
Texture Type: Sprite (2D and UI)
Sprite Mode: Single
Pixels Per Unit: 600 (미사일 본체의 PPU720은 유지)
Pivot: Center (0.5, 0.5)
Mesh Type: Full Rect
Filter Mode: Point
Compression: None
Max Size: 2048
Generate Mip Maps: Off
Alpha Is Transparency: On
캔버스를 자르거나 각 프레임을 개별 크기로 맞추지 마세요.

## 크기 확인
01 점화 약 1.14 x 1.18유닛
02 팽창 약 1.73 x 1.73유닛
03 최대 폭발 약 1.95 x 1.96유닛
04 잔광 약 1.87 x 1.87유닛
모두 Scale1, PPU600, alpha>=16 외곽 기준입니다. 원본 미사일 보이는 길이 약1.40유닛, 기존 폭발 최대지름 약1.94유닛.
최대 폭발 중심 기준 외곽 반경은 불꽃/파편 때문에 원형 판정과 정확히 일치하지 않습니다.
향후 이펙트의 기준 반경을 약1유닛으로 두고 radius에 따라 Visual 자식의 크기를 조절합니다.
그림 크기는 애니메이션 진행에 따라 커졌다가 소멸합니다. 판정 Radius 자체는 스프라이트 외곽과 별개입니다.

## 프리팹 제작 예정
Alien_MissileBlast_01 → 02 → 03 → 04, Samples10, Loop Time Off.
전용 BossAlienMissileExplosion 프리팹으로 만들고 재생 종료 후 제거.
폭발 피해는 미사일 한 발당 한 번만 적용. 애니메이션 매 프레임 피해 금지.
현재 패키지는 이미지이며 프리팹과 스크립트는 사용자가 프로젝트에서 다음 단계에 작성합니다.

## 합의된 패턴 구현 방향 (아직 미구현)
- BossPattern을 상속하는 미사일 패턴 Config를 기존 일반 패턴 목록에 Grab과 함께 등록.
- 기존 목록의 Weight로 선택. 현재 컨트롤러는 직전 패턴 가중치 배율도 반영함.
- 소켓별 FirstDelay, Interval, ShotCount, 곡선 제어점 오프셋 설정.
- FirstDelay 기준 시점은 해치가 열린 Animation Event 수신 시각.
- 기존 NotifyMissileFire(0) 이벤트를 발사 시퀀스 시작 신호로 재정의하거나 명시적인 시작 이벤트로 교체. 연발 타이밍은 Config로 관리.
- 각 발 발사 순간 플레이어 위치를 새로 읽어 목표점 고정. 발사 후 실시간 추적하지 않음.
- 3차 베지어의 제어점 2개로 위/아래 및 바깥쪽으로 휘어 목표점에 도달.
- Config에서 1발당 Damage, ExplosionRadius, 비행 시간, MissilePrefab, ExplosionPrefab 지정.
- 목표 도착 시 폭발 범위를 검사하고 1회 피해 적용. 시각 효과는 별도 프리팹.
- 플레이어 실드, 보스/플레이어 사망, 패턴 중단 정책은 기존 코드를 확인하며 연결.

