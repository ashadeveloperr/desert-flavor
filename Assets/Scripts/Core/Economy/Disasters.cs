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

    /// <summary>
    /// 전염병 진행 상태(결정 EV1~). 정착지 주민 전체를 걸릴 수 있음·아픔·면역으로 나눠 센다(주택별이 아니라 합).
    /// 아픈 주민은 일하지 못하고, 병상만큼 병원에 들어가 빨리 회복한다. 면역은 전염병이 끝나도 남는다.
    /// </summary>
    public sealed class EpidemicState
    {
        public bool Active { get; internal set; }
        /// <summary>주민 수(마지막 갱신 기준).</summary>
        public double Residents { get; internal set; }
        /// <summary>아픈 주민(집 + 병원)의 진행도(소수, 감염·회복 모형이 움직이는 값).</summary>
        public double Sick { get; internal set; }
        /// <summary>일하지 못하는 환자 수(온전한 사람 수, 결정 PI1). <see cref="Sick"/>이 1명분을 다 채우거나 다 비워야 바뀐다.</summary>
        public double SickPeople { get; internal set; }
        /// <summary>환자 수 비율(0~1) = SickPeople ÷ 주민. 일하지 못한 몫(HourRecord·화면).</summary>
        public double SickPeopleShare => Residents > 1e-9 ? SickPeople / Residents : 0;
        /// <summary>그중 병원 병상에 든 환자(병상 수까지).</summary>
        public double Hospitalized { get; internal set; }
        /// <summary>면역인 주민(회복했거나 방역 장비).</summary>
        public double Immune { get; internal set; }
        /// <summary>걸릴 수 있는 주민 = 주민 − 아픔 − 면역.</summary>
        public double Susceptible => Math.Max(0, Residents - Sick - Immune);
        /// <summary>아픈 주민 비율(0~1).</summary>
        public double SickShare => Residents > 1e-9 ? Sick / Residents : 0;
        /// <summary>지난 시간 새 환자·회복한 주민.</summary>
        public double NewCasesLastHour { get; internal set; }
        public double RecoveredLastHour { get; internal set; }
        /// <summary>회복이 새 환자보다 많아 줄어드는 중인지(정점을 지남). 화면의 '약해지는 중' 표시용.</summary>
        public bool Weakening { get; internal set; }
    }

    /// <summary>
    /// 한 정착지의 재해(전염병·모래폭풍·메뚜기떼·작물 탄저병). 경매 관리자처럼 정착지마다 하나 만들어 붙인다(<see cref="Attach"/>).
    /// 수치(유예·창·예고·전염병 속도)는 설정 JSON <c>disasters</c>(<see cref="DisasterSettings"/>·<see cref="EpidemicSettings"/>).
    /// - 일정: 정착지 시작 시각 + GraceHours 뒤 ScheduleWindowHours 안에서, CountWeights 확률로 0~2번(겹칠 수 있음). 같은 시드 = 같은 일정.
    /// - 예고: 시작 WarningHours 전에 Warned 이벤트.
    /// - 전염병(결정 EV1~EV7): 주민 사이에 퍼지는 감염·회복 모형. 시작하면 걸릴 수 있는 주민의 InitialInfectedShare가 아프고,
    ///   매시간 새 환자 = 전파율 × 걸릴 수 있는 주민 × 아픈 주민 ÷ 주민, 회복 = 병상에 든 환자 × 병원 회복률 + 집의 환자 × 집 회복률.
    ///   아픈 주민은 일하지 못한다(<see cref="Settlement"/>의 인원 배치가 뺀다). 아픈 비율이 EndBelowSickShare 아래로 내려가면 끝난다.
    ///   면역은 같은 정착지 안에 남고, 다음 전염병이 시작될 때 ImmunityCarryover만큼만 이어진다. 방역 장비는 처음부터 면역인 주민 비율.
    ///   새로 들어온 주민은 걸릴 수 있고, 떠나는 주민은 아픔·면역 비율대로 떠난다. 죽는 주민은 없다.
    ///   전염병 중 새 전염병이 시작되면 같은 전염병으로 이어진다.
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

        /// <summary>
        /// 재해 세기 배수(2026-10-27 LD3, 이슈 #12 후반 가속, 1 = 설정 그대로). 정해진 길이 재해(모래폭풍·메뚜기떼·탄저병)의 지속 시간,
        /// 메뚜기떼 식량 손실·농장 피해, 탄저병 농장 피해, 모래폭풍 수로 손실 증가분·공사 감속, 전염병 전파율에 곱한다(피해는 최대 100%).
        /// </summary>
        public double Intensity { get; private set; } = 1;

        public event Action<DisasterEvent> Warned;
        public event Action<DisasterEvent> Started;
        public event Action<DisasterEvent> Ended;

        /// <summary>정착지에 재해 일정을 만들어 붙인다. 설정이 없으면 null(재해 없음). regionModifierIds = 이 지역의 약점·강점 id(재해 종류 가중치 배율).</summary>
        /// <param name="intensity">재해 세기 배수(1 = 설정 그대로, 2026-10-27 LD3 후반 가속). <see cref="Intensity"/>.</param>
        public static DisasterManager Attach(Settlement settlement, DisasterSettings settings, int seed, IEnumerable<string> regionModifierIds = null, double intensity = 1)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (settings == null || settings.Types == null || settings.Types.Count == 0) return null;
            var m = new DisasterManager(settlement, settings, seed, regionModifierIds, Math.Max(1, intensity));
            settlement.Disasters = m;
            return m;
        }

        private DisasterManager(Settlement settlement, DisasterSettings settings, int seed, IEnumerable<string> regionModifierIds, double intensity)
        {
            _settlement = settlement;
            _settings = settings;
            Intensity = intensity;

            var rng = new DeterministicRandom(SeedSalts.DisasterSchedule(seed));
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
                if (max > 0) e.DurationHours = (int)Math.Round((min + rng.Next(Math.Max(1, max - min + 1))) * Intensity);
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
                _settlement.LoseFood(Math.Min(1, _settings.Locusts.FoodStockLoss * Intensity) * _settlement.Rules.LocustDamageMultiplier);
            }
            if (e.TypeId == EpidemicId)
            {
                if (Epidemic.Active && _currentEpidemic != null)
                {
                    // 진행 중인 전염병과 합쳐진다: 이 일정은 바로 앞 전염병에 흡수됐다고 보고 같은 시각에 끝난 것으로 기록.
                    e.State = DisasterState.Ended;
                    e.EndHour = e.StartHour;
                    Started?.Invoke(e);
                    Ended?.Invoke(e);
                    return;
                }
                SyncResidents();
                Epidemic.Immune *= _settings.Epidemic.ImmunityCarryover; // 지난 전염병의 면역은 일부만 남는다(결정 EV4)
                ApplyImmuneFloor();
                Epidemic.Sick = Epidemic.Susceptible * Math.Min(1, _settings.Epidemic.InitialInfectedShare);
                Epidemic.SickPeople = 0;
                UpdateSickPeople();
                Epidemic.Hospitalized = Math.Min(Epidemic.Sick, HospitalBeds);
                Epidemic.NewCasesLastHour = Epidemic.Sick;
                Epidemic.RecoveredLastHour = 0;
                Epidemic.Active = true;
                Epidemic.Weakening = false;
                _currentEpidemic = e;
            }
            Started?.Invoke(e);
        }

        /// <summary>운영 중인 병원 병상 합(레벨표 <c>beds</c>).</summary>
        public double HospitalBeds => _settlement.OperatingOf(BuildingKinds.Hospital).Sum(h => _settlement.LevelOf(h).Beds ?? 0);

        /// <summary>지금 일하지 못하는 아픈 주민(전염병 중). 인원 배치가 주민에서 뺀다.</summary>
        public double SickResidents => Epidemic.Active ? Math.Min(Epidemic.SickPeople, _settlement.Residents) : 0;

        /// <summary>환자 진행도가 바뀐 뒤 온전한 환자 수를 맞춘다(결정 PI1).</summary>
        private void UpdateSickPeople() =>
            Epidemic.SickPeople = Math.Min(WholePeople.Next(Epidemic.SickPeople, Epidemic.Sick), Epidemic.Residents);

        /// <summary>
        /// 주민 수 변화를 반영한다: 늘어난 주민은 걸릴 수 있음, 줄어든 주민은 아픔·면역 비율대로 떠난다.
        /// </summary>
        private void SyncResidents()
        {
            double now = _settlement.Residents;
            double before = Epidemic.Residents;
            if (now < before - 1e-12 && before > 1e-12)
            {
                double keep = now / before;
                Epidemic.Sick *= keep;
                Epidemic.Immune *= keep;
            }
            Epidemic.Residents = now;
            Epidemic.Sick = Math.Min(Epidemic.Sick, now);
            Epidemic.Immune = Math.Min(Epidemic.Immune, now - Epidemic.Sick);
            UpdateSickPeople();
        }

        /// <summary>방역 장비: 아프지 않은 주민 중 이 비율은 처음부터 면역(회복으로 얻은 면역이 더 크면 그쪽, 결정 EV5).</summary>
        private void ApplyImmuneFloor()
        {
            double floor = _settlement.Rules.EpidemicImmuneShare * Math.Max(0, Epidemic.Residents - Epidemic.Sick);
            Epidemic.Immune = Math.Max(Epidemic.Immune, Math.Min(floor, Epidemic.Residents - Epidemic.Sick));
        }

        /// <summary>
        /// 시설이 이번 시간 받는 생산 배율(1 = 피해 없음). 작물 시설(경작지·과수원·허브 농장)만 메뚜기떼·탄저병 배율(장비가 피해를 줄임).
        /// 전염병은 생산 배율이 아니라 아픈 주민이 일하지 못하는 것으로 나타난다(결정 EV2).
        /// </summary>
        public double ProductionFactor(Facility f)
        {
            if (f == null) return 1;
            if (SettlementRules.ResourceOf(f.Kind) == ResourceKind.None) return 1;
            double factor = 1;
            if (SettlementRules.IsCrop(f.Kind)) // 경작지·과수원·허브 농장(결정 FD8)
            {
                SettlementRules r = _settlement.Rules;
                if (IsActive(Locusts)) factor *= 1 - Math.Min(1, (1 - _settings.Locusts.FarmProduction) * Intensity) * r.LocustDamageMultiplier;
                if (IsActive(Blight)) factor *= 1 - Math.Min(1, (1 - _settings.Blight.FarmProduction) * Intensity) * r.BlightDamageMultiplier;
            }
            return Math.Max(0, factor);
        }

        /// <summary>이 종류의 재해가 지금 진행 중인지.</summary>
        public bool IsActive(string typeId) => _events.Any(e => e.TypeId == typeId && e.State == DisasterState.Active);

        private bool IsActiveExcept(string typeId, DisasterEvent except) =>
            _events.Any(e => e != except && e.TypeId == typeId && e.State == DisasterState.Active);

        /// <summary>이번 시간 수로 거리 손실 배율(모래폭풍 중 ×2, 수로 방풍막이면 증가분이 줄어듦). 평소 1.</summary>
        public double CanalLossFactor =>
            IsActive(Sandstorm) ? 1 + (_settings.Sandstorm.CanalLossMultiplier - 1) * Intensity * _settlement.Rules.SandstormDamageMultiplier : 1;

        /// <summary>이번 시간 공사(건설·개량·철거) 진행 속도(모래폭풍 중 0.5). 평소 1.</summary>
        public double ConstructionSpeed => IsActive(Sandstorm) ? Math.Max(0.1, 1 - (1 - _settings.Sandstorm.ConstructionSpeed) * Intensity) : 1;

        /// <summary>
        /// 정착지가 매시간 정산을 마칠 때 부른다(주민 떠남·돌아옴 뒤): 주민 수를 맞추고, 전염병 중이면 새 환자·회복을 한 시간만큼 진행한다.
        /// 아픈 비율이 EndBelowSickShare 아래로 내려가면 남은 환자는 회복한 것으로 보고 끝낸다.
        /// </summary>
        internal void EndHour(long h)
        {
            SyncResidents();
            if (!Epidemic.Active) return;
            EpidemicSettings p = _settings.Epidemic;
            ApplyImmuneFloor();
            double n = Epidemic.Residents;
            double beds = HospitalBeds;
            double inHospital = Math.Min(Epidemic.Sick, beds);
            double newCases = n > 1e-9 ? Math.Min(Epidemic.Susceptible, p.TransmissionPerHour * Intensity * _settlement.Rules.Effects.EpidemicTransmissionMultiplier * Epidemic.Susceptible * Epidemic.Sick / n) : 0;
            double recovered = Math.Min(Epidemic.Sick,
                inHospital * p.HospitalRecoveryPerHour + (Epidemic.Sick - inHospital) * p.HomeRecoveryPerHour);
            Epidemic.Sick = Math.Max(0, Epidemic.Sick + newCases - recovered);
            UpdateSickPeople();
            Epidemic.Immune = Math.Min(n - Epidemic.Sick, Epidemic.Immune + recovered);
            Epidemic.Hospitalized = Math.Min(Epidemic.Sick, beds);
            Epidemic.NewCasesLastHour = newCases;
            Epidemic.RecoveredLastHour = recovered;
            Epidemic.Weakening = recovered > newCases;
            if (Epidemic.SickShare < p.EndBelowSickShare || n <= 1e-9)
            {
                Epidemic.Immune = Math.Min(n, Epidemic.Immune + Epidemic.Sick);
                Epidemic.Sick = 0;
                Epidemic.SickPeople = 0;
                Epidemic.Hospitalized = 0;
                Epidemic.Active = false;
                if (_currentEpidemic != null)
                {
                    _currentEpidemic.State = DisasterState.Ended;
                    _currentEpidemic.EndHour = h;
                    Ended?.Invoke(_currentEpidemic);
                    _currentEpidemic = null;
                }
            }
        }
    }
}
