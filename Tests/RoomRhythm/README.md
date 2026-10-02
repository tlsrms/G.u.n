# 유지하는 회귀 검사

- `MafiaStageChecks.cs`: 저장된 전체 채보의 맵 초안·적용 맵·실행 노트를 대조하고, 정확/이른/늦은 입력·실패, 등장 공백, 디버그 자동 진행·재시작을 검사합니다. `Run-Checks.ps1`에 포함됩니다.

단순 시연용 스크립트가 아니라 판정·편집·저장·씬 참조의 오류를 검출하는 검사입니다. Unity 플레이 모드를 실행하지 않습니다.

- `Run-Checks.ps1`: 이동 판정, 비트/맵 편집, 오프셋, 최고 기록 검사와 C# 컴파일. 프로젝트의 생성된 csproj 참조 및 .NET 런타임이 필요합니다.
- `StageSelectSceneChecks.py`: 튜토리얼/안전지대/선택 씬과 캐릭터 프리팹 연결 검사
- `MafiaStageSceneChecks.py`: 마피아 씬의 참조·계층, 전체 맵의 43개 방 좌표·크기, 일반 적·보스 표적, 단일 Combat과 허브 연결 검사
- `RockBossChecks.py`: 보스 관절·파츠·Animator·스프라이트 참조 검사. Pillow 필요

프로젝트 루트에서 실행:

```powershell
./Tests/RoomRhythm/Run-Checks.ps1
python ./Tests/RoomRhythm/StageSelectSceneChecks.py
python ./Tests/RoomRhythm/MafiaStageSceneChecks.py
python ./Tests/RoomRhythm/RockBossChecks.py
```

옛 FirstMovement 차트 전용 검사와 중복된 씬 검사 스크립트는 제거했습니다. 더 이상 `-SkipAuthoredAssets` 옵션이 필요하지 않습니다.
