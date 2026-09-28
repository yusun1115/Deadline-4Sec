# 타이틀 화면 직접 편집

1. Unity의 Play를 끈다.
2. Project에서 `Assets/Scenes/EndlessRun.unity`를 연다. 이미 열려 있던 씬이면 저장된 파일을 다시 연다.
3. Hierarchy에서 `Canvas > TitlePanel`을 펼친다.
4. Inspector에서 수정하고 **Ctrl+S**로 씬을 저장한다.

| 수정 대상 | Hierarchy 경로 | Inspector 항목 |
| --- | --- | --- |
| 타이틀 제목 | Canvas / TitlePanel / TitleText | TextMeshPro - Text (UI): Text Input, Font Size, Vertex Color |
| 부제 | Canvas / TitlePanel / SubtitleText | TextMeshPro - Text (UI): Text Input, Font Size, Vertex Color |
| 시작 버튼 문구 | Canvas / TitlePanel / StartButton / Label | Text Input |
| 튜토리얼 버튼 문구 | Canvas / TitlePanel / TutorialButton / Label | Text Input |
| 설정 버튼 문구 | Canvas / TitlePanel / SettingsButton / Label | Text Input |
| 버튼 색·이미지 | 각 Button 오브젝트 | Image: Color, Source Image / Button: Colors |
| 위치·크기 | 원하는 제목/버튼 오브젝트 | Rect Transform: Pos X/Y, Width/Height, Anchors |
| 배경 색·이미지 | Canvas / TitlePanel | Image: Color, Source Image |
| 화면 비율 대응 | Canvas | Canvas Scaler: Reference Resolution, Match |

- Scene 뷰에서 UI를 수정하려면 2D를 켜고 원하는 오브젝트를 선택한 뒤 **F**로 화면에 맞춘다. Rect Tool(**T**)로 이동/크기 조절이 가능하다.
- 글자 크기가 자동 조절되면 TextMeshPro의 **Auto Size**를 끄거나 Min/Max를 조절한다.
- 버튼의 `Label`은 버튼 크기에 맞춰 늘어난다. 버튼 외형은 부모 Button에서, 문구는 자식 Label에서 편집한다.
- 게임 폰트는 **GFCRedSpirit-Bold SDF**를 사용한다.
- 버튼 기능은 Button의 **On Click**에 연결되어 있다. 외형을 편집할 때 연결을 유지하면 그대로 작동한다.
- Play 중 변경은 Unity의 일반 동작에 따라 종료 시 되돌아간다. 외형 변경은 Play를 끄고 저장한다.

## 설정·결과 화면

- 설정: `Canvas / SettingsPanel` (평소 비활성). 편집할 때 활성화하고 TitlePanel을 잠시 끄면 확인하기 쉽다.
- 결과의 메뉴 버튼: `Canvas / ResultPanel / MainMenuButton`.
- 실제 게임의 시작 씬은 **EndlessRun**이다. TestScene·GrayboxCourse에도 같은 구조를 저장했다.
