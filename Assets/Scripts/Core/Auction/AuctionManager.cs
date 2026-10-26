using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;

namespace DesertSettlement.Core.Auction
{
    public enum AuctionKind
    {
        Regular,
        Emergency,
    }

    /// <summary>입찰자 개별 제안</summary>
    public sealed class AuctionBid
    {
        public BidderProfile Bidder { get; internal set; }
        public ValuationResult Valuation { get; internal set; }
        /// <summary>입찰 총액 = 입찰자 관점 가치 × 당일 시세 × (1 − 긴급 감액).</summary>
        public double TotalOffer { get; internal set; }
        /// <summary>현금 지급액 = 총액 × 현금 비율.</summary>
        public double Cash { get; internal set; }
        /// <summary>다음 정착지 시작 식량 추가분.</summary>
        public double NextSettlementFoodBonus { get; internal set; }
        /// <summary>다음 정착지 무료 완공 배치(종류 → 개수). 비어 있을 수 있다.</summary>
        public IReadOnlyDictionary<string, int> NextSettlementFreeBuilds { get; internal set; } = new Dictionary<string, int>();
    }

    /// <summary>열린 경매 한 번.</summary>
    public sealed class AuctionEvent
    {
        public AuctionKind Kind { get; internal set; }
        /// <summary>이 시간이 시작될 때 열림(직전 24시간 실적으로 평가).</summary>
        public long Hour { get; internal set; }
        /// <summary>당일 시세(경매 당일 공개).</summary>
        public double Market { get; internal set; }
        public double Penalty { get; internal set; }
        public IReadOnlyList<AuctionBid> Bids { get; internal set; }
    }

    /// <summary>매각 결과. 캠페인이 받아서 다음 정착지로 넘어간다.</summary>
    public sealed class SaleResult
    {
        public AuctionEvent Auction { get; internal set; }
        public AuctionBid Bid { get; internal set; }
        /// <summary>매각 시점 정착지 자금(음수일 수 있음). 매각 대금과 합쳐 캠페인 자금이 된다.</summary>
        public double SettlementCash { get; internal set; }
    }

    public enum AuctionPhase
    {
        /// <summary>경매장이 아직 없음(자금 0이면 즉시 파산).</summary>
        NoAuctionHall,
        /// <summary>다음 정기 경매 대기 중(참가 입찰자 예고됨).</summary>
        Scheduled,
        /// <summary>경매가 열려 플레이어의 매각/보류 선택을 기다림. 게임 시간은 멈춰야 한다.</summary>
        Open,
        Sold,
        Bankrupt,
    }

    /// <summary>
    /// 정착지 하나의 경매 진행(4단계 확정 규칙).
    /// - 첫 경매장 완공 → 첫 경매 = 완공 + auctionRepeatsGameHours(2026-10-26 AU1: 48시간, 2026-10-23 72, 원래 96), 이후 같은 간격마다. 완공 직후와 보류 직후에 다음 경매 참가 입찰자 예고.
    /// - 입찰자 수 = 경매장 1개일 때 attendeesWithOneAuctionHall(2명) + 가동 중인 경매장이 하나 늘 때마다 1명(2026-10-26 AH1, attendeesMin~attendeesMax = 1~4로 제한).
    ///   예고 때 입찰자 순서를 섞어 두고 앞에서부터
    ///   경매장 수만큼 온다: 경매장을 더 지으면 다음 사람이 추가되고, 철거·폐쇄하면 뒤에서부터 빠지며, 다시 열면 같은 사람이 돌아온다.
    /// - 경매 당일 시세(±10%) 공개, 입찰자별 가치·조건(현금 비율, 다음 정착지 식량)으로 입찰.
    /// - 자금 0 이하: 경매장과 광산(공사 중 포함)이 모두 있으면 긴급 매각 경매(−20%), 하나라도 없으면 즉시 파산.
    ///   긴급 매각을 보류하고 자금 &lt; 0이 24시간 이어지면 파산. 자금이 다시 0보다 커지면 해제.
    /// 정착지의 정각 정산 직후(<see cref="Settlement.HourSettled"/>)에 판정한다. 무작위는 시드로 재현 가능.
    /// </summary>
    public sealed class AuctionManager
    {
        private readonly Settlement _settlement;
        private readonly AuctionSettings _settings;
        private readonly Random _rng;
        /// <summary>예고 때 섞은 입찰자 순서(전체). 앞에서부터 <see cref="AttendeeCount"/>명이 온다.</summary>
        private readonly List<BidderProfile> _order = new List<BidderProfile>();
        private bool _emergencyDeclined;

        public AuctionPhase Phase { get; private set; } = AuctionPhase.NoAuctionHall;
        public long? NextAuctionHour { get; private set; }
        /// <summary>다음 정기 경매에 올 입찰자(예고). 가동 중인 경매장 수에 따라 바로 늘고 준다(AH1).</summary>
        public IReadOnlyList<BidderProfile> AnnouncedBidders => _order.Take(AttendeeCount).ToList();

        /// <summary>가동 중인 경매장 수.</summary>
        public int OperatingAuctionHalls => _settlement.OperatingOf(BuildingKinds.AuctionHall).Count();

        /// <summary>
        /// 경매에 오는 입찰자 수 = attendeesWithOneAuctionHall + (가동 중인 경매장 수 − 1)을 attendeesMin~attendeesMax(설정, 1~4)로 제한한 값
        /// (2026-10-26 AH1: 경매장 1·2·3개 = 2·3·4명). 경매장을 모두 닫았어도 예정된 경매에는 최소 attendeesMin명이 온다.
        /// </summary>
        public int AttendeeCount
        {
            get
            {
                int max = Math.Min(_settings.AttendeesMax, _settings.Bidders.Count);
                int min = Math.Min(_settings.AttendeesMin, max);
                return Math.Max(min, Math.Min(max, _settings.AttendeesWithOneAuctionHall + OperatingAuctionHalls - 1));
            }
        }
        public AuctionEvent CurrentAuction { get; private set; }
        /// <summary>자금이 음수가 된 시각(파산 카운트다운 시작). 아니면 null.</summary>
        public long? NegativeCashSinceHour { get; private set; }
        public string BankruptcyReason { get; private set; }
        public int AuctionsHeld { get; private set; }
        /// <summary>플레이어가 보류한 횟수. 보류할 때마다 다음 경매 시세가 HoldMarketPenalty만큼 줄어든다(2026-10-23 게임성 보강, Docs/decision_log_auto.md F1).</summary>
        public int Holds { get; private set; }
        /// <summary>지금까지의 보류로 줄어든 시세 배율(보류 0번 = 1).</summary>
        public double HoldFactor => Math.Pow(1 - Math.Max(0, Math.Min(1, _settings.HoldMarketPenalty)), Holds);

        /// <summary>파산까지 남은 시간(카운트다운 중이 아니면 null).</summary>
        public long? HoursUntilBankruptcy => NegativeCashSinceHour.HasValue
            ? Math.Max(0, NegativeCashSinceHour.Value + _settings.BankruptcyGraceHours - _settlement.CurrentHour)
            : (long?)null;

        public event Action<IReadOnlyList<BidderProfile>> BiddersAnnounced;
        public event Action<AuctionEvent> AuctionOpened;
        public event Action<SaleResult> Sold;
        /// <summary>플레이어가 경매를 보류했을 때(저장 파일 명령 기록용, 2026-10-23).</summary>
        public event Action Held;
        public event Action<string> Bankrupted;

        public AuctionManager(Settlement settlement, int seed)
        {
            _settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            _settings = settlement.Rules.Config.Auction ?? throw new InvalidOperationException("설정에 auction이 없습니다.");
            _rng = new Random(seed);
            settlement.FacilityCompleted += OnFacilityCompleted;
            settlement.HourSettled += OnHourSettled;
        }

        public bool IsOpen => Phase == AuctionPhase.Open;
        public bool IsFinished => Phase == AuctionPhase.Sold || Phase == AuctionPhase.Bankrupt;

        private void OnFacilityCompleted(Facility f)
        {
            if (f.Kind != BuildingKinds.AuctionHall || IsFinished) return;
            if (NextAuctionHour.HasValue)
            {
                // 두 번째 경매장부터: 예고에 한 명 추가(일정은 첫 경매장 기준 그대로, AH1).
                BiddersAnnounced?.Invoke(AnnouncedBidders);
                return;
            }
            NextAuctionHour = _settlement.AuctionHallCompletedHour + _settlement.Rules.Config.ConfirmedRules.AuctionRepeatsGameHours;
            if (Phase == AuctionPhase.NoAuctionHall) Phase = AuctionPhase.Scheduled;
            Announce();
        }

        private void OnHourSettled(HourRecord record)
        {
            if (IsFinished || IsOpen) return;
            long now = _settlement.CurrentHour; // 방금 정산한 시간 + 1
            double cash = _settlement.Cash;
            bool hasHall = _settlement.OperatingOf(BuildingKinds.AuctionHall).Any() || NextAuctionHour.HasValue;
            // 광산은 공사 중이어도 '있음'(개발 중 자산 가치가 있으므로). 철거 중은 없음.
            bool hasMine = _settlement.Facilities.Any(f => f.Kind == BuildingKinds.Mine && f.State != FacilityState.Demolishing);

            if (cash <= 0 && (!hasHall || !hasMine))
            {
                GoBankrupt(!hasHall ? "경매장 없이 자금이 바닥났습니다." : "광산 없이 자금이 바닥났습니다(팔 가치가 없음).");
                return;
            }

            // 파산 카운트다운.
            if (cash < 0)
            {
                if (!NegativeCashSinceHour.HasValue) NegativeCashSinceHour = now;
                if (now - NegativeCashSinceHour.Value >= _settings.BankruptcyGraceHours)
                {
                    GoBankrupt($"자금이 {_settings.BankruptcyGraceHours}시간 동안 음수였습니다.");
                    return;
                }
            }
            else
            {
                NegativeCashSinceHour = null;
            }
            if (cash > 0) _emergencyDeclined = false;

            if (NextAuctionHour.HasValue && now >= NextAuctionHour.Value)
            {
                Open(AuctionKind.Regular, now, AnnouncedBidders.ToList());
                return;
            }
            if (cash <= 0 && !_emergencyDeclined)
            {
                if (_order.Count == 0) Shuffle();
                Open(AuctionKind.Emergency, now, AnnouncedBidders.ToList());
            }
        }

        private void Open(AuctionKind kind, long hour, List<BidderProfile> bidders)
        {
            double market = _settings.MarketCenter * (1 + _settings.MarketSwing * (2 * _rng.NextDouble() - 1)) * HoldFactor;
            double penalty = kind == AuctionKind.Emergency ? _settings.EmergencyPenalty : 0;
            var bids = new List<AuctionBid>();
            foreach (BidderProfile b in bidders)
            {
                ValuationResult v = SettlementValuation.EvaluateFor(_settlement, hour, b);
                double total = v.Value * market * (1 - penalty);
                bids.Add(new AuctionBid
                {
                    Bidder = b,
                    Valuation = v,
                    TotalOffer = total,
                    Cash = total * b.CashShare,
                    NextSettlementFoodBonus = b.NextSettlementFoodBonus,
                    NextSettlementFreeBuilds = b.NextSettlementFreeBuilds != null
                        ? new Dictionary<string, int>(b.NextSettlementFreeBuilds)
                        : new Dictionary<string, int>(),
                });
            }
            CurrentAuction = new AuctionEvent { Kind = kind, Hour = hour, Market = market, Penalty = penalty, Bids = bids };
            Phase = AuctionPhase.Open;
            AuctionsHeld++;
            AuctionOpened?.Invoke(CurrentAuction);
        }

        /// <summary>입찰 하나를 받아 매각한다. 정착지는 끝나고, 캠페인이 결과를 받아 다음 정착지로 간다.</summary>
        public SaleResult Sell(int bidIndex)
        {
            if (!IsOpen) throw new InvalidOperationException("열린 경매가 없습니다.");
            if (bidIndex < 0 || bidIndex >= CurrentAuction.Bids.Count) throw new ArgumentOutOfRangeException(nameof(bidIndex));
            var result = new SaleResult { Auction = CurrentAuction, Bid = CurrentAuction.Bids[bidIndex], SettlementCash = _settlement.Cash };
            Phase = AuctionPhase.Sold;
            Sold?.Invoke(result);
            return result;
        }

        /// <summary>
        /// 보류. 정기 경매면 다음 경매(+auctionRepeatsGameHours, 48시간)를 잡고 새 참가자를 예고한다.
        /// 긴급 매각이면 자금이 다시 0보다 커질 때까지 긴급 경매를 다시 열지 않는다(파산 카운트다운은 계속).
        /// </summary>
        public void Hold()
        {
            if (!IsOpen) throw new InvalidOperationException("열린 경매가 없습니다.");
            Held?.Invoke();
            Holds++;
            AuctionEvent auction = CurrentAuction;
            CurrentAuction = null;
            Phase = AuctionPhase.Scheduled;
            if (auction.Kind == AuctionKind.Regular)
            {
                NextAuctionHour = auction.Hour + _settlement.Rules.Config.ConfirmedRules.AuctionRepeatsGameHours;
                Announce();
            }
            else
            {
                _emergencyDeclined = true;
            }
            if (_settlement.Cash <= 0) _emergencyDeclined = true; // 정기 경매를 보류해도 같은 위기에서 긴급 경매가 바로 다시 열리지 않게
        }

        private void Announce()
        {
            Shuffle();
            BiddersAnnounced?.Invoke(AnnouncedBidders);
        }

        /// <summary>입찰자 순서를 새로 섞는다(결정적 Fisher–Yates). 예고(첫 경매장 완공·보류) 때만.</summary>
        private void Shuffle()
        {
            _order.Clear();
            _order.AddRange(_settings.Bidders);
            for (int i = _order.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
        }

        private void GoBankrupt(string reason)
        {
            Phase = AuctionPhase.Bankrupt;
            BankruptcyReason = reason;
            CurrentAuction = null;
            Bankrupted?.Invoke(reason);
        }
    }
}
