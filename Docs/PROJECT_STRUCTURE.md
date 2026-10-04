# 작업물 구조 요약

이 문서는 작업 범위를 고르는 탐색 지도다. 아래에서 요청과 맞는 진입점을 선택하고 필요한 파일만 확인한다. 모든 항목을 매번 읽거나 모든 연결 파일을 한꺼번에 열 필요는 없다. 경로는 프로젝트 루트 기준이다.

## 구조 판단

현재 Unity 프로젝트는 런타임 코드, Unity 에디터 코드, 씬·에셋, 회귀 검사, 오프라인 제작 도구로 구분되어 있다. 이 구분을 유지하며 요약을 위해 기존 파일을 이동하지 않는다. `Assets/Scripts/RoomRhythm/` 내부는 기능별 파일로 나뉘어 있으며 아래 작업별 표로 진입점을 찾는다.

## 폴더 지도

```text
G.u.n/
├─ AGENTS.md                     작업 범위 제한과 구조 요약 갱신 규칙
├─ Docs/PROJECT_STRUCTURE.md     이 탐색 지도
├─ Assets/
│  ├─ Scripts/
│  │  ├─ RoomRhythm/             게임 실행 코드와 기능별 설명 문서
│  │  │  └─ Editor/              맵·채보 편집기, Inspector, 판정 설정 UI
│  │  └─ *.md                   기획, 채보 편집기 사용법
│  ├─ Scenes/                   시작·튜토리얼·안전지대·허브·스테이지 씬
│  ├─ RoomChart/                튜토리얼 및 스테이지 채보 에셋
│  ├─ Prefabs/Characters/       플레이어·적·보스·다리·표적 프리팹과 설명
│  ├─ Animations/               Unity Animation 창에서 편집하는 모션 클립과 Controller
│  ├─ Arts/                    캐릭터 파츠, 스프라이트, 폰트
│  ├─ Audio/                   음악·카운트인·스테이지 효과음
│  ├─ Resources/               판정 설정 JSON, 화면 효과 셰이더·머티리얼
│  ├─ Settings/                렌더 파이프라인·렌더러·씬 템플릿 설정
│  ├─ UI Toolkit/              UI 테마
│  └─ _Recovery/               기본 탐색 대상에서 제외; 복구 요청 때 확인
├─ Tests/RoomRhythm/            C# 로직 검사, Python 에셋 검사, 실행 스크립트
├─ Tools/
│  └─ Characters/              캐릭터 제작·미리보기 스크립트와 템플릿
├─ Packages/                   패키지 선언과 잠금 파일
└─ ProjectSettings/            Unity 프로젝트 설정
```

루트의 `.csproj`, `.slnx`는 IDE·컴파일 관련 파일이다. 게임 기능 수정의 기본 진입점은 아니다. `Assets/InputSystem_Actions.inputactions`는 입력 액션 에셋이며, 실제 입력 처리 변경은 먼저 아래 코드 진입점에서 사용 관계를 확인한다. 루트 에셋인 `Assets/DefaultVolumeProfile.asset`, `Assets/UniversalRenderPipelineGlobalSettings.asset`는 화면·렌더링 설정 작업 때만 확인한다.

## 작업별 코드 진입점

아래 파일명은 별도 경로가 없으면 `Assets/Scripts/RoomRhythm/` 기준이다. 같은 행의 파일도 요청에 필요한 것만 선택한다.

| 요청 영역 | 먼저 확인할 파일 | 필요할 때 연결할 영역 |
| --- | --- | --- |
| 플레이 시작·재시작·전체 세션 연결 | `RoomSession.cs` | `RoomRun.cs`, `SongTimeline.cs`, `RoomRestartTransition.cs`. 사망 연출 후 페이드가 가려진 동안 복원하고 시작 방에서 새 키보드·마우스 입력을 대기. `StageProgression.cs`의 자동 시작은 최초 입장 때 `Update`에서 실행하며 첫 Dynamic 입력 갱신 완료를 기다림. 검사: `Tests/RoomRhythm/StageStartupChecks.cs` |
| 인스펙터 Debug Mode·무적 자동 진행 | `RoomSession.cs`의 `debugMode`, `RoomRun.cs`의 `AdvanceAutomatically` | `Editor/RoomInspectors.cs`의 `Tools > Gun > Select Stage Session`으로 열린 씬의 컴포넌트 선택. `RoomCombat.cs`의 자동 사격 연출, `StageProgression.cs`의 디버그 기록 저장 제외. 시작 시 적용하며 전체 채보를 자동 진행 |
| 이동·문·적의 성공/실패 판정 | `RoomRun.cs`, `TimingRules.cs` | `RoomChart.cs`, `Tests/RoomRhythm/RoomRunChecks.cs` |
| 키보드·마우스 입력·음악 시간·입력 보정 | `RoomKeyboard.cs`, `SongTimeline.cs`, `InputOffsetSettings.cs` | `RoomKeyboard`가 시작 버튼과 이동·사격 입력을 수집하고 Dynamic 갱신 완료 여부를 제공. `RoomSession`이 입력 시계 준비 전 시작 및 전환 중 입력을 차단. `OffsetCalibration.cs`, `OffsetCalibrationScene.cs`, 오프셋 관련 검사 |
| 판정 허용 범위·설정 UI | `JudgmentSettingsConfig.cs`, `TimingRules.cs` | `Assets/Resources/JudgmentSettings.json`, `Editor/JudgmentSettingsProvider.cs` |
| 채보 데이터·비트·맵 모델 | `RoomChart.cs`, `BeatChart.cs`, `MapChart.cs` | `MapTimelineEditing.cs`, `Assets/RoomChart/`, 비트·맵 검사 |
| 마피아 전체 채보·씬 연결 | `Assets/RoomChart/Stage1_Full.asset`, `Assets/Scenes/Stages/MafiaStage01.md` | 기존 맵 에디터로 전체 채보 편집. `Tests/RoomRhythm/MafiaStageChecks.cs`, `MafiaStageSceneChecks.py` |
| 맵·채보 편집기 | `Editor/MapChartWindow.cs` | `MapChartCanvas.cs`, `MapChartTimeline.cs`, `MapChartSelection.cs`, `MapAudioPreview.cs`, `MapSceneStore.cs` (모두 `Editor/`) |
| 타임라인 음원 파형 | `Editor/MapChartTimeline.cs`의 `DrawWaveform` | `Editor/MapAudioPreview.cs`가 PCM을 약 5ms 구간의 RMS·최대 진폭으로 분석·캐시. 압축 음원은 로컬 원본을 비동기 디코딩하며 원본 가져오기 설정은 유지. 표시 대비·박자·첫 박 시각·음악 지연·반복 재생 반영. `Assets/Scripts/채보 편집기 사용법.md`; 컴파일 참조: `Tests/RoomRhythm/Run-Checks.ps1` |
| 방 자동 배치·직사각형 면 연결 | `MapChart.cs`의 `AttachedPosition`, `Connection`, `PassagePosition` | `Editor/MapChartCanvas.cs`의 드래그 맞춤과 미리보기, `Editor/MapChartWindow.cs`의 크기 변경·방향 버튼. `Editor/MapSceneStore.cs`와 `RoomBinding.cs`가 공유 면의 문·통로 배치에 연결하고 `RoomSession.cs`가 실제 씬 연결을 검증. `MapExtensions.md`, `Tests/RoomRhythm/MapChartChecks.cs` |
| 기존 경로 사이에 방·복도 삽입 | `MapChart.cs`의 `InsertRoom`, `Editor/MapChartCanvas.cs`의 `DrawPendingRoom`·`CommitRoomPlacement` | 추가 창에서 크기·박자 지정 후 뒤쪽 방들을 함께 평행 이동해 연결. 기존 방 ID·박자·뒤쪽 상대 배치를 보존하며 분리된 사본 검증 후 반영. 검사: `Tests/RoomRhythm/MapChartChecks.cs` |
| 직사각형 내부 이동·도착 칸 | `MapChart.cs`의 `PlayerAnchor`, `PlayerMovePosition` | 다음 방과 접한 기본 크기 칸의 중심을 도착점으로 사용하고 공유 통로를 경유. `RoomSession.cs`의 실제 이동, `Editor/MapChartCanvas.cs`의 미리보기·화살표, `Editor/MapSceneStore.cs`의 시작 위치가 같은 계산을 사용. 기존 방별 이동 시간·곡선 적용. `RoomCinematics.cs`는 실제 위치에서 충돌 연출. 검사: `Tests/RoomRhythm/MapChartChecks.cs` |
| 적 좌표 입력·마우스 배치 | `Editor/MapChartWindow.cs`, `Editor/MapChartCanvas.cs` | `TimingRules.cs`의 `EnemyPlacement`가 기존 8방향 또는 방 기준 X/Y를 평가. `MapChart.cs`·`BeatChart.cs`·`MapTimelineEditing.cs`가 저장/변환/복제, `MapSceneStore.cs`·`RoomEnemy.cs`·`RoomCombat.cs`가 씬 적용/런타임 연결. `MapChartChecks.cs` 검사 |
| Inspector 편집 | `Editor/RoomInspectors.cs` | 편집 대상 데이터 모델 |
| 방·문·적의 씬 연결과 표시 | `RoomBinding.cs`, `RoomDoor.cs`, `RoomEnemy.cs` | `RoomSession.cs`, `RoomCombat.cs` |
| 일반 적 피격·쓰러짐 | `RoomEnemy.cs`의 `Defeat`, `RoomCombat.cs`의 `PresentHit` | 명중 즉시 판정 대상에서 제외하고 스프라이트만 밀림·회전·주저앉음·페이드. 방 표시 수명에 맞춰 시체를 숨기며 `Configure`로 자세·불투명도·판정선을 복원. `RoomFeedback`의 기존 피격 효과 유지. 검사: `Tests/RoomRhythm/EnemyRestartChecks.cs` |
| 조준·사격·대상 선택 | `RoomAim.cs`, `RoomCombat.cs`, `TargetSelection.cs` | `StageActionTarget.cs`, `StagePropTarget.cs` |
| 카메라·판정 표시·사망/이동 연출 | `RoomCamera.cs`, `RoomFeedback.cs`, `JudgmentPresentation.cs` | `RoomCinematics.cs`, `RoomRestartTransition.cs`, `DebugTimingBar.cs` |
| 적 등장 확대·판정 HUD 잘림 | `RoomCombat.HasVisibleEnemies`, `RoomSession.cs`의 `SetCombatFocus`, `RoomCamera.cs`의 `FitHud` | 실제 등장한 현재 방 적만 전투 확대. 카메라 자식 판정 글자/바의 위치·크기는 배율 보정하며 판정 바는 기본 화면 안에 배치. 기본 맵 배율과 보스 인트로 구도는 별도 |
| 스테이지 전용 진행·보스·재시작 | `StageDirector.cs`, `MafiaStageDirector.cs` | `StageResetState.cs`, 아래 확장 문서 |
| 마피아 복도·사무실·창문 탈출 | `MafiaStageDirector.cs`, `MafiaIntroTiming.cs` | `MafiaOfficeSet.cs`의 소품·연기·탄환·음향, `RoomCamera.cs`의 구도 전환, `RoomEnemy.TargetAt`·`RoomCombat`의 출현/조준 위치, `RoomDoor`의 외형 교체. 사용법은 `Assets/Scenes/Stages/MafiaStage01.md` |
| 클리어 후 이동·안전지대 | `StageProgression.cs`, `SafeRoomController.cs` | `Assets/Scenes/README.md` |
| 허브·곡 선택·턴테이블 | `StageSelectScene.cs`, `StageSelectSurface.cs`, `StageSelection.cs` | `Assets/Scenes/Hub/`, `Assets/Scenes/README.md` |
| 최고 기록 저장 | `StageRecordStore.cs` | `Tests/RoomRhythm/StageRecordStoreChecks.cs` |
| 플레이어 관절·걷기 | `GeometricPlayerRig.cs`, `TopDownLegMotion.cs` | `Assets/Prefabs/Characters/README.md`, `TopDownLegs.md` |
| 아날로그 화면 효과 | `AnalogScreenFeature.cs` | `Assets/Resources/AnalogScreen.shader`, `RetroVideo.shader`, 관련 렌더링 설정 |

## 주요 연결 관계

- 허브에서 자동 진행을 설정하려면 `StageSelectScene.cs`의 인스펙터 `Debug Mode`를 사용한다. 곡 START 시 `StageSelection.cs`를 통해 입장 스테이지의 `RoomSession`에 전달된다. 허브에는 `RoomSession`이 없어도 된다. 스테이지를 직접 실행할 때는 해당 `RoomSession`의 `Debug Mode`를 사용한다.
- `RoomChart`는 음악·채보·맵 데이터를 담는 ScriptableObject다. 실제 채보 에셋은 `Assets/RoomChart/`에 있다.
- 일반 적의 선택적 자유 배치는 맵·비트·실행 노트에 `EnemyPlacement`로 보존한다. 에디터 미리보기·씬 배치·게임 설정/검증은 같은 좌표 평가를 사용한다. 보스 전용 표적의 위치는 기존 `StageActionTarget.PositionAt`이 계속 소유한다.
- 일반 적의 재시작 배치 검증은 `RoomEnemy.PlacementPosition`(설정된 채보 기준 위치)을 사용하고, 등장·조준은 `TargetAt(time)`을 사용한다. 최초 실행/에디터 검증에서는 실제 씬 위치를 검사한다. 복도 등장 연출 후 반복 재시작 검사는 `Tests/RoomRhythm/EnemyRestartChecks.cs`에 있다.
- `RoomSession`은 채보, `SongTimeline`, `RoomKeyboard`, `RoomRun`, 방 연결과 전투·피드백을 연결하는 런타임 진입점이다. 판정 문제는 `RoomRun`부터, 표시 문제는 해당 표시 컴포넌트부터 확인한다.
- `StageDirector`는 스테이지별 진행 확장점이다. `RoomSession`의 한 채보 판정 결과에 반응하고 채보 완료 뒤 퇴장을 진행한다. 재시작은 `StageResetState` 및 `StageDirector.md`를 확인한다.
- 마피아 씬과 허브는 `Stage1_Full.asset` 하나를 참조한다. 사용자 새 채보의 복도 뒤 2칸 폭 사무실에서 15박 인트로를 거쳐 위쪽 창문을 깨고 탈출한다. `MafiaStageDirector`는 방 ID와 실제 적용 노트로 시각·위치를 다시 연결하며 `MafiaOfficeSet`의 소품은 맵 재생성 계층 밖에 둔다. 구형 고정 Shots·보스 표적 경로는 제거했다. 이후 보스전·범용 연출 편집기는 후속 작업이다.
- 마피아 모션 편집은 Unity 기본 Animation 창과 `Assets/Animations/Mafia/`를 사용한다. 15개 클립과 기존 연습 클립을 Controller에서 선택한다. 게임에서는 Animator 자동 재생을 끄고 `MafiaStageDirector`가 흡연·총 꺼내기·견착·3발 클립을 음악 시각으로 샘플링한다. 방패·좌우 전환·부위 피격 연결은 후속 작업이다.
- `Editor/`는 Unity 편집용 코드다. 현재 진입점은 `Window > Gun > 시각적 맵 에디터`, `Tools > Gun > Select Stage Session`, `Project Settings > Gun`의 `Audio`·`Judgment`와 채보·세션 Inspector다. 편집 화면 문제는 해당 편집기 파일, 저장·실행 데이터 문제는 연결된 모델로 범위를 좁힌다.
- `Tools/`는 오프라인 제작 도구이고 게임 실행 결과물은 `Assets/`에 있다. 제작 도구 재실행은 에셋을 덮어쓸 수 있으므로 해당 README와 수정 목적을 먼저 확인한다.

## 씬·콘텐츠·제작 작업

| 작업 | 경로와 안내 |
| --- | --- |
| 씬 역할·진행 흐름 | `Assets/Scenes/README.md`부터 확인. `Opening/`, `Tutorials/`, `SafeRooms/`, `Hub/`, `Stages/`, `Development/`, `Settings/`로 구분 |
| 마피아 스테이지 | `Assets/Scenes/Stages/MafiaStage01.md`: 복도·사무실·위쪽 창문 탈출의 사용법/수정 위치와 이후 3×3 홀 구상을 구분. 실행 채보는 `Assets/RoomChart/Stage1_Full.asset`, 현재 연출은 `MafiaStageDirector.cs` |
| 마피아 클립 선택·모션 편집 | `Docs/MAFIA_MOTION_PRACTICE.md`의 클립 목록·발사 기준 시각·편집 방법 → `Assets/Animations/Mafia/`. `MafiaStage01`의 `Mafia presentation / Mafia boss`를 선택하고 Unity 기본 Animation 창에서 편집. `Tests/RoomRhythm/MafiaAnimationChecks.py`로 참조·곡선·견착 접점 검사 |
| 플레이어·일반 적·보스 제작 | `Tools/Characters/README.md` → 해당 `BuildGeometricPlayer.py`, `BuildRegularEnemy.py`, `BuildRockBoss.py`, `BuildMafiaBoss.py` |
| 다리 제작·외형 미리보기 | `Tools/Characters/BuildLegPrefabs.py`, `PreviewGeometricPlayer.py`; 같은 폴더의 `Templates/`, `Previews/` |
| 보스·다리 편집 안내 | `Assets/Prefabs/Characters/`의 `MafiaBoss.md`, `RockBoss.md`, `TopDownLegs.md` |
| 패키지·Unity 버전 문제 | `Packages/manifest.json`, 필요할 때 `packages-lock.json`; `ProjectSettings/ProjectVersion.txt` |

씬·프리팹은 먼저 설명 문서와 관련 코드에서 작업 범위를 정한다. YAML 내용은 요청이 있을 때만 읽고, `.meta`는 GUID·참조 문제를 조사할 때만 읽는다. 씬 문서에 적힌 진행·연결 설명은 실제 참조 검증을 대신하지 않는다.

## 상세 문서와 검사

- 전체 채보 기반 보스 연출 개편 계획: `Docs/CHART_PRESENTATION_REFACTOR_PLAN.md`. 한 씬·한 전체 채보, 세 액션의 외형 대체, 검증·제거 순서와 현재 진행 상태. 전체 채보·씬/허브 연결과 분할 구조 제거, 적 자유 배치 구현은 완료. 직사각형의 공유 통로 경유·다음 출구 칸 도착 이동은 위 진입점에서 처리한다. 임의 내부 경유점·홀 외형/반복 방문 확장, 노트 기반 연출 연결·편집기 및 Unity 실행 검증은 남아 있다. 구체적인 콘텐츠 기준은 위 마피아 스테이지 문서에 둔다.
- 편집기 사용법: `Assets/Scripts/채보 편집기 사용법.md`.
- 기획 원문: `Assets/Scripts/기획.md`. 현재 구현 확인을 대신하지 않으며 이번 개편의 제작 흐름은 위 개편 계획을 따른다.
- 확장 설명: `Assets/Scripts/RoomRhythm/`의 `MapExtensions.md` (방 크기·이동), `StageDirector.md` (전용 진행·재시작), `StageTargets.md` (장애물·표적).
- 검사 시작점: `Tests/RoomRhythm/README.md`, `Run-Checks.ps1`. C# 컴파일·로직 검사에는 생성된 프로젝트 참조와 .NET 런타임이 필요하다.
- C# 검사: 같은 폴더의 `RoomRunChecks.cs`, `BeatChartChecks.cs`, `MapChartChecks.cs`, `OffsetCalibrationChecks.cs`, `SongOffsetChecks.cs`, `StageRecordStoreChecks.cs`, `MafiaStageChecks.cs` 중 변경 영역에 해당하는 검사를 확인한다.
- 시작·재시작 순서 검사: `Tests/RoomRhythm/StageStartupChecks.cs`, `StageStartupTestDoubles.cs`. 실제 진행·입력·음악 코드와 엔진/세션 경계 대역으로 시계 준비·최초 자동 시작·재시작 대기를 검사하며 Unity 플레이 검증은 별도다.
- 에셋 검사: 같은 폴더의 `StageSelectSceneChecks.py`, `RockBossChecks.py`, `MafiaStageSceneChecks.py`, `MafiaAnimationChecks.py`. 마지막 검사는 마피아 클립·리그의 곡선을 샘플링해 참조·키 연결·양손 접점·반동 횟수를 확인한다. 씬·프리팹 검사 요청 범위에서 선택하며 Unity 플레이 모드 검증을 대신하지 않는다.

## 유지 원칙

구조 변경을 끝낸 직후 `AGENTS.md`의 갱신 명령을 따른다. 폴더 역할, 기능 경계, 주요 진입점과 연결 문서만 유지한다. 모든 파일·메서드·에셋 값이나 작업 이력을 나열하지 않는다. 이 문서의 경로가 실제와 다르면 관련 범위만 확인해 수정한다.
