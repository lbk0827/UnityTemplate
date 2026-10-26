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
