# Unity IAP Fake Store

- 패키지: `com.unity.purchasing` 5.4.1 (GRP1과 동일 버전).
- 공용 연결: `BK.Purchasing.UnityFakeStore`, Unity IAP 5 `StoreController("fake")` 사용.
- 에디터에서만 초기화한다. 모바일 플레이어에서는 결제를 시작하지 않는다. 실제 스토어 상품 등록, 영수증 검증, 복원 연결은 별도 작업이다.
- IAP 5의 구형 `StandardPurchasingModule.useFakeStoreAlways/useFakeStoreUIMode`는 비기능 호환 속성이므로 사용하지 않는다.
- 게임은 상품 ID/종류와 지급 콜백을 공급한다. 콜백은 지급과 저장이 완료된 경우에만 `true`를 반환하고, 그 후 주문을 확정한다. 실패한 저장/확정은 `RetryPending()`으로 재시도한다.
- `BK.Meta.PurchaseRewards`는 거래 ID와 지급된 재화를 동일한 `WalletData` 파일에 저장한다. 같은 거래 재수신 시 보상을 다시 지급하지 않는다.
- Fake Store의 조회된 확정 이력은 취소 주문을 포함할 수 있으므로 지급 근거로 사용하지 않는다. 새 구매의 pending 콜백만 처리한다. 에디터 테스트용이며 실결제 복원을 구현한 모듈은 아니다.
- Fake Store 창의 기본 가격은 `$0.01`일 수 있다. 게임 상점 가격은 게임의 상품 테이블을 사용한다.

## 확인 방법

게임에서 `IStore.InitializeAsync()`에 상품과 지급 콜백을 연결하고, 구매 버튼에서 `TryPurchase()`를 호출한다. Unity의 **Fake Store** 창에서 구매/취소를 선택한다. 취소 시 보상을 지급하지 않고, 성공 시 저장 완료 후 주문을 확정한다. `Dispose()` 시 IAP 이벤트 구독을 해제한다.

## Bounce 연결 (`develop-bounce`)

1. `BK > Integration > Open Bounce Bootstrap`으로 부트스트랩을 열고 Play 한다.
2. 상점에서 코인·묶음·No Ads의 가격 버튼을 누른다. 로비의 Welcome Deal과 현재 주차의 유료 단계 버튼도 연결되어 있다.
3. 공식 Fake Store 창에서 Buy/Cancel을 선택한다. Buy는 테스트 재화·소유 상태를 저장하고, Cancel은 지급하지 않는다. 실제 과금은 없다.

- `BouncePurchases`가 테이블의 `MissileIngame`, `ExtraballIngame`, `BombIngame`, `LaserIngame`을 실제 Bounce 부스터로 변환한다.
- No Ads와 Welcome Deal은 한 번만 구매할 수 있다. No Ads는 로컬 소유 상태를 저장한다(이 프로젝트에는 실제 광고 SDK가 연결되어 있지 않다).
- 숨겨진 단계 상품은 해당 주차의 현재 유료 단계에서만 구매할 수 있다. 구매 중 주차가 바뀌면 상품 보상은 지급하지만 새 주차의 단계는 진행시키지 않는다.
- 구매 의도는 `BouncePurchaseIntent.json`, 거래 ID·상품 ID·단계 식별 정보는 보상과 함께 `WalletData.json`에 기록된다. 지갑 저장 후 단계 진행을 저장하고, 마지막에 주문을 확정한다. 재실행 시 지갑에 저장된 거래로 누락된 단계 진행을 복구한다.
- 에디터에서 구매한 재화도 기존 로컬 프로필에 저장된다. 자동 테스트는 `BK_KIT_TEST_SAVE_DIR`의 임시 프로필을 사용한다.
- 화면 가격은 상품 테이블의 USD 표시값이며 Fake Store 창의 `$0.01`과 다를 수 있다. 실제 스토어 가격 연동은 출시 결제 연결 시 수행한다.

## 검증 (2026-10-26, Unity 6000.3.21f1)

- `main`: 전체 EditMode 126/126, 공식 Fake Store 구매·취소·저장 재시도 PlayMode 1/1 통과.
- `develop-bounce`: 전체 EditMode 135/135, 상점 구매 및 OfferFlow PlayMode 5/5 통과. 공식 Fake Store 단독 테스트도 통과.
- 구매 지급 테스트: 부스터 ID 변환, 거래 중복 방지, No Ads/Welcome 구매 제한, 저장 실패 재시도, 유료 단계 진행, 주차 전환 중 구매, 지갑 거래를 통한 단계 복구.
- 화면 테스트: 공식 Fake Store 창의 구매/취소 콜백을 실행해 상점 코인·No Ads, 양쪽 주차의 유료 단계, Welcome Deal 보상과 저장을 확인.
- 로그/XML: `Logs/iap-main-edit.*`, `Logs/iap-main-play.*`, `Logs/iap-bounce-edit.*`, `Logs/iap-bounce-play2.*` (로컬 검증 산출물).
- 첫 `-nographics` 실행은 그래픽 장치가 필요한 기존 인게임 에디터 UI 테스트에서 실패하여, 그래픽 모드 배치로 재실행해 전부 통과했다.
- 모바일 실결제·복원과 기기 빌드는 이번 검증 대상이 아니다.
