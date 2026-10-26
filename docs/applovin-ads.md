# AppLovin MAX 광고 연결

## GRP1_Common 조사 결과와 적용 범위

원본 프로젝트: `D:/grp1_common`.

| GRP1 구현 | UnityTemplate 적용 |
|---|---|
| `Assets/RootBox/Runtime/Scripts/SDK/AppLovinMax/AppLovinMaxService.cs` | `BK.Ads.AppLovinAdProvider`: 초기화 완료 후 선로드, 메인 스레드 콜백, 이벤트 해제 |
| 같은 폴더 `AdUnit.cs` | 전면/보상형의 준비 상태, 닫힘/보상/실패 구분, 로드 재시도, 중복 요청 차단 |
| `Assets/RootBox/Runtime/Scripts/Project/Manager/AdManager.cs` | `AdService`: 광고 제거와 선택형 보상 광고 분리, 배너 숨김 |
| `Marketing/InterstitialGateEvaluator.cs`, 플랫폼 광고 정책 가이드 | 로컬 시작 스테이지와 90초 쿨다운. 광고 표시 콜백이 타이머를 갱신 |
| `Guide_StageClearPreAdCredit_CrossProject.md` | 일반 클리어 보상은 광고 전에 저장; 광고 실패로 기본 보상을 잃지 않음 |

RootBox, UniRx, DUG, Firebase, AppsFlyer, GameForge, SRDebugger를 복사하지 않고 BKFramework/UniTask에 맞춰 연결한다. GRP1 계정 키·광고 ID·개인 테스트 기기 ID도 복사하지 않는다. 캠페인 분류·리모트 광고 정책·매출 분석 전송은 이번 범위에 포함하지 않는다.

## 패키지

- AppLovin MAX `com.applovin.mediation.ads` **8.5.1**: GRP1과 동일.
- External Dependency Manager **1.2.190**: Unity 6 Windows에 iOS Build Support가 없을 때 구버전 1.2.182의 iOS Resolver 참조 오류를 피하도록 직접 고정. 패키지 캐시를 수정하지 않는다.
- 공식 MAX 레지스트리 `https://unity.packages.applovin.com`, EDM4U는 OpenUPM.
- MAX 기본 네트워크만 설치한다. AdMob/Meta/UnityAds 등 추가 미디에이션 어댑터는 해당 앱의 네트워크 구성에 맞춰 AppLovin Integration Manager에서 설치한다.

## 공용 API와 에디터 테스트

`BK/Ads/AppLovin Settings`로 `AdSettings`를 만들고 `AdService(new AppLovinAdProvider(settings), settings, clock, adsRemoved)`를 연결한다. 스코프 종료 시 `Dispose()`한다.

- `Initialize()`는 게임 부팅을 기다리게 하지 않는다. 준비 전 요청은 `NotReady`로 반환한다.
- `ShowAsync(Rewarded, stage, placement)`는 `Rewarded` 결과에만 게임 보상을 지급한다. 그냥 닫기, 실패, 중복 요청은 지급하지 않는다.
- 전면은 광고 제거 여부·시작 스테이지·쿨다운을 확인한다. 보상형 광고 시청도 다음 전면까지의 쿨다운을 갱신한다.
- 한 번에 전체 화면 광고 하나만 표시한다. 300초 동안 종료 콜백이 없으면 기다리는 호출자에게 타임아웃을 반환하되, 늦은 콜백이 다음 광고의 보상이 되지 않도록 기존 광고 종료 전 새 요청을 차단한다.
- `SetBannerVisible()`은 광고 제거 및 전체 화면 광고 여부를 함께 반영한다.
- `simulateInEditor=true`이면 MAX의 **공식 에디터 스텁 광고**를 사용한다. `bk_editor_*`는 이 스텁만을 위한 이름이며 모바일 ID가 아니다.
- 보상형 스텁에서 **Grant Reward**를 누른 뒤 닫으면 보상 성공, 그냥 닫으면 취소다. 실제 광고/수익은 발생하지 않는다.

## 모바일 연결

1. AppLovin > Integration Manager에서 해당 계정의 SDK Key를 입력한다.
2. 게임 `AdSettings`의 Android/iOS Rewarded, Interstitial, Banner에 **해당 앱에서 발급한** Ad Unit ID를 입력한다. 사용하지 않는 유형은 빈 값으로 둘 수 있다.
3. 필요한 네트워크 어댑터, AdMob App ID, MAX의 CMP/동의 흐름 및 개인정보처리방침 URL을 앱에 맞게 구성한다. 이 코드는 GDPR 동의를 임의로 승인하거나 ATT 거부를 다른 동의값으로 변환하지 않는다.
4. 테스트 기기 광고 ID를 설정하고 `enableMobileAds`를 켠다. 에디터 시뮬레이션과 실제 기기 테스트 모드는 별개다.
5. Android/iOS Build Support와 각 플랫폼 빌드 도구를 설치하고 의존성을 Resolve한다. 실제 기기에서 MAX Mediation Debugger와 테스트 광고로 초기화·노출·취소·보상·백그라운드 복귀를 확인한다.

키/ID가 없는 기본 상태에서는 모바일 SDK 초기화를 시작하지 않는다. 기기 광고 노출과 매출은 에디터 스텁 테스트만으로 검증됐다고 간주하지 않는다.

공식 참고: [MAX Unity 통합](https://support.axon.ai/en/max/unity/overview/integration/), [공식 Unity 플러그인](https://github.com/AppLovin/AppLovin-MAX-Unity-Plugin), [EDM4U](https://github.com/googlesamples/unity-jar-resolver).
