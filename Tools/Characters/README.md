# 캐릭터 제작 도구

게임 실행에 포함되지 않는 도형 에셋 원본과 오프라인 제작 도구입니다.

- `BuildGeometricPlayer.py`: 플레이어 파츠·월드/UI 프리팹 제작
- `BuildRockBoss.py`: 바위 보스 파츠·프리팹·미리보기 제작
- `PreviewGeometricPlayer.py`: 플레이어 미리보기만 갱신
- `Templates/`: 프리팹 컴포넌트 직렬화 템플릿
- `Previews/`: 정적 외형 미리보기

Python과 Pillow가 필요합니다. 제작 스크립트 재실행은 해당 프리팹과 이미지를 덮어씁니다. Unity에서 편집한 포즈를 보존해야 한다면 재실행하지 마세요. 게임은 Assets에 저장된 결과만 사용합니다.
