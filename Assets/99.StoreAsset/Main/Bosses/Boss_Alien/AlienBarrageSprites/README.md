# Alien 포격 스프라이트

프로젝트에 자동으로 추가하지 않은 전달용 에셋입니다. 내장 image_gen으로 생성했습니다.

- Missile_01~03.png: 아래를 향한 미사일 3종. 각 파일은 별도 외형입니다.
- Explosion_01~04.png: 점화 → 확장 → 최대 폭발 → 잔광 순서입니다.
- 모든 PNG는 1254×1254이며 투명 배경입니다. 원본의 알파를 유지했습니다.

## Unity 가져오기 권장값

- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Pivot: Center (0.5, 0.5)
- Filter Mode: Point (no filter)
- Compression: None
- Generate Mip Maps: Off
- Alpha Is Transparency: On
- Max Size: 2048
- 미사일 PPU: 805 (외형 길이 약 1.4유닛)
- 폭발 PPU: 600 (최대 폭발 지름 약 1.96유닛). 4개 프레임 모두 같은 PPU를 사용하세요.

기존 Missile_Body는 PPU720, 보이는 길이 1009px로 약 1.40유닛입니다. 새 이미지는 실제 외형 픽셀 길이가 달라 PPU805를 권장합니다. 기존 폭발과 같은 PPU600을 사용합니다. 비교 기준은 Transform Scale (1,1,1)이며 부모 Scale에 따라 최종 크기가 달라집니다.

기존 미사일 그림은 왼쪽을 향하지만 새 그림은 아래를 향합니다. 기존 일반 미사일 프리팹에 그대로 교체하기보다 포격 전용 외형으로 사용하세요.

폭발 애니메이션 시작값: Samples 12, 프레임 0/1/2/3에 Explosion_01/02/03/04를 배치합니다. 잔광은 이후 DOTween으로 페이드아웃할 예정입니다. Unity에서 애니메이션 재생과 피벗 안정성은 아직 검증하지 않았습니다. 이미지 크기와 실제 피해 radius는 별도입니다.

생성 지시 요약: 개별 투명 PNG, 기존 금속·빨강·보라 픽셀풍의 아래 방향 미사일 3종, 노랑·주황·보라 방사형 폭발 4단계, 글자·배경·시트 없음. generation_prompts.json에 개별 지시를 보관했습니다.
