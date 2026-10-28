# 기존 커밋 제목 변경 기록

2026-10-28, 개발자 요청으로 날짜·임시 문구 중심이던 기존 커밋 13개의 제목을 변경 내용에 맞게 정리했다.
각 커밋의 파일 트리, 부모 순서, 작성자·커미터와 원래 날짜를 보존했다. 제목과 그에 따라 달라지는 부모 ID만 변경했다.

| 날짜 | 변경 전 제목 | 변경 후 제목 | 이전 ID | 새 ID |
| --- | --- | --- | --- | --- |
| 2026-09-16 | Initial commit | chore: Unity URP 프로젝트 초기 설정 | `e5ae5d5` | [`15ac276`](https://github.com/yusun1115/Deadline-4Sec/commit/15ac276f01e983531d334b3c94304d2343a1badb) |
| 2026-09-17 | 기본시스템구현 | feat: 3레인 이동과 공중 전투·4초 생존 시스템 구현 | `14fa319` | [`3cca35f`](https://github.com/yusun1115/Deadline-4Sec/commit/3cca35f159414ad8733026fd7a95d3d056af8bbc) |
| 2026-09-18 | 장애물 패턴 | feat: 장애물·코스 패턴과 점수 시스템 추가 | `3c051d2` | [`a391a34`](https://github.com/yusun1115/Deadline-4Sec/commit/a391a348a61c47b9a54abd9f34efc20ff19e8eff) |
| 2026-09-26 | 0926 | feat: 게임 진행과 모바일 스와이프·전투 피드백 구현 | `792f78b` | [`38c0b8b`](https://github.com/yusun1115/Deadline-4Sec/commit/38c0b8b8f0c337e43d476c40509398fafc746793) |
| 2026-09-28 | 0928 | feat: 튜토리얼·편집 가능한 메뉴와 전투 판정 개선 | `3ebac73` | [`0865ca2`](https://github.com/yusun1115/Deadline-4Sec/commit/0865ca2ad14dd971a7551346f30f5e79de4ea5c9) |
| 2026-09-28 | 0928 | feat: 코인 수집과 파워업·강화 시스템 추가 | `d0a8598` | [`966bbe2`](https://github.com/yusun1115/Deadline-4Sec/commit/966bbe256efd52959b1f1c5e1dbf45b3c84d8773) |
| 2026-09-29 | 0929 | feat: 부활·소모품·스킨 상점과 선물 보상 추가 | `6efa2d5` | [`3bceaeb`](https://github.com/yusun1115/Deadline-4Sec/commit/3bceaebf1bae39cab1286564a3a823140b2656ed) |
| 2026-10-02 | 1002 | feat: 사신 캐릭터·고딕 배경과 전투·사운드 연출 적용 | `5dabf67` | [`0a3eacc`](https://github.com/yusun1115/Deadline-4Sec/commit/0a3eacc53193754302b79120b99f98039f4cbaa6) |
| 2026-10-09 | 1009 | feat: 콤보 HUD와 파워업·획득·사망 피드백 개선 | `4614954` | [`ffe8aeb`](https://github.com/yusun1115/Deadline-4Sec/commit/ffe8aeb381da90c1674b1cedebbf99e212df358b) |
| 2026-10-14 | 1014 | feat: 클래식 모드와 모드 선택·로딩·결과 화면 구현 | `d0d06c0` | [`fe580c4`](https://github.com/yusun1115/Deadline-4Sec/commit/fe580c4655c58925dfdd354403ad958dc2c86a5e) |
| 2026-10-15 | 1015 | feat: 업적과 Google Play 리더보드 연동 기반 추가 | `fb623ee` | [`30ca556`](https://github.com/yusun1115/Deadline-4Sec/commit/30ca55658603fb158ea3a0df25437dae8798ed99) |
| 2026-10-18 | 1018 | feat: 일일 퀘스트·재화 상점과 상층 코스 추가 | `43a759c` | [`f1cf92d`](https://github.com/yusun1115/Deadline-4Sec/commit/f1cf92dd9cf1a8de6c64d52a38b1bd4325571f07) |
| 2026-10-20 | 1020 | feat: 벽 달리기와 낙하 판정·체크포인트 부활 구현 | `06bfd35` | [`c393916`](https://github.com/yusun1115/Deadline-4Sec/commit/c3939168ffd6e9674a2fb148f2a680d8f0d5a3c0) |

## 보존 검증과 복구

- 기존 13개 커밋과 새 커밋의 tree ID가 각각 동일함을 확인했다.
- 원본 author·committer 필드와 메시지 본문을 보존했다. 날짜를 새로 작성하지 않았다.
- 원본 이력은 로컬 백업 참조 `refs/backup/codex/before-commit-title-cleanup-20261028`와 검증한 Git bundle로 보관했다.
- 원본 코드·에셋의 변경 없이 제목을 정리했으므로, 이 작업에 따른 Unity 기능 변경은 없다.

문서의 이전 기준 리비전 `06bfd358fbacf2ed4275fe416043b88c090de0f7`은 새 리비전 `c3939168ffd6e9674a2fb148f2a680d8f0d5a3c0`과 같은 파일 내용을 갖는다.

최신 목록은 [main 커밋 기록](https://github.com/yusun1115/Deadline-4Sec/commits/main/)에서 확인할 수 있다.
