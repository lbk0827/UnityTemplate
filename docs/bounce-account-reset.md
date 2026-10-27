# Unity Editor 신규 계정 초기화

메뉴: **BK > Integration > Reset All Bounce Game Data (New Account)**

확인창에서 `백업 후 초기화`를 누르면 실행 중인 Play를 종료한 뒤 저장 데이터를 이동한다. 종료 시 저장이 끝난 다음 처리하므로 이전 메모리 상태가 새 프로필에 다시 저장되지 않는다. 도메인 재로드를 비활성화한 Editor에서도 Fake Store 등의 정적 캐시가 남지 않도록 스크립트를 다시 로드한다.

다음 실행은 **BK > Integration > Open Bounce Bootstrap → Play**로 시작한다. 1레벨, 초기 재화·하트·부스터, 기본 프로필·옵션, 미확인 가이드, 미구매 상태, 초기 연승·오퍼 상태가 새로 구성된다.

초기화 범위:

- `Application.persistentDataPath/Bounce/Profiles/local` 전체: 진행도, 재화, 부스터, NoAds 및 구매 원장·미완료 구매, 프로필, 가이드, 옵션, 오퍼, 연승, 알림 설정 등 모든 저장 슬롯. `.bak`·임시·복구 파일과 이후 추가되는 슬롯도 포함한다.
- 같은 앱의 `Unity/UnityPurchasing`: Unity IAP 로컬 거래 중복 처리 캐시.
- 광고 쿨다운과 진행 중인 보상 등의 메모리 상태는 Play 종료 및 스크립트 재로드로 초기화한다. 현재 게임은 계정 서버 없이 로컬 저장을 사용한다.

백업은 `Application.persistentDataPath/Bounce/EditorBackups/<UTC시간-GUID>/`에 남으며 Console에 경로가 표시된다. 원복하려면 Play를 종료한 상태에서 백업의 `restore.txt`를 따른다. 현재 새 계정 데이터는 먼저 따로 보관한다.

다른 프로젝트·다른 프로필은 초기화하지 않는다. 프로젝트에서 게임 저장에 사용하지 않는 PlayerPrefs 전체를 지우지 않는다. 이 기능은 실제 스토어 구매 취소나 OS 알림 권한 초기화 기능이 아니다. 모바일 플레이어에는 포함되지 않는다.

검증은 임시 폴더에서만 실행한다. 사용 중인 실제 프로필은 테스트가 초기화하지 않는다.
