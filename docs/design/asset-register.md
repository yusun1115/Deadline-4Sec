# 외부·생성형 에셋 등록부

기준일: 2026-10-26. 파일 존재와 실제 런타임 사용 여부는 구분한다. 최종 빌드 포함 목록은 기말 전 확정한다.

| 항목 | 프로젝트 위치·출처 | 조건·확인 상태 | 남은 기록 |
| --- | --- | --- | --- |
| UI·컨셉 이미지 | `Assets/Art/2D/`, `Assets/Art/Reference/`. ChatGPT 또는 Codex imagegen 시안 후 개발자 직접 리터칭 | 개발자 확인. 입력 이미지의 이용 권한과 생성 이력을 함께 보관 | 프롬프트, 원본, 리터칭 전후, 생성 날짜·도구 |
| 3D 모델·텍스처 | `Assets/Art/3DModel/`, `TripoRaw/`, Tripo | 개발자가 생성 당시 유료 사용 확인. [Tripo 공식 이용 안내](https://www.tripo3d.ai/help/privacy-policy/how-to-use-tripo-models-commercially)는 유료 생성물의 이용·수정·상업 이용 권한을 설명함. 입력 이미지 권리는 별도로 확인 | 모델별 task ID·원본·생성 당시 요금제 이력, 최적화 내역 |
| BGM | `Assets/Sound/BGM/BGM_MainScreen.mp3`, `BGM_InGame.mp3`, Suno 활용 | 개발자가 생성 당시 유료 사용 확인. [Suno 약관](https://about.suno.com/terms)과 [요금제 안내](https://suno.com/pricing)의 유료 플랜 조건을 적용. 개별 곡의 원본 출처와 허용 다운로드 이력을 연결 | 곡 URL·ID·생성일·다운로드일·요금제·편집 이력. 모든 로컬 BGM이 Suno 결과물인지 파일별 확정 |
| FREE Casual Game SFX Pack | `Assets/Casual Game Sounds U6/CasualGameSounds/`, Dustyroom, productId 54116 | [공식 Asset Store](https://assetstore.unity.com/packages/audio/sound-fx/free-casual-game-sfx-pack-54116)에 Standard Unity Asset Store EULA 표기. 패키지 내부 `license.pdf`도 보관. 원본 파일 재배포 조건 확인 | 사용한 클립 목록, 취득 이력, 최종 빌드 포함 여부 |
| DOTween (HOTween v2) | `Assets/Plugins/Demigiant/DOTween/`, Demigiant, metadata 1.2.825 | [공식 DOTween 라이선스](https://dotween.demigiant.com/license.php). 일반판 상업·비상업 사용 가능, 저작권 고지·원문 유지. Pro와 구분 | 사용 버전·원문 readme 보존 |
| GFC Red Spirit / 투혼경남체 | `Assets/Font/GFCRedSpirit-Bold.ttf`, 경남FC | 파일 내부 이름·메타데이터 확인. [공식 배포처](https://www.gyeongnamfc.com/)의 상세 배포·임베딩·재배포 조건 확인 후 원문 보관 | 원래 다운로드 링크, 라이선스 파일. 조건 불명확 시 교체 |
| HiKR 하이커체 | `Assets/Font/HiKR-ExtraBold.ttf`, 한국관광공사 | [공식 소개·배포 페이지](https://korean.visitkorea.or.kr/detail/rem_detail.do?cotid=8653d837-4660-46a2-a419-7b470d89a70c#station_font). 게임 임베딩·저장소 배포 조건을 확인하고 원문 보관 | 실제 사용 범위와 배포 조건 |
| Callifont Sharpie | `Assets/Font/CallifontSharpie-Bold.ttf` | 원 배포처·라이선스 파일을 현재 저장소에서 확인하지 못함 | 출처·사용 여부 확인. 미확인 시 최종 빌드에서 제외/교체 |
| Demonic Dynamic Music pack | `Assets/Demonic Dynamic Music pack/` | 폴더·오디오 존재, 원 공급자와 라이선스 원문 미확인 | 사용 여부·취득 출처 확인. 미확인 시 제외/교체 |
| Google Play Games / External Dependency Manager | `Assets/GooglePlayGames/`, `Assets/ExternalDependencyManager/` | SDK 코드와 EDM의 `Editor/LICENSE` 존재. 실제 서비스 ID 미설정 | SDK 원문 고지 보존, 기말 오프라인 범위에서는 서비스 제외 |

레퍼런스 게임은 설계 참고용이다. 스토어의 설명을 참고했고 캐릭터·모델·사운드를 가져오지 않았다.

공개 GitHub에 외부 원본 에셋을 올리는 권한과 컴파일한 게임 안에서 사용하는 권한은 다를 수 있다. 저장소 공개 범위와 실제 포함 파일을 기말 전 점검한다.
