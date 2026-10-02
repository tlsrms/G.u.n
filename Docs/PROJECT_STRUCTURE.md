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
│  ├─ Arts/                    캐릭터 파츠, 스프라이트, 폰트
│  ├─ Audio/                   음악과 카운트인 음원
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
| 플레이 시작·재시작·전체 세션 연결 | `RoomSession.cs` | `RoomRun.cs`, `SongTimeline.cs`, `RoomRestartTransition.cs` |
| 인스펙터 Debug Mode·무적 자동 진행 | `RoomSession.cs`의 `debugMode`, `RoomRun.cs`의 `AdvanceAutomatically` | `Editor/RoomInspectors.cs`의 `Tools > Gun > Select Stage Session`으로 열린 씬의 컴포넌트 선택. `RoomCombat.cs`의 자동 사격 연출, `StageProgression.cs`의 디버그 기록 저장 제외. 시작 시 적용하며 전체 채보를 자동 진행 |
| 이동·문·적의 성공/실패 판정 | `RoomRun.cs`, `TimingRules.cs` | `RoomChart.cs`, `Tests/RoomRhythm/RoomRunChecks.cs` |
| 키 입력·음악 시간·입력 보정 | `RoomKeyboard.cs`, `SongTimeline.cs`, `InputOffsetSettings.cs` | `OffsetCalibration.cs`, `OffsetCalibrationScene.cs`, 오프셋 관련 검사 |
| 판정 허용 범위·설정 UI | `JudgmentSettingsConfig.cs`, `TimingRules.cs` | `Assets/Resources/JudgmentSettings.json`, `Editor/JudgmentSettingsProvider.cs` |
| 채보 데이터·비트·맵 모델 | `RoomChart.cs`, `BeatChart.cs`, `MapChart.cs` | `MapTimelineEditing.cs`, `Assets/RoomChart/`, 비트·맵 검사 |
| 마피아 전체 채보·씬 연결 | `Assets/RoomChart/Stage1_Full.asset`, `Assets/Scenes/Stages/MafiaStage01.md` | 기존 맵 에디터로 전체 채보 편집. `Tests/RoomRhythm/MafiaStageChecks.cs`, `MafiaStageSceneChecks.py` |
| 맵·채보 편집기 | `Editor/MapChartWindow.cs` | `MapChartCanvas.cs`, `MapChartTimeline.cs`, `MapChartSelection.cs`, `MapAudioPreview.cs`, `MapSceneStore.cs` (모두 `Editor/`) |
| Inspector 편집 | `Editor/RoomInspectors.cs` | 편집 대상 데이터 모델 |
| 방·문·적의 씬 연결과 표시 | `RoomBinding.cs`, `RoomDoor.cs`, `RoomEnemy.cs` | `RoomSession.cs`, `RoomCombat.cs` |
| 조준·사격·대상 선택 | `RoomAim.cs`, `RoomCombat.cs`, `TargetSelection.cs` | `StageActionTarget.cs`, `StagePropTarget.cs` |
| 카메라·판정 표시·사망/이동 연출 | `RoomCamera.cs`, `RoomFeedback.cs`, `JudgmentPresentation.cs` | `RoomCinematics.cs`, `RoomRestartTransition.cs`, `DebugTimingBar.cs` |
| 스테이지 전용 진행·보스·재시작 | `StageDirector.cs`, `MafiaStageDirector.cs` | `StageResetState.cs`, 아래 확장 문서 |
| 클리어 후 이동·안전지대 | `StageProgression.cs`, `SafeRoomController.cs` | `Assets/Scenes/README.md` |
| 허브·곡 선택·턴테이블 | `StageSelectScene.cs`, `StageSelectSurface.cs`, `StageSelection.cs` | `Assets/Scenes/Hub/`, `Assets/Scenes/README.md` |
| 최고 기록 저장 | `StageRecordStore.cs` | `Tests/RoomRhythm/StageRecordStoreChecks.cs` |
| 플레이어 관절·걷기 | `GeometricPlayerRig.cs`, `TopDownLegMotion.cs` | `Assets/Prefabs/Characters/README.md`, `TopDownLegs.md` |
| 아날로그 화면 효과 | `AnalogScreenFeature.cs` | `Assets/Resources/AnalogScreen.shader`, `RetroVideo.shader`, 관련 렌더링 설정 |

## 주요 연결 관계

- 허브에서 자동 진행을 설정하려면 `StageSelectScene.cs`의 인스펙터 `Debug Mode`를 사용한다. 곡 START 시 `StageSelection.cs`를 통해 입장 스테이지의 `RoomSession`에 전달된다. 허브에는 `RoomSession`이 없어도 된다. 스테이지를 직접 실행할 때는 해당 `RoomSession`의 `Debug Mode`를 사용한다.
- `RoomChart`는 음악·채보·맵 데이터를 담는 ScriptableObject다. 실제 채보 에셋은 `Assets/RoomChart/`에 있다.
- `RoomSession`은 채보, `SongTimeline`, `RoomKeyboard`, `RoomRun`, 방 연결과 전투·피드백을 연결하는 런타임 진입점이다. 판정 문제는 `RoomRun`부터, 표시 문제는 해당 표시 컴포넌트부터 확인한다.
- `StageDirector`는 스테이지별 진행 확장점이다. `RoomSession`의 한 채보 판정 결과에 반응하고 채보 완료 뒤 퇴장을 진행한다. 재시작은 `StageResetState` 및 `StageDirector.md`를 확인한다.
- 마피아 씬과 허브는 `Stage1_Full.asset` 하나를 참조한다. 43개 방과 하나의 Combat을 사용하며 `guard15`에서 보스 등장 공백을 지난다. 구간 API·분할 채보·이관 도구는 제거했다. 등장 전용 편집 도구는 제거했고 현재 등장은 `MafiaStageDirector`가 처리한다. 노트 기반 연출 연결과 범용 연출 편집기는 후속 계획에 남아 있다.
- `Editor/`는 Unity 편집용 코드다. 현재 진입점은 `Window > Gun > 시각적 맵 에디터`, `Tools > Gun > Select Stage Session`, `Project Settings > Gun`의 `Audio`·`Judgment`와 채보·세션 Inspector다. 편집 화면 문제는 해당 편집기 파일, 저장·실행 데이터 문제는 연결된 모델로 범위를 좁힌다.
- `Tools/`는 오프라인 제작 도구이고 게임 실행 결과물은 `Assets/`에 있다. 제작 도구 재실행은 에셋을 덮어쓸 수 있으므로 해당 README와 수정 목적을 먼저 확인한다.

## 씬·콘텐츠·제작 작업

| 작업 | 경로와 안내 |
| --- | --- |
| 씬 역할·진행 흐름 | `Assets/Scenes/README.md`부터 확인. `Opening/`, `Tutorials/`, `SafeRooms/`, `Hub/`, `Stages/`, `Development/`, `Settings/`로 구분 |
| 마피아 스테이지 | `Assets/Scenes/Stages/MafiaStage01.md`, `Assets/RoomChart/Stage1_Full.asset`, `MafiaStageDirector.cs` |
| 플레이어·일반 적·보스 제작 | `Tools/Characters/README.md` → 해당 `BuildGeometricPlayer.py`, `BuildRegularEnemy.py`, `BuildRockBoss.py`, `BuildMafiaBoss.py` |
| 다리 제작·외형 미리보기 | `Tools/Characters/BuildLegPrefabs.py`, `PreviewGeometricPlayer.py`; 같은 폴더의 `Templates/`, `Previews/` |
| 보스·다리 편집 안내 | `Assets/Prefabs/Characters/`의 `MafiaBoss.md`, `RockBoss.md`, `TopDownLegs.md` |
| 패키지·Unity 버전 문제 | `Packages/manifest.json`, 필요할 때 `packages-lock.json`; `ProjectSettings/ProjectVersion.txt` |

씬·프리팹은 먼저 설명 문서와 관련 코드에서 작업 범위를 정한다. YAML 내용은 요청이 있을 때만 읽고, `.meta`는 GUID·참조 문제를 조사할 때만 읽는다. 씬 문서에 적힌 진행·연결 설명은 실제 참조 검증을 대신하지 않는다.

## 상세 문서와 검사

- 전체 채보 기반 보스 연출 개편 계획: `Docs/CHART_PRESENTATION_REFACTOR_PLAN.md`. 한 씬·한 전체 채보, 세 액션의 외형 대체, 이관·검증·제거 순서와 현재 진행 상태. 전체 채보·씬/허브 연결과 분할 구조 제거는 완료. 노트 기반 연출 연결·범용 연출 편집기 및 Unity 실행 검증은 남아 있다.
- 편집기 사용법: `Assets/Scripts/채보 편집기 사용법.md`.
- 기획 원문: `Assets/Scripts/기획.md`. 현재 구현 확인을 대신하지 않으며 이번 개편의 제작 흐름은 위 개편 계획을 따른다.
- 확장 설명: `Assets/Scripts/RoomRhythm/`의 `MapExtensions.md` (방 크기·이동), `StageDirector.md` (전용 진행·재시작), `StageTargets.md` (장애물·표적).
- 검사 시작점: `Tests/RoomRhythm/README.md`, `Run-Checks.ps1`. C# 컴파일·로직 검사에는 생성된 프로젝트 참조와 .NET 런타임이 필요하다.
- C# 검사: 같은 폴더의 `RoomRunChecks.cs`, `BeatChartChecks.cs`, `MapChartChecks.cs`, `OffsetCalibrationChecks.cs`, `SongOffsetChecks.cs`, `StageRecordStoreChecks.cs`, `MafiaStageChecks.cs` 중 변경 영역에 해당하는 검사를 확인한다.
- 에셋 검사: 같은 폴더의 `StageSelectSceneChecks.py`, `RockBossChecks.py`, `MafiaStageSceneChecks.py`. 씬·프리팹 검사 요청 범위에서 선택하며 실제 실행 방법·의존성은 해당 검사에서 확인한다. Unity 플레이 모드 검증을 대신하지 않는다.

## 유지 원칙

구조 변경을 끝낸 직후 `AGENTS.md`의 갱신 명령을 따른다. 폴더 역할, 기능 경계, 주요 진입점과 연결 문서만 유지한다. 모든 파일·메서드·에셋 값이나 작업 이력을 나열하지 않는다. 이 문서의 경로가 실제와 다르면 관련 범위만 확인해 수정한다.
