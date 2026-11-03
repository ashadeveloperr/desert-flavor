using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 식품 창고에 쌓이는 재화(결정 FD1~). 곡물·과일·빵·고기는 식량(주민이 먹음), 맥주·와인은 기호품(주민이 마심), 향신료는 상위 재화와 함께 소모.
    /// 수치(식량 효율·소비 가중치·등급 점수·보관 순위·판매가)는 설정 JSON <c>goods</c>(<see cref="GoodsSettings"/>), 키는 <see cref="GoodsCatalog.Id"/>.
    /// </summary>
    public enum Goods
    {
        Grain,
        Fruit,
        Bread,
        Meat,
        Beer,
        Wine,
        Spice,
    }

    /// <summary>재화 목록·이름과 설정 키. 새 재화 = 이 enum + 아래 표 한 줄 + JSON <c>goods</c> 한 항목.</summary>
    public static class GoodsCatalog
    {
        public static readonly Goods[] All = (Goods[])Enum.GetValues(typeof(Goods));
        /// <summary>주민이 먹는 식량(소비 가중치로 함께 먹음).</summary>
        public static readonly Goods[] Foods = { Goods.Grain, Goods.Fruit, Goods.Bread, Goods.Meat };
        /// <summary>주민이 마시는 기호품.</summary>
        public static readonly Goods[] Drinks = { Goods.Beer, Goods.Wine };

        private static readonly (Goods Goods, string Id, string Name)[] Table =
        {
            (Goods.Grain, "grain", "곡물"),
            (Goods.Fruit, "fruit", "과일"),
            (Goods.Bread, "bread", "빵"),
            (Goods.Meat, "meat", "고기"),
            (Goods.Beer, "beer", "맥주"),
            (Goods.Wine, "wine", "와인"),
            (Goods.Spice, "spice", "향신료"),
        };

        /// <summary>설정 JSON <c>goods</c>의 키.</summary>
        public static string Id(Goods g) => Table[(int)g].Id;

        /// <summary>화면에 보이는 한국어 이름.</summary>
        public static string Name(Goods g) => Table[(int)g].Name;

        public static Goods? FromId(string id)
        {
            foreach ((Goods goods, string key, string _) in Table) if (key == id) return goods;
            return null;
        }

        public static bool IsFood(Goods g) => Array.IndexOf(Foods, g) >= 0;
        public static bool IsDrink(Goods g) => Array.IndexOf(Drinks, g) >= 0;

        /// <summary>설정에서 이 재화의 수치. 없으면 기본(효율 1, 가중치 0, 보관 순위 0).</summary>
        public static GoodsSettings Settings(EconomyConfig config, Goods g) =>
            config?.Goods != null && config.Goods.TryGetValue(Id(g), out GoodsSettings s) && s != null ? s : GoodsSettings.Default;
    }
}
