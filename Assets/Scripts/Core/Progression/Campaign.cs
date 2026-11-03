using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;

namespace DesertSettlement.Core.Progression
{
    public enum CampaignState
    {
        /// <summary>정착지를 운영 중.</summary>
        InSettlement,
        /// <summary>매각 후 다음 행선지를 고르는 중(다음 정착지 진입 전).</summary>
        ChoosingDestination,
        Bankrupt,
    }

    /// <summary>지나온 정착지의 매각 기록.</summary>
    public sealed class SaleRecord
    {
        public int SettlementNumber;
        public string MapId;
        public long Hour;
        public string BidderName;
        public bool Emergency;
        public double SaleCash;
        /// <summary>매각 순간 남은 재고를 일괄 매도한 금액(2026-10-27 이슈 #18, 매각 대금과 별도, 자금에 포함).</summary>
        public double StockSaleCash;
        public double FoodBonus;
        /// <summary>다음 정착지 무료 완공 배치(농장주 혜택 등).</summary>
        public Dictionary<string, int> FreeBuilds = new Dictionary<string, int>();
        /// <summary>받은 영구 모디파이어(결정 BM1). 없으면 null.</summary>
        public BidderModifierDefinition Modifier;
        public double CampaignCashAfter;
    }

    /// <summary>
    /// 정착지를 이어 가는 캠페인(무한 개척 루프).
    /// 정착지 진입 → 운영 → 경매(매각/보류) → 매각 시 대금 100% + 그 입찰자의 영구 모디파이어 → 행선지 선택 → 다음 정착지.
    /// - 자금과 영구 모디파이어(결정 BM1, 곱연산으로 쌓임)만 이어지고, 식량·석재·구매 단가는 정착지마다 새로 시작한다(확정).
    /// - 입찰자 혜택(다음 정착지 시작 식량, 무료 완공 배치 — 농장주는 완공된 농장 1개)은 다음 정착지 한 번에만 적용.
    /// - 다음 맵 선택은 <see cref="ChooseNextMap"/> 한곳에 모았다. 절차 맵 설정이 있으면 처음 FixedMapsBeforeGenerated개는 고정 맵,
    ///   그 뒤로는 정착지에 들어가는 순간의 보유 자금으로 난이도 D를 정해 맵을 생성한다(보유 자금 비례 난이도, 2026-10-23 확정).
    /// </summary>
    public sealed class Campaign
    {
        private readonly EconomyConfig _config;
        private readonly IReadOnlyList<MapDefinition> _maps;
        private readonly int _seed;
        private readonly ProceduralMapConfig _procedural;
        private readonly List<BidderModifierDefinition> _modifiers = new List<BidderModifierDefinition>();
        private readonly List<SaleRecord> _sales = new List<SaleRecord>();
        private double _pendingFoodBonus;
        private readonly Dictionary<string, int> _pendingFreeBuilds = new Dictionary<string, int>();

        public CampaignState State { get; private set; }
        /// <summary>정착지 밖의 자금(행선지 선택·다음 정착지 시작 자금). 정착지 안에서는 Current.Cash를 본다.</summary>
        public double Cash { get; private set; }
        /// <summary>1부터.</summary>
        public int SettlementNumber { get; private set; }

        // ── 이번 캠페인 기록(2026-10-24 재미 보강 RC1): 행선지·파산 화면에 보이고, Runtime이 최고 기록과 비교해 저장한다. ──
        /// <summary>매각 뒤 가장 많았던 보유 자금(매각 전이면 지금 자금).</summary>
        public double PeakCash => _sales.Count == 0 ? Cash : Math.Max(Cash, _sales.Max(s => s.CampaignCashAfter));
        /// <summary>지금까지 매각 대금 합.</summary>
        public double TotalSaleCash => _sales.Sum(s => s.SaleCash);
        public MapDefinition CurrentMap { get; private set; }
        /// <summary>지금 정착지가 생성 맵이면 생성 정보(난이도 등), 고정 맵이면 null.</summary>
        public GeneratedMap CurrentGenerated { get; private set; }
        public bool UsesProceduralMaps => _procedural != null;
        /// <summary>지금 정착지의 지역 모디파이어(고정 맵이면 없음).</summary>
        public IReadOnlyList<RegionModifierDefinition> CurrentModifiers { get; private set; } = new List<RegionModifierDefinition>();
        public Settlement Current { get; private set; }
        public AuctionManager Auctions { get; private set; }
        /// <summary>지금 정착지의 재해(설정에 재해가 없으면 null).</summary>
        public DisasterManager Disasters { get; private set; }
        public IReadOnlyList<SaleRecord> Sales => _sales;
        /// <summary>지금까지 받은 영구 모디파이어(받은 순서, 같은 것이 여러 번 있을 수 있음).</summary>
        public IReadOnlyList<BidderModifierDefinition> Modifiers => _modifiers;
        /// <summary>받은 모디파이어를 모두 곱한 효과(다음 정착지에 걸린다).</summary>
        public EconomyEffects ModifierEffects => EconomyEffects.Combine(_modifiers.Select(m => m.Effects));
        public string BankruptcyReason { get; private set; }

        public event Action SettlementStarted;
        public event Action<SaleRecord> SettlementSold;
        public event Action<string> WentBankrupt;

        /// <param name="maps">고정 맵. 절차 맵을 쓰면 처음 FixedMapsBeforeGenerated개 정착지에만 순서대로 쓰인다(생성 실패 시 대체용으로도 쓰인다).</param>
        /// <param name="procedural">null이면 고정 맵만 순환한다.</param>
        public Campaign(EconomyConfig config, IReadOnlyList<MapDefinition> maps, int seed = 20261022, ProceduralMapConfig procedural = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (maps == null || maps.Count == 0) throw new ArgumentException("맵이 하나 이상 필요합니다.", nameof(maps));
            _maps = maps;
            _seed = seed;
            _procedural = procedural;
            Cash = config.Prototype.StartingCash;
        }

        /// <summary>
        /// 다음 정착지에 들어간다(처음 시작 포함). 행선지 선택 상태에서만, 또는 첫 정착지.
        /// offerIndex = <see cref="NextOffers"/> 중 고른 후보(고정 맵 차례면 0뿐).
        /// 받은 영구 모디파이어(<see cref="Modifiers"/>)와 고른 지역의 모디파이어 효과가 이 정착지 규칙에 걸린다(모두 곱연산).
        /// 시작 자금은 보유 자금 × 시작 자금 모디파이어.
        /// </summary>
        public Settlement StartNextSettlement(int offerIndex = 0)
        {
            if (State == CampaignState.Bankrupt) throw new InvalidOperationException("파산한 캠페인입니다.");
            if (Current != null && State != CampaignState.ChoosingDestination) throw new InvalidOperationException("매각 후에만 다음 정착지로 갈 수 있습니다.");
            IReadOnlyList<RegionOffer> offers = NextOffers();
            if (offerIndex < 0 || offerIndex >= offers.Count) throw new ArgumentOutOfRangeException(nameof(offerIndex), "없는 지역 후보입니다.");
            RegionOffer offer = offers[offerIndex];
            _log.Add(GameCommand.ForStart(SettlementNumber + 1, offerIndex));

            SettlementNumber++;
            CurrentMap = MapFor(offer);
            CurrentModifiers = CurrentGenerated != null ? offer.Modifiers : new List<RegionModifierDefinition>();
            SettlementRules rules = SettlementRules.FromConfig(_config);
            EconomyEffects owned = ModifierEffects;
            rules.StartingCash = Cash * owned.StartingCashMultiplier;
            rules.StarterFood += _pendingFoodBonus;
            rules.ApplyEffects(owned);
            foreach (RegionModifierDefinition m in CurrentModifiers) rules.ApplyEffects(m.Effects);
            rules.UpkeepMultiplier *= DifficultyUpkeepFactor(CurrentGenerated?.Params.D);
            foreach (KeyValuePair<string, int> kv in _pendingFreeBuilds) rules.FreeBuilds[kv.Key] = kv.Value;
            _pendingFoodBonus = 0;
            _pendingFreeBuilds.Clear();

            Current = new Settlement(CurrentMap.CreateMap(), rules);
            Auctions = new AuctionManager(Current, SeedSalts.Auction(_seed, SettlementNumber));
            // 후반 가속(LD3, 이슈 #12): 생성 맵은 D가 LateStartD를 넘은 만큼 재해가 세진다.
            double disasterIntensity = CurrentGenerated != null ? 1 + _procedural.Difficulty.DisasterLatePerD * _procedural.Difficulty.LateExcess(CurrentGenerated.Params.D) : 1;
            Disasters = DisasterManager.Attach(Current, _config.Disasters, SeedSalts.Disasters(_seed, SettlementNumber), CurrentModifiers.Select(m => m.Id), disasterIntensity);
            // 저장 파일 명령 기록(2026-10-23 로드맵 2-①): 정착지 명령·경매 결정을 모은다.
            Current.CommandRecorded += Log;
            Settlement recorded = Current;
            Auctions.Held += () => Log(new GameCommand { Type = GameCommand.Hold, Hour = recorded.CurrentHour });
            Auctions.Sold += sale => Log(GameCommand.ForSell(recorded.CurrentHour, IndexOfBid(sale)));
            Auctions.Sold += OnSold;
            Auctions.Bankrupted += OnBankrupt;
            State = CampaignState.InSettlement;
            SettlementStarted?.Invoke();
            return Current;
        }

        /// <summary>
        /// 난이도 비례 운영비 배율(후보, 기본 0이면 1): 생성 맵에서 D가 높을수록 운영비가 비싸진다.
        /// UpkeepMinD(기본 0): D가 낮아도 이 D만큼은 운영비를 올린다(고정 맵은 D 0으로 본다, null). 초반 긴장 후보(2026-10-24 재미 보강 측정).
        /// </summary>
        private double DifficultyUpkeepFactor(double? generatedD)
        {
            if (_procedural == null || _procedural.Difficulty.UpkeepPerD <= 0 || (generatedD == null && _procedural.Difficulty.UpkeepMinD <= 0)) return 1;
            DifficultyCurve curve = _procedural.Difficulty;
            // 후반 가속(LD1, 이슈 #12): D가 LateStartD를 넘은 만큼의 제곱항.
            double late = generatedD.HasValue ? curve.LateExcess(generatedD.Value) : 0;
            return 1 + curve.UpkeepPerD * Math.Max(generatedD ?? 0, curve.UpkeepMinD) + curve.UpkeepLatePerD2 * late * late;
        }

        /// <summary>
        /// 이 후보에 들어가면 걸릴 운영비 배율(위쪽 띠 '운영비 ×'와 같은 값, 2026-10-27 이슈 #6 행선지 창).
        /// 받은 모디파이어 × 지역 모디파이어 × 난이도 비례. 지형 생성이 실패해 고정 맵으로 대신하는 드문 경우는 반영하지 않는다.
        /// </summary>
        public double PreviewUpkeepMultiplier(RegionOffer offer)
        {
            double m = ModifierEffects.UpkeepMultiplier;
            if (!offer.IsFixedMap) m *= offer.Effects.UpkeepMultiplier;
            return m * DifficultyUpkeepFactor(offer.IsFixedMap ? (double?)null : offer.Params.D);
        }

        /// <summary>후보 지역의 맵. 생성 차례면 후보 시드로 만들고, 실패하면(규칙을 못 맞춤) 고정 맵으로 대신한다.</summary>
        private MapDefinition MapFor(RegionOffer offer)
        {
            CurrentGenerated = null;
            if (offer.IsFixedMap) return offer.FixedMap;
            GeneratedMap g = ProceduralMapGenerator.Generate(offer.Params, _procedural.Generator, offer.Seed);
            if (g == null) return _maps[(SettlementNumber - 1) % _maps.Count];
            CurrentGenerated = g;
            return g.Map;
        }

        /// <summary>다음에 들어갈 정착지 번호(행선지 선택·첫 진입 전에는 +1, 정착지 안에서는 지금 번호).</summary>
        private int NextSettlementNumber => State == CampaignState.ChoosingDestination || Current == null ? SettlementNumber + 1 : SettlementNumber;

        /// <summary>
        /// 다음 지역 후보(2026-10-23 확정: 2곳). 난이도는 지금 보유 자금 + 지나온 생성 맵 정착지 수로 정한다.
        /// 후보끼리는 시드와 모디파이어가 다르고(두 번째 후보는 첫 후보의 약점을 가능하면 피함), 고정 맵 차례면 고정 맵 하나.
        /// 같은 캠페인·같은 자금이면 같은 후보가 나온다. 지형은 들어갈 때 만든다(여기서는 만들지 않아 가볍다).
        /// </summary>
        public IReadOnlyList<RegionOffer> NextOffers()
        {
            int next = NextSettlementNumber;
            if (_procedural == null || next <= _procedural.FixedMapsBeforeGenerated)
                return new[] { new RegionOffer { Index = 0, FixedMap = _maps[(next - 1) % _maps.Count] } };

            int generatedBefore = next - 1 - _procedural.FixedMapsBeforeGenerated;
            DifficultyParams p = DifficultyParams.For(DifficultyParams.DifficultyFromProgress(Cash, generatedBefore, _procedural.Difficulty), _procedural.Difficulty);
            var offers = new List<RegionOffer>();
            var taken = new HashSet<string>();
            for (int i = 0; i < Math.Max(1, _procedural.RegionChoices); i++)
            {
                int seed = SeedSalts.RegionOffer(SeedSalts.Map(_seed, next), i);
                List<RegionModifierDefinition> mods = ScaleWeaknesses(_procedural.Modifiers.Roll(p.D, seed, taken), p.D);
                foreach (RegionModifierDefinition m in mods.Where(m => m.Kind == RegionModifierKind.Weakness)) taken.Add(m.Id);
                offers.Add(new RegionOffer { Index = i, Seed = seed, Params = p, Modifiers = mods });
            }
            return offers;
        }

        /// <summary>
        /// 후반 가속(LD2, 이슈 #12): D가 LateStartD를 넘으면 약점 효과를 1 + WeaknessLatePerD × (D − LateStartD)배로(강점은 그대로).
        /// 후보 단계에서 바꿔 두므로 행선지 카드·운영비 미리보기·실제 규칙이 같은 값을 쓴다.
        /// </summary>
        private List<RegionModifierDefinition> ScaleWeaknesses(List<RegionModifierDefinition> mods, double d)
        {
            double s = 1 + _procedural.Difficulty.WeaknessLatePerD * _procedural.Difficulty.LateExcess(d);
            if (s <= 1 + 1e-9) return mods;
            return mods.Select(m => m.Kind != RegionModifierKind.Weakness ? m : new RegionModifierDefinition
            {
                Id = m.Id, Name = m.Name, Kind = m.Kind, Description = m.Description, Effects = m.Effects.ScaledWeakness(s),
            }).ToList();
        }

        /// <summary>다음 정착지(첫 후보)의 생성 난이도. 고정 맵 차례면 null.</summary>
        public DifficultyParams PreviewNextDifficulty() => NextOffers()[0].Params;

        // ───────────── 저장 파일 명령 기록(2026-10-23 로드맵 2-①, SaveGame) ─────────────

        private readonly List<GameCommand> _log = new List<GameCommand>();

        /// <summary>이 캠페인에서 성공한 플레이어 명령 전부(순서대로). 저장 파일에 그대로 들어간다.</summary>
        public IReadOnlyList<GameCommand> CommandLog => _log;

        /// <summary>캠페인 시드(같은 시드 + 같은 명령 = 같은 결과).</summary>
        public int Seed => _seed;

        private void Log(GameCommand c)
        {
            c.Settlement = SettlementNumber;
            _log.Add(c);
        }

        private static int IndexOfBid(SaleResult sale)
        {
            for (int i = 0; i < sale.Auction.Bids.Count; i++)
                if (ReferenceEquals(sale.Auction.Bids[i], sale.Bid)) return i;
            return 0;
        }

        private void OnSold(SaleResult sale)
        {
            Cash = sale.SettlementCash + sale.Bid.Cash;
            _pendingFoodBonus = sale.Bid.NextSettlementFoodBonus;
            _pendingFreeBuilds.Clear();
            foreach (KeyValuePair<string, int> kv in sale.Bid.NextSettlementFreeBuilds) _pendingFreeBuilds[kv.Key] = kv.Value;
            if (sale.Bid.Modifier != null) _modifiers.Add(sale.Bid.Modifier); // 영구 모디파이어(결정 BM1)
            var record = new SaleRecord
            {
                SettlementNumber = SettlementNumber,
                MapId = CurrentMap.Id,
                Hour = sale.Auction.Hour,
                BidderName = sale.Bid.Bidder.Name,
                Emergency = sale.Auction.Kind == AuctionKind.Emergency,
                SaleCash = sale.Bid.Cash,
                StockSaleCash = sale.StockSaleCash,
                FoodBonus = sale.Bid.NextSettlementFoodBonus,
                FreeBuilds = new Dictionary<string, int>(_pendingFreeBuilds),
                Modifier = sale.Bid.Modifier,
                CampaignCashAfter = Cash,
            };
            _sales.Add(record);
            State = CampaignState.ChoosingDestination;
            SettlementSold?.Invoke(record);
        }

        private void OnBankrupt(string reason)
        {
            BankruptcyReason = reason;
            State = CampaignState.Bankrupt;
            WentBankrupt?.Invoke(reason);
        }
    }
}
