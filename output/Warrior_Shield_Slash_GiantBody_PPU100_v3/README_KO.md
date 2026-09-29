# Warrior — GiantSword body reference / PPU100

사용할 최종 폴더: Frames
Shield_Idle / Shield_Run / Slash_Idle / Slash_Run 각각 24장, 총 96장.
게임용 파일: 각각 2048×2048 RGBA PNG, 투명 배경, 프레임별 개별 파일.
기존 v1 및 PPU100_v2와 크기 기준이 다릅니다. 이번 v3만 사용하세요.

## Unity Import 설정
Texture Type: Sprite (2D and UI)
Sprite Mode: Single
Pixels Per Unit: 100
Max Size: 2048
Pivot: Center (0.5, 0.5)
Mesh Type: Full Rect
Filter Mode: Point
Compression: None
Generate Mip Maps: Off
Alpha Is Transparency: On
Wrap Mode: Clamp
플랫폼별 Override에도 위 값이 적용되는지 확인하세요.
기존 Transform Scale은 그대로 두세요.
PNG에는 Unity Pivot/PPU 값이 내장되지 않습니다. 위 값을 Inspector에서 설정해야 합니다.
이전 안내의 PPU66.666667, Pivot(0.5,0.423828125)은 이번 파일에 사용하지 마세요.

## 몸체 기준
현재 프로젝트의 GiantSword_Idle_01.png 및 GiantSword_Move_01.png를 기준으로 삼았습니다.
대검까지 포함한 전체 알파 경계 높이를 기준으로 삼지 않았습니다.
기준은 대검을 제외한 몸체 영역의 머리/안테나부터 발까지의 높이입니다.
Idle 기준 몸체: 264px. 달리기 첫 자세의 기준 몸체: 238px.
기준 Idle 발 바닥: 위쪽 기준 y=1162. 공통 Pivot 픽셀: (1024,1024).
PPU100에서 Idle 기준 높이는 2.64 월드 단위입니다.
Idle은 생성 과정에서 달라진 프레임 크기를 몸체 높이 264px로 정규화했습니다.
달리기는 첫 프레임을 기준 자세 238px에 맞춘 뒤 해당 동작에 동일 배율을 적용했습니다.
달리기는 다리 굽힘 때문에 화면상 전체 높이가 프레임마다 달라집니다.

## 제작과 검증
몸체만 보이는 GiantSword 참조와 기존 Shield/Slash 장비만 분리한 참조로
내장 image_gen을 사용해 다시 생성했습니다.
슬래시 Idle은 동일한 체형 참조의 대기 프레임 두 생성본에서 18+6장을 조합했습니다.
몸체와 장비를 같은 배율로 패키징해 생성된 장비의 가로세로 비율을 유지했습니다.
이 작업은 원본 무기 픽셀을 모든 프레임에 그대로 복사한 방식은 아닙니다.
기준 높이/캔버스/Pivot을 맞췄지만, AI 생성된 세부 도트와 해부학적 폭,
장비의 세부 형상이 원본과 픽셀 단위로 동일함을 보장하지 않습니다.
이 한계는 크기 보정과 별개입니다.

PNG 수, 캔버스 크기, 투명 모서리, 빈 프레임과 출력 경계 잘림을 확인했습니다.
각 *_validation.csv는 몸체 높이와 최종 알파 경계의 측정 기록입니다.
Preview/Compare_Idle.png와 Compare_Run.png는 모두 같은 픽셀 배율로 나란히 배치했습니다.
preview.html에서 대검 원본을 겹쳐 보거나 1장씩 넘겨 볼 수 있습니다.
미리보기/Reference 폴더는 게임용 스프라이트로 사용하지 마세요.

## 재생
Idle: 12fps부터 확인 (24프레임/2초), Loop On.
Run: 40fps부터 확인 (24프레임/0.6초), Loop On.
기존 공용 Idle이 Label01~48이라면 별도 24프레임 클립을 사용하거나
24장을 각 2개의 Label에 배정해야 합니다.
이번 작업은 새 에셋 제작만 수행했으며 기존 Assets/클립/Library/프리팹은 수정하지 않았습니다.
새 에셋을 Unity에 임포트하여 실제 게임 재생을 확인하지는 않았습니다.

생성 프롬프트: PROMPTS.txt

