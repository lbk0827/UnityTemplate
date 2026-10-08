# sf → UnityTemplate 이식 로드맵

> 조사 결과와 사용자 결정(2026-10-08)을 묶은 상위 로드맵. 각 마일스톤은 별도 실행 계획 문서로 쪼갠다.

## 결정 사항 (사용자 확정)

- 권장 순서대로 진행한다.
- StoreManager / AdManager / AppsFlyer / Firebase / 서버 연동은 **이식하지 않는다**. 우리 게임에서 서버를 붙여 직접 구현한다. 단, 나중에 끼울 수 있도록 **추상화 지점**(`ITimeSource`, 결제/광고 인터페이스)은 남긴다.
- Animation Sequencer는 공개 MIT 패키지(`com.brunomikoski.animationsequencer`)를 설치하고, sf 쪽에서 가져올 수 있는 것은 가져온다.
- 로비/상점 뷰는 sf를 참고만 하고 BK.UI 위에 다시 구현한다.

## 소스 제약

- sf = `D:\ngfe_sf`. RootBox는 asmdef가 없고 사내 비공개 패키지(`com.doubleugames.*`) 위에 있어 코드를 그대로 못 옮긴다. 패턴·로직만 재구현한다.
- 스택 차이: UniRx→R3, DOTween→(DOTween은 Animation Sequencer 때문에 추가), Sonity/NiceVibrations/SRDebugger/Newtonsoft 없음.
- Smash 인게임 코어(`SmashCarnival/`, 레벨, 블록 VFX, 물리값, `RefLevels/`)는 출처 문제로 이식 금지.

## 마일스톤

| # | 마일스톤 | 계획 문서 | 상태 |
|---|---|---|---|
| A | Animation Sequencer + DOTween 설치, sf 팝업 시퀀스 프리팹 이식 | `2026-10-08-animation-sequencer-install.md` | 완료 (EditMode 16/16) |
| B | 프레임워크 기반: 범용 세이브 서비스, 옵션 통일, Back/Escape 스택 | `2026-10-08-save-options-back.md` | 완료 (EditMode 32/32, PlayMode 11/11). 암호화(sf PlayerDataCrypto 상당)는 미포함, 필요 시 SaveFile에 바이트 계층 추가. Bounce `LocalSaveStore`는 G에서 교체 |
| C | UI 계층: 팝업 딤, 스크린 커버(StageReveal)+SceneFlow, 메시지/토스트, 씬 전환 페이드 | `2026-10-08-ui-dim-cover-message.md` | 완료 (프리팹 생성, EditMode 78/78, PlayMode 12/12). StagePreloaderRegistry는 D로 |
| D | 메타 루프: 지갑/하트 충전, 스테이지 입장 게이트, PendingRewardQueue/DisplayLock, 실패/컨티뉴 사다리, 프리로더 레지스트리 | `2026-10-08-meta-loop.md` | 완료 (EditMode 78/78). 클리어/실패 팝업·코인 플라이 UI는 G에서 |
| E | 툴링: xlsx 익스포터 CLI + 범용 임포터, sf 에디터 툴 4종 이식, 규약 테스트 | `2026-10-08-tooling.md` | 완료. LocalizationTool(Unity Localization 의존)·BuildSettingsVerifier는 미이식. Bounce `ShopTableImporter`는 범용 임포터로 대체 가능 |
| F | 메타 확장 | F1 `2026-10-08-meta-extensions.md` (주간 스텝 오퍼·데일리 리워드·윈 스트릭) / F2 로컬 푸시(예정) | F1 코드 완료(Roslyn 컴파일 검증, EditMode 테스트 17개 작성). Unity 실행은 에디터가 닫힌 뒤 |
| G | 로비/상점 재구현 (sf Recipe/Sequence 프리팹 재이식 포함) | (예정) | |

## 검증 수단

- Unity 6000.3.21f1 배치모드: `Unity.exe -batchmode -nographics -quit -projectPath D:\UnityTemplate -logFile <log>` 로 컴파일, `-runTests -testPlatform EditMode` 로 테스트. MCP가 죽어 있어도 동작한다. 프로젝트가 에디터에 열려 있으면(`Temp/UnityLockfile`) 실패한다.
