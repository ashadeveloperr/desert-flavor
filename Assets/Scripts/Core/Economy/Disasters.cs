using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Progression;

namespace DesertSettlement.Core.Economy
{
    public enum DisasterState
    {
        /// <summary>일정만 잡힘(아직 모름).</summary>
        Scheduled,
        /// <summary>예고됨(시작 WarningHours 전부터).</summary>
        Warned,
        Active,
        Ended,
    }

    /// <summary>정착지에 잡힌 재해 하나.</summary>
    public sealed class DisasterEvent
    {
        public int Index { get; internal set; }
        public string TypeId { get; internal set; }
        public string Name { get; internal set; }
        public long StartHour { get; internal set; }
        public long WarningHour { get; internal set; }
        /// <summary>끝난 시각(끝나기 전에는 -1). 전염병은 심각도가 0이 되면 끝나므로 미리 정해져 있지 않다.</summary>
        public long EndHour { get; internal set; } = -1;
        /// <summary>정해진 길이로 끝나는 재해(모래폭풍·메뚜기떼·탄저병)의 지속 시간. 전염병은 0. 시드로 정해지고 예고 때는 보여 주지 않는다.</summary>
        public int DurationHours { get; internal set; }
        /// <summary>정해진 길이 재해의 예정 종료 시각(StartHour + DurationHours). 전염병은 -1.</summary>
        public long PlannedEndHour => DurationHours > 0 ? StartHour + DurationHours : -1;
        public DisasterState State { get; internal set; }
    }

    /// <summary>전염병 진행 상태(심각도 S, 저항력 R).</summary>
    public sealed class EpidemicState
    {
        public bool Active { get; internal set; }
        /// <summary>심각도 0~1.</summary>
        public double Severity { get; internal set; }
        /// <summary>저항력 0~1.</summary>
        public double Resistance { get; internal set; }
        /// <summary>이번 시간 생산 시설 효율 감소 = S × (1 − R). 병원 인근은 이 값 × HospitalDamageFactor.</summary>
        public double Damage => Active ? Severity * (1 - Resistance) : 0;
        /// <summary>심각도가 줄어드는 중인지(정점을 지남). 마지막 시간 갱신 기준, 화면의 '약해지는 중' 표시용.</summary>
        public bool Weakening { get; internal set; }
    }

    /// <summary>
    /// 한 정착지의 재해(2026-10-23: 전염병 + 모래폭풍·메뚜기떼·작물 탄저병). 경매 관리자처럼 정착지마다 하나 만들어 붙인다(<see cref="Attach"/>).
    /// - 일정: 정착지 시작 시각 + GraceHours(48h) 뒤 ScheduleWindowHours 안에서, CountWeights 확률로 0~2번(겹칠 수 있음). 같은 시드 = 같은 일정.
    /// - 예고: 시작 WarningHours(6h) 전에 Warned 이벤트.
    /// - 전염병: 시작하면 심각도 InitialSeverity. 매시간 피해 = S × (1 − R)만큼 생산 시설(농장·채석장·광산) 효율을 낮추고(병원 인근 1칸은 절반),
    ///   R은 피해에 비례해 오르며(방역 장비는 이 속도 ×1.3), R ≥ 비율(0.7) × S이면 S가 줄어 0이 되면 끝난다(2026-10-26 EP1: 정점 약 31h·35%, 약 50h).
    ///   끝난 뒤 R은 천천히 줄되 시작 저항력 효과(InitialEpidemicResistance)가 있으면 그 아래로는 안 내려간다.
    ///   전염병 중 새 전염병이 시작되면 같은 전염병으로 이어진다(심각도는 큰 쪽).
    /// - 모래폭풍·메뚜기떼·탄저병(2026-10-23 D1~D7): 시드로 정한 길이 동안 효과(수로 손실·공사 속도, 농장 생산·식량 재고, 농장 생산). 장비가 피해를 줄인다.
    ///   같은 종류가 겹치면 효과는 한 번(더 늦게 끝나는 쪽까지). 지역 약점이 있으면 종류 가중치가 바뀐다(RegionWeightMultipliers).
    /// 수치는 설정 JSON의 disasters.
    /// </summary>
    public sealed class DisasterManager
    {
        private readonly Settlement _settlement;
        private readonly DisasterSettings _settings;
        private readonly List<DisasterEvent> _events = new List<DisasterEvent>();
        private DisasterEvent _currentEpidemic;

        /// <summary>재해 종류 id(설정 JSON disasters.types[].id).</summary>
        public const string EpidemicId = "epidemic", Sandstorm = "sandstorm", Locusts = "locusts", Blight = "blight";

        public IReadOnlyList<DisasterEvent> Events => _events;
        public EpidemicState Epidemic { get; } = new EpidemicState();
        public DisasterSettings Settings => _settings;

        public event Action<DisasterEvent> Warned;
        public event Action<DisasterEvent> Started;
        public event Action<DisasterEvent> Ended;

        /// <summary>정착지에 재해 일정을 만들어 붙인다. 설정이 없으면 null(재해 없음). regionModifierIds = 이 지역의 약점·강점 id(재해 종류 가중치 배율).</summary>
        public static DisasterManager Attach(Settlement settlement, DisasterSettings settings, int seed, IEnumerable<string> regionModifierIds = null)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (settings == null || settings.Types == null || settings.Types.Count == 0) return null;
            var m = new DisasterManager(settlement, settings, seed, regionModifierIds);
            settlement.Disasters = m;
            return m;
        }

        private DisasterManager(Settlement settlement, DisasterSettings settings, int seed, IEnumerable<string> regionModifierIds)
        {
            _settlement = settlement;
            _settings = settings;
            Epidemic.Resistance = settlement.Rules.EpidemicResistanceFloor;

            var rng = new DeterministicRandom(unchecked(seed * 13 + 0xD15A));
            int count = PickIndex(settings.EffectiveCountWeights, rng);
            long first = settlement.CurrentHour + settings.GraceHours;
            var starts = Enumerable.Range(0, count).Select(_ => first + rng.Next(Math.Max(1, settings.ScheduleWindowHours))).OrderBy(x => x).ToList();
            for (int i = 0; i < starts.Count; i++)
            {
                DisasterTypeDefinition type = settings.Types[PickIndex(settings.Types.Select(t => t.Weight * RegionMultiplier(settings, regionModifierIds, t.Id)).ToList(), rng)];
                _events.Add(new DisasterEvent
                {
                    Index = i, TypeId = type.Id, Name = type.Name ?? type.Id,
                    StartHour = starts[i], WarningHour = starts[i] - settings.WarningHours, State = DisasterState.Scheduled,
                });
            }
            // 정해진 길이 재해의 지속 시간은 일정을 다 잡은 뒤에 뽑는다(전염병만 있던 때의 일정 난수 순서를 바꾸지 않으려고).
            foreach (DisasterEvent e in _events)
            {
                (int min, int max) = DurationRange(e.TypeId);
                if (max > 0) e.DurationHours = min + rng.Next(Math.Max(1, max - min + 1));
            }
        }

        private static double RegionMultiplier(DisasterSettings settings, IEnumerable<string> modifierIds, string typeId)
        {
            if (settings.RegionWeightMultipliers == null || modifierIds == null) return 1;
            double m = 1;
            foreach (string id in modifierIds)
                if (settings.RegionWeightMultipliers.TryGetValue(id, out Dictionary<string, double> byType) && byType.TryGetValue(typeId, out double x)) m *= x;
            return m;
        }

        private (int min, int max) DurationRange(string typeId)
        {
            switch (typeId)
            {
                case Sandstorm: return (_settings.Sandstorm.MinHours, _settings.Sandstorm.MaxHours);
                case Locusts: return (_settings.Locusts.MinHours, _settings.Locusts.MaxHours);
                case Blight: return (_settings.Blight.MinHours, _settings.Blight.MaxHours);
                default: return (0, 0);
            }
        }

        private static int PickIndex(IList<double> weights, DeterministicRandom rng)
        {
            if (weights == null || weights.Count == 0) return 0;
            double total = weights.Sum(w => Math.Max(0, w));
            if (total <= 0) return 0;
            double x = rng.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                x -= Math.Max(0, weights[i]);
                if (x < 0) return i;
            }
            return weights.Count - 1;
        }

        /// <summary>예고됐거나 진행 중인 재해(HUD용).</summary>
        public IEnumerable<DisasterEvent> Visible => _events.Where(e => e.State == DisasterState.Warned || e.State == DisasterState.Active);

        /// <summary>정착지가 매시간 정산을 시작할 때 부른다: 예고·시작 처리.</summary>
        internal void BeginHour(long h)
        {
            // 정해진 길이 재해: 예정 종료 시각이 되면 끝난다(그 시간부터 효과 없음).
            foreach (DisasterEvent e in _events.Where(x => x.State == DisasterState.Active && x.DurationHours > 0 && h >= x.PlannedEndHour))
            {
                e.State = DisasterState.Ended;
                e.EndHour = e.PlannedEndHour;
                Ended?.Invoke(e);
            }
            foreach (DisasterEvent e in _events)
            {
                if (e.State == DisasterState.Scheduled && h >= e.WarningHour)
                {
                    e.State = DisasterState.Warned;
                    Warned?.Invoke(e);
                }
                if (e.State == DisasterState.Warned && h >= e.StartHour) Start(e);
            }
        }

        private void Start(DisasterEvent e)
        {
            e.State = DisasterState.Active;
            if (e.TypeId == Locusts && !IsActiveExcept(Locusts, e))
            {
                // 메뚜기떼가 날아든 순간 식량 재고를 잃는다(방충망이면 줄어듦). 이미 메뚜기떼가 있으면 두 번 잃지 않는다.
                _settlement.LoseFood(_settings.Locusts.FoodStockLoss * _settlement.Rules.LocustDamageMultiplier);
            }
            if (e.TypeId == EpidemicId)
            {
                if (Epidemic.Active && _currentEpidemic != null)
                {
                    // 진행 중인 전염병과 합쳐진다: 이 일정은 바로 앞 전염병에 흡수됐다고 보고 같은 시각에 끝난 것으로 기록.
                    Epidemic.Severity = Math.Max(Epidemic.Severity, _settings.Epidemic.InitialSeverity);
                    e.State = DisasterState.Ended;
                    e.EndHour = e.StartHour;
                    Started?.Invoke(e);
                    Ended?.Invoke(e);
                    return;
                }
                Epidemic.Active = true;
                Epidemic.Weakening = false;
                Epidemic.Severity = _settings.Epidemic.InitialSeverity;
                _currentEpidemic = e;
            }
            Started?.Invoke(e);
        }

        /// <summary>
        /// 시설이 이번 시간 받는 생산 배율(1 = 피해 없음). 생산 시설만.
        /// 전염병: 1 − 피해(병원 인근은 피해 절반). 농장은 메뚜기떼·탄저병 배율도 곱한다(장비가 피해를 줄임).
        /// </summary>
        public double ProductionFactor(Facility f)
        {
            if (f == null) return 1;
            if (f.Kind != BuildingKinds.Farm && f.Kind != BuildingKinds.Quarry && f.Kind != BuildingKinds.Mine) return 1;
            double factor = 1;
            if (Epidemic.Active)
            {
                double damage = Epidemic.Damage;
                if (IsNearHospital(f)) damage *= _settings.Epidemic.HospitalDamageFactor;
                factor *= Math.Max(0, 1 - damage);
            }
            if (f.Kind == BuildingKinds.Farm)
            {
                SettlementRules r = _settlement.Rules;
                if (IsActive(Locusts)) factor *= 1 - (1 - _settings.Locusts.FarmProduction) * r.LocustDamageMultiplier;
                if (IsActive(Blight)) factor *= 1 - (1 - _settings.Blight.FarmProduction) * r.BlightDamageMultiplier;
            }
            return Math.Max(0, factor);
        }

        /// <summary>이 종류의 재해가 지금 진행 중인지.</summary>
        public bool IsActive(string typeId) => _events.Any(e => e.TypeId == typeId && e.State == DisasterState.Active);

        private bool IsActiveExcept(string typeId, DisasterEvent except) =>
            _events.Any(e => e != except && e.TypeId == typeId && e.State == DisasterState.Active);

        /// <summary>이번 시간 수로 거리 손실 배율(모래폭풍 중 ×2, 수로 방풍막이면 증가분이 줄어듦). 평소 1.</summary>
        public double CanalLossFactor =>
            IsActive(Sandstorm) ? 1 + (_settings.Sandstorm.CanalLossMultiplier - 1) * _settlement.Rules.SandstormDamageMultiplier : 1;

        /// <summary>이번 시간 공사(건설·개량·철거) 진행 속도(모래폭풍 중 0.5). 평소 1.</summary>
        public double ConstructionSpeed => IsActive(Sandstorm) ? _settings.Sandstorm.ConstructionSpeed : 1;

        /// <summary>가동 중인 병원 반경(설정 hospitalRadius, 기본 1칸) 안에 있는지. 병원이 여럿이어도 절반은 한 번.</summary>
        public bool IsNearHospital(Facility f) =>
            !f.Edge.HasValue && _settlement.OperatingOf(BuildingKinds.Hospital).Any(hp => hp.Tile.DistanceTo(f.Tile) <= _settings.Epidemic.HospitalRadius);

        /// <summary>정착지가 매시간 정산을 마칠 때 부른다: 심각도·저항력 갱신, 끝남 처리.</summary>
        internal void EndHour(long h)
        {
            EpidemicSettings p = _settings.Epidemic;
            double floor = _settlement.Rules.EpidemicResistanceFloor;
            if (Epidemic.Active)
            {
                double damage = Epidemic.Damage;
                double growth = (p.ResistanceBasePerHour + p.ResistancePerDamage * damage) * _settlement.Rules.EpidemicResistanceGrowthMultiplier;
                Epidemic.Resistance = Math.Min(1, Epidemic.Resistance + growth);
                Epidemic.Weakening = Epidemic.Resistance >= p.SeverityFallResistanceRatio * Epidemic.Severity;
                Epidemic.Severity = Epidemic.Weakening
                    ? Math.Max(0, Epidemic.Severity - p.SeverityFallPerHour)
                    : Math.Min(1, Epidemic.Severity + p.SeverityRisePerHour);
                if (Epidemic.Severity <= 1e-9)
                {
                    Epidemic.Active = false;
                    Epidemic.Severity = 0;
                    if (_currentEpidemic != null)
                    {
                        _currentEpidemic.State = DisasterState.Ended;
                        _currentEpidemic.EndHour = h;
                        Ended?.Invoke(_currentEpidemic);
                        _currentEpidemic = null;
                    }
                }
            }
            else
            {
                Epidemic.Resistance = Math.Max(floor, Epidemic.Resistance - p.ResistanceDecayPerHour);
            }
        }
    }
}
