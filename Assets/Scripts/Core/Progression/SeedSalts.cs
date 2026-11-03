namespace DesertSettlement.Core.Progression
{
    /// <summary>
    /// 캠페인 시드에서 정착지별 시드를 만들 때 섞는 수(결정 SD1). 값을 바꾸면 같은 시드로 다른 게임이 나와
    /// 저장 파일(같은 시드로 명령을 다시 실행)과 테스트·시뮬레이터의 골든 값이 모두 깨진다. 바꾸지 않는다.
    /// </summary>
    public static class SeedSalts
    {
        /// <summary>정착지의 경매(입찰자 예고·시세) 난수.</summary>
        public static int Auction(int campaignSeed, int settlementNumber) => unchecked(campaignSeed + settlementNumber * 7919);

        /// <summary>정착지의 재해 관리자에 넘기는 시드.</summary>
        public static int Disasters(int campaignSeed, int settlementNumber) => unchecked(campaignSeed + settlementNumber * 4421);

        /// <summary>재해 관리자가 받은 시드로 재해 일정 난수를 만든다.</summary>
        public static int DisasterSchedule(int disasterSeed) => unchecked(disasterSeed * 13 + 0xD15A);

        /// <summary>n번째 정착지의 생성 맵 기준 시드.</summary>
        public static int Map(int campaignSeed, int settlementNumber) => unchecked(campaignSeed * 31 + settlementNumber * 104729);

        /// <summary>다음 지역 후보 i의 시드(맵 생성·지역 모디파이어).</summary>
        public static int RegionOffer(int mapSeed, int offerIndex) => unchecked(mapSeed + offerIndex * 7777);
    }
}
