# Warrior Shield / Slash actions
제작: 2026-09-29. 내장 image_gen으로 생성한 뒤 크기·기준점을 정렬한 에셋입니다.

## 게임용 파일
Frames/Shield_Deploy: 12장
Frames/Shield_Guard: 6장
Frames/Shield_Lower: 12장
Frames/Slash_Attack: 6장
총 36장, 각 2048×2048 RGBA PNG, 투명 배경, 개별 프레임.

## Unity 임포트
Texture Type: Sprite (2D and UI)
Sprite Mode: Single
Pixels Per Unit: 100
Max Size: 2048
Pivot: Center (0.5, 0.5)
Filter Mode: Point
Compression: None
Generate Mip Maps: Off
Alpha Is Transparency: On
기존 Transform Scale 유지. 플랫폼 Override도 확인하세요.
PNG 파일 자체에는 Unity PPU 및 Pivot 설정이 저장되지 않습니다.
아틀라스 Max Size는 개별 PNG 설정과 별개입니다. 2048 프레임과 패딩 때문에 2048 아틀라스 패킹이 실패하면 아틀라스 크기를 별도로 조정해야 합니다.

## 최신 원본 기준
Shield 몸체: Assets/99.StoreAsset/Main/Character/Warrior/2.Shield/Shield_Idle/Shield_Idle_01.png
Slash 몸체 및 검: Assets/99.StoreAsset/Main/Character/Warrior/3.Slash/Slash_Idle/Slash_Idle_01.png
최대 방패: Assets/99.StoreAsset/Main/Character/Warrior/2.Shield/Shield_Guard/Shield_Guard_01.png

Idle의 머리부터 발까지 기준 높이 264px, 발 기준 y=1162, 공통 피벗 (1024,1024).
Shield 새 동작은 생성된 몸체와 장비 영역을 나누어 각각 균일 배율로 정렬했습니다.
Guard 방패 최대 높이는 원본 PNG의 알파 범위 1320px를 기준으로 맞췄습니다.
기존 Guard는 Unity에서 1024px 텍스처/PPU33.33334로 읽혔습니다. 원본 3072px/PPU100과 같은 월드 크기입니다.
몸체를 줄이는 동안 방패까지 같이 줄어들지 않도록 별도로 정규화했습니다.
Slash 공격은 기준 몸체 배율을 유지하며 숙이는 동작은 화면상 높이가 낮아집니다.

## 연결
Deploy 01과 Lower 12: 현재 Shield Idle 01 원본을 그대로 사용.
Deploy 12와 Guard 01: 동일 프레임.
Lower 01~12: Deploy 12~01의 역순. 별개의 12개 파일로 제공.
Slash Attack 06: 현재 Slash Idle 01 원본을 그대로 사용.
Slash 03~04의 검 궤적은 PNG에 포함되어 있습니다. 기존 별도 Trail과 중복 여부를 적용 시 확인하세요.
애니메이터, 클립, Sprite Library, 프리팹, 원본 Assets는 수정하지 않았습니다.
새 파일의 Label 연결과 클립 교체는 사용자가 직접 진행해야 합니다.

## 미리보기와 검증
preview.html: 재생, 프레임 선택, 몸체 확대, 현재 Idle 겹쳐 보기.
Reference는 제작 참조이며 Frames만 게임용으로 사용하세요.
validation.csv: 생성 프레임 몸체 기준 높이와 출력 배율 기록. Shield 행의 마지막 값은 방패 높이입니다.
Deploy/Lower의 원본 재사용 프레임은 해당 생성 기록에서 제외됩니다.
검증 결과는 CHECKS.txt에 기록했습니다.
실제 Unity 임포트/인게임 재생은 검증하지 않았습니다.

AI 재생성으로 갑옷과 장비의 세부 도트가 원본 픽셀과 완전히 동일하지는 않습니다.
몸체 높이·발 위치·최대 방패 높이 정렬과 별개로 외형 세부는 미리보기에서 확인해 주세요.

