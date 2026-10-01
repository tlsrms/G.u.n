# 씬 안내

전체 흐름을 확인할 때는 `Opening/AwakeningScene`을 실행합니다.
턴테이블과 레코드 선택을 작업할 때는 `Hub/StageSelectScene`만 열면 됩니다.

| 폴더 / 씬 | 용도 |
| --- | --- |
| `Opening/AwakeningScene` | 사각 공간에서 깨어나는 시작 씬. 오른쪽 통로 → 첫 튜토리얼. 빌드의 시작 씬. |
| `Tutorials/Stage01` ~ `Stage05` | `Tutorial_1` ~ `Tutorial_5` 채보를 사용하는 튜토리얼. 클리어하면 다음 안전 구역으로 이동. |
| `SafeRooms/SafeRoom01` ~ `SafeRoom04` | 같은 번호의 튜토리얼을 마친 뒤 쉬는 무음 공간. 오른쪽 통로 → 다음 튜토리얼. |
| `Hub/StageSelectScene` | 모든 튜토리얼 이후 도착하는 턴테이블 안전 구역. 레코드 선택과 스테이지 입장. |
| `Development/MainScene` | 원래 사용하던 맵·전투·채보 편집용 작업 씬. 본편 진행과 별개이며 빌드 목록에서는 비활성화. |
| `Settings/OffsetScene` | 리듬 입력 오프셋 측정·보정용 작업 씬. 본편 진행에 포함되지 않음. |

진행 순서:

`Awakening → Stage01 → SafeRoom01 → Stage02 → SafeRoom02 → Stage03 → SafeRoom03 → Stage04 → SafeRoom04 → Stage05 → StageSelectScene`

허브 레코드는 현재 기존 `Stage01` ~ `Stage05`에 연결되어 있습니다. 보스를 추가하면
`Game Session > Stage Select Scene > Stages`에서 각 `Chart`와 `Scene Name`을 교체합니다.
허브에서 입장한 스테이지는 클리어하거나 Esc로 나오면 허브로 돌아옵니다.

## 허브 Hierarchy

- `Main Camera`: 화면 렌더링.
- `Room Stage / Environment - silent square`: 바닥, 벽, 오른쪽 통로와 문.
- `Room Stage / Player`: 방 중앙에 배치한 플레이어.
- `Room Stage / Turntable interaction point`: 방 안 턴테이블의 상호작용 위치.
- `Room Stage / Selection presentation`: 선택 화면 배경과 최소 조작 안내. `Stage Records Area`에 곡명·음원 길이·최고 정확도·START 버튼을 표시하며 맵 미리보기는 없음.
- `Room Stage / Record Station / Turntable`: 목재 캐비닛, 금속 데크, 플래터와 조작부.
- `Room Stage / Record Station / Record Rack`: 오른쪽 위 꽂이. 슬롯별 `Case 01` ~ `05` 안에 실제 레코드가 숨겨져 있음.
- `Room Stage / Record Station / Case Display - front view`: 선택한 케이스가 즉시 이동하여 정면으로 표시되는 오른쪽 중앙 위치.
- `Room Stage / Record Station / Turntable / Record Transport`: 케이스에서 나온 실제 레코드가 이동·재생되는 동안 사용하는 부모. 복제 레코드는 없음.
- `Room Stage / Record Station / Turntable / 04 Pickup assembly`: 플래터 위로 내려오는 톤암.
- `Room Stage / Scene Fade`: 씬 전환 암전.
- `Game Session`: 이동·출구·레코드 선택과 오디오를 관리하는 컴포넌트.
- `EventSystem`: UI 입력.

방 안과 선택 확대 화면은 **동일한 Record Station**을 사용합니다. 케이스 선택만으로는
음악이 시작되지 않습니다. 케이스가 오른쪽 중앙으로 순간 이동하고 곡 정보가 나타납니다.
START를 누르면 레코드가 케이스 왼쪽에서 빠져나와 왼쪽 턴테이블에 놓이고, 톤암이 내려간 뒤
음악과 회전이 시작됩니다. 턴테이블은 박자에 맞춰 크기나 밝기가 변하지 않습니다.
턴테이블 근처에서 `E`를 누르면 확대 화면으로 돌아와 레코드를 케이스에 다시 넣습니다.

곡명은 Room Chart의 `Song Title`을 사용하며 비워두면 음원 이름을 표시합니다.
길이는 연결된 AudioClip에서 읽습니다. 최고 정확도는 클리어한 플레이의
`Accurate 판정 수 / 전체 성공 판정 수 × 100`이며 이동·문·적 판정을 모두 포함합니다.
스테이지별 최고 기록만 PlayerPrefs에 저장합니다. 기록이 없는 곡은 `BEST —`로 표시합니다.

안전 구역: 열린 출구에서 D 한 번으로 다음 씬에 진입. 자유 이동 없음. 케이스 선택: 클릭 또는 A·D / 방향키.
선택 후 START 클릭 또는 Space·Enter로 재생합니다. 선택 전 Space·Enter는 현재 케이스를 표시합니다.
게임패드: 안전 구역 출구는 방향패드 오른쪽, 선택 확인 A, 턴테이블 재선택 Y.

모든 화면 오브젝트는 씬에 저장되어 있으며 실행 중 생성하지 않습니다. 씬 이동 시 `.meta`를
함께 이동해 기존 GUID를 유지했습니다. 씬 이름 기반 연결과 빌드 순서는 유지합니다.


## Player and selection presentation

All 12 player scenes use linked geometric character prefabs from `Assets/Prefabs/Characters`. The world and UI rigs have 17 separate polygon parts and editable shoulder/elbow/wrist, hip/knee/ankle, spine and neck joints. The pistol is a separate right-hand child; firing follows its Muzzle transform. Gameplay rotates the parent toward aim, while safe rooms use D to advance directly through an open exit, without free movement. See `Assets/Prefabs/Characters/README.md` for animation editing.

The hub room station uses scale 0.25. Opening first darkens the map, then brings the large deck and rack down from above. Closing moves the deck, rack and selected case upward before fading the black panel; the small room objects are restored behind that panel. No runtime scene objects are created for this presentation.

정수리 탑뷰 탐정 외형: 코트와 중절모, 권총을 든 오른팔이 보입니다. 다리는 정지 중 숨겨지고 이동할 때 번갈아 드러납니다. 왼팔 관절은 프리팹에 보존하되 비활성화했습니다.


## 이동 연출과 입력

안전지대에서 D를 누르면 통로 중앙에 정렬한 뒤 오른쪽 출구로 빠르게 이동하고, 퇴장 모션이 끝난 뒤 페이드하며 다음 씬을 불러옵니다. 자유 이동은 없습니다. 스테이지 선택 UI에서는 곡 선택 조작이 우선이며, 레이아웃과 검은 패널이 모두 사라진 다음 문이 열립니다.

안전지대와 인게임 모두 캐릭터는 마우스 방향을 바라봅니다. 발사한 탄환의 기존 궤적은 유지하면서 캐릭터 방향은 계속 갱신됩니다. 인게임 방 이동 잔상은 제거했으며, 도착 시 한 번만 이동 방향으로 짧은 회색 먼지가 퍼집니다. 먼지는 기존 효과 풀을 재사용합니다.

인게임 캐릭터 크기는 카메라 배율·화면 비율을 보정해 안전지대의 UI 리그(1600×1000 기준 60단위)와 같은 화면 크기로 유지합니다. 도착 먼지는 0.28~0.38초 유지하며, 사격 라인은 현재 총구 끝부터 발사 시 선택된 대상 중심까지 연결됩니다.
