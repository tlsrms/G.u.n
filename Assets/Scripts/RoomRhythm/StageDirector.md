# 스테이지 전용 진행과 재시작

기존 씬은 RoomSession의 Stage Director를 비워 두면 됩니다. 채보 완료 시 음악 정지, 클리어 피드백, 기록 저장과 다음 씬 이동은 기존 경로를 그대로 사용합니다. 기존 맵 데이터와 에디터 저장 형식은 변경하지 않았습니다.

보스별 진행은 StageDirector를 상속한 컴포넌트를 씬에 배치하고 RoomSession의 Stage Director 필드에 연결합니다. 런타임에서 생성하지 않습니다. 이 참조가 있으면 채보 완료만으로 스테이지가 끝나지 않으므로 반드시 전용 코드에서 CompleteStage()를 호출해야 합니다.

## 호출 순서

1. ResetStage(): 최초 초기화, 재시작, 채보 재설정 때 호출. 전용 상태와 보스 포즈, 연출 플래그를 복원합니다.
2. BeginStage(): 공통 음악 시계와 채보가 시작된 뒤 호출.
3. OnAction(result): 입력 묶음과 시간 초과 판정을 처리한 뒤 발생 순서대로 전달.
4. OnChartCompleted(): 채보 완료 시 실행당 한 번 호출. 이때 음악은 계속 흐릅니다.
5. Tick(songTime): 기본 화면 갱신 뒤 호출. 사망·스테이지 완료 상태에서는 호출하지 않습니다.
6. CompleteStage(): 채보가 완료된 상태에서만 성공하며 중복 호출은 false를 반환합니다. RoomSession.IsCleared가 참이 되어 기존 StageProgression이 기록 저장과 씬 전환을 처리합니다.

OnAction은 상태를 기록하는 데 사용하고, 기본 화면보다 우선하는 시각 효과는 Tick에서 반영하세요. RoomSession은 여전히 기본 방·플레이어 화면의 소유자입니다. 카메라 등 다른 컴포넌트와의 전용 제어권 전환은 이후 단계에서 다룹니다. 콜백에서 직접 세션을 재시작하거나 판정 모델을 변경하지 마세요.

## 액션 결과

MoveStarted는 이동 입력 성공, MoveArrived는 도착, DoorBroken은 돌파 성공, EnemyDefeated는 사격 성공, Failed는 치명적인 판정 실패입니다. 결과는 종류, 대상 ID, 발생 시간, 판정 등급, 실패 사유를 담습니다. 이동·문의 대상 ID는 목적지 방 ID이고 사격은 적 ID입니다. 특정 대상이 없는 실패의 ID는 null일 수 있습니다. 실패 사유로 문 충돌·문 놓침·적 놓침·방향 오류 등을 구분합니다.

도착 시간과 시간 초과 실패 시간은 발견한 프레임 시간이 아니라 모델의 실제 도착 시각·판정 마감 시각입니다. 도착은 새 판정이 아니므로 등급은 None입니다. 일반 빗나감이나 무시된 입력까지 성공·실패 이벤트로 만들지는 않습니다. 재시작 시 전달되지 않은 결과는 폐기합니다.

Tick의 songTime은 기존 SongTimeline.Time으로 입력 보정 전 음악 진행 시계입니다. OnAction.Time은 입력 보정을 적용한 판정 시각입니다. 음악 시작 지연과 박자 변환에는 기존 채보 설정을 사용하고, 보스마다 별도 타이머를 누적하지 마세요. 시간 이벤트는 정확한 시간 일치가 아니라 구간 통과와 실행 여부로 판단해야 프레임 지연에도 누락되지 않습니다.

## 한 채보 실행과 복원

스테이지는 하나의 전체 채보를 실행합니다. 보스 등장 공백도 노트 사이의 간격으로 작성하며, 중간에 채보·Combat·음악 시계를 교체하지 않습니다. 보스는 판정 결과에 반응하고 마지막 판정 뒤에도 퇴장 연출을 이어갈 수 있습니다.

`RoomSession`은 초기화 때 `StageResetState.Capture()`로 지정 루트의 원래 부모·위치·회전·크기·활성·렌더러·색을 저장합니다. 재시작 때 판정·음악·피드백을 초기화하고 `StageResetState.Restore()`, Director의 `ResetStage()`, 전용 표적의 `ResetTarget()` 순서로 복원합니다. Director는 코루틴과 실행별 플래그, 자신이 제어한 자세를 정리합니다.

방 크기·이동은 [MapExtensions.md](MapExtensions.md), 장애물·이동 표적은 [StageTargets.md](StageTargets.md)를 참고하세요. 마피아의 현재 연결은 `Assets/Scenes/Stages/MafiaStage01.md`에 있습니다. 범용 연출 편집기와 노트 기준 모션 연결은 `Docs/CHART_PRESENTATION_REFACTOR_PLAN.md`의 후속 단계입니다.

Unity에서 확인할 항목: 기존 스테이지의 자동 시작·클리어·재시작, 진행 코드 연결 시 채보 종료 후 음악 유지, 전용 CompleteStage 호출 뒤 기존 클리어 이동. 이번 단계에서는 Unity를 실행하지 않았습니다.
