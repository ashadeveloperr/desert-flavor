using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>4단계: 가치 평가(개발 중 자산·입찰자), 경매 진행(예고·시세·긴급·파산), 캠페인(매각·장비·다음 정착지).</summary>
    public class AuctionCampaignTests
    {
        private static EconomyConfig _config;
        private static List<MapDefinition> _maps;

        [OneTimeSetUp]
        public void Load()
        {
            string data = Path.Combine(Application.dataPath, "Data");
            _config = EconomyConfigLoader.Parse(File.ReadAllText(Path.Combine(data, "Config", "economy_balance_v0.8_config.json")));
            _maps = Directory.GetFiles(Path.Combine(data, "Maps"), "*.json").OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => MapLoader.Parse(File.ReadAllText(p))).ToList();
        }

        private static BidderProfile Bidder(string id) => _config.Auction.Bidders.First(b => b.Id == id);

        /// <summary>맵 1 + 샘플 배치로 정착지와 경매 관리자를 만든다.</summary>
        private static (Settlement s, AuctionManager a) NewRun(Action<SettlementRules> tweak = null, int seed = 1)
        {
            SettlementRules rules = SettlementRules.FromConfig(_config);
            tweak?.Invoke(rules);
            var s = new Settlement(_maps[0].CreateMap(), rules);
            var a = new AuctionManager(s, seed);
            Assert.IsEmpty(_maps[0].RequestSampleLayout(s));
            return (s, a);
        }

        private static void RunUntilEvent(Settlement s, AuctionManager a, int maxHours = 1000)
        {
            for (int i = 0; i < maxHours && !a.IsOpen && !a.IsFinished; i++) s.AdvanceHour();
        }

        // ───────────── 설정·맵 ─────────────

        [Test]
        public void Config_HasAuctionBiddersAndEquipment()
        {
            Assert.AreEqual(2, _config.Auction.Bidders.Count);
            Assert.AreEqual(0.2, _config.Auction.EmergencyPenalty, 1e-9);
            Assert.AreEqual(1, Bidder("farmer").NextSettlementFreeBuilds[BuildingKinds.Farm], "농장주 혜택: 완공된 농장 1개");
            Assert.AreEqual(2.5, _config.AuctionSeed.OreProduction24hValueFactor, 1e-9);
            Assert.AreEqual(1.1, _config.Equipment.Single(e => e.Id == "wellPump").WellSupplyMultiplier, 1e-9);
        }

        [Test]
        public void Maps_LoadAndTheirSampleLayoutsAreValid()
        {
            Assert.AreEqual(2, _maps.Count, "고정 맵 2종");
            foreach (MapDefinition m in _maps)
            {
                var s = new Settlement(m.CreateMap(), SettlementRules.FromConfig(_config));
                Assert.IsEmpty(m.RequestSampleLayout(s), m.Id);
            }
            Assert.AreEqual(TerrainType.WaterSource, _maps[1].CreateMap().GetTile(new HexCoord(-2, 1)).Terrain);
        }

        // ───────────── 가치 평가 ─────────────

        private static Settlement UpgradeMineAt(int startHour, double developmentRatio, int until = 229)
        {
            (Settlement s, AuctionManager _) = NewRun(r => r.DevelopmentAssetRatio = developmentRatio);
            for (int h = 0; h < until; h++)
            {
                if (h == startHour) Assert.IsTrue(s.RequestUpgrade(s.Facilities.First(f => f.Kind == BuildingKinds.Mine).Id).Success);
                s.AdvanceHour();
            }
            return s;
        }

        [Test]
        public void DevelopmentAssets_SoftenTheLateInvestmentPenalty()
        {
            // 경매(229h) 14시간 전 시작한 개량 → 경매 시점에 개량 중(가동 중단).
            double without = SettlementValuation.Evaluate(UpgradeMineAt(215, 0.0), 229).Value;
            Settlement with = UpgradeMineAt(215, 0.5);
            double withValue = SettlementValuation.Evaluate(with, 229).Value;
            Assert.Greater(withValue, without * 1.5, "개발 중 자산이 개량 중 광산의 기대 생산을 반영");
            Assert.IsNotEmpty(SettlementValuation.DevelopmentAssets(with, 229));

            ValuationResult industrialist = SettlementValuation.EvaluateFor(with, 229, Bidder("industrialist"));
            ValuationResult farmer = SettlementValuation.EvaluateFor(with, 229, Bidder("farmer"));
            Assert.Greater(industrialist.DevelopmentOre, farmer.DevelopmentOre * 2, "산업가가 공사 중 광산을 더 높게 평가");
        }

        [Test]
        public void BiddersWeighTheSameSettlementDifferently()
        {
            (Settlement s, AuctionManager _) = NewRun();
            s.AdvanceHours(133);
            ValuationResult neutral = SettlementValuation.Evaluate(s, 133);
            ValuationResult industrialist = SettlementValuation.EvaluateFor(s, 133, Bidder("industrialist"));
            ValuationResult farmer = SettlementValuation.EvaluateFor(s, 133, Bidder("farmer"));

            Assert.AreEqual(neutral.BaseValue * 1.15, industrialist.BaseValue, 1e-6, "산업가 광물 가중치 1.15");
            Assert.Less(industrialist.FoodMultiplier, neutral.FoodMultiplier, "산업가는 식량 배율을 덜 반영");
            Assert.Greater(farmer.FoodMultiplier, neutral.FoodMultiplier, "농장주는 식량 배율을 더 반영");
        }

        // ───────────── 경매 진행 ─────────────

        [Test]
        public void AuctionHallCompletion_SchedulesFirstAuctionAndAnnouncesBidders()
        {
            (Settlement s, AuctionManager a) = NewRun();
            int announcements = 0;
            a.BiddersAnnounced += _ => announcements++;

            s.AdvanceHours(38);
            Assert.AreEqual(37, s.AuctionHallCompletedHour);
            Assert.AreEqual(37 + 72, a.NextAuctionHour, "완공 + 72h");
            Assert.AreEqual(1, announcements, "경매장 완공 직후 예고");
            Assert.That(a.AnnouncedBidders.Count, Is.InRange(1, 2));
            Assert.AreEqual(AuctionPhase.Scheduled, a.Phase);
        }

        [Test]
        public void RegularAuction_OpensOnTime_WithMarketAndAnnouncedBidders()
        {
            (Settlement s, AuctionManager a) = NewRun();
            s.AdvanceHours(38);
            List<string> announced = a.AnnouncedBidders.Select(b => b.Id).ToList();
            RunUntilEvent(s, a);

            Assert.IsTrue(a.IsOpen);
            AuctionEvent e = a.CurrentAuction;
            Assert.AreEqual(AuctionKind.Regular, e.Kind);
            Assert.AreEqual(109, e.Hour);
            Assert.AreEqual(109, s.CurrentHour, "경매가 열리면 그 시간은 정산하지 않고 멈춘다");
            CollectionAssert.AreEqual(announced, e.Bids.Select(b => b.Bidder.Id).ToList(), "예고된 입찰자가 온다");
            AuctionSettings cfg = _config.Auction;
            Assert.That(e.Market, Is.InRange(cfg.MarketCenter * (1 - cfg.MarketSwing), cfg.MarketCenter * (1 + cfg.MarketSwing)));
            foreach (AuctionBid bid in e.Bids)
            {
                Assert.AreEqual(bid.Valuation.Value * e.Market, bid.TotalOffer, 1e-6);
                Assert.AreEqual(bid.TotalOffer * bid.Bidder.CashShare, bid.Cash, 1e-6);
            }
        }

        [Test]
        public void Hold_SchedulesNextAuctionAndAnnouncesAgain()
        {
            (Settlement s, AuctionManager a) = NewRun();
            RunUntilEvent(s, a);
            int announcements = 0;
            a.BiddersAnnounced += _ => announcements++;

            a.Hold();
            Assert.AreEqual(109 + 72, a.NextAuctionHour);
            Assert.AreEqual(1, announcements, "보류 직후 다음 경매 예고");
            RunUntilEvent(s, a);
            Assert.IsTrue(a.IsOpen);
            Assert.AreEqual(181, a.CurrentAuction.Hour);
        }

        [Test]
        public void SameSeed_SameBiddersAndMarket()
        {
            (Settlement s1, AuctionManager a1) = NewRun(seed: 42);
            (Settlement s2, AuctionManager a2) = NewRun(seed: 42);
            RunUntilEvent(s1, a1);
            RunUntilEvent(s2, a2);
            Assert.AreEqual(a1.CurrentAuction.Market, a2.CurrentAuction.Market);
            CollectionAssert.AreEqual(a1.CurrentAuction.Bids.Select(b => b.Bidder.Id), a2.CurrentAuction.Bids.Select(b => b.Bidder.Id));
        }

        [Test]
        public void CashHittingZeroWithAuctionHall_OpensEmergencyAuctionWithPenalty()
        {
            (Settlement s, AuctionManager a) = NewRun(r => r.StartingCash = 250); // 경매가 72h 간격이 되며 400으로는 정기 경매(109h)까지 버텨서 낮춤
            RunUntilEvent(s, a);

            Assert.IsTrue(a.IsOpen);
            AuctionEvent e = a.CurrentAuction;
            Assert.AreEqual(AuctionKind.Emergency, e.Kind);
            Assert.Less(e.Hour, 109, "정기 경매 전에 자금이 바닥남");
            Assert.LessOrEqual(s.Cash, 0);
            Assert.AreEqual(0.2, e.Penalty, 1e-9);
            foreach (AuctionBid bid in e.Bids) Assert.AreEqual(bid.Valuation.Value * e.Market * 0.8, bid.TotalOffer, 1e-6);
        }

        [Test]
        public void DecliningEmergencySale_BankruptsAfter24NegativeHours()
        {
            (Settlement s, AuctionManager a) = NewRun(r => r.StartingCash = 250); // 경매가 72h 간격이 되며 400으로는 정기 경매(109h)까지 버텨서 낮춤
            string reason = null;
            a.Bankrupted += r => reason = r;
            for (int guard = 0; guard < 20 && !a.IsFinished; guard++)
            {
                RunUntilEvent(s, a);
                if (a.IsOpen) a.Hold(); // 긴급·정기 모두 보류
            }

            Assert.AreEqual(AuctionPhase.Bankrupt, a.Phase);
            Assert.IsNotNull(reason);
            Assert.Less(s.Cash, 0);
        }

        [Test]
        public void CashHittingZeroWithoutAuctionHall_IsImmediateBankruptcy()
        {
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.StartingCash = 20;
            var s = new Settlement(_maps[0].CreateMap(), rules);
            var a = new AuctionManager(s, 1);
            s.RequestBuild(BuildingKinds.Well, new HexCoord(0, 0));
            s.RequestBuild(BuildingKinds.Farm, new HexCoord(1, -1));
            RunUntilEvent(s, a);

            Assert.AreEqual(AuctionPhase.Bankrupt, a.Phase);
            Assert.IsNull(s.AuctionHallCompletedHour);
            Assert.LessOrEqual(s.Cash, 0);
            Assert.Greater(s.History.Last().Cash, -5, "0을 넘어선 첫 시간에 바로 파산");
        }

        [Test]
        public void CashHittingZeroWithAuctionHallButNoMine_IsImmediateBankruptcy()
        {
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.StartingCash = 150;
            var s = new Settlement(_maps[0].CreateMap(), rules);
            var a = new AuctionManager(s, 1);
            s.RequestBuild(BuildingKinds.Well, new HexCoord(0, 0));
            s.RequestBuild(BuildingKinds.Farm, new HexCoord(1, -1));
            s.RequestBuild(BuildingKinds.AuctionHall, new HexCoord(-1, 0));
            s.RequestBuild(BuildingKinds.Quarry, new HexCoord(2, -1));
            RunUntilEvent(s, a);

            Assert.IsNotNull(s.AuctionHallCompletedHour, "경매장은 있음");
            Assert.AreEqual(AuctionPhase.Bankrupt, a.Phase, "광산이 없으면 긴급 매각 없이 파산");
            StringAssert.Contains("광산", a.BankruptcyReason);
        }

        // ───────────── 캠페인 ─────────────

        [Test]
        public void EquipmentUpgrade_ScalesEffect_CostsMore_AndCarriesToNextSettlement()
        {
            // 2026-10-23 로드맵 1-④ 장비 강화(Docs/decision_log_auto.md E1~E5).
            var campaign = new Campaign(_config, _maps, seed: 7);
            Settlement first = campaign.StartNextSettlement();
            Assert.IsEmpty(campaign.CurrentMap.RequestSampleLayout(first));
            first.BuyStone(5);
            RunUntilEvent(first, campaign.Auctions);
            campaign.Auctions.Sell(0);
            Assert.AreEqual(CampaignState.Shopping, campaign.State);

            Assert.IsNull(campaign.UpgradeCost("wellPump"), "가지지 않은 장비는 강화 불가");
            Assert.IsTrue(campaign.BuyEquipment("wellPump").Success);
            Assert.AreEqual(1, campaign.EquipmentLevel("wellPump"));
            Assert.AreEqual(400 * 2.0, campaign.UpgradeCost("wellPump").Value, 1e-9, "Lv1 → Lv2 = 가격 × 2.0");
            Assume.That(campaign.Cash, Is.GreaterThanOrEqualTo(800));
            Assert.IsTrue(campaign.UpgradeEquipment("wellPump").Success);
            Assert.AreEqual(2, campaign.EquipmentLevel("wellPump"));
            Assert.AreEqual(400 * 3.0, campaign.UpgradeCost("wellPump").Value, 1e-9, "Lv2 → Lv3 = 가격 × 3.0");

            EquipmentDefinition crew = _config.Equipment.Single(e => e.Id == "extraCrew");
            Assert.AreEqual(1, campaign.MaxEquipmentLevel(crew), "정수 효과(추가 작업반)는 강화 없음");
            Assert.AreEqual(3, campaign.MaxEquipmentLevel(_config.Equipment.Single(e => e.Id == "wellPump")));

            Settlement second = campaign.StartNextSettlement();
            Assert.AreEqual(1 + 0.1 * 1.3, second.Rules.WellSupplyMultiplier, 1e-9, "Lv2 효과 = 기본 +10%의 1.3배");
            Assert.IsFalse(campaign.UpgradeEquipment("wellPump").Success, "정착지 안에서는 강화 불가");
        }

        [Test]
        public void EconomyEffects_Scaled_ScalesDeviationAndAdditiveValues()
        {
            var e = new EconomyEffects { WellSupplyMultiplier = 1.1, StoneCostMultiplier = 0.9, ExtraStorage = 50, ExtraConstructionSlots = 1, EfficiencyCap = 0.9 };
            EconomyEffects s = e.Scaled(1.5);
            Assert.AreEqual(1.15, s.WellSupplyMultiplier, 1e-12);
            Assert.AreEqual(0.85, s.StoneCostMultiplier, 1e-12);
            Assert.AreEqual(75, s.ExtraStorage, 1e-12);
            Assert.AreEqual(1, s.ExtraConstructionSlots, "정수 슬롯은 그대로");
            Assert.AreEqual(0.9, s.EfficiencyCap, 1e-12, "약점용 효율 상한은 그대로");
        }

        [Test]
        public void Campaign_SellBuyEquipment_NextSettlementStartsFresh()
        {
            var campaign = new Campaign(_config, _maps, seed: 7);
            Settlement first = campaign.StartNextSettlement();
            Assert.AreEqual(1000.0, first.Cash);
            Assert.IsEmpty(campaign.CurrentMap.RequestSampleLayout(first));
            first.BuyStone(5);
            RunUntilEvent(first, campaign.Auctions);
            Assert.IsTrue(campaign.Auctions.IsOpen);

            // 농장주가 오면 농장주에게(식량 혜택 확인), 아니면 첫 입찰.
            IReadOnlyList<AuctionBid> bids = campaign.Auctions.CurrentAuction.Bids;
            int pick = Math.Max(0, bids.ToList().FindIndex(b => b.Bidder.Id == "farmer"));
            AuctionBid chosen = bids[pick];
            double settlementCash = first.Cash;
            campaign.Auctions.Sell(pick);

            Assert.AreEqual(CampaignState.Shopping, campaign.State);
            Assert.AreEqual(settlementCash + chosen.Cash, campaign.Cash, 1e-6, "대금 100% + 남은 정착지 자금");
            Assert.AreEqual(1, campaign.Sales.Count);

            double before = campaign.Cash;
            Assert.IsTrue(campaign.BuyEquipment("wellPump").Success);
            Assert.AreEqual(before - 400, campaign.Cash, 1e-9);
            Assert.IsFalse(campaign.BuyEquipment("wellPump").Success, "중복 구매 불가");

            Settlement second = campaign.StartNextSettlement();
            Assert.AreEqual(2, campaign.SettlementNumber);
            Assert.AreEqual(_maps[1].Id, campaign.CurrentMap.Id, "다음 고정 맵");
            Assert.AreEqual(campaign.Cash, second.Cash, 1e-9, "자금 이월");
            Assert.AreEqual(48 + chosen.NextSettlementFoodBonus, second.Food, 1e-9, "식량은 이월 없이 새로 + 입찰자 혜택");
            Assert.AreEqual(75, second.Stone, 1e-9, "석재는 이월 없음");
            Assert.AreEqual(4.0, second.NextPrice(false), 1e-9, "구매 단가 초기화");
            Assert.AreEqual(1.1, second.Rules.WellSupplyMultiplier, 1e-9, "영구 장비 효과");
            int expectedFreeFarms = chosen.Bidder.Id == "farmer" ? 1 : 0;
            Assert.AreEqual(expectedFreeFarms, second.FreeBuildsRemaining(BuildingKinds.Farm), "농장주에게 팔았으면 완공 농장 1개");
        }

        [Test]
        public void FreeBuild_PlacesAnOperatingFacilityImmediately_WithoutStoneOrSlot()
        {
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.FreeBuilds[BuildingKinds.Farm] = 1;
            var s = new Settlement(_maps[0].CreateMap(), rules);

            CommandResult r = s.RequestBuild(BuildingKinds.Farm, new HexCoord(1, -1));
            Assert.IsTrue(r.Success);
            Facility farm = s.FacilityAt(new HexCoord(1, -1));
            Assert.AreEqual(FacilityState.Operating, farm.State, "바로 완공");
            Assert.AreEqual(75.0, s.Stone, "석재 차감 없음");
            Assert.IsEmpty(s.Jobs, "작업 대기열을 쓰지 않음");
            Assert.AreEqual(0, s.FreeBuildsRemaining(BuildingKinds.Farm));
            Assert.AreEqual(0.0, farm.StoneInvested, "철거해도 환급 없음");

            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, new HexCoord(1, 0)).Success);
            Assert.AreEqual(FacilityState.Constructing, s.FacilityAt(new HexCoord(1, 0))?.State ?? FacilityState.Constructing);
            Assert.AreEqual(1, s.Jobs.Count, "두 번째 농장부터는 일반 건설");
        }

        [Test]
        public void StarterSupplies_AreClampedToStorage()
        {
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.StarterFood = 500;
            var s = new Settlement(_maps[0].CreateMap(), rules);
            Assert.AreEqual(rules.BaseFoodStorage, s.Food, "시작 식량도 저장 한도를 넘지 않는다");
        }

        [Test]
        public void WellPump_RaisesWellSupply()
        {
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.WellSupplyMultiplier = 1.2;
            var s = new Settlement(_maps[0].CreateMap(), rules);
            s.RequestBuild(BuildingKinds.Well, new HexCoord(0, 0));
            s.AdvanceHours(12);
            Assert.AreEqual(120.0, s.LastWater.TotalSupply, 1e-9, "L1 우물 100 × 1.2");
        }

        [Test]
        public void Campaign_CannotSkipAheadOrContinueAfterBankruptcy()
        {
            var campaign = new Campaign(_config, _maps);
            campaign.StartNextSettlement();
            Assert.Throws<InvalidOperationException>(() => campaign.StartNextSettlement(), "매각 전에는 다음 정착지 불가");
            Assert.IsFalse(campaign.BuyEquipment("wellPump").Success, "정착지 안에서는 상점 불가");
        }
    }
}
