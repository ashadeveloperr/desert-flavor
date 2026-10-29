using System.Collections.Generic;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 캠페인 단계 명령의 입구(결정 CM1): 경매(매각·보류)·상점(장비)·출발·새 캠페인·샘플 배치. 화면은 이 메서드를 부르고,
    /// 정착지 안 명령(건설·수로·개량·철거·취소·순서·폐쇄·구매·판매)은 <see cref="Settlement"/>를 직접 부른다.
    /// 시계 재개·정착지 연결처럼 세션이 함께 할 일이 생겨도 이 한 곳만 고치면 된다.
    /// </summary>
    public sealed partial class GameSession
    {
        public void SellTo(int bidIndex)
        {
            if (Auctions != null && Auctions.IsOpen) Auctions.Sell(bidIndex);
        }

        public void HoldAuction()
        {
            if (Auctions == null || !Auctions.IsOpen) return;
            Auctions.Hold();
            Clock.SetPaused(false);
        }

        public CommandResult BuyEquipment(string id) => Campaign.BuyEquipment(id);

        public CommandResult UpgradeEquipment(string id) => Campaign.UpgradeEquipment(id);

        /// <summary>현장 장비 슬롯에 넣거나 뺀다.</summary>
        public CommandResult SetEquipped(string id, bool equipped) => Campaign.SetEquipped(id, equipped);

        /// <summary>상점에서 다음 지역 후보(offerIndex)를 골라 들어간다.</summary>
        public void GoToNextSettlement(int offerIndex = 0)
        {
            if (Campaign.State != CampaignState.Shopping) return;
            BeginSettlement(offerIndex);
        }

        /// <summary>파산 후(또는 아무 때나) 새 캠페인을 시작한다.</summary>
        public void RestartCampaign() => Initialize();

        /// <summary>지금 맵 파일의 샘플 배치를 예약한다. 실패한 항목 설명을 돌려준다.</summary>
        public List<string> RequestSampleLayout()
        {
            List<string> failures = Campaign.CurrentMap.RequestSampleLayout(Settlement);
            foreach (string m in failures) Debug.LogWarning("[GameSession] 샘플 배치 " + m);
            return failures;
        }
    }
}
