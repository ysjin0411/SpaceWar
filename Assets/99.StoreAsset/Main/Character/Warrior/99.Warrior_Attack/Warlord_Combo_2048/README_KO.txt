워로드 GiantSword / Hammer 3연격 스프라이트

1. 결과물
Frames/GiantSword/Attack1 : 24 프레임
Frames/GiantSword/Attack2 : 28 프레임
Frames/GiantSword/Attack3 : 40 프레임
Frames/Hammer/Attack1    : 24 프레임
Frames/Hammer/Attack2    : 28 프레임
Frames/Hammer/Attack3    : 40 프레임
각 무기의 Recovery      : 16 프레임 (선택적으로 사용하는 대기 자세 복귀)
공격 본편 총 184 PNG, 선택 복귀 포함 총 216 PNG입니다.
모두 개별 2048 x 2048 RGBA PNG이며, 캐릭터/무기/Trail이 한 장에 포함됩니다.
시트를 잘라 쓸 필요가 없고, 다른 프레임과 겹치지 않습니다.

2. Unity 임포트 설정
Texture Type             : Sprite (2D and UI)
Sprite Mode              : Single
Pixels Per Unit          : 60
Pivot                    : Center (0.5, 0.5)
Mesh Type                : Full Rect
Max Size                 : 2048
Filter Mode              : Point (no filter)
Compression              : None
Generate Mip Maps        : Off
Alpha Source             : Input Texture Alpha
Alpha Is Transparency    : On
Wrap Mode                : Clamp
기존 SpriteRenderer/Transform의 크기는 유지합니다.
Frames 폴더의 PNG들을 한꺼번에 선택해 공통 설정을 적용하면 됩니다.

3. PPU를 60으로 지정하는 이유
현재 연결된 원본 Idle PNG는 3072 x 3072, PPU 100입니다.
새 프레임은 원본 몸체와 무기에 동일하게 0.6 배율을 적용했습니다.
따라서 새 픽셀 수 / 60 = 원본 픽셀 수 / 100 이므로 월드 공간의 크기가 같습니다.
PPU 100을 그대로 적용하면 캐릭터와 무기가 작아집니다.
2048은 캔버스 및 텍스처의 최대 크기이며, 몸체 자체의 높이를 뜻하지 않습니다.
원본의 피벗을 새 캔버스의 중앙으로 옮겼고 원본 발-피벗 간 거리를 보존했습니다.
새 PNG에는 Center 피벗을 사용하세요. 원본의 0.42382812 피벗 값을 복사하지 마세요.

4. 애니메이션 구성
파일명의 숫자 순서대로 사용합니다. 권장 Samples는 30 FPS입니다.
Attack1: 머리 위 준비 -> 전방 아래로 내려베기/내려치기.
Attack2: 1연격 끝 자세 -> 왼쪽으로 준비 -> 왼쪽에서 오른쪽으로 수평 휘두르기 -> 무기를 몸 뒤로 보내기.
Attack3: 2연격 끝 자세 -> 점프 -> 몸체와 무기의 360도 회전 -> -45도 방향으로 내려찍기.
화면 좌표에서는 오른쪽 아래 방향이며, 수학 좌표계의 각도로 -45도입니다.
Recovery는 Attack3의 끝 자세에서 첫 준비 자세로 돌아옵니다. 3연격 본편과 별도입니다.
Attack1 마지막과 Attack2 처음, Attack2 마지막과 Attack3 처음은 픽셀 단위로 같습니다.
Recovery를 포함하면 마지막과 처음도 같아서 연결된 반복 미리보기가 가능합니다.
3연격이 종료된 뒤 게임의 기존 Idle로 전환하려면 Recovery 사용 여부를 직접 선택하세요.

5. Trail과 투명도
Trail은 원본 무기의 프레임별 이동 경로를 따라 계산한 주황/금색 곡선입니다.
PNG에는 배경, 지면, 바닥 그림자가 없습니다. Trail의 발광 가장자리는 반투명입니다.
GIF와 Overview 이미지의 어두운 배경은 미리보기용이며 PNG에는 포함되지 않습니다.
Trail이 이미 PNG 안에 있으므로 기존 별도 Trail 이펙트를 함께 재생하면 중복됩니다.

6. 제작 및 검수 범위
이전 생성 시트의 비율이 달라지는 문제를 피하기 위해 현재 장착 원본 이미지의 몸체/무기 픽셀을 재사용했습니다.
몸체는 부위별 관절 회전으로 움직이고, 가려져 있던 허리/손잡이의 작은 영역은 연결을 위해 보완했습니다.
프레임마다 몸체를 다시 그리거나 자동 크롭에 따라 크기를 바꾸지 않았습니다.
2연격에서 검/자루의 보이는 길이가 달라지는 부분은 수평 회전의 투영이며, 원본 배율 변경이 아닙니다.
망치 머리는 옆면을 유지하며 앞면은 원본에 있는 좁은 면만 보이도록 구성했습니다.
Validation.json에 PNG 규격, 투명 가장자리, 연결 프레임, 관절 배율, 회전각 검사 결과가 있습니다.
Unity 프로젝트 파일, 프리팹, 애니메이터, 스프라이트 라이브러리는 수정하지 않았습니다.
Unity 에디터에 직접 적용하거나 게임에서 재생 테스트한 결과는 아닙니다.
