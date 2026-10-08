# 캐릭터 제작 도구

게임 실행에 포함되지 않는 도형 에셋 원본과 오프라인 제작 도구입니다.

- `BuildGeometricPlayer.py`: 플레이어 파츠·월드/UI 프리팹 제작
- `BuildRegularEnemy.py`: 일반 적 파츠·프리팹·미리보기 제작

  `--fallen-only`로 실행하면 기존 서 있는 그림을 덮어쓰지 않고 `Assets/Resources/RegularEnemyFallen.png`의 누운 전신 자세만 생성합니다. `FALLEN_SHAPES`가 원본이며 `RoomEnemy`가 사망 연출에 사용합니다.
- `BuildLegPrefabs.py`: 플레이어에 저장된 다리 관절을 독립 월드/UI 보행 프리팹으로 추출
- `BuildRockBoss.py`: 바위 보스 파츠·프리팹·미리보기 제작
- `BuildMafiaBoss.py`: 1스테이지 마피아 보스 파츠·프리팹·미리보기 제작 (공통 보스 제작 함수 재사용)
- `PreviewGeometricPlayer.py`: 플레이어 미리보기만 갱신
- `Templates/`: 프리팹 컴포넌트 직렬화 템플릿
- `Previews/`: 캐릭터 외형과 모션 참고 미리보기

Python과 Pillow가 필요합니다. 제작 스크립트 재실행은 해당 프리팹과 이미지를 덮어씁니다. Unity에서 편집한 포즈를 보존해야 한다면 재실행하지 마세요. 게임은 Assets에 저장된 결과만 사용합니다.

`Previews/MafiaMotionsPreview.png`와 `.gif`는 마피아 클립 15개의 저장된 곡선과 리그를 샘플링한 오프라인 참고 화면입니다. Unity 실행 녹화가 아니며 클립을 수정해도 자동 갱신되지 않습니다. 실제 편집 대상은 `Assets/Animations/Mafia/`의 `.anim`이며, [마피아 모션 편집 안내](../../Docs/MAFIA_MOTION_PRACTICE.md)를 따릅니다.
