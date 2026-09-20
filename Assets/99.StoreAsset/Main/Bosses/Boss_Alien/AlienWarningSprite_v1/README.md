# Alien Warning Sprite v1

내장 image_gen으로 생성. 프로젝트는 수정하지 않았습니다.

## 이미지와 크기
- PNG: 1254×1254, RGBA. 외부 배경 투명. 중앙 샘플 알파 17/255.
- Unity 영역: X110, Y126, W1034, H1000. PNG는 수정하지 않고 Import 영역으로 희미한 외곽 노이즈를 제외합니다.
- PPU500, Full Rect, Border 각 88px, Pivot Center, Point 필터, 압축 없음.
- SpriteRenderer의 Sliced와 size로 전달받은 실제 공격 범위에 맞춥니다. 루트 Scale은 반드시 (1,1,1).
- 경계 발광에는 약간의 시각 여백이 있으므로 최종 화면에서 판정과 가독성을 확인하세요.

## 적용 순서
1. 플레이 종료. Sprites 폴더를 PNG와 .meta를 함께 파일 탐색기로 프로젝트 에셋 폴더에 복사합니다.
2. Code/BossAlienAttackWarning.cs의 내용을 기존 스크립트에 붙여 넣습니다. 프로젝트에 같은 이름의 코드를 하나 더 추가하지 않습니다. 기존 .meta는 유지합니다.
3. BossAlienAttackWarning 프리팹 루트의 Line Renderer를 제거하고 Sprite Renderer를 추가합니다.
4. PNG를 펼친 Alien_AttackWarning_01 스프라이트를 연결합니다. Material LaserLineMaterial, Color White, Draw Mode Sliced, Sorting Layer Default, Order in Layer 8.
5. 기존 컴포넌트 Start Alpha 0.35, End Alpha 0.9로 설정합니다. 기존 직렬화 값이 남으므로 직접 바꿉니다.
6. BossAlienResonancePattern.cs의 기존 ShowAttack 함수 전체를 아래로 교체합니다.

```csharp
// 공격 표시 방법은 예고 컴포넌트가 담당한다.
private static void ShowAttack(BossAlienAttackWarning warning)
{
    warning.ShowAttack();
}
```

패턴 에셋의 Warning Prefab 연결은 같은 프리팹을 수정하므로 유지됩니다. Unity 컴파일과 실제 화면 확인은 적용 후 필요합니다.

## 생성 프롬프트
Use case: stylized-concept. Asset type: ONE production Unity 2D sci-fi enemy attack warning decal sprite, designed for 9-slice rectangular resizing. Square 1024x1024 canvas. A clean axis-aligned SQUARE DANGER ZONE outline, perfectly straight thin hot-pink/crimson luminous edges, small sharp pixel-art reinforced corner brackets, no rounded corners, all four outer edges form a precisely aligned rectangle. Frame centered with equal modest transparent padding. Interior is mostly transparent with very faint translucent magenta diagonal hatch lines, restrained low-opacity pink fill; game characters and background must remain readable through it. The border should be bright, the interior subtle. Industrial alien hologram style compatible with detailed pixel-art space shooter. Corner details confined to corners; straight edges and simple center suitable for stretching with Unity 9-slicing. No text, letters, digits, symbols, logos, warning triangles, perspective, shadows, objects, scenery. Actual RGBA transparency, exterior completely transparent, no baked checkerboard or black background. Entire frame visible without clipping. ONE isolated warning area sprite only, not sheet or multiple assets. Crisp pixel clusters, not blurry illustration. This is an attack telegraph, NOT an explosion.
