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
    /// - 다음 맵 선택은 <see cref="ChooseNextMap"/> 한곳에 모았다. 후속 규칙 '보유 자금 비례 난이도'는 여기에 넣는다.
    /// </summary>
    public sealed class Campaign
    {
        private readonly EconomyConfig _config;
        private readonly IReadOnlyList<MapDefinition> _maps;
        private readonly int _seed;
        private readonly HashSet<string> _owned = new HashSet<string>();
        private readonly List<SaleRecord> _sales = new List<SaleRecord>();
        private double _pendingFoodBonus;
        private readonly Dictionary<string, int> _pendingFreeBuilds = new Dictionary<string, int>();

        public CampaignState State { get; private set; }
        /// <summary>정착지 밖의 자금(상점·다음 정착지 시작 자금). 정착지 안에서는 Current.Cash를 본다.</summary>
        public double Cash { get; private set; }
        /// <summary>1부터.</summary>
        public int SettlementNumber { get; private set; }
        public MapDefinition CurrentMap { get; private set; }
        public Settlement Current { get; private set; }
        public AuctionManager Auctions { get; private set; }
        public IReadOnlyCollection<string> OwnedEquipment => _owned;
        public IReadOnlyList<SaleRecord> Sales => _sales;
        public IReadOnlyList<EquipmentDefinition> Shop => _config.Equipment ?? new List<EquipmentDefinition>();
        public string BankruptcyReason { get; private set; }

        public event Action SettlementStarted;
        public event Action<SaleRecord> SettlementSold;
        public event Action<string> WentBankrupt;

        public Campaign(EconomyConfig config, IReadOnlyList<MapDefinition> maps, int seed = 20261022)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (maps == null || maps.Count == 0) throw new ArgumentException("맵이 하나 이상 필요합니다.", nameof(maps));
            _maps = maps;
            _seed = seed;
            Cash = config.Prototype.StartingCash;
        }

        /// <summary>다음 정착지에 들어간다(처음 시작 포함). 상점 상태에서만, 또는 첫 정착지.</summary>
        public Settlement StartNextSettlement()
        {
            if (State == CampaignState.Bankrupt) throw new InvalidOperationException("파산한 캠페인입니다.");
            if (Current != null && State != CampaignState.Shopping) throw new InvalidOperationException("매각 후에만 다음 정착지로 갈 수 있습니다.");

            SettlementNumber++;
            CurrentMap = ChooseNextMap();
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.StartingCash = Cash;
            rules.StarterFood += _pendingFoodBonus;
            rules.WellSupplyMultiplier = OwnedDefinitions().Aggregate(1.0, (m, e) => m * e.WellSupplyMultiplier);
            foreach (KeyValuePair<string, int> kv in _pendingFreeBuilds) rules.FreeBuilds[kv.Key] = kv.Value;
            _pendingFoodBonus = 0;
            _pendingFreeBuilds.Clear();

            Current = new Settlement(CurrentMap.CreateMap(), rules);
            Auctions = new AuctionManager(Current, _seed + SettlementNumber * 7919);
            Auctions.Sold += OnSold;
            Auctions.Bankrupted += OnBankrupt;
            State = CampaignState.InSettlement;
            SettlementStarted?.Invoke();
            return Current;
        }

        /// <summary>
        /// 다음 맵. 지금은 고정 맵을 순서대로 돈다.
        /// TODO(후속 확정 규칙): 보유 자금(Cash)에 비례해 더 어려운 정착지를 고르거나 지형을 악화시킨다.
        /// </summary>
        private MapDefinition ChooseNextMap() => _maps[(SettlementNumber - 1) % _maps.Count];

        public bool Owns(string equipmentId) => _owned.Contains(equipmentId);

        /// <summary>상점에서 장비 구매. 이미 가졌거나 자금이 모자라면 실패.</summary>
        public CommandResult BuyEquipment(string equipmentId)
        {
            if (State != CampaignState.Shopping) return CommandResult.Fail("매각 후 상점에서만 살 수 있습니다.");
            EquipmentDefinition e = Shop.FirstOrDefault(x => x.Id == equipmentId);
            if (e == null) return CommandResult.Fail("없는 장비입니다.");
            if (_owned.Contains(e.Id)) return CommandResult.Fail("이미 가진 장비입니다.");
            if (Cash < e.Price) return CommandResult.Fail($"자금이 부족합니다({e.Price:0} 필요).");
            Cash -= e.Price;
            _owned.Add(e.Id);
            return CommandResult.Ok();
        }

        private IEnumerable<EquipmentDefinition> OwnedDefinitions() => Shop.Where(e => _owned.Contains(e.Id));

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
