# 씬 안내

전체 흐름을 확인할 때는 `Opening/AwakeningScene`을 실행합니다.
턴테이블과 레코드 선택을 작업할 때는 `Hub/StageSelectScene`만 열면 됩니다.

| 폴더 / 씬 | 용도 |
| --- | --- |
| `Opening/AwakeningScene` | 사각 공간에서 깨어나는 시작 씬. 오른쪽 통로 → 첫 튜토리얼. 빌드의 시작 씬. |
| `Tutorials/Stage01` ~ `Stage05` | `Tutorial_1` ~ `Tutorial_5` 채보를 사용하는 튜토리얼. 클리어하면 다음 안전 구역으로 이동. |
| `SafeRooms/SafeRoom` | 모든 튜토리얼이 공유하는 무음 공간. 왼쪽 통로로 입장한 뒤 문이 닫히고 오른쪽 출구로 다음 목적지 이동. |
| `Hub/StageSelectScene` | 모든 튜토리얼 이후 도착하는 턴테이블 안전 구역. 레코드 선택과 스테이지 입장. |
| `Development/MainScene` | 원래 사용하던 맵·전투·채보 편집용 작업 씬. 본편 진행과 별개이며 빌드 목록에서는 비활성화. |
| `Settings/OffsetScene` | 리듬 입력 오프셋 측정·보정용 작업 씬. 본편 진행에 포함되지 않음. |

진행 순서:

`Awakening → Stage01 → SafeRoom → Stage02 → SafeRoom → Stage03 → SafeRoom → Stage04 → SafeRoom → Stage05 → SafeRoom → StageSelectScene`

안전방은 하나의 씬을 재사용합니다. 각 튜토리얼의 `StageProgression`에서 `Next Safe Scene`은 `SafeRoom`, `Next Tutorial Scene`은 다음 튜토리얼(5번은 `StageSelectScene`)을 지정합니다. `SafeRoomTransit`가 다음 목적지와 클리어 입장 여부를 한 번 전달하고 안전방에서 소비합니다.

클리어 후에는 화면이 전환되고 캐릭터가 왼쪽의 열린 통로에서 방 안으로 들어옵니다. 캐릭터가 문을 완전히 지나면 왼쪽 문이 가속하며 닫히고, 중앙에 도착한 뒤 오른쪽 문이 열립니다. 입장 중에는 D 입력을 받지 않습니다. 왼쪽 문은 이후 닫힌 상태로 유지됩니다.

튜토리얼 도중 Esc로 안전방에 돌아오면 입장 연출 없이 현재 튜토리얼을 다시 선택할 수 있습니다. 첫 튜토리얼의 Esc는 시작 씬으로 돌아갑니다. 허브에서 선택한 곡은 클리어/Esc 시 안전방을 거치지 않고 허브로 돌아갑니다. 안전방을 Editor에서 직접 실행하면 기본 목적지 `Stage02`를 사용합니다.

허브 레코드는 현재 기존 `Stage01` ~ `Stage05`에 연결되어 있습니다. 보스를 추가하면
`Game Session > Stage Select Scene > Stages`에서 각 `Chart`와 `Scene Name`을 교체합니다.
허브에서 입장한 스테이지는 클리어하거나 Esc로 나오면 허브로 돌아옵니다.

## 허브 Hierarchy

- `Main Camera`: 화면 렌더링.
- `Room Stage / Environment - silent square`: 바닥, 벽, 오른쪽 통로와 문.
- `Room Stage / Player`: 방 중앙에 배치한 플레이어.
- `Room Stage / Turntable interaction point`: 방 안 턴테이블의 상호작용 위치.
- `Room Stage / Record Station`: 맵의 작은 턴테이블은 유지합니다. 이전 꽂이와 케이스 표시만 숨깁니다. `Selection presentation`은 새 확대 UI로 대체하며 곡 목록·폰트·오디오 연결은 유지합니다.
- `Game Session / Radial stage selection`: `StageSelectScene`이 생성하는 중앙 턴테이블 화면. `StageSelectTurntable`이 데크, 회전 판, 고정된 스테이지 구역, 중앙 기록, 톤암, 볼륨 바와 시작 버튼을 배치합니다.
- `RecordDialGraphic`: 흑백 판의 홈·광택과 구역 UI를 그립니다. 선택 중에는 정지하고, 판을 드래그하면 구역 UI까지 함께 회전합니다. 고정된 바늘 도착점의 구역만 밝게 표시합니다.
- `RecordDiscDrag`: 판의 회전 입력을 받습니다. 톤암은 길이와 굽힘이 고정되어 있고 클릭하면 축만 회전하여 판에 내려옵니다. 바늘이 닿은 뒤 음악과 자동 회전이 시작되며, 구역 UI는 사라지고 중앙에 해당 곡 정보·최고 기록을 표시합니다. 톤암을 다시 클릭하면 음악·회전을 멈추고 원래 위치로 돌아가며 구역 UI를 복원합니다. `RecordTurntableState`가 이동 중 재클릭과 늦은 음원 로딩 완료도 처리합니다.
- 오른쪽 세로 볼륨 바: 스테이지별 음량을 저장합니다. 미리듣기와 START 이후 실제 음악에 같은 값이 적용됩니다.
- `EventSystem`: 기존 UI 입력을 사용합니다.

입장 시 맵을 먼저 보여주고, 배경을 어둡게 한 뒤 확대 데크가 내려옵니다. START는 데크를 위로 올리고 배경 페이드를 해제하여 맵으로 복귀합니다. 작은 턴테이블에서 선택한 음악을 계속 재생하며 D로 열린 출구를 통해 해당 스테이지에 진입합니다. 턴테이블 근처에서 E 또는 게임패드 Y로 다시 선택창을 열 수 있습니다. 음원이나 빌드에 등록된 씬이 없는 구역은 시작할 수 없습니다. 키보드 A·D / 방향키와 게임패드 좌우는 판을 구역 단위로 회전합니다. Space·Enter / 게임패드 A는 선택 중에는 톤암을 내리고, 재생 중에는 START를 실행합니다. 재생 전에는 START와 볼륨 조작을 비활성화합니다.

곡명은 Room Chart의 `Song Title`을 사용하며 비워두면 음원 이름을 표시합니다.
길이는 연결된 AudioClip에서 읽습니다. 최고 정확도는 클리어한 플레이의
`Accurate 판정 수 / 전체 성공 판정 수 × 100`이며 이동·문·적 판정을 모두 포함합니다.
스테이지별 최고 기록과 음악 음량을 PlayerPrefs에 별도로 저장합니다. 기록이 없는 곡은 `BEST —`로 표시합니다.

일반 안전 구역의 D 출구 조작은 유지합니다. 턴테이블 선택 화면은 런타임 UI이며, 선택창 밖의 기존 방 오브젝트는 씬에 저장되어 있습니다. 씬 이름 기반 연결과 빌드 순서는 유지합니다.


## Player and selection presentation

The player scenes use linked geometric character prefabs from `Assets/Prefabs/Characters`. The world and UI rigs have 17 separate polygon parts and editable shoulder/elbow/wrist, hip/knee/ankle, spine and neck joints. The pistol is a separate right-hand child; firing follows its Muzzle transform. Gameplay rotates the parent toward aim, while safe rooms use D to advance directly through an open exit, without free movement. See `Assets/Prefabs/Characters/README.md` for animation editing.

The hub keeps its small authored turntable in the map, hides only the former rack/case presentation, and creates a centered selection UI at runtime. While selecting, dragging rotates the vinyl and sector labels together under a fixed needle landing point. Clicking the rigid tonearm starts playback after it lands; the sector UI disappears and the center shows the selected record. Clicking again parks the arm, stops playback and restores the rotating selection UI. The backdrop fades before the expanded deck slides down; START raises it and reveals the map again, with the selected stage assigned to the exit.

정수리 탑뷰 탐정 외형: 코트와 중절모, 권총을 든 오른팔이 보입니다. 다리는 정지 중 숨겨지고 이동할 때 번갈아 드러납니다. 왼팔 관절은 프리팹에 보존하되 비활성화했습니다.


## 이동 연출과 입력

안전지대에서 D를 누르면 통로 중앙에 정렬한 뒤 오른쪽 출구로 빠르게 이동하고, 퇴장 모션이 끝난 뒤 페이드하며 다음 씬을 불러옵니다. 자유 이동은 없습니다. 스테이지 선택 UI에서는 이동을 막고, START 퇴장 연출이 끝난 뒤 맵의 출구 조작을 다시 허용합니다.

안전지대와 인게임 모두 캐릭터는 마우스 방향을 바라봅니다. 발사한 탄환의 기존 궤적은 유지하면서 캐릭터 방향은 계속 갱신됩니다. 인게임 방 이동 잔상은 제거했으며, 도착 시 한 번만 이동 방향으로 짧은 회색 먼지가 퍼집니다. 먼지는 기존 효과 풀을 재사용합니다.

인게임 캐릭터 크기는 카메라 배율·화면 비율을 보정해 안전지대의 UI 리그(1600×1000 기준 60단위)와 같은 화면 크기로 유지합니다. 도착 먼지는 0.28~0.38초 유지하며, 사격 라인은 현재 총구 끝부터 발사 시 선택된 대상 중심까지 연결됩니다.
