# Bounce 광고 연결

공용 모듈과 모바일 설정은 [AppLovin MAX 가이드](applovin-ads.md)를 참고한다. Bounce 연결은 `develop-bounce`에서 관리한다.

## 실행 및 설정

`BK > Integration > Open Bounce Bootstrap`으로 시작한다. 설정 에셋은 `Assets/_Project/Bounce/Resources/BounceAdsSettings.asset`이다.

- 로비 하트 버튼 → `Watch ad +1`: 광고 보상 콜백과 닫힘을 확인한 뒤 하트 1개를 저장한다. 가득 찬 상태에서는 비활성이다.
- 12스테이지부터 클리어 화면의 광고 버튼: 기본 50골드는 먼저 저장하고 광고 완료 시 추가 50골드를 한 번만 지급한다. 취소하면 기본 보상을 유지하며 다시 선택할 수 있다.
- 일반 보상 받기: 12스테이지부터 전면 광고를 시도한다. 준비되지 않았으면 바로 진행한다. 보상형을 포함한 마지막 광고 표시부터 90초 간격을 둔다.
- 기본 배너는 꺼져 있다. `Show Lobby Banner`를 켜면 로비 하단에 표시하며, 인게임과 전체 화면 광고 중에는 숨긴다. 활성화 전 기기별 Safe Area와 하단 UI 겹침을 확인한다.
- NoAds 상품 구매값은 전면·배너에 적용된다. 선택형 보상 광고는 유지한다.

에디터에서는 SDK Key 없이 MAX 공식 스텁이 열린다. `Grant Reward` 후 닫으면 보상, 그냥 닫으면 취소다. 클리어 광고를 초기 레벨에서 확인하려면 Inspector에서 `First Reward Stage` 또는 `First Interstitial Stage`를 1로 바꾸고, 테스트 후 12로 복구한다. GRP1 기본값 12/90초를 유지했으며 Bounce 초기 튜토리얼에는 강제 광고를 넣지 않았다.

## 저장과 동시 실행

기본 클리어 보상은 광고 이전에 지급·저장한다. 추가 보상은 완료 시점에 지급하며 기존 저장 실패 알림·재시도 처리를 사용한다. 광고 중에는 중복 광고, 구매, 화면 이동을 차단하고 게임 시간·오디오 상태를 복원한다. 클리어 추가 보상은 같은 라운드에서 한 번만 받을 수 있다.

## 검증 범위

공용 정책 테스트와 MAX 공식 스텁 테스트, Bounce 하트 팝업·클리어 추가 보상의 취소/지급/저장/중복 차단 테스트를 포함한다. 기존 Unity IAP Fake Store 구매 테스트도 함께 실행한다. 실제 모바일 광고·미디에이션·CMP는 별도 기기 검증 대상이다.

2026-10-26 검증: EditMode 140/140, 관련 PlayMode 9/9 통과. 광고 UI 문구 수정 후 `AdFlowTests` 2/2 재통과 및 하트/클리어 화면 캡처 확인. 로컬 결과는 `Logs/ads-bounce-edit.xml`, `Logs/ads-bounce-play.xml`, `Logs/ads-bounce-ui.xml`에 있다.

모바일은 기본 비활성이다. 해당 앱의 SDK Key와 플랫폼별 Ad Unit ID를 입력하고 공용 가이드의 모바일 설정을 마친 뒤 `Enable Mobile Ads`를 켠다. GRP1의 키와 광고 ID는 복사하지 않았다.
