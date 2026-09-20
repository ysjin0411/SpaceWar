# Alien Resonance Sprite v1

생성: 내장 image_gen 도구. 프로젝트 파일은 변경하지 않았습니다.

## 에셋
- PNG 원본: 724×2172, RGBA, 완전 투명 픽셀 약 69.5%.
- Unity 스프라이트 영역: X 140, Y 12, Width 430, Height 2136. 외곽의 거의 보이지 않는 알파 1~3 노이즈와 투명 여백을 제외합니다. PNG 자체는 수정하지 않았습니다.
- PPU 1068, Pivot Center, Point 필터, 압축 없음, Max Size 4096.
- 네이티브 스프라이트 영역 크기 약 0.4026×2 유닛. 실행 코드는 Config의 두께×너비(현재 0.4×2)에 맞춥니다.
- 한 장의 이동 충격파 스프라이트입니다. 프레임 애니메이션은 포함하지 않습니다.

## 적용
1. 플레이 모드를 종료합니다.
2. Sprites 폴더를 PNG와 .meta를 함께 Windows 파일 탐색기로 프로젝트의 원하는 에셋 폴더에 복사합니다. PNG만 Unity 창에 드래그하면 동봉한 Import 설정은 적용되지 않습니다.
3. Code/BossAlienResonanceWave.cs 내용을 기존 프로젝트의 같은 스크립트에 붙여 넣습니다. 기존 스크립트와 .meta는 삭제하지 않습니다.
4. BossAlienResonanceWave 프리팹 루트의 Line Renderer만 제거합니다.
5. 루트 아래 Visual 자식을 만들고 Transform을 Reset합니다. Sprite Renderer를 추가합니다.
6. PNG를 펼쳐 나온 Alien_ResonanceWave_01 스프라이트를 Visual의 Sprite에 연결합니다. Color White, Material LaserLineMaterial, Sorting Layer Default, Order in Layer 16.
7. 루트의 BossAlienResonanceWave 컴포넌트의 Visual 필드에 자식 SpriteRenderer를 연결하고 저장합니다. 루트 Scale은 (1,1,1)을 유지합니다.

예고용 BossAlienAttackWarning의 Line Renderer는 유지합니다. 이동과 피해 판정은 기존 방식입니다. 실제 Unity 컴파일/플레이 검증은 사용자가 적용 후 수행해야 합니다.

## 생성 프롬프트
Use case: stylized-concept. Asset type: production 2D Unity pixel-art enemy energy shockwave sprite, ONE isolated sprite, not a sheet. Create a striking purple alien resonance shockwave for a detailed pixel-art sci-fi shooter. A tall narrow vertical ragged plasma blade moving toward the RIGHT: nearly straight jagged white-hot lavender leading spine along right side, branching violet electrical filaments and short torn magenta/purple plasma ribbons trailing LEFT. Shape is tall and narrow, approximately 1:5 width:height, centered, symmetrical vertical extent, NOT a ring, NOT a circular explosion, NOT a horizontal laser. Crisp deliberate pixel clusters and hard stepped edges, layered dark violet silhouettes, luminous lavender and white cores, restrained translucent violet edge glow. Strong readable silhouette at 0.4 units wide and 2 units tall. Portrait canvas ideally 512x1536; entire sprite and sparks contained, transparent padding all sides. No long disconnected sparks; tight compact outline. Actual transparent alpha background, absolutely no black backdrop, no checkerboard drawn into image, no floor, no text, no border, no characters. One coherent vertical electric shock front only.
