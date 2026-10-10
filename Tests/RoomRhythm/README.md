# 유지하는 회귀 검사

- `MafiaAmbushChecks.cs`: 목적지 방 ID 기반 고기방패 연출 시간, 채보 이동·복제별 연결, 선행 시간 오류, 일반 적 판정과 디버그 진행 검사. `Run-Checks.ps1`에 포함.
- `MafiaAmbushAssetChecks.py`: 고기방패 프리팹의 내부 참조·공유 보스·4개 모션·판정 없는 부하·효과·기본 방향/크기 검사. Unity 가져오기·재생은 별도.

- `MafiaStageChecks.cs`: 저장된 적용 맵·실행 노트를 대조하고, 정확/이른/늦은 입력·실패, 사무실 공백·15박·3발 탄환·창문·디버그 자동 진행·재시작을 검사합니다. 작업 중인 미적용 맵 초안은 달라도 됩니다. `Run-Checks.ps1`에 포함됩니다.

단순 시연용 스크립트가 아니라 판정·편집·저장·씬 참조의 오류를 검출하는 검사입니다. Unity 플레이 모드를 실행하지 않습니다.

- `Run-Checks.ps1`: 이동 판정, 비트/맵 편집, 오프셋, 최고 기록 검사와 C# 컴파일. 프로젝트의 생성된 csproj 참조 및 .NET 런타임이 필요합니다.
- `RecordTurntableChecks.cs`: 레코드 회전·구역 선택·톤암 내리기·음원 대기·재생·복귀 상태 검사. `Run-Checks.ps1`에 포함.
- `StageMusicPreviewChecks.cs`: 실제 미리듣기 코드와 톤암 상태를 연결해 로딩 중 취소·곡 교체·시간 초과·실패 후 재선택·페이드 중 볼륨 조절을 검사. 오디오 로딩/재생 API는 대역이며 실제 출력음 검증은 별도. `Run-Checks.ps1`에 포함.
- `StageRecordStoreChecks.cs`: 최고 기록과 곡별 볼륨의 독립 저장·잘못된 값 처리·기존 저장 키 호환 검사. `Run-Checks.ps1`에 포함.
- `SafeRoomChecks.cs`: 실제 `StageProgression`·`SafeRoomController`·`SafeRoomTransit`으로 5개 튜토리얼의 클리어→공용 안전방 입장→문 닫힘→다음 씬 흐름, 입장 중 입력 차단, Esc 재입장 경로, 레코드 플레이의 허브 복귀를 검사. 시간·입력·코루틴·씬 로딩 경계는 대역이며 `Run-Checks.ps1`에 포함.
- `SafeRoomSceneChecks.py`: 공용 안전방의 왼쪽 통로·닫힌 문·입장 지점·UI 렌더러·로컬 참조와 튜토리얼 5개/빌드 목록 연결 검사. Unity 화면 재생 검증은 별도.
- `StageStartupChecks.cs` / `StageStartupTestDoubles.cs`: 실제 `StageProgression`, `RoomKeyboard`, `SongTimeline`, `RoomRun` 코드로 최초 Dynamic 입력 갱신 이후 시작, 에디터/플레이 시계 원점 차이, 시작·사격 입력, 재시작 대기를 검사. 엔진의 입력·오디오 API와 씬 연결이 필요한 `RoomSession` 경계는 대역이므로 Unity 실행 검증을 대신하지 않음. `Run-Checks.ps1`에 포함.
- `EnemyRestartChecks.cs`: 실제 `RoomEnemy`, `RoomCombat`, `RoomRun` 코드로 복도 적 등장·사망·반복 재시작을 검사. 수동/자동 사격의 쓰러짐, 즉시 판정 제외, 판정선 숨김, 음악 정지 후 페이드, 방 퇴장, 연출 도중 재시작의 자세·불투명도 복원 및 잘못된 씬/채보 배치 검출 포함. 엔진 경계는 대역이며 `Run-Checks.ps1`에 포함.
- `StageSelectSceneChecks.py`: 튜토리얼/안전지대/선택 씬과 캐릭터 프리팹 연결 검사
- `MafiaStageSceneChecks.py`: 마피아 씬의 참조·계층, 적용 맵의 방 좌표·크기, 일반 적, 사무실 소품·클립·효과음·카메라, 단일 Combat과 허브 연결 검사
- `MafiaAnimationChecks.py`: 마피아 모션 15개의 리그/Controller 참조, 편집·실행 곡선 일치, 반복 연결, 반동 횟수, 키 사이 양손·개머리판 접점 검사. Python 표준 라이브러리만 사용하며 Unity 가져오기·미리보기는 별도로 확인
- `RockBossChecks.py`: 보스 관절·파츠·Animator·스프라이트 참조 검사. Pillow 필요

프로젝트 루트에서 실행:

```powershell
./Tests/RoomRhythm/Run-Checks.ps1
python ./Tests/RoomRhythm/SafeRoomSceneChecks.py
python ./Tests/RoomRhythm/StageSelectSceneChecks.py
python ./Tests/RoomRhythm/MafiaStageSceneChecks.py
python ./Tests/RoomRhythm/MafiaAnimationChecks.py
python ./Tests/RoomRhythm/RockBossChecks.py
```

옛 FirstMovement 차트 전용 검사와 중복된 씬 검사 스크립트는 제거했습니다. 더 이상 `-SkipAuthoredAssets` 옵션이 필요하지 않습니다.
