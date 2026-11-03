using System;
using System.Globalization;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 물 배분(<see cref="Water.WaterNetworkSolver"/>)에 넘기는 문자열 id 규칙(결정 SD3). 만들고 읽는 곳은 모두 이 도우미를 쓴다.
    /// 시설(우물·물을 받는 시설)은 시설 번호 그대로("12"), 물탱크는 "tank" + 번호("tank12" — 방출할 때는 공급원, 충전할 때는 수요처).
    /// </summary>
    public static class WaterIds
    {
        private const string TankPrefix = "tank";

        /// <summary>시설의 id(시설 번호).</summary>
        public static string Of(Facility f) => Of(f.Id);

        /// <summary>시설 번호의 id.</summary>
        public static string Of(int facilityId) => facilityId.ToString(CultureInfo.InvariantCulture);

        /// <summary>물탱크의 id("tank" + 번호).</summary>
        public static string Tank(int facilityId) => TankPrefix + Of(facilityId);

        public static bool IsTank(string id) => id.StartsWith(TankPrefix, StringComparison.Ordinal);

        /// <summary>시설 id면 시설 번호를 돌려준다. 물탱크 id나 숫자가 아니면 false.</summary>
        public static bool TryFacilityId(string id, out int facilityId) =>
            int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out facilityId);

        /// <summary>화면에 보일 번호 부분(물탱크 id면 접두어를 뗀다).</summary>
        public static string NumberOf(string id) => IsTank(id) ? id.Substring(TankPrefix.Length) : id;
    }
}
