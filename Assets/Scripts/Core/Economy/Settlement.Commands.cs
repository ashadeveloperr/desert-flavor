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

        private CommandResult Record(CommandResult r, GameCommand c)
        {
            if (r.Success)
            {
                c.Hour = CurrentHour;
                CommandRecorded?.Invoke(c);
                // 2026-10-26 JQ2(이슈 #8): 명령 순간 착공. 새 작업뿐 아니라 취소(슬롯)·순서 변경·석재 구매로 시작할 수 있게 된 작업도.
                // 저장 불러오기는 같은 명령을 같은 시각에 다시 부르므로 결과가 같다.
                if (Rules.StartJobsOnCommand) StartJobs(CurrentHour, onCommand: true);
            }
            return r;
        }

        /// <summary>
        /// 타일 시설 건설 예약. 석재는 착공 때 차감된다.
        /// 무료 완공 배치(<see cref="FreeBuildsRemaining"/>)가 남아 있으면 대기열 없이 바로 운영 중인 L1 시설이 생긴다(JobId -1).
        /// </summary>
        public CommandResult RequestBuild(string kind, HexCoord tile) =>
            Record(DoRequestBuild(kind, tile), new GameCommand { Type = GameCommand.Build, Kind = kind, Q = tile.Q, R = tile.R });

        /// <summary>변 위 수로 건설 예약.</summary>
        public CommandResult RequestCanal(EdgeId edge) =>
            Record(DoRequestCanal(edge), GameCommand.ForCanal(edge));

        public CommandResult RequestUpgrade(int facilityId) =>
            Record(DoRequestUpgrade(facilityId), new GameCommand { Type = GameCommand.Upgrade, Id = facilityId });

        public CommandResult RequestDemolish(int facilityId) =>
            Record(DoRequestDemolish(facilityId), new GameCommand { Type = GameCommand.Demolish, Id = facilityId });

        /// <summary>
        /// 작업 취소. 대기 중이면 비용 없이 삭제, 진행 중이면 투입 석재의 <see cref="SettlementRules.CancelRefundRatio"/>를 돌려받고 원상태로 되돌린다.
        /// </summary>
        public CommandResult CancelJob(int jobId) =>
            Record(DoCancelJob(jobId), new GameCommand { Type = GameCommand.Cancel, Id = jobId });

        /// <summary>대기 중인 작업의 순서를 바꾼다. newQueuedIndex는 '대기 중 작업들' 사이의 위치(0 = 맨 앞).</summary>
        public CommandResult MoveQueuedJob(int jobId, int newQueuedIndex) =>
            Record(DoMoveQueuedJob(jobId, newQueuedIndex), GameCommand.ForMove(jobId, newQueuedIndex));

        /// <summary>시설 폐쇄/재개(수로는 물길 차단/개방). 폐쇄 중에는 물 수요·식량 소비 0, 유지비 일부.</summary>
        public CommandResult SetClosed(int facilityId, bool closed) =>
            Record(DoSetClosed(facilityId, closed), new GameCommand { Type = closed ? GameCommand.Close : GameCommand.Open, Id = facilityId });

        /// <summary>식량 외부 구매(1단위씩, 단가 상승). 저장 한도·자금 안에서만. 실제 구매 수를 돌려준다.</summary>
        public int BuyFood(int units)
        {
            int bought = DoBuyFood(units);
            if (bought > 0) Record(CommandResult.Ok(), new GameCommand { Type = GameCommand.BuyFood, Amount = units });
            return bought;
        }

        /// <summary>석재 외부 구매(1단위씩, 단가 상승). 저장 한도·자금 안에서만. 실제 구매 수를 돌려준다.</summary>
        public int BuyStone(int units)
        {
            int bought = DoBuyStone(units);
            if (bought > 0) Record(CommandResult.Ok(), new GameCommand { Type = GameCommand.BuyStone, Amount = units });
            return bought;
        }

        /// <summary>식량 시장 판매(1단위씩, 판매가 하락). 운영 중인 시장이 있어야 한다. 실제 판매 수를 돌려준다(2026-10-27 이슈 #18).</summary>
        public int SellFood(int units)
        {
            int sold = DoSellFood(units);
            if (sold > 0) Record(CommandResult.Ok(), new GameCommand { Type = GameCommand.SellFood, Amount = units });
            return sold;
        }

        /// <summary>석재 시장 판매(1단위씩, 판매가 하락). 운영 중인 시장이 있어야 한다. 실제 판매 수를 돌려준다.</summary>
        public int SellStone(int units)
        {
            int sold = DoSellStone(units);
            if (sold > 0) Record(CommandResult.Ok(), new GameCommand { Type = GameCommand.SellStone, Amount = units });
            return sold;
        }
    }
}
