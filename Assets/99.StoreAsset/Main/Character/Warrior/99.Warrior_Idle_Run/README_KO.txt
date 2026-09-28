워로드 Idle + 달리기 Move 수정본

Move를 달리기로 교체했습니다.
- 무기별 Move 24프레임, 40 FPS, 0.6초 반복
- 짧은 지면 접촉, 양발이 뜨는 비행 구간, 높은 무릎 회수, 전진 기울기
- 기존 Idle 무기별 48프레임 / 20 FPS 그대로 포함
- 총 144개 개별 투명 RGBA PNG, 모두 2048 x 2048

Unity 설정:
Sprite (2D and UI), Single, PPU 60, Pivot Center (0.5, 0.5)
Max Size 2048, Mesh Type Full Rect, Point, Compression None
Mip Maps Off, Alpha Is Transparency On
Move Animation Sample Rate 40, Idle Sample Rate 20, Loop Time On
파일을 번호순으로 사용하세요. 실제 이동은 게임 코드가 담당합니다.

기존 원본과 앞서 제공한 공격/Idle 에셋에 같은 배율을 적용했습니다.
원본 픽셀 0.6배 / PPU 60은 원본 PPU 100의 월드 크기와 같습니다.
투명 여백을 자르거나 프레임마다 자동 크기 조절을 하지 마세요.
GIF/JPG는 배경이 있는 확인용이고 게임용 PNG는 투명 배경입니다.
Move 폴더의 이미지가 이번 달리기 수정본입니다.

프로젝트는 수정하지 않았습니다. Unity 내부 재생 테스트는 수행하지 않았습니다.
