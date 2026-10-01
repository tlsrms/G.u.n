# 바위 보스 — 탑뷰 관절 리그

보스전 순서는 **2스테이지**입니다. 1스테이지에는 `MafiaBoss.prefab`을 사용합니다. 아직 곡/전투 채보에 연결하지 않은 리그 에셋이며, 기존 튜토리얼 Stage01~05와는 별도의 보스전 배정입니다.

`RockBoss.prefab`을 씬에 배치해 사용합니다. 머리 윗면, 넓은 등과 어깨, 전방으로 내민 거대한 주먹이 보이는 흑백 도형 보스입니다. 전방은 local +Y, 화면 안쪽은 Z입니다. 발/다리/정면 얼굴은 없습니다. 8종 바위 스프라이트를 16개 독립 파츠로 배치했습니다.

## 애니메이션 편집

루트 `RockBoss`에는 Animator가 있습니다. 루트를 선택하고 Animation 창에서 클립을 만든 뒤 Controller를 연결하면 됩니다. 현재 자동 재생 Controller나 팔다리 포즈를 덮어쓰는 스크립트는 없습니다.

| 경로 / 관절 | 용도 |
| --- | --- |
| Motion | 보스 전체의 돌진·반동·들썩임. 게임상 위치를 담당하는 루트와 분리 |
| Motion/Body | 몸통 비틀기, 리듬에 따른 준비 동작 |
| Body/Neck | 머리 회전. 자식 Head는 머리 외형 |
| Body/LeftShoulder → LeftElbow → LeftWrist | 왼팔 준비·내려찍기 |
| Body/RightShoulder → RightElbow → RightWrist | 오른팔 준비·내려찍기 |
| LeftWrist/LeftSlamContact, RightWrist/RightSlamContact | 충격파·먼지·맵 파괴 발생 위치 |
| Body/BackLeft, BackCenter, BackRight | 등 바위 조각의 들림·분리·복귀 |
| 어깨 아래 LeftOuterShard / RightOuterShard | 어깨 바위 흔들림·파편 분리 |
| Body/CorePivot/CoreTarget | 코어 타격 또는 노출 연출의 기준 위치 |
| GroundCenter / Forward | 바닥 중심 및 기본 전방 기준점 |

표의 Body 경로는 `RockBoss/Motion/Body` 기준입니다. 회전은 local Z, 팔의 높낮이처럼 보이는 준비 동작은 local position/scale 트랙으로 제작합니다. 관절 scale은 기본 (1,1,1)이고 외형 크기는 각 관절의 자식 SpriteRenderer에만 적용되어 있습니다. 양손을 독립적으로 애니메이션할 수 있습니다.

박자 연동 시 곡의 시간축에서 Animator 상태/클립 시간을 제어하고, 내려찍는 박자에서 SlamContact를 기준으로 효과·판정을 호출하면 됩니다. **충돌/피격 판정, 전투 AI, 곡 배정 및 공격 클립은 아직 포함하지 않은 캐릭터 리그 에셋입니다.**

## 소스

`Tools/Characters/BuildRockBoss.py`: 도형 꼭짓점·회색 면·관절 배치 및 오프라인 프리팹 제작 소스. 실행 중 오브젝트를 생성하지 않습니다. 재실행하면 보스 프리팹의 편집 내용을 덮어쓰므로 직접 수정한 포즈/설정은 별도로 보존하세요. GUID는 유지합니다.

`Tools/Characters/Previews/RockBossPreview.png`: 제작 좌표로 렌더링한 정적 미리보기이며 Unity 화면 캡처가 아닙니다.
