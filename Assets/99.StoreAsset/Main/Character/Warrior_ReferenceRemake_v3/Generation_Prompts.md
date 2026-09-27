# 제작 프롬프트 명세

사용 도구: 내장 image_gen (CLI/API fallback 미사용).
참조: 프로젝트의 Warrior/GiantSword/Warrior_GiantSword_Idle_1.png, Warrior/GiantHammer/Warrior_Hammer_Idle_1.png, Warrior/Slash/Warrior_Slash_Idle_1.png, Warrior_Shield_Comparison.jpg, 분리된 GiantSword/Hammer Original_Weapon.

공통: reference-faithful compact angular navy mechanized suit; white/silver chest trim and faceplate; orange shoulders, helmet stripe, knees; cyan visor and backpack antenna. Fixed right-facing three-quarter game view, clean pixel clusters, complete limbs, real transparent RGBA background, isolated frames, no baked shadows or VFX.

GiantSword: six-column five-row sheet. Six idle, six alternating-leg walk, six horizontal cut, six rising reverse cut, six overhead downward slam. Whole-body articulation. Straight silver/orange/navy mechanical sword with cyan rails. Weapon 1.3× original relative to unchanged body, no body redesign.
Hammer: six-column five-row sheet. Same five state types, broad cylindrical segmented double-ended mechanical head and long rigid shaft; orange/silver/navy/cyan original design. Whole-body weight transfer, hammer 1.3× original relative to body.
Slash: six-column three-row sheet. Six idle, six run, six attack. Same original armor, replace dagger with mechanical longsword, no shield, clear intact leg articulation.
Shield: six-column four-row sheet. Six small-shield idle, six move, six mechanical deploy, six guard hold loop. Broad orange/navy symmetrical mechanical shield with cyan center strip and silver reinforcement. Only shield expands, body does not scale. Additional extraction prompt requested removal of all background haze while retaining sprites.
Rigid sword layer: single horizontal right-facing giant sword, exact source silhouette and palette, fully visible hilt and guard, orthographic view, no glow or background.
Rigid hammer layer: single sideways-T giant hammer with long horizontal handle and tall segmented vertical barrel head at right; exact source concept, transparent background.
Effects: six-column five-row sheet. Row1 cyan shield deployment fan to right, row2 stable cyan barrier loop, row3 white/cyan block collision flash, row4 orange/cyan hammer ground shock impact, row5 rightward cyan/orange slash projectile loop. All individual alpha sprites with clear gutters, no characters or weapons.

생성 원화의 프레임 수/분리/손 위치/투명 픽셀을 후처리로 검수했으며, 프레임별로 달라진 자이언트 소드·해머 크기는 고정된 새 무기 레이어로 대체했습니다. 궤적은 그 레이어를 실제 회전 구간에 표본 배치해 만들었습니다. 참고용 생성 궤적 시트는 최종 정렬 궤적 대신 사용하지 않았습니다.
