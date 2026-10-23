using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlementSim
{
    // Tools/desert_balance_sim_v08.py 의 C# 이식. 규칙 후보 비교를 위해 일부 처리를 옵션으로 뺐다.
    // 원본과 같은 옵션(V08 순서·FIFO·직렬 물 모델)이면 원본 회귀값과 같아야 한다.

    public enum TickOrder { V08CompleteFirst, CheckpointCompleteAtEnd }
    public enum QueuePolicy { FifoBlock, SkipBlocked }

    public sealed class LevelData
    {
        public double Stone, Hours, WaterSupply, WaterNeed, Yield, FoodUse, Upkeep, Capacity;
    }

    public sealed class Building
    {
        public string Kind;
        public int Level = 1;
        public string State = "operating"; // operating / constructing / upgrading / closed
        public int? Deadline;
        public int Target = 1;
    }

    public sealed class Job
    {
        public string Name, Action;
        public int Request;
    }

    public sealed class HourLog
    {
        public int H;
        public double Cash, Food, Stone, F, Ore, FoodProd, FoodValid, StoneProd, StoneValid, FoodDemand, WaterFarm, WaterQuarry, WaterMine, Deficit;
    }

    public sealed class Valuation
    {
        public double Ore, FoodAverage, StoneAverage, FoodMultiplier, StoneMultiplier, InfraMultiplier, BaseValue, Value, Bid;
    }

    public sealed class EconomySim
    {
        public static readonly Dictionary<string, LevelData[]> Levels = new Dictionary<string, LevelData[]>
        {
            ["well"] = new[] { L(8, 10, supply: 100, food: .25, upkeep: .60), L(18, 10, supply: 140, food: .30, upkeep: .90), L(30, 14, supply: 190, food: .40, upkeep: 1.25) },
            ["farm"] = new[] { L(12, 6, need: 30, yield: 4, food: .75, upkeep: .50), L(16, 8, need: 35, yield: 6, food: .95, upkeep: .75), L(26, 12, need: 40, yield: 8.5, food: 1.20, upkeep: 1.10) },
            ["quarry"] = new[] { L(14, 8, need: 25, yield: 1, food: .90, upkeep: .70), L(18, 9, need: 30, yield: 1.6, food: 1.10, upkeep: .95), L(28, 13, need: 35, yield: 2.4, food: 1.35, upkeep: 1.30) },
            ["mine"] = new[] { L(24, 12, need: 50, yield: 12, food: 1.80, upkeep: 1.50), L(28, 14, need: 50, yield: 18, food: 2.20, upkeep: 2.00), L(44, 18, need: 55, yield: 25, food: 2.80, upkeep: 2.90) },
            ["auction"] = new[] { L(18, 12, food: .20, upkeep: .60), L(20, 12, food: .25, upkeep: .80) },
            ["warehouse"] = new[] { L(8, 6, upkeep: .20, cap: 100), L(12, 8, upkeep: .35, cap: 200) },
            ["tank"] = new[] { L(10, 6, upkeep: .30, cap: 240), L(14, 8, upkeep: .50, cap: 420) },
            ["canal"] = new[] { L(2, 2, upkeep: .05, cap: 100), L(6, 3, upkeep: .08, cap: 150) },
            ["gate"] = new[] { L(3, 2, upkeep: .03) },
        };

        private static LevelData L(double stone, double hours, double supply = 0, double need = 0, double yield = 0,
            double food = 0, double upkeep = 0, double cap = 0) =>
            new LevelData { Stone = stone, Hours = hours, WaterSupply = supply, WaterNeed = need, Yield = yield, FoodUse = food, Upkeep = upkeep, Capacity = cap };

        public static readonly double LossHalf = Math.Sqrt(0.95);

        // 옵션
        public string Scenario;
        public TickOrder Order = TickOrder.V08CompleteFirst;
        public QueuePolicy Queue = QueuePolicy.FifoBlock;
        /// <summary>가치 평가에 저장 한도로 폐기된 생산분도 넣을지(확정 규칙 = true, 원본 v0.8 = false).</summary>
        public bool CountDiscardedInValuation;
        /// <summary>광물 24h 가치 계수(원본 v0.8 = 4.5, 설정 auctionSeed.oreProduction24hValueFactor).</summary>
        public double OreValueFactor = 4.5;
        /// <summary>물 모델. null이면 원본의 직렬 3구간 공식.</summary>
        public Func<EconomySim, Dictionary<string, double>> WaterModel;

        // 상태
        public double Cash, Stone, Food, F;
        public double TerrainMine = 1, EquipMining = 1;
        public double[] FlowFactors = { 1, 1, 1 };
        public Dictionary<string, Building> Structures = new Dictionary<string, Building>();
        public List<Job> Queued = new List<Job>();
        public Dictionary<int, List<Job>> Events = new Dictionary<int, List<Job>>();
        public Dictionary<int, int> PurchaseFood = new Dictionary<int, int>(), PurchaseStone = new Dictionary<int, int>();
        public Dictionary<string, int> CompletedHour = new Dictionary<string, int>();
        public Dictionary<string, int> StartedHour = new Dictionary<string, int>();
        public int BoughtFood, BoughtStone;
        public double PaidFood, PaidStone, OverflowFood, OverflowStone, Shortage;
        public int? FirstShortage, FirstAuction, FirstInsolvency;
        public List<HourLog> Log = new List<HourLog>();

        public EconomySim(string scenario, IEnumerable<(string name, string action, int time)> actions = null,
            double cash = 1000, double stone = 75, double food = 48, double f = 1)
        {
            Scenario = scenario;
            Cash = cash; Stone = stone; Food = food; F = f;
            Plan("well", "build", 0); Plan("farm", "build", 0);
            Plan("canal1", "build", 6); Plan("canal2", "build", 8);
            Plan("quarry", "build", 10); Plan("mine", "build", 10);
            Plan("canal3", "build", 18); Plan("auction", "build", 20);
            if (actions != null) foreach (var a in actions) Plan(a.name, a.action, a.time);
        }

        public static string KindOf(string name) => name.StartsWith("canal") ? "canal" : name;

        public void Plan(string name, string action, int time)
        {
            if (!Events.TryGetValue(time, out var list)) Events[time] = list = new List<Job>();
            list.Add(new Job { Name = name, Action = action, Request = time });
        }

        public LevelData Data(string name, int? level = null)
        {
            Structures.TryGetValue(name, out Building b);
            int lv = level ?? (b?.Level ?? 1);
            return Levels[KindOf(name)][lv - 1];
        }

        public double MaxCap()
        {
            double cap = 100;
            if (Structures.TryGetValue("warehouse", out Building b) && b.State == "operating") cap += Data("warehouse").Capacity;
            return cap;
        }

        public bool Active(string name) => Structures.TryGetValue(name, out Building b) && b.State == "operating";

        private void Complete(int h)
        {
            foreach (var kv in Structures)
            {
                Building b = kv.Value;
                if (b.Deadline != h) continue;
                if (b.State == "upgrading") b.Level = b.Target;
                b.State = "operating"; b.Deadline = null;
                CompletedHour[kv.Key] = h;
                if (kv.Key == "auction" && FirstAuction == null) FirstAuction = h + 96; // 원본 v0.8 스크립트 재현용(96h 고정). 게임은 설정값(72h)을 쓴다
            }
        }

        private void StartJobs(int h)
        {
            int current = Structures.Values.Count(b => b.State == "constructing" || b.State == "upgrading");
            int i = 0;
            while (i < Queued.Count && current < 2)
            {
                Job job = Queued[i];
                Structures.TryGetValue(job.Name, out Building b);
                if (job.Action == "build" && b != null) throw new InvalidOperationException("duplicate " + job.Name);
                bool blocked = false;
                LevelData target = null;
                int level = 1;
                if (job.Action == "upgrade" && (b == null || b.State != "operating")) blocked = true;
                else
                {
                    level = job.Action == "build" ? 1 : b.Level + 1;
                    target = Data(job.Name, level);
                    if (Stone + 1e-8 < target.Stone) blocked = true;
                }

                if (blocked)
                {
                    if (Queue == QueuePolicy.FifoBlock) break;
                    i++;
                    continue;
                }

                Queued.RemoveAt(i);
                Stone -= target.Stone;
                if (job.Action == "build") Structures[job.Name] = new Building { Kind = KindOf(job.Name), State = "constructing", Deadline = h + (int)target.Hours };
                else { b.State = "upgrading"; b.Target = level; b.Deadline = h + (int)target.Hours; }
                StartedHour[job.Name + ":" + job.Action + ":" + level] = h;
                current++;
            }
        }

        /// <summary>원본과 같은 직렬 물 모델: 우물 → canal1(농장) → canal2(채석장) → canal3(광산).</summary>
        public Dictionary<string, double> SerialWater()
        {
            var output = new Dictionary<string, double> { ["farm"] = 0, ["quarry"] = 0, ["mine"] = 0 };
            if (!Active("well")) return output;
            double q = Data("well").WaterSupply;
            string[] names = { "farm", "quarry", "mine" };
            for (int i = 0; i < 3; i++)
            {
                string edge = "canal" + (i + 1);
                if (!Active(edge)) break;
                q = Math.Min(q, Data(edge).Capacity);
                q *= LossHalf;
                if (Active(names[i]))
                {
                    double need = Data(names[i]).WaterNeed;
                    double take = Math.Min(need, q);
                    output[names[i]] = take / need;
                    q -= take;
                }
                q *= LossHalf;
            }
            return output;
        }

        private void Buy(int h)
        {
            if (!PurchaseFood.TryGetValue(h, out int amount) || amount <= 0) return;
            int buy = Math.Min(amount, Math.Max(0, (int)(MaxCap() - Food)));
            for (int i = 0; i < buy; i++)
            {
                double cost = 2 * Math.Pow(1.02, BoughtFood);
                if (Cash < cost) break;
                Cash -= cost; Food += 1; BoughtFood++; PaidFood += cost;
            }
        }

        private void BuyStone(int h)
        {
            PurchaseStone.TryGetValue(h, out int requested);
            int count = Math.Min(requested, Math.Max(0, (int)(MaxCap() - Stone)));
            for (int i = 0; i < count; i++)
            {
                double cost = 4 * Math.Pow(1.03, BoughtStone);
                if (Cash < cost) break;
                Cash -= cost; Stone += 1; BoughtStone++; PaidStone += cost;
            }
        }

        public void Tick(int h)
        {
            if (Order == TickOrder.V08CompleteFirst) Complete(h);

            if (Events.TryGetValue(h, out var events))
            {
                foreach (Job ev in events)
                {
                    if (ev.Action == "close" || ev.Action == "open")
                    {
                        if (!Structures.TryGetValue(ev.Name, out Building b)) throw new InvalidOperationException("Cannot toggle missing " + ev.Name);
                        if (ev.Action == "close" && b.State == "operating") b.State = "closed";
                        else if (ev.Action == "open" && b.State == "closed") b.State = "operating";
                    }
                    else Queued.Add(ev);
                }
            }
            BuyStone(h);
            StartJobs(h);
            Buy(h);

            Dictionary<string, double> water = WaterModel != null ? WaterModel(this) : SerialWater();
            string[] wn = { "farm", "quarry", "mine" };
            for (int i = 0; i < 3; i++) water[wn[i]] = water.GetValueOrDefault(wn[i]) * FlowFactors[i];

            double demand = 0, upkeep = 0, pFood = 0, pStone = 0, pOre = 0;
            foreach (var kv in Structures)
            {
                Building b = kv.Value;
                if (b.State == "constructing") continue;
                LevelData d = Data(kv.Key);
                if (b.State == "operating")
                {
                    demand += d.FoodUse;
                    upkeep += d.Upkeep;
                    if (kv.Key == "farm") pFood += d.Yield * water["farm"] * F;
                    if (kv.Key == "quarry") pStone += d.Yield * water["quarry"] * F;
                    if (kv.Key == "mine") pOre += d.Yield * water["mine"] * F * TerrainMine * EquipMining;
                }
                else upkeep += 0.3 * d.Upkeep;
            }
            Cash -= upkeep;
            if (Cash < 0 && FirstInsolvency == null) FirstInsolvency = h;

            double deficit = Math.Max(0, demand - (Food + pFood));
            double unboundedFood = Math.Max(0, Food + pFood - demand);
            double cap = MaxCap();
            OverflowFood += Math.Max(0, unboundedFood - cap);
            double creditedFood = Math.Max(0, pFood - Math.Max(0, unboundedFood - cap));
            Food = Math.Min(cap, unboundedFood);
            OverflowStone += Math.Max(0, Stone + pStone - cap);
            double creditedStone = Math.Max(0, pStone - Math.Max(0, Stone + pStone - cap));
            Stone = Math.Min(cap, Stone + pStone);
            Shortage += deficit;
            if (deficit > 1e-9)
            {
                FirstShortage ??= h;
                F = Math.Max(0, F - 0.2 / 24 * Math.Min(1, deficit / (0.25 * demand)));
            }
            else if (demand > 0 && Active("farm") && water["farm"] > 0)
            {
                F = Math.Min(1, F + 0.3 / 24);
            }

            Log.Add(new HourLog
            {
                H = h, Cash = Cash, Food = Food, Stone = Stone, F = F, Ore = pOre, FoodProd = pFood, FoodValid = creditedFood,
                StoneProd = pStone, StoneValid = creditedStone, FoodDemand = demand,
                WaterFarm = water["farm"], WaterQuarry = water["quarry"], WaterMine = water["mine"], Deficit = deficit,
            });

            // 기획서 4.2 초안 순서: 시간대 끝에 작업 완료 처리 → 다음 시간대부터 유효.
            if (Order == TickOrder.CheckpointCompleteAtEnd) Complete(h + 1);
        }

        public EconomySim Run(int until)
        {
            for (int h = 0; h < until; h++) Tick(h);
            return this;
        }

        public Valuation Value(int h, double multiplier = 1.05)
        {
            List<HourLog> q = Log.Skip(Math.Max(0, h - 24)).Take(Math.Min(24, h)).ToList();
            if (q.Count < 24) throw new InvalidOperationException("not a full 24h window");
            double ore = q.Sum(x => x.Ore);
            // 원본 v0.8: 저장 한도로 폐기된 양 제외. 2026-10-22 확정 규칙: 폐기분 포함(CountDiscardedInValuation).
            double food = q.Sum(x => CountDiscardedInValuation ? x.FoodProd : x.FoodValid) / 24;
            double stone = q.Sum(x => CountDiscardedInValuation ? x.StoneProd : x.StoneValid) / 24;
            double mf = 1 + 0.50 * Math.Min(1, food / 8);
            double ms = 1 + 1.00 * Math.Min(1, stone / 2);
            double infra = 1 + (Active("well") ? 0.04 : 0) + (Active("auction") ? 0.01 : 0)
                + Enumerable.Range(1, 3).Count(i => Active("canal" + i)) * 0.01 + (Active("warehouse") ? 0.025 : 0);
            infra = Math.Min(1.20, infra);
            double baseValue = OreValueFactor * ore;
            return new Valuation
            {
                Ore = ore, FoodAverage = food, StoneAverage = stone, FoodMultiplier = mf, StoneMultiplier = ms, InfraMultiplier = infra,
                BaseValue = baseValue, Value = baseValue * mf * ms * infra, Bid = baseValue * mf * ms * infra * multiplier,
            };
        }
    }
}
