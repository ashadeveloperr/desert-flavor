using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 식품 창고와 재화(결정 FD1~FD8). 곡물·과일·빵·고기(식량), 맥주·와인(기호품), 향신료가 한 창고 한도(<see cref="FoodCapacity"/>)를 함께 쓴다.
    /// - 먹기(<see cref="Eat"/>): 재고가 있는 식량끼리 소비 가중치 비율로 식량 수요를 나눠 먹고, 모자란 몫은 다른 식량이 채운다. 실제 소비량 = 맡은 몫 ÷ 식량 효율.
    /// - 마시기: 기호품도 같은 방식(1인당 수요, 없어도 배고프지 않음).
    /// - 가공(<see cref="Process"/>): 주민이 먼저 먹고 남은 원료로 목축지·제분소·양조장·포도주 공방이 만든다(시설 번호 순).
    /// - 넘침(<see cref="DiscardOverflow"/>): 창고가 넘치면 보관 순위가 낮은 재화(곡물 → 과일 → 빵·맥주 → 고기·와인·향신료)부터 버린다.
    /// 원본 v0.8(곡물만)과 같은 결과가 나오도록 곡물 하나일 때는 예전 '식량' 계산과 같다. 정착지 전체 설명은 Settlement.cs.
    /// </summary>
    public sealed partial class Settlement
    {
        private readonly double[] _stock = new double[GoodsCatalog.All.Length];

        /// <summary>재화 재고.</summary>
        public double Stock(Goods g) => _stock[(int)g];

        /// <summary>식량 재고 합(곡물·과일·빵·고기). 곡물만 있는 원본 v0.8에서는 예전 '식량'과 같다.</summary>
        public double Food => GoodsCatalog.Foods.Sum(g => _stock[(int)g]);

        /// <summary>식품 창고에 든 양(모든 재화 합). 한도는 <see cref="FoodCapacity"/>.</summary>
        public double PantryUsed => _stock.Sum();

        /// <summary>메뚜기떼로 버려진 양을 뺀, 창고가 넘쳐 버려진 재화 합(재화별 <see cref="DiscardedOf"/>).</summary>
        public double FoodDiscarded => _discarded.Sum();
        private readonly double[] _discarded = new double[GoodsCatalog.All.Length];
        public double DiscardedOf(Goods g) => _discarded[(int)g];

        private void AddGoods(Goods g, double amount) => _stock[(int)g] = Math.Max(0, _stock[(int)g] + amount);

        /// <summary>식량 재고를 비율만큼 잃는다(메뚜기떼, 결정 D4·FD8). 술·향신료는 안전.</summary>
        internal void LoseFood(double fraction)
        {
            double keep = 1 - Math.Max(0, Math.Min(1, fraction));
            foreach (Goods g in GoodsCatalog.Foods) _stock[(int)g] *= keep;
        }

        private GoodsSettings SettingsOf(Goods g) => GoodsCatalog.Settings(Rules.Config, g);

        /// <summary>
        /// 가중치 비율로 수요를 나눠 소비한다. value(g) = 1단위가 채우는 몫, weight(g) = 소비 가중치.
        /// 재고가 바닥난 재화의 몫은 남은 재화끼리 다시 나눈다. 재화별 소비 몫(채운 양)을 eaten에 더하고, 채운 합을 돌려준다.
        /// </summary>
        private double Consume(double demand, Goods[] goods, Func<Goods, double> value, Func<Goods, double> weight, double[] eaten)
        {
            double filled = 0;
            for (int round = 0; round < goods.Length && demand - filled > 1e-12; round++)
            {
                List<Goods> available = goods.Where(g => _stock[(int)g] > 1e-12 && value(g) > 0 && weight(g) > 0).ToList();
                if (available.Count == 0) break;
                double total = available.Sum(weight);
                double remaining = demand - filled;
                foreach (Goods g in available)
                {
                    double want = remaining * weight(g) / total;                // 맡은 몫
                    double units = Math.Min(_stock[(int)g], want / value(g));     // 실제 소비량
                    _stock[(int)g] -= units;
                    filled += units * value(g);
                    eaten[(int)g] += units * value(g);
                }
            }
            return filled;
        }

        /// <summary>
        /// 식량 수요(식량 단위)를 먹는다. 가중치가 하나도 없으면(원본 v0.8 설정) 곡물만 먹는다. 채운 식량을 돌려준다.
        /// </summary>
        private double Eat(double demand, double[] eaten)
        {
            if (demand <= 0) return 0;
            bool weighted = GoodsCatalog.Foods.Any(g => SettingsOf(g).EatWeight > 0);
            return weighted
                ? Consume(demand, GoodsCatalog.Foods, FoodValueOf, g => SettingsOf(g).EatWeight, eaten)
                : Consume(demand, new[] { Goods.Grain }, _ => 1, _ => 1, eaten);
        }

        private double FoodValueOf(Goods g) => Rules.FoodValue(g);

        /// <summary>기호품 수요(단위)를 마신다(맥주·와인, 가중치 비율). 채운 양을 돌려준다.</summary>
        private double Drink(double demand, double[] drunk) =>
            demand <= 0 ? 0 : Consume(demand, GoodsCatalog.Drinks, _ => 1, g => SettingsOf(g).DrinkWeight, drunk);

        /// <summary>
        /// 가공 시설: 시간당 능력(최대 × 물 × 일손 × 재해) 중 남은 원료로 만들 수 있는 만큼 만든다(시설 번호 순).
        /// 만든 양을 produced에 더하고, 원료가 모자란 비율을 시설에 남긴다(화면 표시).
        /// </summary>
        private void Process(Facility f, double capacity, double[] produced, ref double netFood, ref double netFoodValued, double maturity)
        {
            Goods output = SettlementRules.GoodsOf(f.Kind).Value;
            Goods input = SettlementRules.InputOf(f.Kind).Value;
            double per = LevelOf(f).InputPerOutput ?? 0;
            double made = capacity;
            if (per > 0) made = Math.Min(capacity, _stock[(int)input] / per);
            f.InputRatio = capacity > 1e-12 ? made / capacity : 1;
            _stock[(int)input] -= made * per;
            AddGoods(output, made);
            produced[(int)output] += made;
            double net = Rules.NetFoodValue(f.Kind, made, LevelOf(f));
            netFood += net;
            netFoodValued += net * maturity;
        }

        /// <summary>
        /// 창고가 넘치면 보관 순위가 낮은 재화부터 버린다(같은 순위는 재화 순서). 버린 식량 가치(곡물 1 기준)를 돌려준다.
        /// </summary>
        private double DiscardOverflow()
        {
            double over = PantryUsed - FoodCapacity;
            double lostFood = 0;
            foreach (Goods g in GoodsCatalog.All.OrderBy(x => SettingsOf(x).KeepRank).ThenBy(x => (int)x))
            {
                if (over <= 1e-12) break;
                double drop = Math.Min(over, _stock[(int)g]);
                _stock[(int)g] -= drop;
                _discarded[(int)g] += drop;
                over -= drop;
                lostFood += drop * (GoodsCatalog.IsFood(g) ? Math.Max(Rules.FoodValue(g), 0) : 0);
            }
            return lostFood;
        }
    }
}
