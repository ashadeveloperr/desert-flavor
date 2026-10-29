using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Progression;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>
    /// EditMode 테스트가 읽는 데이터 파일(설정·맵)의 경로와 불러오기(결정 TD1). 파일 위치는 여기 한 곳에만 적는다.
    /// 불러오기 함수는 부를 때마다 새로 읽으므로, 테스트가 값을 바꿔도 다른 테스트에 번지지 않는다.
    /// </summary>
    internal static class TestData
    {
        public static string DataFolder => Path.Combine(Application.dataPath, "Data");
        public static string EconomyConfigPath => Path.Combine(DataFolder, "Config", "economy_balance_v0.8_config.json");
        public static string ProceduralConfigPath => Path.Combine(DataFolder, "Config", "procedural_map_config.json");
        public static string MapsFolder => Path.Combine(DataFolder, "Maps");

        public static string EconomyJson => File.ReadAllText(EconomyConfigPath);
        public static string ProceduralJson => File.ReadAllText(ProceduralConfigPath);

        public static EconomyConfig Economy() => EconomyConfigLoader.Parse(EconomyJson);
        public static ProceduralMapConfig Procedural() => ProceduralMapConfigLoader.Parse(ProceduralJson);

        /// <summary>고정 맵 파일 경로(이름순 = 캠페인 순서).</summary>
        public static IEnumerable<string> MapPaths => Directory.GetFiles(MapsFolder, "*.json").OrderBy(p => p, StringComparer.Ordinal);

        public static List<MapDefinition> Maps() => MapPaths.Select(p => MapLoader.Parse(File.ReadAllText(p))).ToList();
    }

    /// <summary>
    /// 일부러 고정한 회귀값(결정 TD2): 원본 v0.8 경제 스크립트를 <c>SettlementRules.ForV08Regression</c>으로 돌린 결과이고,
    /// 시뮬레이터의 v0.8 이식본 교차 검증과 같은 값이다. 규칙을 바꾸지 않았는데 달라지면 버그다.
    /// 규칙을 일부러 바꿔 달라지면 시뮬레이터와 함께 원인을 확인한 뒤에만 고친다.
    /// </summary>
    internal static class V08Golden
    {
        /// <summary>첫 경매 시각(경매장 37h + 원본 경매 간격 96h). 게임의 경매 간격(지금 48h)과는 별개인 v0.8 기준 시점이다.</summary>
        public const int FirstAuctionHour = 133;
        public const int AuctionHallCompletedHour = 37;
        public const double CashBeforeFirstAuction = 540.05;
        public const double MineWaterRate = 0.7885;
        public const double FoodAtFirstAuction = 91.5;
        public const double StoneAtFirstAuction = 100.0;
        public const double StoneDiscarded = 8.0;
        /// <summary>첫 경매 기준 가치 V(광물 가치 계수 2.5로 평가).</summary>
        public const double FirstAuctionValue = 1149.63;
        /// <summary>첫 경매에서 즉시 매각한 뒤 현금(중립 입찰 배율 auctionSeed.fixedTestBidMultiplier).</summary>
        public const double CashAfterSellingAtFirstAuction = 1747.16;
        /// <summary>두 번째 경매 시각과, 첫 경매 때 광산 L2 + 창고를 지으며 보류한 뒤 매각한 현금.</summary>
        public const int SecondAuctionHour = 229;
        public const double CashAfterHoldWithMineUpgradeAndWarehouse = 1959.54;
    }
}
