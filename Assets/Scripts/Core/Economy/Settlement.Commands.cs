using System;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 플레이어 명령의 공개 진입점(2026-10-23 저장·불러오기, 로드맵 2-①). 실제 처리는 Settlement.cs의 Do… 메서드가 하고,
    /// 성공한 명령만 <see cref="CommandRecorded"/>로 알린다. 캠페인이 이것을 모아 저장 파일의 명령 기록을 만든다
    /// (불러오기 = 같은 시드로 명령을 다시 실행, <see cref="SaveGame"/>). Docs/decision_log_auto.md S1~S5.
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
            Record(DoRequestCanal(edge), new GameCommand { Type = GameCommand.Canal, Q = edge.Owner.Q, R = edge.Owner.R, Index = (int)edge.Side });

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
            Record(DoMoveQueuedJob(jobId, newQueuedIndex), new GameCommand { Type = GameCommand.Move, Id = jobId, Index = newQueuedIndex });

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
    }
}
