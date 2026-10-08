# 메타 확장 F1: 주간 스텝 오퍼 · 데일리 리워드 · 윈 스트릭 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** sf의 엔드리스 오퍼(주간 로테이션 선형 스텝 체인), 데일리 리워드(7일 순환 + 보너스 슬롯 + 시간당 무료 코인), 윈 스트릭(연승 카운트·티어 보상·중도 이탈 회수)을 **BK.Meta의 순수 로직 + 세이브 슬롯 + 서비스**로 재구현한다. UI와 IAP는 없다(유료 스텝은 게임이 나중에 결제 성공 시 `ConfirmPaid`를 호출하는 훅만 둔다).

**Architecture:** 기존 BK.Meta 패턴 그대로. 카탈로그 인터페이스(`IStepOfferCatalog`, `IDailyRewardCatalog`, `IWinStreakCatalog`)는 게임이 공급, 시간은 `IClock`, 지급은 `IWallet.Add`(부스터도 Plain 재화로 취급), 영속은 `SaveData`. 상태 변화는 R3 `Observable`로 알린다. sf의 IAP 전용 장치(고가 티어 점프, 주간 원장, 지연 지급 intent)와 GameForge 게이트는 제외한다.

**sf에서 추출한 규칙(권위):**
- 주차: `epochUtc`(설정) 기준 `weekIndex = floor((now-epoch)/7d)`, epoch 이전은 -1. 짝수 주 = 체인(gift) 타입, 홀수 주 = 수직(offer) 타입. 한 주에 한 타입만 활성.
- 라운드 = (offerId, weekIndex). 새 주차면 그 타입 슬롯을 step 1로 리셋하고 `startedUnix = weekStart`(now 아님), autoOpened 해제. 같은 라운드 재진입은 진행 보존. 만료 `end = weekStart + 7d`. `nextStep > maxStep`이면 완료(비활성).
- 스텝은 엄격 선형: 현재 스텝만 클릭 가능. 무료 스텝: 먼저 advance 성공 → 지급. 유료 스텝: 결제 성공 후 advance(실패해도 지급은 유지). 다른 라운드의 advance는 거부.
- 가시 창: `nextStep`부터 `slotCount`개, 첫 번째만 IsCurrent, 나머지 Lock, 지난 스텝은 렌더 안 함.
- 남은 시간 표기: ≥24h `Xd Yh`, ≥1h `Xh Ym`, 그 외 `Xm Ys`.
- 데일리: 7일 순환(`% 7`), "새 날" = 로컬 달력 날짜가 바뀜(롤링 24h 아님). 결석 페널티 없음, 다중 catch-up 없음. 보너스 슬롯 3개는 같은 날짜 안에서 순서대로(인덱스 == 진행값일 때만), 날짜 바뀌면 0으로 리셋. 시간당 무료 보상은 `now >= last + 1h`. 뱃지 수 = (일일 미수령?1:0) + 남은 보너스 + (시간당 가능?1:0). sf 버그(생성자에서 received 반전)는 반영하지 않는다.
- 윈 스트릭: `Increment = min(max(s,0)+1, cap)`(cap≤0 → 10). 시도 시작 시 `attemptInProgress = true` 저장. 클리어 시 +1, 티어 보상(최고 임계치 ≤ streak)을 **덮어쓰기**로 pending에 저장, 표식 해제. 재시도/로비 이탈/강제 종료는 리셋: 표식이 켜진 채 다음 Started 또는 `Abandon()`이 오면 리셋 + pending 비움. 컨티뉴 후 승리는 유지. pending은 다음 판 시작 시 한 번만 소비(`ConsumePending`). 게이지: 티어별 등분 선형 보간, 마지막 티어 이상은 1.
- 언락 게이트: `unlockStage` 미만 스테이지 클리어는 누적 대신 리셋.

---

### Task 1: 공통 — `ItemGrant`와 `WeekClock`

**Files:** `Meta/Common/ItemGrant.cs`, `Meta/Common/WeekRotation.cs`, 테스트 `WeekRotationTests.cs`

```csharp
namespace BK.Meta
{
    /// <summary>(currency id, amount) pair granted through IWallet.</summary>
    [System.Serializable] public struct ItemGrant { public string itemId; public long amount; public ItemGrant(string id, long a) { itemId = id; amount = a; } }

    /// <summary>Week index math shared by weekly content. Epoch is a UTC instant chosen by the game.</summary>
    public sealed class WeekRotation
    {
        public static readonly System.TimeSpan Week = System.TimeSpan.FromDays(7);
        public System.DateTime EpochUtc { get; }
        public WeekRotation(System.DateTime epochUtc) => EpochUtc = System.DateTime.SpecifyKind(epochUtc, System.DateTimeKind.Utc);
        public int IndexOf(System.DateTime nowUtc) => nowUtc < EpochUtc ? -1 : (int)((nowUtc - EpochUtc).Ticks / Week.Ticks);
        public System.DateTime StartOf(int index) => EpochUtc + System.TimeSpan.FromTicks(Week.Ticks * index);
        public System.DateTime EndOf(int index) => StartOf(index) + Week;
        public static string FormatRemaining(System.TimeSpan t)
        {
            if (t < System.TimeSpan.Zero) t = System.TimeSpan.Zero;
            if (t.TotalHours >= 24) return $"{(int)t.TotalDays}d {t.Hours}h";
            if (t.TotalMinutes >= 60) return $"{(int)t.TotalHours}h {t.Minutes}m";
            return $"{(int)t.TotalMinutes}m {t.Seconds}s";
        }
    }
}
```

테스트: epoch 정확히 = 0, 7일-1틱 = 0, 7일 = 1, epoch 이전 = -1, `FormatRemaining` 3구간.

- [ ] 작성 → 테스트 → 커밋 `feat(meta): add ItemGrant and WeekRotation`

---

### Task 2: 주간 스텝 오퍼

**Files:** `Meta/Offer/StepOfferDefinition.cs`, `IStepOfferCatalog.cs`, `StepOfferData.cs`, `StepOfferCampaign.cs`, `StepOfferLogic.cs`, `StepOffers.cs`; 테스트 `StepOfferLogicTests.cs`, `StepOffersTests.cs`

```csharp
namespace BK.Meta
{
    public enum StepOfferType { None = 0, Vertical = 1, Chain = 2 }

    public sealed class StepOfferStep { public int Step; public bool IsPaid; public string ProductId; public ItemGrant[] Rewards = System.Array.Empty<ItemGrant>(); }

    /// <summary>One campaign table. One per type; the same offer returns every week it is scheduled.</summary>
    public sealed class StepOfferDefinition { public int OfferId; public StepOfferType Type; public StepOfferStep[] Steps = System.Array.Empty<StepOfferStep>(); public int MaxStep => Steps.Length; }

    public interface IStepOfferCatalog
    {
        System.DateTime EpochUtc { get; }          // week 0 start; even weeks → Chain, odd → Vertical
        int UnlockStage { get; }                   // hidden below this stage
        System.Collections.Generic.IReadOnlyList<StepOfferDefinition> Offers { get; }
    }

    [System.Serializable]
    public sealed class StepOfferData : BK.Save.SaveData
    {
        [System.Serializable] public sealed class Slot { public int type; public int offerId; public int weekIndex = -1; public int nextStep; public long startedTicks; public bool autoOpened; }
        public override int CurrentVersion => 1;
        public System.Collections.Generic.List<Slot> slots = new();
        public Slot Find(StepOfferType type) { foreach (var s in slots) if (s.type == (int)type) return s; return null; }
    }

    public readonly struct StepOfferCampaign
    {
        public readonly StepOfferDefinition Definition; public readonly int WeekIndex; public readonly int NextStep; public readonly System.DateTime EndUtc; public readonly bool AutoOpened;
        public StepOfferCampaign(StepOfferDefinition d, int week, int next, System.DateTime end, bool autoOpened) { Definition = d; WeekIndex = week; NextStep = next; EndUtc = end; AutoOpened = autoOpened; }
        public bool IsCompleted => NextStep > Definition.MaxStep;
        public StepOfferStep Current => IsCompleted ? null : Definition.Steps[NextStep - 1];
        public System.TimeSpan Remaining(System.DateTime nowUtc) => EndUtc > nowUtc ? EndUtc - nowUtc : System.TimeSpan.Zero;
    }

    public readonly struct StepOfferSlotView { public readonly StepOfferStep Step; public readonly bool IsCurrent; public readonly bool IsLocked; /* ctor */ }

    public static class StepOfferLogic
    {
        public static StepOfferType TypeOfWeek(int weekIndex) => weekIndex < 0 ? StepOfferType.None : (weekIndex % 2 == 0 ? StepOfferType.Chain : StepOfferType.Vertical);
        public static StepOfferDefinition Select(IReadOnlyList<StepOfferDefinition> all, StepOfferType type) // lowest OfferId of that type with MaxStep > 0, else null
        /// <summary>Begins or continues the round for (offer, week). Returns true when the slot was (re)started.</summary>
        public static bool BeginRound(StepOfferData.Slot slot, StepOfferDefinition d, int weekIndex, DateTime weekStartUtc)
        public static IReadOnlyList<StepOfferSlotView> VisibleWindow(StepOfferDefinition d, int nextStep, int slotCount)
    }
}
```

`StepOffers` 서비스:

```csharp
public sealed class StepOffers : IDisposable
{
    public StepOffers(IStepOfferCatalog catalog, ISaveService saves, IWallet wallet, IClock clock, IStageProgress progress)
    public bool IsUnlocked => progress.CurrentStage.CurrentValue >= catalog.UnlockStage;
    /// <summary>Evaluates "now": may begin a new round and persist. Call at event points (lobby enter, popup open, after claim), not from a timer.</summary>
    public StepOfferCampaign? GetActive();
    public IReadOnlyList<StepOfferSlotView> Window(in StepOfferCampaign c, int slotCount);
    /// <summary>Free step only: advances first, then grants rewards through the wallet. False on round mismatch, paid step, or completed.</summary>
    public bool TryClaimFree(in StepOfferCampaign c);
    /// <summary>Game calls this after a successful purchase of the current paid step. Advances only; granting is the store's job. False on mismatch.</summary>
    public bool ConfirmPaid(in StepOfferCampaign c, string productId);
    public void MarkAutoOpened(in StepOfferCampaign c);
    public Observable<Unit> ProgressChanged { get; }   // local advance (lobby button); popups should NOT close on it
}
```

`GetActive()`: week = rotation.IndexOf(now); type = TypeOfWeek; def = Select; null이면 null. slot = data.Find(type) ?? new; `BeginRound`가 (offerId, week) 불일치 또는 nextStep<=0이면 리셋(nextStep=1, startedTicks=weekStart, autoOpened=false) 후 MarkDirty. 완료/만료면 null. 캠페인 반환.
`TryClaimFree`: 재평가 후 `(offerId, weekIndex, nextStep)`이 c와 같아야 함; step.IsPaid면 false; nextStep++ → MarkDirty → 각 reward를 `wallet.Add(itemId, amount, "step_offer")` → ProgressChanged.

테스트(`StepOffersTests`: FakeClock, Wallet(Gold, Missile Plain), progress stage 50, 카탈로그 epoch=2026-01-02 07:00Z, Vertical 6스텝(3,6 유료), Chain 7스텝(4 유료)):
- `EvenWeekIsChainOddWeekIsVertical`
- `NewWeekResetsRoundAndStampsWeekStart`
- `SameRoundKeepsProgressAcrossReentry`
- `FreeClaimAdvancesAndGrants_PaidClaimRefused`
- `ConfirmPaidAdvancesWithoutGranting`
- `StaleCampaignClaimIsRejected` (다른 주차의 캠페인 값으로 Claim → false, 상태 불변)
- `CompletedAndExpiredRoundsAreInactive`
- `BeforeEpochOrLockedStageIsInactive`
- `VisibleWindowHasOneCurrent` (StepOfferLogicTests)

- [ ] 작성 → 테스트 → 커밋 `feat(meta): add weekly step offers`

---

### Task 3: 데일리 리워드

**Files:** `Meta/Daily/DailyRewardDefinition.cs`(`IDailyRewardCatalog { IReadOnlyList<ItemGrant[]> Days; IReadOnlyList<ItemGrant[]> BonusSlots; ItemGrant[] Hourly; }`), `DailyRewardsData.cs` (`nextDay, lastDailyTicks, bonusIndex, lastBonusDateTicks, lastHourlyTicks`), `DailyRewardLogic.cs`(순수: `IsNewLocalDay(lastUtcTicks, nowUtc)`, `NextDay(day, cycle)`, `HourlyReady`), `DailyRewards.cs`

```csharp
public sealed class DailyRewards : IDisposable
{
    public DailyRewards(IDailyRewardCatalog catalog, ISaveService saves, IWallet wallet, IClock clock)
    public int CycleLength => catalog.Days.Count;
    public int NextDay { get; }                 // 0-based index into Days
    public bool CanClaimDaily { get; }          // local date advanced since last claim
    public bool TryClaimDaily(out ItemGrant[] granted);
    public int BonusClaimed { get; }            // resets when the local date changes
    public bool CanClaimBonus(int index);       // index == BonusClaimed && index < BonusSlots.Count
    public bool TryClaimBonus(int index, out ItemGrant[] granted);
    public bool CanClaimHourly { get; }
    public TimeSpan TimeToHourly { get; }
    public bool TryClaimHourly(out ItemGrant[] granted);
    public int ClaimableCount { get; }          // badge
    public Observable<Unit> Changed { get; }
}
```

"로컬 날짜"는 `clock.UtcNow.ToLocalTime().Date`. 테스트는 `FakeClock`으로 자정 경계(로컬 23:59 → 다음날 00:01)를 넘겨 확인한다. 테스트: `FirstClaimIsDayZeroThenLocked`, `NextLocalDateUnlocksNextDayAndWrapsAfterCycle`, `BonusSlotsAreSequentialAndResetDaily`, `HourlyRewardNeedsOneHour`, `BadgeCountsAllClaimables`, `StatePersists`.

- [ ] 작성 → 테스트 → 커밋 `feat(meta): add daily rewards with bonus slots and hourly gift`

---

### Task 4: 윈 스트릭

**Files:** `Meta/WinStreak/WinStreakDefinition.cs` (`WinStreakTier { int Threshold; ItemGrant[] Rewards; }`, `IWinStreakCatalog { int Cap; int UnlockStage; IReadOnlyList<WinStreakTier> Tiers; }`), `WinStreakData.cs` (`streak, attemptInProgress, List<ItemGrant> pending`), `WinStreakLogic.cs`(순수), `WinStreak.cs`

```csharp
public static class WinStreakLogic
{
    public const int DefaultCap = 10;
    public static int Increment(int streak, int cap) => Math.Min(Math.Max(streak, 0) + 1, cap <= 0 ? DefaultCap : cap);
    public static WinStreakTier RewardFor(int streak, IReadOnlyList<WinStreakTier> tiers); // highest threshold <= streak, order independent; null if none
    public static float GaugeFill(int streak, IReadOnlyList<int> thresholdsAscending); // equal segments per tier, 1 at/above last
}

public sealed class WinStreak : IDisposable
{
    public WinStreak(IWinStreakCatalog catalog, ISaveService saves, IStageProgress progress)
    public ReadOnlyReactiveProperty<int> Current { get; }
    public bool AttemptInProgress { get; }
    public IReadOnlyList<ItemGrant> Pending { get; }
    /// <summary>One-shot: returns pending grants and clears them. The game applies them as the next attempt's boosters.</summary>
    public IReadOnlyList<ItemGrant> ConsumePending();
    /// <summary>Explicit quit/retry mid-attempt.</summary>
    public void Abandon();
    public float GaugeFill { get; }
}
```

구독: `progress.Started` → 표식이 이미 켜져 있었으면(재시도/강제종료 후 재진입) `Reset`; 표식 켬. `progress.Cleared` → stage < UnlockStage면 `Reset`, 아니면 `streak = Increment`, `pending = RewardFor(streak)?.Rewards ?? empty`(덮어쓰기), 표식 끔. `Abandon()` → 표식 켜져 있을 때만 리셋+pending 비움(멱등). `Reset`은 0이면 쓰기 없음.
생성 시 표식이 켜져 있으면(강제 종료 후 부팅) 즉시 리셋한다(sf AbandonGuard).

테스트: `IncrementClampsAtCapAndNegativeIsZero`, `RewardForPicksHighestThresholdUnsorted`, `GaugeFillLandsOnTierLines`, `WinsAccumulateAndSetPendingOverwrite`, `RetryViaStartedWhileInProgressResets`, `AbandonIsIdempotent`, `ForceQuitMarkerResetsOnConstruct`, `ClearBelowUnlockStageResets`, `ConsumePendingIsOneShot`.

- [ ] 작성 → 테스트 → 커밋 `feat(meta): add win streak with tier rewards and abandon guard`

---

### Task 5: 설치 + 문서

- `MetaInstaller.Install(...)`에 선택 인자 추가: `IStepOfferCatalog stepOffers = null, IDailyRewardCatalog daily = null, IWinStreakCatalog winStreak = null` → null이 아닐 때만 등록(각각 `StepOffers`, `DailyRewards`, `WinStreak` 싱글턴).
- CLAUDE.md Meta 줄에 세 시스템 추가; 로드맵 F 행을 "F1 완료, F2 로컬 푸시 별도 계획".
- 전체 EditMode 실행.

- [ ] 커밋 `docs: record meta extensions`
