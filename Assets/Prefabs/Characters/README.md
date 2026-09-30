# 도형 탐정 플레이어 리그

정수리에서 본 인간 탐정입니다. 코트·중절모·오른손 권총을 도형으로 표현합니다. 다리와 왼팔은 탑뷰에서 보이지 않도록 LeftHip, RightHip, LeftShoulder 오브젝트를 비활성화했습니다. RightPauldron도 숨겨 갑옷 같은 어깨판을 없앴습니다. 관절은 편집용으로 보존하지만 현재 실루엣에는 나타나지 않습니다.

- `GeometricPlayer.prefab`: MainScene과 Stage01~05의 공용 SpriteRenderer 리그.
- `GeometricPlayerUI.prefab`: AwakeningScene, SafeRoom01~04, StageSelectScene의 공용 UI Image 리그.
- `Assets/Arts/GeometricPlayer/`: 질감 없이 다각형 면으로 만든 10종 파츠 이미지. 좌우 재사용을 포함해 리그에는 17개 독립 렌더러가 있습니다.

기존 Player는 이동과 조준 방향을 담당하고, 그 아래 프리팹은 외형과 관절을 담당합니다. 씬에 저장된 프리팹 인스턴스이며 실행 중 생성하지 않습니다. 공용 외형은 프리팹에서 수정하고, 특정 씬만 바꿀 때는 인스턴스 override를 사용합니다.

## 관절 편집

`Hips → Spine → Neck → Head`가 몸통과 머리입니다.

`Spine → Left/RightShoulder → Left/RightElbow → Left/RightWrist`는 팔입니다. 오른손의 `Grip → Pistol` 아래가 아닌, Grip의 별도 자식 `Muzzle`이 실제 발사 위치입니다. 권총 외형을 바꾸면 Muzzle도 총구 끝으로 옮겨 주세요.

`Hips → Left/RightHip → Left/RightKnee → Left/RightAnkle`은 다리입니다. 각 관절 아래의 Thigh, Shin, Foot 등은 외형 파츠입니다. 관절은 부모 위치에 회전축을 두었고 파츠 이미지는 자식으로 분리했습니다.

2D 관절은 local Z rotation으로 회전합니다. 관절 scale은 (1,1,1)로 유지하고, 길이·너비는 자식 외형이나 다음 관절 위치로 조절하세요. 팔꿈치/무릎 이동과 회전 모두 직접 키를 줄 수 있습니다.

Animation 창에서 **프리팹 루트 GeometricPlayer / GeometricPlayerUI**를 선택해 클립을 생성하면 같은 루트에 Animator를 붙여 사용할 수 있습니다. 두 리그의 자식 경로는 같아 Z 회전 트랙을 공유할 수 있지만 UI 위치 단위는 월드 리그의 60배이므로 position 트랙은 별도로 작성합니다. 현재 자동 보행/팔다리 회전이나 Animator Controller는 없으며, 사용자가 작성한 관절 포즈를 스크립트가 덮어쓰지 않습니다. 부모 Player 전체의 방향만 조준 으로 회전합니다. 안전지대에서는 자유 이동 없이 D로 다음 씬에 진입합니다.

## 생성 소스와 검증

도형 꼭짓점, 톤, 기본 포즈: `Tools/Characters/BuildGeometricPlayer.py`.
이 스크립트는 오프라인 제작용이며 Unity 런타임 코드가 아닙니다. 다시 실행하면 프리팹 기본 포즈와 파츠 이미지를 덮어쓰므로 **Unity에서 편집한 뒤에는 필요 없이 재실행하지 마세요**. GUID는 유지합니다.

미리보기: `Tools/Characters/PreviewGeometricPlayer.py`. Unity 렌더링 결과가 아닌 제작 좌표로 그린 정적 미리보기입니다.

