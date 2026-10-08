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
| B | 프레임워크 기반: 범용 세이브 서비스, 옵션 통일, Back/Escape 스택 | (예정) | |
| C | UI 계층: 팝업 딤, StageRevealCoordinator, 메시지/토스트, 씬 전환 페이드 | (예정) | |
| D | 메타 루프: 하트, 스테이지 클리어 플로우(PendingRewardQueue), 실패/컨티뉴 | (예정) | |
| E | 툴링: TableExporter 적응, 범용 에디터 툴, 테스트 관례 | (예정) | |
| F | 메타 확장: 엔드리스 오퍼 로직, 데일리 리워드, 윈 스트릭, 로컬 푸시 | (예정) | |
| G | 로비/상점 재구현 (sf Recipe/Sequence 프리팹 재이식 포함) | (예정) | |

## 검증 수단

- Unity 6000.3.21f1 배치모드: `Unity.exe -batchmode -nographics -quit -projectPath D:\UnityTemplate -logFile <log>` 로 컴파일, `-runTests -testPlatform EditMode` 로 테스트. MCP가 죽어 있어도 동작한다. 프로젝트가 에디터에 열려 있으면(`Temp/UnityLockfile`) 실패한다.
