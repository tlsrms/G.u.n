# 1스테이지 보스 — 마피아

`MafiaBoss.prefab`은 담배를 문 날씬한 체격의 인간 마피아 보스입니다. 정수리 탑뷰, 일반적인 정장 어깨와 팔 비율, 뒤로 넘긴 머리, 흰 셔츠 소매와 반지 낀 손을 흑백 도형으로 표현했습니다. 다리와 정면 얼굴은 없습니다. 전방은 local +Y입니다. 기본 자세에서 머리·담배·양팔·손이 모두 화면 위쪽(+Y)을 향합니다.

9종 스프라이트를 사용한 12개 독립 외형 파츠이며, 루트 Animator를 갖춘 Transform 리그입니다. 보스전 순서는 **1스테이지 MafiaBoss / 2스테이지 RockBoss**입니다. 마피아의 현재 채보·전투 연결은 `Assets/Scenes/Stages/MafiaStage01.md`를 참고하세요.

## 관절과 연출 기준점

- `MafiaBoss/Motion`: 등장·퇴장·돌진·전체 반동. 씬 배치 루트와 분리
- `Motion/Body`: 묵직한 호흡, 몸통 비틀기, 준비 동작
- `Body/Neck`: 머리 방향. 자식 Head는 외형 파츠
- `Body/LeftShoulder → LeftElbow → LeftWrist`: 왼팔·손
- `Body/RightShoulder → RightElbow → RightWrist`: 오른팔·손
- 각 손목의 `LeftHandContact` / `RightHandContact`: 타격·효과 기준점
- `Body/Neck/Mouth/CigarettePivot`: 입에 문 담배의 위치/각도
- 담배 아래 `AshPivot/Ash`: 재를 흔들거나 떨어뜨리는 개별 파츠
- `AshPivot/SmokeOrigin`: 사무실 인트로의 연기 효과 연결 위치
- `Body/HitCenter`, 루트의 `GroundCenter`, `Forward`: 피격·바닥·전방 기준점

원본 프리팹의 Animator Controller는 비워 두었습니다. `MafiaStage01`의 보스 인스턴스에는 편집 연습용 Controller를 연결하고 Animator를 꺼 두어 자동 재생을 막았습니다. 처음 편집한다면 [마피아 모션 첫 연습](../../../Docs/MAFIA_MOTION_PRACTICE.md)을 따라 씬의 보스를 선택하세요. 관절의 local Z 회전과 local position을 키로 지정하면 됩니다. 부모 관절 scale은 (1,1,1)이고, 외형 크기는 자식 SpriteRenderer에만 적용했습니다. 담배는 손이 아니라 머리를 따라갑니다.

`Assets/Animations/Mafia/`에 스테이지용 클립 15개를 추가했습니다. 총을 드는 동작은 몸통을 비스듬히 세우고 양팔 관절을 맞춰 오른손·왼손·개머리판의 접촉을 유지합니다. 흡연·총 꺼내기·대기·단발/3발·부하 방패 자세·좌우 자세 전환·머리/몸통/양팔 피격을 같은 Controller에서 선택할 수 있습니다. 클립은 `Motion/Body` 아래만 제어하며 보스 루트의 실제 이동·회전이나 부하·문·책상은 별도로 연결합니다. 몸통 외형은 가로 `.98 → .72`, 앞뒤 `.48 → .38`로 줄였고, 어깨 X는 `±.39 → ±.29`로 좁혔습니다. 소매 폭도 맞췄으며 부모 관절의 Scale은 유지합니다. 15개 클립의 팔 자세를 새 어깨 간격에 맞춰 다시 계산했고, 사용자가 수정한 오른팔 연습 클립은 보존했습니다.

리그 자체에는 AI·판정 코드가 없습니다. 사무실에서는 `MafiaStageDirector`가 흡연·총 꺼내기·견착·3발 클립을 곡 시각으로 재생하며, `MafiaOfficeSet`이 연기와 효과를 담당합니다. 캐릭터 파츠와 기준점은 프리팹에 저장되어 있습니다.

제작 원본: `Tools/Characters/BuildMafiaBoss.py` (공통 보스 제작 함수는 BuildRockBoss.py).
정적 미리보기: `Tools/Characters/Previews/MafiaBossPreview.png`.
제작 스크립트를 재실행하면 해당 보스의 이미지·프리팹을 덮어쓰므로 Unity에서 편집한 포즈를 별도로 보존하세요. GUID는 유지합니다.

오른손에는 별도 AK 소총 파츠가 있습니다. `RightWrist/RifleGrip/AK`는 손목 포즈를 따라가며, 형제 `Muzzle`은 총구 끝의 사격 효과 기준점입니다. 기존 관절 경로와 프리팹 GUID는 유지했습니다.
