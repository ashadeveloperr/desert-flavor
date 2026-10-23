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
    }

    /// <summary>
    /// 한 정착지의 재해(2026-10-23: 전염병). 경매 관리자처럼 정착지마다 하나 만들어 붙인다(<see cref="Attach"/>).
    /// - 일정: 정착지 시작 시각 + GraceHours(48h) 뒤 ScheduleWindowHours 안에서, CountWeights 확률로 0~2번(겹칠 수 있음). 같은 시드 = 같은 일정.
    /// - 예고: 시작 WarningHours(6h) 전에 Warned 이벤트.
    /// - 전염병: 시작하면 심각도 InitialSeverity. 매시간 피해 = S × (1 − R)만큼 생산 시설(농장·채석장·광산) 효율을 낮추고(병원 인근 1칸은 절반),
    ///   R은 피해에 비례해 오르며(방역 장비는 이 속도 ×1.5), R ≥ S이면 S가 줄어 0이 되면 끝난다.
    ///   끝난 뒤 R은 천천히 줄되 시작 저항력 효과(InitialEpidemicResistance)가 있으면 그 아래로는 안 내려간다.
    ///   전염병 중 새 전염병이 시작되면 같은 전염병으로 이어진다(심각도는 큰 쪽).
    /// 수치는 설정 JSON의 disasters.
    /// </summary>
    public sealed class DisasterManager
    {
        private readonly Settlement _settlement;
        private readonly DisasterSettings _settings;
        private readonly List<DisasterEvent> _events = new List<DisasterEvent>();
        private DisasterEvent _currentEpidemic;

        public IReadOnlyList<DisasterEvent> Events => _events;
        public EpidemicState Epidemic { get; } = new EpidemicState();
        public DisasterSettings Settings => _settings;

        public event Action<DisasterEvent> Warned;
        public event Action<DisasterEvent> Started;
        public event Action<DisasterEvent> Ended;

        /// <summary>정착지에 재해 일정을 만들어 붙인다. 설정이 없으면 null(재해 없음).</summary>
        public static DisasterManager Attach(Settlement settlement, DisasterSettings settings, int seed)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (settings == null || settings.Types == null || settings.Types.Count == 0) return null;
            var m = new DisasterManager(settlement, settings, seed);
            settlement.Disasters = m;
            return m;
        }

        private DisasterManager(Settlement settlement, DisasterSettings settings, int seed)
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
                DisasterTypeDefinition type = settings.Types[PickIndex(settings.Types.Select(t => t.Weight).ToList(), rng)];
                _events.Add(new DisasterEvent
                {
                    Index = i, TypeId = type.Id, Name = type.Name ?? type.Id,
                    StartHour = starts[i], WarningHour = starts[i] - settings.WarningHours, State = DisasterState.Scheduled,
                });
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
            if (e.TypeId == "epidemic")
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
                Epidemic.Severity = _settings.Epidemic.InitialSeverity;
                _currentEpidemic = e;
            }
            Started?.Invoke(e);
        }

        /// <summary>시설이 이번 시간 받는 생산 배율(1 = 피해 없음). 생산 시설만, 병원 인근은 피해 절반.</summary>
        public double ProductionFactor(Facility f)
        {
            if (!Epidemic.Active || f == null) return 1;
            if (f.Kind != BuildingKinds.Farm && f.Kind != BuildingKinds.Quarry && f.Kind != BuildingKinds.Mine) return 1;
            double damage = Epidemic.Damage;
            if (IsNearHospital(f)) damage *= _settings.Epidemic.HospitalDamageFactor;
            return Math.Max(0, 1 - damage);
        }

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
                Epidemic.Severity = Epidemic.Resistance >= Epidemic.Severity
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
