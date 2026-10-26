using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DesertSettlement.Core.Config
{
    /// <summary>설정 파일이 잘못됐을 때 발생. 메시지에 문제 목록이 모두 들어 있다.</summary>
    public sealed class ConfigValidationException : Exception
    {
        public IReadOnlyList<string> Problems { get; }

        public ConfigValidationException(IReadOnlyList<string> problems)
            : base("경제 설정 파일 오류:\n- " + string.Join("\n- ", problems))
        {
            Problems = problems;
        }
    }

    /// <summary>
    /// 경제 설정 JSON 문자열을 읽고 검증한다. 파일 읽기는 호출하는 쪽(Unity에서는 TextAsset)이 한다.
    /// 검증 규칙을 추가하려면 <see cref="Validate"/>에 한 줄씩 추가하면 된다.
    /// </summary>
    public static class EconomyConfigLoader
    {
        /// <summary>지원하는 설정 스키마. JSON의 "schema" 값과 같아야 한다.</summary>
        public const string SupportedSchema = "desert-settlement-economy-v0.8";

        public static EconomyConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ConfigValidationException(new[] { "설정 내용이 비어 있습니다." });
            }

            EconomyConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<EconomyConfig>(json);
            }
            catch (JsonException e)
            {
                throw new ConfigValidationException(new[] { "JSON 형식 오류: " + e.Message });
            }

            List<string> problems = Validate(config);
            if (problems.Count > 0) throw new ConfigValidationException(problems);
            return config;
        }

        public static List<string> Validate(EconomyConfig config)
        {
            var problems = new List<string>();
            if (config == null)
            {
                problems.Add("설정을 읽을 수 없습니다.");
                return problems;
            }

            if (config.Schema != SupportedSchema)
            {
                problems.Add($"schema가 '{config.Schema}'입니다. 지원: '{SupportedSchema}'.");
            }

            ConfirmedRules rules = config.ConfirmedRules;
            if (rules == null)
            {
                problems.Add("confirmedRules가 없습니다.");
            }
            else
            {
                if (rules.GameDayRealSecondsAt1x <= 0) problems.Add("confirmedRules.gameDayRealSecondsAt1x는 0보다 커야 합니다.");
                if (rules.ConstructionSlots <= 0) problems.Add("confirmedRules.constructionSlots는 1 이상이어야 합니다.");
                if (rules.PostStartCancelStoneRefundRatio < 0 || rules.PostStartCancelStoneRefundRatio > 1)
                {
                    problems.Add("confirmedRules.postStartCancelStoneRefundRatio는 0~1이어야 합니다.");
                }
                if (rules.AuctionHistoryGameHours <= 0) problems.Add("confirmedRules.auctionHistoryGameHours는 0보다 커야 합니다.");
                if (rules.AuctionRepeatsGameHours <= 0) problems.Add("confirmedRules.auctionRepeatsGameHours는 0보다 커야 합니다.");
            }

            if (config.Prototype == null)
            {
                problems.Add("prototype이 없습니다.");
            }
            else if (config.Prototype.EconomicTickGameHours <= 0)
            {
                problems.Add("prototype.economicTickGameHours는 1 이상이어야 합니다.");
            }

            AuctionSettings auction = config.Auction;
            if (auction == null)
            {
                problems.Add("auction이 없습니다.");
            }
            else
            {
                if (auction.Bidders == null || auction.Bidders.Count == 0) problems.Add("auction.bidders가 비어 있습니다.");
                if (auction.AttendeesMin < 1 || auction.AttendeesMax < auction.AttendeesMin) problems.Add("auction.attendeesMin/Max 범위가 잘못됐습니다.");
                if (auction.AttendeesWithOneAuctionHall < 1) problems.Add("auction.attendeesWithOneAuctionHall은 1 이상이어야 합니다.");
                if (auction.MarketSwing < 0 || auction.MarketSwing >= 1) problems.Add("auction.marketSwing은 0 이상 1 미만이어야 합니다.");
                if (auction.EmergencyPenalty < 0 || auction.EmergencyPenalty >= 1) problems.Add("auction.emergencyPenalty는 0 이상 1 미만이어야 합니다.");
                if (auction.BankruptcyGraceHours < 1) problems.Add("auction.bankruptcyGraceHours는 1 이상이어야 합니다.");
                if (auction.Bidders != null)
                {
                    foreach (BidderProfile b in auction.Bidders)
                    {
                        if (string.IsNullOrEmpty(b.Id)) problems.Add("auction.bidders에 id 없는 입찰자가 있습니다.");
                        if (b.CashShare <= 0 || b.CashShare > 1) problems.Add($"auction.bidders.{b.Id}.cashShare는 0 초과 1 이하여야 합니다.");
                    }
                }
            }
            if (config.Equipment != null)
            {
                foreach (EquipmentDefinition e in config.Equipment)
                {
                    if (string.IsNullOrEmpty(e.Id)) problems.Add("equipment에 id 없는 장비가 있습니다.");
                    if (e.Price < 0) problems.Add($"equipment.{e.Id}.price는 음수일 수 없습니다.");
                }
            }

            if (config.BuildingLevels == null || config.BuildingLevels.Count == 0)
            {
                problems.Add("buildingLevels가 비어 있습니다.");
                return problems;
            }

            foreach (string kind in BuildingKinds.All)
            {
                if (!config.BuildingLevels.ContainsKey(kind)) problems.Add($"buildingLevels.{kind}가 없습니다.");
            }

            foreach (KeyValuePair<string, List<BuildingLevel>> pair in config.BuildingLevels)
            {
                if (pair.Value == null || pair.Value.Count == 0)
                {
                    problems.Add($"buildingLevels.{pair.Key}에 레벨이 없습니다.");
                    continue;
                }

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    BuildingLevel level = pair.Value[i];
                    string where = $"buildingLevels.{pair.Key}[L{i + 1}]";
                    if (level == null)
                    {
                        problems.Add($"{where}가 비어 있습니다.");
                        continue;
                    }
                    RequireNonNegative(problems, where, "stone", level.Stone);
                    RequirePositive(problems, where, "hours", level.Hours);
                    RequireNonNegative(problems, where, "foodUse", level.FoodUse);
                    RequireNonNegative(problems, where, "upkeep", level.Upkeep);
                    if (level.LossPerWholeEdge.HasValue && (level.LossPerWholeEdge < 0 || level.LossPerWholeEdge >= 1))
                    {
                        problems.Add($"{where}.lossPerWholeEdge는 0 이상 1 미만이어야 합니다.");
                    }
                }
            }

            return problems;
        }

        private static void RequireNonNegative(List<string> problems, string where, string field, double? value)
        {
            if (!value.HasValue) problems.Add($"{where}.{field}가 없습니다.");
            else if (value.Value < 0) problems.Add($"{where}.{field}는 음수일 수 없습니다: {value.Value}");
        }

        private static void RequirePositive(List<string> problems, string where, string field, double? value)
        {
            if (!value.HasValue) problems.Add($"{where}.{field}가 없습니다.");
            else if (value.Value <= 0) problems.Add($"{where}.{field}는 0보다 커야 합니다: {value.Value}");
        }
    }
}
