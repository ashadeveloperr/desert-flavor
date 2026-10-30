using System;
using System.Collections.Generic;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using Newtonsoft.Json;

namespace DesertSettlement.Core.Progression
{
    /// <summary>
    /// 성공한 플레이어 명령 하나(저장 파일 한 줄). 어느 정착지의 몇 시간째(<see cref="Hour"/>)에 무엇을 했는지.
    /// 쓰지 않는 칸은 0/null(결정 S1~S5). <see cref="Index"/>는 종류마다 뜻이 달라서, 그 칸을 쓰는 명령은 아래 For… 생성 함수로 만들고
    /// 이름 있는 속성(<see cref="CanalEdge"/>·<see cref="NewQueuedIndex"/>·<see cref="OfferIndex"/>·<see cref="BidIndex"/>)으로 읽는다(결정 SD2, 저장 형식은 그대로).
    /// </summary>
    public sealed class GameCommand
    {
        public const string Start = "start";                // 다음 정착지로(Index = 지역 후보)
        public const string Build = "build";                // Kind, Q, R
        public const string Canal = "canal";                // Q, R, Index = 변 방향
        public const string Upgrade = "upgrade";            // Id = 시설
        public const string Demolish = "demolish";          // Id
        public const string Cancel = "cancel";              // Id = 작업
        public const string Move = "move";                  // Id = 작업, Index = 새 위치
        public const string Close = "close";                // Id
        public const string Open = "open";                  // Id
        public const string BuyFood = "buyFood";            // Amount
        public const string BuyStone = "buyStone";          // Amount
        public const string SellFood = "sellFood";          // Amount (2026-10-27 시장 판매, 이슈 #18)
        public const string SellStone = "sellStone";        // Amount
        public const string Hire = "hire";                  // Id = 시설 (우선 고용, 결정 LB7)
        public const string Unhire = "unhire";              // Id
        public const string SellGoods = "sellGoods";        // Kind = 재화 id, Amount (곡물 밖의 재화 판매, 결정 FD6)
        public const string Sell = "sell";                  // Index = 입찰
        public const string Hold = "hold";
        public const string BuyEquipment = "buyEquipment";  // Kind = 장비 id
        public const string UpgradeEquipment = "upgradeEquipment";
        public const string Equip = "equip";                // Kind
        public const string Unequip = "unequip";            // Kind

        public int Settlement { get; set; }
        public long Hour { get; set; }
        public string Type { get; set; }
        public string Kind { get; set; }
        public int Q { get; set; }
        public int R { get; set; }
        public int Id { get; set; }
        public int Index { get; set; }
        public int Amount { get; set; }

        public static GameCommand ForCanal(EdgeId edge) =>
            new GameCommand { Type = Canal, Q = edge.Owner.Q, R = edge.Owner.R, Index = (int)edge.Side };
        public static GameCommand ForMove(int jobId, int newQueuedIndex) => new GameCommand { Type = Move, Id = jobId, Index = newQueuedIndex };
        public static GameCommand ForStart(int settlementNumber, int offerIndex) =>
            new GameCommand { Type = Start, Settlement = settlementNumber, Index = offerIndex };
        public static GameCommand ForSell(long hour, int bidIndex) => new GameCommand { Type = Sell, Hour = hour, Index = bidIndex };

        /// <summary>수로 명령의 변(Q·R 타일 + Index 방향).</summary>
        [JsonIgnore] public EdgeId CanalEdge => EdgeId.Of(new HexCoord(Q, R), (HexDirection)Index);
        /// <summary>순서 변경 명령의 새 위치('대기 중 작업들' 사이, 0 = 맨 앞).</summary>
        [JsonIgnore] public int NewQueuedIndex => Index;
        /// <summary>다음 정착지 출발 명령의 지역 후보 번호.</summary>
        [JsonIgnore] public int OfferIndex => Index;
        /// <summary>매각 명령의 입찰 번호.</summary>
        [JsonIgnore] public int BidIndex => Index;
    }

    /// <summary>저장 파일 내용. 캠페인 시드 + 명령 기록 + 저장한 순간의 위치(정착지 번호·시간).</summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public int Seed { get; set; }
        public int SettlementNumber { get; set; }
        /// <summary>저장한 순간 지금 정착지의 게임 시간(마지막 명령 뒤로 이만큼 시간을 더 진행).</summary>
        public long Hour { get; set; }
        /// <summary>표시용(저장 시각, 설명).</summary>
        public string SavedAt { get; set; }
        public string Summary { get; set; }
        public List<GameCommand> Commands { get; set; } = new List<GameCommand>();
    }

    /// <summary>
    /// 저장·불러오기. 상태를 통째로 쓰지 않고 '같은 시드로 같은 명령을 같은 시각에 다시 실행'해서 되살린다
    /// (Core는 시드가 같으면 결과가 같다: 경매 입찰자·시세, 재해, 생성 맵 모두 시드 결정론). 파일 읽기·쓰기는 <see cref="SaveGameJson"/>.
    /// 설정 JSON이나 규칙 코드가 바뀌면 같은 명령도 다른 결과가 날 수 있다 — 그때 실패한 명령은 건너뛰고 <see cref="ReplayResult.Problems"/>에 남긴다.
    /// </summary>
    public static class SaveGame
    {
        public static SaveData Capture(Campaign c, string savedAt = null)
        {
            return new SaveData
            {
                Seed = c.Seed,
                SettlementNumber = c.SettlementNumber,
                Hour = c.Current?.CurrentHour ?? 0,
                SavedAt = savedAt,
                Summary = $"정착지 {c.SettlementNumber}, {c.Current?.CurrentHour ?? 0}시간째, 자금 {(c.State == CampaignState.InSettlement ? c.Current.Cash : c.Cash):0}",
                Commands = new List<GameCommand>(c.CommandLog),
            };
        }

        public sealed class ReplayResult
        {
            public Campaign Campaign;
            public List<string> Problems = new List<string>();
        }

        public static ReplayResult Replay(EconomyConfig config, IReadOnlyList<MapDefinition> maps, ProceduralMapConfig procedural, SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Version > SaveData.CurrentVersion) throw new InvalidOperationException($"더 새 버전의 저장 파일입니다(v{data.Version}).");
            var result = new ReplayResult { Campaign = new Campaign(config, maps, data.Seed, procedural) };
            Campaign c = result.Campaign;
            foreach (GameCommand cmd in data.Commands)
            {
                try
                {
                    if (!Apply(c, cmd)) result.Problems.Add($"정착지 {cmd.Settlement} {cmd.Hour}시간째 {cmd.Type} 실패(건너뜀)");
                }
                catch (Exception e)
                {
                    result.Problems.Add($"정착지 {cmd.Settlement} {cmd.Hour}시간째 {cmd.Type}: {e.Message}");
                }
            }
            if (c.State == CampaignState.InSettlement && c.SettlementNumber == data.SettlementNumber) AdvanceTo(c, data.Hour);
            if (c.SettlementNumber != data.SettlementNumber) result.Problems.Add($"정착지 번호가 다릅니다(저장 {data.SettlementNumber}, 복원 {c.SettlementNumber}).");
            return result;
        }

        /// <summary>게임에서처럼 시간을 진행: 경매가 열리면 멈춘다(플레이어 결정을 기다림).</summary>
        private static void AdvanceTo(Campaign c, long hour)
        {
            while (c.State == CampaignState.InSettlement && c.Current.CurrentHour < hour && !c.Auctions.IsOpen && !c.Auctions.IsFinished)
                c.Current.AdvanceHour();
        }

        private static bool Apply(Campaign c, GameCommand cmd)
        {
            switch (cmd.Type)
            {
                case GameCommand.Start: c.StartNextSettlement(cmd.OfferIndex); return true;
                case GameCommand.BuyEquipment: return c.BuyEquipment(cmd.Kind).Success;
                case GameCommand.UpgradeEquipment: return c.UpgradeEquipment(cmd.Kind).Success;
                case GameCommand.Equip: return c.SetEquipped(cmd.Kind, true).Success;
                case GameCommand.Unequip: return c.SetEquipped(cmd.Kind, false).Success;
            }

            if (c.State != CampaignState.InSettlement || c.SettlementNumber != cmd.Settlement) return false;
            AdvanceTo(c, cmd.Hour);
            Settlement s = c.Current;
            switch (cmd.Type)
            {
                case GameCommand.Build: return s.RequestBuild(cmd.Kind, new HexCoord(cmd.Q, cmd.R)).Success;
                case GameCommand.Canal: return s.RequestCanal(cmd.CanalEdge).Success;
                case GameCommand.Upgrade: return s.RequestUpgrade(cmd.Id).Success;
                case GameCommand.Demolish: return s.RequestDemolish(cmd.Id).Success;
                case GameCommand.Cancel: return s.CancelJob(cmd.Id).Success;
                case GameCommand.Move: return s.MoveQueuedJob(cmd.Id, cmd.NewQueuedIndex).Success;
                case GameCommand.Close: return s.SetClosed(cmd.Id, true).Success;
                case GameCommand.Open: return s.SetClosed(cmd.Id, false).Success;
                case GameCommand.BuyFood: return s.BuyFood(cmd.Amount) > 0;
                case GameCommand.BuyStone: return s.BuyStone(cmd.Amount) > 0;
                case GameCommand.SellFood: return s.SellFood(cmd.Amount) > 0;
                case GameCommand.SellStone: return s.SellStone(cmd.Amount) > 0;
                case GameCommand.Hire: return s.PrioritizeHiring(cmd.Id).Success;
                case GameCommand.Unhire: return s.ClearHiringPriority(cmd.Id).Success;
                case GameCommand.SellGoods: return GoodsCatalog.FromId(cmd.Kind) is Goods g && s.SellGoods(g, cmd.Amount) > 0;
                case GameCommand.Sell:
                    if (!c.Auctions.IsOpen) return false;
                    c.Auctions.Sell(cmd.BidIndex);
                    return true;
                case GameCommand.Hold:
                    if (!c.Auctions.IsOpen) return false;
                    c.Auctions.Hold();
                    return true;
                default:
                    throw new InvalidOperationException($"알 수 없는 명령: {cmd.Type}");
            }
        }
    }
}
