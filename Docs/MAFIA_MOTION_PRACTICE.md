# 마피아 모션 편집과 첫 연습

## 스테이지용 클립 15개

`Assets/Animations/Mafia/`에 Unity 기본 Animation 창에서 편집할 수 있는 `.anim` 클립을 준비했다. 기존 `Mafia_MotionPractice.controller`에 새 클립들을 추가했으므로 `MafiaStage01`의 **Mafia presentation / Mafia boss**를 그대로 선택하면 된다. 사용자가 수정한 `Mafia_RightArm_Practice`는 보존했다.

| 선택할 클립 | 길이 | 동작 |
| --- | --- | --- |
| `Mafia_01_SeatedSmoke` | 3초 · 반복 | 책상 대치용 상체 자세. 양팔을 쉬고 작은 호흡·고개·담배 움직임. AK는 숨김 |
| `Mafia_02_DrawAK` | 0.9초 | 오른손을 아래로 뻗어 AK를 꺼내고 양손으로 잡아 어깨에 견착 |
| `Mafia_03_AimIdle` | 1.8초 · 반복 | 양팔 견착을 유지하는 조준 대기·호흡 |
| `Mafia_04_FireSingle` | 0.32초 | 1발 반동 후 견착 복귀 |
| `Mafia_05_FireBurst3` | 0.72초 | 3번 구분되는 총기·어깨·몸통 반동 후 견착 복귀 |
| `Mafia_06_ShieldEnter` | 0.75초 | 몸을 낮추고 옆으로 비켜 부하 뒤에서 조준하는 자세로 전환 |
| `Mafia_07_ShieldIdle` | 1.8초 · 반복 | 부하 뒤에 몸을 숨긴 견착 대기 |
| `Mafia_08_ShieldFireBurst3` | 0.72초 | 부하 뒤의 자세에서 3발 연사 반동 |
| `Mafia_09_ShieldWithdraw` | 0.65초 | 부하 뒤 자세를 풀고 기본 견착으로 복귀 |
| `Mafia_10_AimAdjustLeft` | 0.6초 | 왼쪽으로 자리를 바꿀 때의 상체 무게 이동·시선 선행 |
| `Mafia_11_AimAdjustRight` | 0.6초 | 오른쪽으로 자리를 바꿀 때의 상체 무게 이동·시선 선행 |
| `Mafia_12_HitHead` | 0.36초 | 머리가 뒤로 밀리고 작게 반동을 거쳐 복귀 |
| `Mafia_13_HitTorso` | 0.36초 | 몸통이 크게 뒤로 밀리고 머리가 따라 반응 |
| `Mafia_14_HitLeftArm` | 0.36초 | 왼쪽 어깨·팔의 피격 반응. 두 손은 총에 유지 |
| `Mafia_15_HitRightArm` | 0.36초 | 오른쪽 어깨·팔과 총이 함께 밀렸다가 복귀 |

길이는 원본 모션의 기준값이다. 아직 최종 음악 박자나 채보 길이를 정한 것이 아니다. 기본 조준 방향은 보스 루트의 로컬 +Y이며, 씬에서 보스 전체를 회전해 플레이어를 향하게 한다.

## 지금 확인하는 순서

1. Unity의 게임 실행을 멈추고 `MafiaStage01` 씬을 연다. 새 에셋의 가져오기가 끝날 때까지 기다린다.
2. **Hierarchy** 검색창에서 `Mafia boss`를 찾아 선택한다. **Scene** 화면에 마우스를 놓고 **F**를 누른다.
3. **Window → Animation → Animation**을 연다. **Record**가 켜져 있으면 먼저 끈다.
4. Animation 창의 클립 이름 목록에서 **Mafia_03_AimIdle**을 고른다. 몸을 비스듬히 세우고 두 손으로 AK를 잡는 자세를 확인한다.
5. **Animation 창 안의 작은 ▶**로 재생한다. 다음으로 **Mafia_02_DrawAK**, **Mafia_05_FireBurst3**, **Mafia_12_HitHead**를 골라 차이를 본다.
6. 자세를 수정하려면 재생을 멈추고 원하는 시각을 선택한 다음 **Record**를 켜고 관절을 수정한다. 끝나면 **Record → Preview**를 끄고 **File → Save Project**로 저장한다.

Preview를 끄면 보스가 씬에 원래 저장된 자세로 돌아온다. 클립의 변경 사항은 남는다. Animator 컴포넌트는 계속 꺼 두었다. **사무실 인트로는 흡연·총 꺼내기·견착·3발 클립을 곡 시각에 연결했다.** 방패 등장·대기·난사·퇴장은 독립 `MafiaShieldAmbush` 프리팹에서 사용한다. 배치 방법은 `Assets/Prefabs/Characters/MafiaShieldAmbush.md`를 참고한다. 좌우 전환·부위 피격의 보스전 연결은 남아 있다.

몸 전체 느낌은 `Motion/Body`, 고개는 `Motion/Body/Neck`, 팔은 양쪽 `Shoulder → Elbow → Wrist`에서 수정한다. 두 손·총·어깨의 접점은 제작 시 맞춘 키프레임으로 저장했다. 총이나 한 팔을 바꾸면 다른 팔의 접점도 함께 맞춘다. 총을 꺼내는 동안 숨김은 `RightWrist/RifleGrip/AK`의 Scale 키로 처리하며, Preview 종료 시 원래 크기가 복원된다.

## 연출에 연결할 기준 시각과 범위

- 단발의 발사 기준은 클립 안 **0.08초**다. 두 3발 클립은 **0.12 / 0.28 / 0.44초**를 기준으로 각각 한 번 반동이 시작한다. 사무실 인트로의 총구 섬광·총성·탄환은 이 시각에 연결했다. 현재 클립에는 총알 생성이나 판정용 Animation Event가 없다.
- 총 꺼내기는 **0.34초부터 AK가 보이고 0.9초에 견착이 완료**된다. 그 뒤 조준 대기나 발사 클립을 이어 붙인다. 클립만 바꿔 보더라도 필요한 자세·총 표시가 모두 설정된다.
- 피격은 각 클립 시작 직후 빠르게 밀리고 약 0.05초까지 충격을 유지한 다음 반동을 거쳐 복귀한다. 앞에서 오는 공격을 기준으로 보스 로컬 뒤쪽으로 밀린다. 각 사격 노트를 맞은 부위 클립에 연결하는 작업은 후속 단계다.
- 피격 클립은 기본 견착 자세를 포함한다. 현재는 각각 단독 재생하는 일반 클립이며, 발사 중 피격을 겹쳐 재생할 때는 이후 연출 평가기에서 자세 혼합·중복 제어를 처리해야 한다.
- 모든 클립은 `Motion/Body`와 그 자식만 움직인다. 보스 루트·`Motion`의 경로는 건드리지 않으므로 마지막 홀의 중앙 칸 안 위치 조정과 플레이어를 향한 회전을 별도로 연결할 수 있다. 좌우 자세 전환 클립은 실제 칸 이동 경로를 포함하지 않는다.
- 책상·의자·창문·담배 연기·레터박스·카메라·소리는 별도 씬 연출이며 현재 사무실 인트로에 연결되어 있다. 부하 방패 클립만 재생해도 부하 오브젝트가 생기지는 않는다.

오프라인 자세 미리보기: [클립 모음 이미지](../Tools/Characters/Previews/MafiaMotionsPreview.png), [움직이는 모음](../Tools/Characters/Previews/MafiaMotionsPreview.gif). 저장된 클립의 곡선을 샘플링한 참고 화면이며 Unity 실행 녹화가 아니다.

몸통의 가로 폭·앞뒤 두께와 어깨 간격을 줄이고 15개 클립의 양팔 자세를 다시 맞췄다. 관절 전체를 비균일 Scale로 눌러서 줄이지 않았다.

`Tests/RoomRhythm/MafiaAnimationChecks.py`로 클립 경로·Controller 참조·편집/실행 곡선 일치·반복 연결·반동 횟수·키 사이 양손/개머리판 접점을 검사한다. Unity 가져오기, 실제 Animation 창의 편집·미리보기와 게임 실행 확인은 사용자가 담당한다.

## 기존 오른팔 클립으로 키프레임 연습

목표는 `MafiaStage01` 안의 마피아 오른팔을 움직여 보고, 중간 자세 하나를 직접 바꾸는 것이다. 이번에는 Unity 기본 **Animation** 창을 사용한다. 현재 사무실은 곡 시각으로 모션을 샘플링하며, 범용 연출 편집기는 후속 작업이다.

## 준비된 것

- 씬: `Assets/Scenes/Stages/MafiaStage01.unity`
- 편집할 보스: Hierarchy의 `Mafia presentation / Mafia boss`
- 동작 파일: `Assets/Animations/Mafia/Mafia_RightArm_Practice.anim`
- 연결 파일: 같은 폴더의 `Mafia_MotionPractice.controller`. 이미 연결되어 있으므로 직접 수정할 필요가 없다.

처음 준비한 동작은 오른쪽 어깨를 0초에 -10도, 0.5초에 -55도, 1초에 -10도로 회전하는 연습이었다. 사용자가 수정한 값은 그대로 보존하므로 현재 중간 각도는 다를 수 있다. 어깨 아래의 팔·손·소총이 함께 움직이며, 최종 전투 모션은 위 스테이지용 클립을 사용한다.

실전 마피아 모션은 [스테이지 목표 연출](../Assets/Scenes/Stages/MafiaStage01.md)에 맞춰 **양팔 견착**으로 준비했다. 이 연습 클립은 키프레임 편집을 익히는 용도로 보존한다.

## 1. 보스를 화면에 띄우기

1. Unity 상단의 게임 실행 ▶가 켜져 있으면 눌러서 끈다. 편집 모드에서 시작한다.
2. 아래쪽 **Project** 창은 프로젝트 파일 목록이다. `Assets → Scenes → Stages → MafiaStage01`을 더블클릭해서 씬을 연다.
3. 왼쪽 **Hierarchy** 창은 현재 씬의 오브젝트 목록이다. 검색 칸에 `Mafia boss`를 입력하고 같은 이름의 오브젝트를 선택한다.
4. 가운데 **Scene** 탭을 선택한다. 마우스를 Scene 화면 위에 두고 **F**를 누르면 선택한 보스로 화면이 이동한다. 필요하면 Scene 창의 **2D**를 켠다.

확인: Scene 화면에 마피아 보스와 소총이 보인다. 이번 연습은 **Scene** 화면에서 본다.

## 2. 준비된 동작 재생하기

1. `Mafia boss`를 선택한 상태에서 상단 메뉴 **Window → Animation → Animation**을 누른다. 이름이 비슷한 **Animator** 창이 아닌 **Animation** 창이다.
2. Animation 창의 클립 이름이 **Mafia_RightArm_Practice**인지 확인한다. 다른 이름이면 클립 선택 목록에서 이 이름을 선택한다.
3. **Animation 창 안의 작은 ▶**를 누른다. Unity 맨 위의 게임 실행 ▶는 이번에 사용하지 않는다.
4. Scene 화면에서 오른팔과 소총이 회전했다가 원래 자세로 돌아오는지 확인한다. 다시 작은 ▶를 눌러 멈춘다.

Animation 창의 시간축을 클릭하거나 재생 위치를 앞뒤로 옮겨도 자세를 확인할 수 있다. 다이아몬드 모양의 점은 그 순간의 자세를 저장한 **키프레임**이다.

확인: 처음·중간·끝에 세 자세가 있고, 그 사이가 부드럽게 이어진다.

## 3. 중간 자세 하나 바꿔보기

1. Animation 창의 **빨간 동그라미 Record** 버튼을 켠다. 이후 바꾸는 관절 값이 동작 파일에 기록된다.
2. 재생 위치를 가운데 키프레임인 **0.5초**로 옮긴다. 이 클립은 초당 60프레임이므로 시간축에서 **0:30**으로 표시될 수 있다. 30초가 아니라 0초 30프레임이다.
3. Hierarchy 검색을 지우고 `Mafia presentation → Mafia boss → Motion → Body → RightShoulder`를 펼쳐 **RightShoulder**를 선택한다.
4. 오른쪽 **Inspector**에서 **Transform → Rotation → Z** 값을 확인하고 `-75`로 바꾼 뒤 Enter를 누른다. 사용자가 이미 수정했다면 현재 각도는 다를 수 있다. X·Y와 Position·Scale은 이번 연습에서 바꾸지 않는다.
5. Animation 창의 **Record** 버튼을 다시 눌러 끈다.
6. Animation 창 안의 작은 ▶를 눌러 팔의 회전 폭이 바뀌는지 확인한다.

확인: 0초와 1초 자세는 같고, 중간 자세만 달라진다. 원래 각도가 좋다면 같은 순서로 가운데 값을 `-55`로 되돌리면 된다.

## 4. 마무리

1. Animation 창에서 재생을 멈추고 **Record**를 끈다.
2. **Preview**를 끄면 보스가 편집 전 자세로 돌아온다. 미리보기를 끝내도 저장한 키프레임은 남는다.
3. **File → Save Project**로 동작 파일을 저장한다. 씬 설정도 변경했다면 Ctrl+S로 씬을 저장한다.

현재 `Mafia boss`의 **Animator 컴포넌트 체크박스는 꺼 둔 상태**다. Animation 창에서 동작을 편집·미리보기하기 위한 연결이며, 게임 실행 때 연습 동작을 자동 재생하지 않도록 한 설정이다. 보스 오브젝트 자체는 편집 화면에서 보이도록 켜져 있고, 게임이 시작되면 기존 진행 코드가 등장 전까지 숨긴다.

이 오른팔 연습 클립은 게임 재생에 쓰지 않는다. 위의 스테이지용 흡연·총 꺼내기·견착·3발 클립은 사무실 연출에 연결되어 있으므로 키프레임을 고치면 게임에서도 변경된 동작을 사용한다. [현재 사무실 흐름과 수정 위치](../Assets/Scenes/Stages/MafiaStage01.md)를 참고한다.

## 화면이 다를 때

- **Animation에 Create만 보임:** `Mafia boss` 오브젝트를 다시 선택한다. 연결된 클립 이름이 보이는지 확인한다.
- **보스가 안 보임:** Hierarchy의 보스 오브젝트 자체가 켜져 있는지 확인하고 Scene 화면에서 F를 누른다.
- **팔을 바꿨는데 동작에 남지 않음:** Record가 켜진 상태에서 0.5초의 Rotation Z를 바꿨는지 확인한다.
- **빨간색 표시가 남음:** Animation의 Record와 Preview를 끈다.

Unity의 [Animation 창 안내](https://docs.unity3d.com/6000.3/Documentation/Manual/animeditor-UsingAnimationEditor.html)를 기준으로 작성했다. Unity 실행·실제 화면 확인은 사용자 담당이며, 개발 과정에서는 에셋의 참조와 관절 경로를 정적으로 검사한다.
