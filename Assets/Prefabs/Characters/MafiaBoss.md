# 1스테이지 보스 — 마피아

`MafiaBoss.prefab`은 담배를 문 날씬한 체격의 인간 마피아 보스입니다. 정수리 탑뷰, 일반적인 정장 어깨와 팔 비율, 뒤로 넘긴 머리, 흰 셔츠 소매와 반지 낀 손을 흑백 도형으로 표현했습니다. 다리와 정면 얼굴은 없습니다. 전방은 local +Y입니다. 기본 자세에서 머리·담배·양팔·손이 모두 화면 위쪽(+Y)을 향합니다.

9종 스프라이트를 사용한 12개 독립 외형 파츠이며, 루트 Animator를 갖춘 Transform 리그입니다. 보스전 순서는 **1스테이지 MafiaBoss / 2스테이지 RockBoss**입니다. 기존 Stage01~05 튜토리얼 배치나 음악 연결을 변경하지 않았으며, 실제 보스전 채보/전투 연결은 후속 작업입니다.

## 관절과 연출 기준점

- `MafiaBoss/Motion`: 등장·퇴장·돌진·전체 반동. 씬 배치 루트와 분리
- `Motion/Body`: 묵직한 호흡, 몸통 비틀기, 준비 동작
- `Body/Neck`: 머리 방향. 자식 Head는 외형 파츠
- `Body/LeftShoulder → LeftElbow → LeftWrist`: 왼팔·손
- `Body/RightShoulder → RightElbow → RightWrist`: 오른팔·손
- 각 손목의 `LeftHandContact` / `RightHandContact`: 타격·효과 기준점
- `Body/Neck/Mouth/CigarettePivot`: 입에 문 담배의 위치/각도
- 담배 아래 `AshPivot/Ash`: 재를 흔들거나 떨어뜨리는 개별 파츠
- `AshPivot/SmokeOrigin`: 추후 연기 효과 연결 위치
- `Body/HitCenter`, 루트의 `GroundCenter`, `Forward`: 피격·바닥·전방 기준점

Animator Controller와 자동 재생 클립은 비워 두었습니다. 프리팹 루트를 선택하고 Animation 창에서 클립을 만든 뒤 Controller에 연결하세요. 관절의 local Z 회전과 local position을 키로 지정하면 됩니다. 부모 관절 scale은 (1,1,1)이고, 외형 크기는 자식 SpriteRenderer에만 적용했습니다. 담배는 손이 아니라 머리를 따라갑니다.

현재 리그에는 AI·피격 판정·공격 타임라인·연기 자동 생성이 없습니다. 모든 파츠와 기준점은 프리팹에 저장되어 있으며 런타임에 생성하지 않습니다.

제작 원본: `Tools/Characters/BuildMafiaBoss.py` (공통 보스 제작 함수는 BuildRockBoss.py).
정적 미리보기: `Tools/Characters/Previews/MafiaBossPreview.png`.
제작 스크립트를 재실행하면 해당 보스의 이미지·프리팹을 덮어쓰므로 Unity에서 편집한 포즈를 별도로 보존하세요. GUID는 유지합니다.

오른손에는 별도 AK 소총 파츠가 있습니다. `RightWrist/RifleGrip/AK`는 손목 포즈를 따라가며, 형제 `Muzzle`은 총구 끝의 사격 효과 기준점입니다. 기존 관절 경로와 프리팹 GUID는 유지했습니다.
