# 생성 및 합성 규격

최종 기준: 이전 검/해머 무기 PNG의 픽셀 크기와 디자인 유지. 무기 크기 배율 1.0, 캐릭터 기준 높이 440, 캔버스 3072×3072. 양손이 실제 손잡이를 감싸도록 손 앞뒤 레이어를 정리한다. 실드는 제외. 해머 Idle 축은 수평 대비 10도. Idle은 발/손/무기를 고정하고 가슴 내부 최대 1픽셀 변화. 투명 배경, 개별 PNG.

이번 기본 이미지 생성 도구의 준비용 포즈 프롬프트:

공통: Use case stylized-concept. Unity pixel-art animation sheet. Preserve identical warrior navy/orange/silver/cyan suit, helmet and body proportions. Transparent RGBA background, no haze or backdrop, no groundshadow, no text, no effects. Six SEPARATE sprites with huge gutters, each complete weapon and boots inside its cell. Landscape 3 columns 2 rows.

Sword3: Giant two-handed sword attack third finisher, six full-body poses in 3 columns 2 rows: ready raised; overhead windup; downward forward slash; deep impact followthrough; recovery; ready. BOTH hands clasp the handle with silver fingers visibly wrapping over it. Match referenced body. Same weapon as placeholder, it will be replaced with exact original asset later.

Hammer2: Heavy hammer second combo horizontal sweep to RIGHT, six full-body poses in 3 columns 2 rows: low ready; rear windup; sweeping forward; extended right; recovery; lowered ready. BOTH hands gripping shaft silver fingers visible. Match referencebody. Same weapon as placeholder, it will be replaced with original asset later.

Hammer3: Heavy hammer third combo overhead slam to RIGHT, six full-body poses in 3 columns 2 rows: raised ready; overhead windup; descending; ground impact; lifting recovery; lowered ready. BOTH hands gripping shaft silver fingers visible. Match referencebody. Same weapon as placeholder, will be replaced with original asset later.

참조: 앞서 생성한 GiantSword/Hammer 동작 포즈. 생성 결과의 임시 무기는 최종 이미지에서 기존 프로젝트 무기로 교체했다. 생성 원본은 완성본으로 패키징하지 않았다. 모든 동작이 프롬프트를 픽셀 단위로 그대로 따르는 것은 아니며, 최종 동작은 Previews에서 확인한다.
