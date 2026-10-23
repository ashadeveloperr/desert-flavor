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
        /// <summary>매각 후 장비 상점(다음 정착지 진입 전).</summary>
        Shopping,
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
        public double FoodBonus;
        /// <summary>다음 정착지 무료 완공 배치(농장주 혜택 등).</summary>
        public Dictionary<string, int> FreeBuilds = new Dictionary<string, int>();
        public double CampaignCashAfter;
    }

    /// <summary>
    /// 정착지를 이어 가는 캠페인(무한 개척 루프).
    /// 정착지 진입 → 운영 → 경매(매각/보류) → 매각 시 대금 100% → 장비 상점 → 다음 정착지.
    /// - 자금과 영구 장비만 이어지고, 식량·석재·구매 단가는 정착지마다 새로 시작한다(확정).
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
        private readonly HashSet<string> _owned = new HashSet<string>();
        private readonly HashSet<string> _loadout = new HashSet<string>();
        private readonly List<SaleRecord> _sales = new List<SaleRecord>();
        private double _pendingFoodBonus;
        private readonly Dictionary<string, int> _pendingFreeBuilds = new Dictionary<string, int>();

        public CampaignState State { get; private set; }
        /// <summary>정착지 밖의 자금(상점·다음 정착지 시작 자금). 정착지 안에서는 Current.Cash를 본다.</summary>
        public double Cash { get; private set; }
        /// <summary>1부터.</summary>
        public int SettlementNumber { get; private set; }
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
        public IReadOnlyCollection<string> OwnedEquipment => _owned;
        public IReadOnlyList<SaleRecord> Sales => _sales;
        public IReadOnlyList<EquipmentDefinition> Shop => _config.Equipment ?? new List<EquipmentDefinition>();
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
        /// 다음 정착지에 들어간다(처음 시작 포함). 상점 상태에서만, 또는 첫 정착지.
        /// offerIndex = <see cref="NextOffers"/> 중 고른 후보(고정 맵 차례면 0뿐).
        /// 현장 장비 슬롯에 넣은 장비(<see cref="Loadout"/>)와 고른 지역의 모디파이어 효과가 이 정착지 규칙에 걸린다.
        /// </summary>
        public Settlement StartNextSettlement(int offerIndex = 0)
        {
            if (State == CampaignState.Bankrupt) throw new InvalidOperationException("파산한 캠페인입니다.");
            if (Current != null && State != CampaignState.Shopping) throw new InvalidOperationException("매각 후에만 다음 정착지로 갈 수 있습니다.");
            IReadOnlyList<RegionOffer> offers = NextOffers();
            if (offerIndex < 0 || offerIndex >= offers.Count) throw new ArgumentOutOfRangeException(nameof(offerIndex), "없는 지역 후보입니다.");
            RegionOffer offer = offers[offerIndex];

            SettlementNumber++;
            CurrentMap = MapFor(offer);
            CurrentModifiers = CurrentGenerated != null ? offer.Modifiers : new List<RegionModifierDefinition>();
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.StartingCash = Cash;
            rules.StarterFood += _pendingFoodBonus;
            foreach (EquipmentDefinition e in Shop.Where(x => _loadout.Contains(x.Id))) rules.ApplyEffects(e.Effects);
            foreach (RegionModifierDefinition m in CurrentModifiers) rules.ApplyEffects(m.Effects);
            foreach (KeyValuePair<string, int> kv in _pendingFreeBuilds) rules.FreeBuilds[kv.Key] = kv.Value;
            _pendingFoodBonus = 0;
            _pendingFreeBuilds.Clear();

            Current = new Settlement(CurrentMap.CreateMap(), rules);
            Auctions = new AuctionManager(Current, _seed + SettlementNumber * 7919);
            Disasters = DisasterManager.Attach(Current, _config.Disasters, _seed + SettlementNumber * 4421);
            Auctions.Sold += OnSold;
            Auctions.Bankrupted += OnBankrupt;
            State = CampaignState.InSettlement;
            SettlementStarted?.Invoke();
            return Current;
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

        /// <summary>다음에 들어갈 정착지 번호(상점·첫 진입 전에는 +1, 정착지 안에서는 지금 번호).</summary>
        private int NextSettlementNumber => State == CampaignState.Shopping || Current == null ? SettlementNumber + 1 : SettlementNumber;

        /// <summary>
        /// 다음 지역 후보(2026-10-23 확정: 2곳). 난이도는 지금 보유 자금 + 지나온 생성 맵 정착지 수로 정하므로, 상점에서 장비를 사면 후보도 다시 계산된다.
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
                int seed = unchecked(MapSeed(next) + i * 7777);
                List<RegionModifierDefinition> mods = _procedural.Modifiers.Roll(p.D, seed, taken);
                foreach (RegionModifierDefinition m in mods.Where(m => m.Kind == RegionModifierKind.Weakness)) taken.Add(m.Id);
                offers.Add(new RegionOffer { Index = i, Seed = seed, Params = p, Modifiers = mods });
            }
            return offers;
        }

        /// <summary>다음 정착지(첫 후보)의 생성 난이도. 고정 맵 차례면 null.</summary>
        public DifficultyParams PreviewNextDifficulty() => NextOffers()[0].Params;

        private int MapSeed(int settlementNumber) => unchecked(_seed * 31 + settlementNumber * 104729);

        public bool Owns(string equipmentId) => _owned.Contains(equipmentId);

        /// <summary>상점에서 장비 구매. 이미 가졌거나 자금이 모자라면 실패. 슬롯이 비어 있으면 바로 장착된다.</summary>
        public CommandResult BuyEquipment(string equipmentId)
        {
            if (State != CampaignState.Shopping) return CommandResult.Fail("매각 후 상점에서만 살 수 있습니다.");
            EquipmentDefinition e = Shop.FirstOrDefault(x => x.Id == equipmentId);
            if (e == null) return CommandResult.Fail("없는 장비입니다.");
            if (_owned.Contains(e.Id)) return CommandResult.Fail("이미 가진 장비입니다.");
            if (Cash < e.Price) return CommandResult.Fail($"자금이 부족합니다({e.Price:0} 필요).");
            Cash -= e.Price;
            _owned.Add(e.Id);
            if (_loadout.Count < EquipmentSlots) _loadout.Add(e.Id);
            return CommandResult.Ok();
        }

        // ───────────── 현장 장비 슬롯 ─────────────

        /// <summary>현장 장비 슬롯 수. 기획서 5.1 확정값 5.</summary>
        public const int EquipmentSlots = 5;

        /// <summary>다음 정착지에 가져갈 장비(최대 5개). 다음 정착지에서도 바꾸지 않으면 그대로 유지된다.</summary>
        public IReadOnlyCollection<string> Loadout => _loadout;

        public bool IsEquipped(string equipmentId) => _loadout.Contains(equipmentId);

        /// <summary>장비를 슬롯에 넣거나 뺀다. 정착지 밖(상점·첫 진입 전)에서만, 가진 장비만, 슬롯 5개까지.</summary>
        public CommandResult SetEquipped(string equipmentId, bool equipped)
        {
            if (State == CampaignState.InSettlement && Current != null) return CommandResult.Fail("장비는 정착지에 들어가기 전에만 바꿀 수 있습니다.");
            if (!_owned.Contains(equipmentId)) return CommandResult.Fail("가지지 않은 장비입니다.");
            if (!equipped) { _loadout.Remove(equipmentId); return CommandResult.Ok(); }
            if (_loadout.Contains(equipmentId)) return CommandResult.Ok();
            if (_loadout.Count >= EquipmentSlots) return CommandResult.Fail($"현장 장비 슬롯 {EquipmentSlots}개가 가득 찼습니다.");
            _loadout.Add(equipmentId);
            return CommandResult.Ok();
        }

        private void OnSold(SaleResult sale)
        {
            Cash = sale.SettlementCash + sale.Bid.Cash;
            _pendingFoodBonus = sale.Bid.NextSettlementFoodBonus;
            _pendingFreeBuilds.Clear();
            foreach (KeyValuePair<string, int> kv in sale.Bid.NextSettlementFreeBuilds) _pendingFreeBuilds[kv.Key] = kv.Value;
            var record = new SaleRecord
            {
                SettlementNumber = SettlementNumber,
                MapId = CurrentMap.Id,
                Hour = sale.Auction.Hour,
                BidderName = sale.Bid.Bidder.Name,
                Emergency = sale.Auction.Kind == AuctionKind.Emergency,
                SaleCash = sale.Bid.Cash,
                FoodBonus = sale.Bid.NextSettlementFoodBonus,
                FreeBuilds = new Dictionary<string, int>(_pendingFreeBuilds),
                CampaignCashAfter = Cash,
            };
            _sales.Add(record);
            State = CampaignState.Shopping;
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
