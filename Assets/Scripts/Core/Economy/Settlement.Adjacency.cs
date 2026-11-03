using System;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Hex;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 인접 보너스(결정 AJ1~AJ4)와 식품 부패율(결정 DC1~DC2). 수치는 설정 <c>adjacency</c>·<c>spoilage</c>, 없으면 보너스·부패 없음.
    /// 맞닿은 6칸의 운영 중 시설만 센다(공사·개량·폐쇄·철거 중은 빠짐). 한 시설이 받는 보너스는 모두 더한다.
    /// - 군집: 원료 생산 시설(<c>adjacency.clusterKinds</c>) 옆 같은 종류 1칸당 생산 + <c>clusterOutputPerNeighbor</c>.
    /// - 연계: 가공 시설 옆 원료 시설(<c>adjacency.inputSources</c>) 1칸당 원료 소모 − <c>inputSavingPerNeighbor</c>, 상한 <c>inputSavingMax</c>.
    /// - 주택 밀집: 맞닿은 주택 1칸당 주민 유입 속도 + <c>houseArrivalPerNeighbor</c>.
    /// - 창고 밀집: 식품 부패율 = <c>spoilage.foodPerDay</c> − 창고들의 이웃 창고 수 평균 × <c>warehouseNeighborReductionPerDay</c>.
    /// 건설 미리보기도 같은 계산을 쓴다(아직 없는 시설은 kind·tile로 묻는다).
    /// </summary>
    public sealed partial class Settlement
    {
        private AdjacencySettings Adjacency => Rules.Adjacency;

        /// <summary>tile에 맞닿은 6칸 중 kind 종류의 운영 중 시설 수.</summary>
        public int NeighborCount(HexCoord tile, string kind)
        {
            int n = 0;
            for (int d = 0; d < 6; d++)
            {
                Facility f = FacilityAt(tile.Neighbor((HexDirection)d));
                if (f != null && f.Kind == kind && f.IsOperating) n++;
            }
            return n;
        }

        /// <summary>tile에 kind 시설이 있을(지을) 때 받는 군집 생산 보너스(0.15 = +15%). 군집 대상이 아니면 0.</summary>
        public double ClusterBonus(string kind, HexCoord tile)
        {
            AdjacencySettings a = Adjacency;
            if (a?.ClusterKinds == null || !a.ClusterKinds.Contains(kind)) return 0;
            return a.ClusterOutputPerNeighbor * NeighborCount(tile, kind);
        }

        /// <summary>tile에 kind 가공 시설이 있을(지을) 때 원료 절약 비율(0.2 = 원료 −20%, 상한 inputSavingMax). 연계 대상이 아니면 0.</summary>
        public double InputSaving(string kind, HexCoord tile)
        {
            AdjacencySettings a = Adjacency;
            if (a?.InputSources == null || !a.InputSources.TryGetValue(kind, out string source)) return 0;
            return Math.Min(a.InputSavingMax, a.InputSavingPerNeighbor * NeighborCount(tile, source));
        }

        /// <summary>가공 원료 배율 = (1 − 연계 원료 절약) × 가공 원료 모디파이어(곱연산, 결정 BM3). 1이면 레벨표 그대로.</summary>
        public double InputFactor(string kind, HexCoord tile) => (1 - InputSaving(kind, tile)) * Rules.Effects.ProcessingInputMultiplier;

        /// <summary>tile의 주택이 받는 유입 속도 보너스(0.2 = +20%).</summary>
        public double HouseArrivalBonus(HexCoord tile) =>
            Adjacency == null ? 0 : Adjacency.HouseArrivalPerNeighbor * NeighborCount(tile, BuildingKinds.House);

        /// <summary>운영 중인 창고들의 이웃 창고 수 평균. 창고가 없으면 0.</summary>
        public double AverageWarehouseNeighbors()
        {
            var warehouses = OperatingOf(BuildingKinds.Warehouse).ToList();
            return warehouses.Count == 0 ? 0 : warehouses.Average(w => NeighborCount(w.Tile, BuildingKinds.Warehouse));
        }

        /// <summary>지금 식품 부패율(하루 비율, 0.01 = 1%/일) = (기본 − 창고 밀집) × 부패 모디파이어. 부패 설정이 없으면 0.</summary>
        public double FoodSpoilagePerDay
        {
            get
            {
                SpoilageSettings sp = Rules.Spoilage;
                if (sp == null) return 0;
                return Math.Max(0, sp.FoodPerDay - sp.WarehouseNeighborReductionPerDay * AverageWarehouseNeighbors()) * Rules.Effects.SpoilageMultiplier;
            }
        }
    }
}
