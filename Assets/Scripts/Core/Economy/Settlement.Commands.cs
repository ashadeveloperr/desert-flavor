using System;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 플레이어 명령의 공개 진입점. 실제 처리는 Settlement.Actions.cs의 Do… 메서드가 하고,
    /// 성공한 명령만 <see cref="CommandRecorded"/>로 알린다. 캠페인이 이것을 모아 저장 파일의 명령 기록을 만든다
    /// (불러오기 = 같은 시드로 명령을 다시 실행, <see cref="SaveGame"/>, 결정 S1~S5).
    /// 새 명령을 더하면 <see cref="GameCommand"/>의 종류 상수와 <see cref="SaveGame"/>.Apply의 case도 함께 더한다.
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>성공한 플레이어 명령. 정착지 번호는 캠페인이 채운다.</summary>
        public event Action<GameCommand> CommandRecorded;

        /// <summary>게임 시계를 따라간 뒤(그 사이 완공된 공사를 먼저 반영) 명령을 처리하고 기록한다.</summary>
        private CommandResult Run(Func<CommandResult> action, GameCommand c)
        {
            SyncClock();
            return Record(action(), c);
        }

        /// <summary><see cref="TimeSource"/>가 있으면 그 시각까지 따라간다(명령 시각 = 그 분).</summary>
        private void SyncClock()
        {
            if (TimeSource != null) AdvanceTo(TimeSource());
        }

        private CommandResult Record(CommandResult r, GameCommand c)
        {
            if (r.Success)
            {
                c.Hour = CurrentHour;
                c.Minute = Minute;
                CommandRecorded?.Invoke(c);
                // 명령 순간 착공(결정 JQ2): 새 작업뿐 아니라 취소(슬롯)·순서 변경·석재 구매로 시작할 수 있게 된 작업도, 명령한 그 분에.
                // 저장 불러오기는 같은 명령을 같은 시각(시간·분)에 다시 부르므로 결과가 같다.
                if (Rules.StartJobsOnCommand) StartJobs(Now, CurrentHour);
                // 화면 표시용 인원 배치 갱신(정각 정산은 매번 새로 계산하므로 결과에 영향 없음, 결정 LB1~).
                if (Rules.LaborOn) LastLabor = AssignLabor();
            }
            return r;
        }

        /// <summary>우선 고용: 이 시설을 고용 1순위로(전에 누른 시설은 한 칸씩 밀림). 물 조건과 상관없이 먼저 배치된다(결정 LB1~).</summary>
        public CommandResult PrioritizeHiring(int facilityId) =>
            Run(() => DoPrioritizeHiring(facilityId), new GameCommand { Type = GameCommand.Hire, Id = facilityId });

        /// <summary>우선 고용 해제: 목록에서 빼서 자동 배치 순서(물 100% → 일부, 완공 순)로 돌린다.</summary>
        public CommandResult ClearHiringPriority(int facilityId) =>
            Run(() => DoClearHiringPriority(facilityId), new GameCommand { Type = GameCommand.Unhire, Id = facilityId });

        /// <summary>
        /// 타일 시설 건설 예약. 석재는 착공 때 차감된다.
        /// 무료 완공 배치(<see cref="FreeBuildsRemaining"/>)가 남아 있으면 대기열 없이 바로 운영 중인 L1 시설이 생긴다(JobId -1).
        /// </summary>
        public CommandResult RequestBuild(string kind, HexCoord tile) =>
            Run(() => DoRequestBuild(kind, tile), new GameCommand { Type = GameCommand.Build, Kind = kind, Q = tile.Q, R = tile.R });

        /// <summary>변 위 수로 건설 예약.</summary>
        public CommandResult RequestCanal(EdgeId edge) =>
            Run(() => DoRequestCanal(edge), GameCommand.ForCanal(edge));

        public CommandResult RequestUpgrade(int facilityId) =>
            Run(() => DoRequestUpgrade(facilityId), new GameCommand { Type = GameCommand.Upgrade, Id = facilityId });

        public CommandResult RequestDemolish(int facilityId) =>
            Run(() => DoRequestDemolish(facilityId), new GameCommand { Type = GameCommand.Demolish, Id = facilityId });

        /// <summary>
        /// 작업 취소. 대기 중이면 비용 없이 삭제, 진행 중이면 투입 석재의 <see cref="SettlementRules.CancelRefundRatio"/>를 돌려받고 원상태로 되돌린다.
        /// </summary>
        public CommandResult CancelJob(int jobId) =>
            Run(() => DoCancelJob(jobId), new GameCommand { Type = GameCommand.Cancel, Id = jobId });

        /// <summary>대기 중인 작업의 순서를 바꾼다. newQueuedIndex는 '대기 중 작업들' 사이의 위치(0 = 맨 앞).</summary>
        public CommandResult MoveQueuedJob(int jobId, int newQueuedIndex) =>
            Run(() => DoMoveQueuedJob(jobId, newQueuedIndex), GameCommand.ForMove(jobId, newQueuedIndex));

        /// <summary>시설 폐쇄/재개(수로는 물길 차단/개방). 폐쇄 중에는 물 수요·식량 소비 0, 유지비 일부.</summary>
        public CommandResult SetClosed(int facilityId, bool closed) =>
            Run(() => DoSetClosed(facilityId, closed), new GameCommand { Type = closed ? GameCommand.Close : GameCommand.Open, Id = facilityId });

        /// <summary>식량 외부 구매(1단위씩, 단가 상승). 저장 한도·자금 안에서만. 실제 구매 수를 돌려준다.</summary>
        public int BuyFood(int units)
        {
            SyncClock();
            int bought = DoBuyFood(units);
            if (bought > 0) Record(CommandResult.Ok(), new GameCommand { Type = GameCommand.BuyFood, Amount = units });
            return bought;
        }

        /// <summary>석재 외부 구매(1단위씩, 단가 상승). 저장 한도·자금 안에서만. 실제 구매 수를 돌려준다.</summary>
        public int BuyStone(int units)
        {
            SyncClock();
            int bought = DoBuyStone(units);
            if (bought > 0) Record(CommandResult.Ok(), new GameCommand { Type = GameCommand.BuyStone, Amount = units });
            return bought;
        }

        /// <summary>식량 시장 판매(1단위씩, 판매가 하락). 운영 중인 시장이 있어야 한다. 실제 판매 수를 돌려준다(2026-10-27 이슈 #18).</summary>
        public int SellFood(int units)
        {
            SyncClock();
            int sold = DoSellFood(units);
            if (sold > 0) Record(CommandResult.Ok(), new GameCommand { Type = GameCommand.SellFood, Amount = units });
            return sold;
        }

        /// <summary>재화 시장 판매(곡물은 식량 판매와 같음, 나머지는 팔기만, 결정 FD6). 실제 판매 수를 돌려준다.</summary>
        public int SellGoods(Goods goods, int units)
        {
            if (goods == Goods.Grain) return SellFood(units);
            SyncClock();
            int sold = SellOther(goods, units);
            if (sold > 0) Record(CommandResult.Ok(), new GameCommand { Type = GameCommand.SellGoods, Kind = GoodsCatalog.Id(goods), Amount = units });
            return sold;
        }

        /// <summary>석재 시장 판매(1단위씩, 판매가 하락). 운영 중인 시장이 있어야 한다. 실제 판매 수를 돌려준다.</summary>
        public int SellStone(int units)
        {
            SyncClock();
            int sold = DoSellStone(units);
            if (sold > 0) Record(CommandResult.Ok(), new GameCommand { Type = GameCommand.SellStone, Amount = units });
            return sold;
        }
    }
}
