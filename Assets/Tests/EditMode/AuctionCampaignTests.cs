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
            _config = TestData.Economy();
            _maps = TestData.Maps();
        }

        private static BidderProfile Bidder(string id) => _config.Auction.Bidders.First(b => b.Id == id);

        /// <summary>맵 1 + 샘플 배치로 정착지와 경매 관리자를 만든다.</summary>
        private static (Settlement s, AuctionManager a) NewRun(Action<SettlementRules> tweak = null, int seed = 1)
        {
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.ConstructionHoursMultiplier = 1; // 경매 일정 시각은 원본 건설 시간 기준으로 고정(게임 값 0.5는 2026-10-23 FL2)
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
            // 기획서 8.4의 6종(결정 BD1~BD6) + 이주 사업가(결정 CL1~CL3).
            CollectionAssert.AreEquivalent(new[] { "industrialist", "farmer", "distributor", "builder", "waterBroker", "developer", "colonist" },
                _config.Auction.Bidders.Select(b => b.Id));
            Assert.AreEqual(1, Bidder("colonist").NextSettlementFreeBuilds[BuildingKinds.House], "이주 사업가 혜택: 무료 주택 +1");
            Assert.Greater(Bidder("colonist").PopulationWeight, Bidder("developer").PopulationWeight, "이주 사업가가 주민을 가장 크게 본다");
            Assert.AreEqual(0.2, _config.Auction.EmergencyPenalty, 1e-9);
            Assert.AreEqual(1, Bidder("farmer").NextSettlementFreeBuilds[BuildingKinds.Farm], "농장주 혜택: 완공된 농장 1개");
            Assert.AreEqual(1, Bidder("distributor").NextSettlementFreeBuilds[BuildingKinds.Warehouse], "유통 사업가 혜택: 창고");
            Assert.AreEqual(1, Bidder("builder").NextSettlementFreeBuilds[BuildingKinds.Quarry], "건설업자 혜택: 채석장");
            Assert.AreEqual(1, Bidder("waterBroker").NextSettlementFreeBuilds[BuildingKinds.WaterTank], "수자원 사업자 혜택: 물탱크");
            Assert.AreEqual(1, Bidder("developer").NextSettlementFreeBuilds[BuildingKinds.AuctionHall], "부동산 개발업자 혜택: 경매장");
            Assert.AreEqual(2.3, _config.AuctionSeed.OreProduction24hValueFactor, 1e-9, "2026-10-23 1-⑦ 재측정: 2.5 → 2.3");
            Assert.AreEqual(1.1, _config.Equipment.Single(e => e.Id == "wellPump").WellSupplyMultiplier, 1e-9);
        }

        [Test]
        public void OpenLandShare_DropsAsDesertIsBuilt_AndOnlyDeveloperFeelsIt()
        {
            var map = new HexMap(3);
            map.GetTile(HexCoord.Zero).Terrain = TerrainType.WaterSource;
            var s = new Settlement(map, SettlementRules.FromConfig(_config));
            Assert.AreEqual(1.0, SettlementValuation.OpenLandShare(s), 1e-12, "빈 맵은 사막이 전부 빈 땅");
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, new HexCoord(1, 0)).Success);
            Assert.Less(SettlementValuation.OpenLandShare(s), 1.0, "건설 예약한 타일은 빈 땅이 아니다");

            ValuationResult neutral = SettlementValuation.Evaluate(s, s.CurrentHour);
            ValuationResult developer = SettlementValuation.EvaluateFor(s, s.CurrentHour, Bidder("developer"));
            Assert.AreEqual(1.0, neutral.OpenLandMultiplier, 1e-12, "중립·다른 입찰자는 빈 땅 배율 1");
            Assert.Greater(developer.OpenLandMultiplier, 1.0, "빈 땅이 기준(0.5)보다 많으면 부동산 개발업자는 더 쳐준다");
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
        public void DevelopmentAssets_InfraUnderConstructionCountsWhatCompletionAdds()
        {
            // 결정 IB1·IB2: 공사 중 인프라 보너스 = 완공되면 늘어날 인프라 배율.
            var map = new HexMap(3);
            map.GetTile(HexCoord.Zero).Terrain = TerrainType.WaterSource;
            var s = new Settlement(map, SettlementRules.FromConfig(_config));
            List<DevelopmentAsset> Infra(string kind) =>
                SettlementValuation.DevelopmentAssets(s, s.CurrentHour + 1).Where(d => d.Kind == kind && d.Infra > 0).ToList();
            void RunUntilIdle()
            {
                for (int i = 0; i < 48 && s.Jobs.Any(); i++) s.AdvanceHour();
                Assert.IsEmpty(s.Jobs);
            }

            Assert.IsTrue(s.RequestBuild(BuildingKinds.AuctionHall, new HexCoord(1, 0)).Success);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.AuctionHall, new HexCoord(-1, 0)).Success);
            Assert.AreEqual(2, s.JobsInProgress);
            Assert.AreEqual(1, Infra(BuildingKinds.AuctionHall).Count, "경매장은 몇 개든 배율이 한 번만 늘어나므로 공사 중에도 하나만 센다");
            RunUntilIdle();

            Assert.IsTrue(s.RequestBuild(BuildingKinds.WaterTank, new HexCoord(0, 1)).Success);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Market, new HexCoord(0, -1)).Success);
            Assert.AreEqual(2, s.JobsInProgress);
            Assert.AreEqual(1, Infra(BuildingKinds.WaterTank).Count, "공사 중 물탱크도 인프라 보너스");
            Assert.AreEqual(1, Infra(BuildingKinds.Market).Count, "공사 중 시장도 인프라 보너스");
            RunUntilIdle();

            Assert.IsTrue(s.RequestBuild(BuildingKinds.AuctionHall, new HexCoord(1, -1)).Success);
            Assert.AreEqual(1, s.JobsInProgress);
            Assert.IsEmpty(Infra(BuildingKinds.AuctionHall), "경매장이 이미 운영 중이면 하나 더 지어도 배율이 그대로라 0");
        }

        [Test]
        public void BiddersWeighTheSameSettlementDifferently()
        {
            (Settlement s, AuctionManager _) = NewRun();
            s.AdvanceHours(133);
            ValuationResult neutral = SettlementValuation.Evaluate(s, 133);
            ValuationResult industrialist = SettlementValuation.EvaluateFor(s, 133, Bidder("industrialist"));
            ValuationResult farmer = SettlementValuation.EvaluateFor(s, 133, Bidder("farmer"));

            // 기반 가치 = 광물 가치 + 주민 가치(결정 CL1): 광물 부분만 비교한다.
            Assert.AreEqual((neutral.BaseValue - neutral.PopulationValue) * 1.3, industrialist.BaseValue - industrialist.PopulationValue, 1e-6,
                "산업가 광물 가중치 1.3(취향 2배, F2)");
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

            s.AdvanceHours(51);
            Assert.AreEqual(50, s.AuctionHallCompletedHour, "샘플 배치(주택 포함, 결정 LB1~: 공사 인원) — 시뮬레이터 `-- labor sample`의 건설 시간 ×1 값");
            Assert.AreEqual(s.AuctionHallCompletedHour + _config.ConfirmedRules.AuctionRepeatsGameHours, a.NextAuctionHour, "완공 + 경매 간격(결정 AU1)");
            Assert.AreEqual(1, announcements, "경매장 완공 직후 예고");
            Assert.AreEqual(_config.Auction.AttendeesWithOneAuctionHall, a.AnnouncedBidders.Count, "경매장 1개의 입찰자 수(결정 AH1)");
            Assert.AreEqual(AuctionPhase.Scheduled, a.Phase);
        }

        [Test]
        public void RegularAuction_OpensOnTime_WithMarketAndAnnouncedBidders()
        {
            (Settlement s, AuctionManager a) = NewRun();
            s.AdvanceHours(51);
            List<string> announced = a.AnnouncedBidders.Select(b => b.Id).ToList();
            RunUntilEvent(s, a);

            Assert.IsTrue(a.IsOpen);
            AuctionEvent e = a.CurrentAuction;
            Assert.AreEqual(AuctionKind.Regular, e.Kind);
            Assert.AreEqual(98, e.Hour);
            Assert.AreEqual(98, s.CurrentHour, "경매가 열리면 그 시간은 정산하지 않고 멈춘다");
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
            // 보류 뒤 48h를 버티도록 이전 시작 자금 700(게임 값 500은 2026-10-24 FB1: 샘플 배치는 143h에 자금이 바닥나 긴급 매각).
            (Settlement s, AuctionManager a) = NewRun(r => r.StartingCash = 700);
            RunUntilEvent(s, a);
            int announcements = 0;
            a.BiddersAnnounced += _ => announcements++;

            a.Hold();
            Assert.AreEqual(98 + 48, a.NextAuctionHour);
            Assert.AreEqual(1, announcements, "보류 직후 다음 경매 예고");
            RunUntilEvent(s, a);
            Assert.IsTrue(a.IsOpen);
            Assert.AreEqual(146, a.CurrentAuction.Hour);
        }

        [Test]
        public void MoreAuctionHalls_AddBidders_ClosingRemovesTheLastAdded()
        {
            // 2026-10-26 AH1(이슈 #10): 경매장 1·2·3개 = 입찰자 2·3·4명. 더 지으면 예고에 한 명 추가, 닫으면 뒤에서부터 빠지고 다시 열면 돌아온다.
            (Settlement s, AuctionManager a) = NewRun(r => r.StartingCash = 5000);
            s.AdvanceHours(51);
            Assert.AreEqual(1, a.OperatingAuctionHalls);
            List<string> two = a.AnnouncedBidders.Select(b => b.Id).ToList();
            Assert.AreEqual(2, two.Count);
            long auctionHour = a.NextAuctionHour.Value;

            s.BuyStone(30);
            HexCoord tile = s.Map.Tiles.Select(t => t.Coord).First(c => s.CanBuild(BuildingKinds.AuctionHall, c).Success);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.AuctionHall, tile).Success);
            int announced = 0;
            a.BiddersAnnounced += _ => announced++;
            s.AdvanceHours(14);
            Facility second = s.FacilityAt(tile);
            Assert.AreEqual(FacilityState.Operating, second.State);
            Assert.AreEqual(1, announced, "두 번째 경매장 완공 때 예고 갱신");
            List<string> three = a.AnnouncedBidders.Select(b => b.Id).ToList();
            Assert.AreEqual(3, three.Count);
            CollectionAssert.AreEqual(two, three.Take(2), "앞의 두 명은 그대로, 한 명 추가");
            Assert.AreEqual(auctionHour, a.NextAuctionHour, "경매 일정은 첫 경매장 기준 그대로");

            Assert.IsTrue(s.SetClosed(second.Id, true).Success);
            CollectionAssert.AreEqual(two, a.AnnouncedBidders.Select(b => b.Id), "닫으면 마지막에 추가된 사람이 빠짐");
            Assert.IsTrue(s.SetClosed(second.Id, false).Success);
            CollectionAssert.AreEqual(three, a.AnnouncedBidders.Select(b => b.Id), "다시 열면 돌아옴");

            RunUntilEvent(s, a);
            Assert.IsTrue(a.IsOpen);
            CollectionAssert.AreEqual(three, a.CurrentAuction.Bids.Select(b => b.Bidder.Id), "경매에는 예고된 세 명");
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
            (Settlement s, AuctionManager a) = NewRun(r => r.StartingCash = 250); // 경매가 72h 간격이 되며 400으로는 정기 경매까지 버텨서 낮춤(지금 정기 경매 98h, 결정 LB8)
            RunUntilEvent(s, a);

            Assert.IsTrue(a.IsOpen);
            AuctionEvent e = a.CurrentAuction;
            Assert.AreEqual(AuctionKind.Emergency, e.Kind);
            Assert.Less(e.Hour, 98, "정기 경매(98h) 전에 자금이 바닥남");
            Assert.LessOrEqual(s.Cash, 0);
            Assert.AreEqual(0.2, e.Penalty, 1e-9);
            foreach (AuctionBid bid in e.Bids) Assert.AreEqual(bid.Valuation.Value * e.Market * 0.8, bid.TotalOffer, 1e-6);
        }

        [Test]
        public void DecliningEmergencySale_BankruptsAfter24NegativeHours()
        {
            (Settlement s, AuctionManager a) = NewRun(r => r.StartingCash = 250); // 경매가 72h 간격이 되며 400으로는 정기 경매까지 버텨서 낮춤(지금 정기 경매 98h, 결정 LB8)
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
            rules.StartingCash = 60; // 정기 경매(98h, 결정 LB8) 전에 자금이 바닥나게(이전 150)
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
            // 강화 비용(800)까지 살 수 있게 시작 자금을 넉넉히(2026-10-26 AU1: 경매 48h라 첫 매각 대금이 줄어 기본 500으로는 모자람).
            EconomyConfig config = TestData.Economy();
            config.Prototype.StartingCash = 1000;
            var campaign = new Campaign(config, _maps, seed: 7);
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
            Assert.That(campaign.Cash, Is.GreaterThanOrEqualTo(800), "강화 비용을 낼 수 있어야 이 테스트가 뜻이 있음");
            Assert.IsTrue(campaign.UpgradeEquipment("wellPump").Success);
            Assert.AreEqual(2, campaign.EquipmentLevel("wellPump"));
            Assert.AreEqual(400 * 3.0, campaign.UpgradeCost("wellPump").Value, 1e-9, "Lv2 → Lv3 = 가격 × 3.0");

            EquipmentDefinition crew = config.Equipment.Single(e => e.Id == "extraCrew");
            Assert.AreEqual(1, campaign.MaxEquipmentLevel(crew), "정수 효과(추가 작업반)는 강화 없음");
            Assert.AreEqual(3, campaign.MaxEquipmentLevel(config.Equipment.Single(e => e.Id == "wellPump")));

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
            Assert.AreEqual(500.0, first.Cash, "시작 자금 500(2026-10-24 재미 보강 FB1, 이전 700)");
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
            Assert.AreEqual(chosen.Cash, campaign.TotalSaleCash, 1e-6, "기록: 누적 매각 대금(FB3)");
            Assert.AreEqual(campaign.Cash, campaign.PeakCash, 1e-6, "기록: 매각 직후 최대 자금");

            double before = campaign.Cash;
            Assert.IsTrue(campaign.BuyEquipment("wellPump").Success);
            Assert.AreEqual(before - 400, campaign.Cash, 1e-9);
            Assert.AreEqual(before, campaign.PeakCash, 1e-6, "장비를 사도 최대 자금 기록은 남는다");
            Assert.IsFalse(campaign.BuyEquipment("wellPump").Success, "중복 구매 불가");

            Settlement second = campaign.StartNextSettlement();
            Assert.AreEqual(2, campaign.SettlementNumber);
            Assert.AreEqual(_maps[1].Id, campaign.CurrentMap.Id, "다음 고정 맵");
            Assert.AreEqual(campaign.Cash, second.Cash, 1e-9, "자금 이월");
            Assert.AreEqual(48 + chosen.NextSettlementFoodBonus, second.Food, 1e-9, "식량은 이월 없이 새로 + 입찰자 혜택");
            Assert.AreEqual(75, second.Stone, 1e-9, "석재는 이월 없음");
            Assert.AreEqual(4.0 * second.Rules.Trade.NoMarketPriceMultiplier, second.NextPrice(false), 1e-9, "구매 단가 초기화(새 정착지는 시장이 없어 상인 값, 2026-10-27 이슈 #18)");
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
            rules.Labor = null; // 장비 배율만 본다: 우물이 사람 없이 다 퍼 올리게(일손 배율은 LaborTests)
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
