using System;
using System.IO;
using DesertSettlement.Core.Progression;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 최고 기록(2026-10-24 재미 보강 RC1~RC3, Docs/decision_log_auto.md): 도달 정착지·최대 보유 자금·누적 매각 대금.
    /// 캠페인이 끝나도 남도록 <c>Application.persistentDataPath/records.json</c>에 저장한다(저장 슬롯과 따로).
    /// 무한 진행 게임에 '한 판 더'를 부르는 목표를 주는 용도. 상점·파산 화면이 <see cref="Submit"/>을 부른다.
    /// </summary>
    public static class RecordBook
    {
        [Serializable]
        public sealed class Records
        {
            public int bestSettlement;
            public double bestPeakCash;
            public double bestTotalSales;
        }

        private static Records _records;

        private static string FilePath => Path.Combine(Application.persistentDataPath, "records.json");

        public static Records Best
        {
            get
            {
                if (_records != null) return _records;
                try { _records = File.Exists(FilePath) ? JsonUtility.FromJson<Records>(File.ReadAllText(FilePath)) : null; }
                catch (Exception e) { Debug.LogWarning("[RecordBook] 기록을 읽지 못함: " + e.Message); }
                return _records ??= new Records();
            }
        }

        /// <summary>이번 캠페인 값을 최고 기록과 비교해 넘으면 갱신·저장한다. 새 기록이 하나라도 있으면 true.</summary>
        public static bool Submit(Campaign c)
        {
            if (c == null) return false;
            Records best = Best;
            bool changed = false;
            if (c.SettlementNumber > best.bestSettlement) { best.bestSettlement = c.SettlementNumber; changed = true; }
            if (c.PeakCash > best.bestPeakCash + 0.5) { best.bestPeakCash = c.PeakCash; changed = true; }
            if (c.TotalSaleCash > best.bestTotalSales + 0.5) { best.bestTotalSales = c.TotalSaleCash; changed = true; }
            if (!changed) return false;
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(best, true)); }
            catch (Exception e) { Debug.LogWarning("[RecordBook] 기록을 저장하지 못함: " + e.Message); }
            return true;
        }

        /// <summary>'이번 캠페인 / 최고 기록' 두 줄 문구.</summary>
        public static string Describe(Campaign c)
        {
            Records best = Best;
            return $"이번 캠페인: 정착지 {c.SettlementNumber}까지 · 최대 자금 {c.PeakCash:0} · 누적 매각 {c.TotalSaleCash:0}\n" +
                   $"최고 기록: 정착지 {best.bestSettlement} · 최대 자금 {best.bestPeakCash:0} · 누적 매각 {best.bestTotalSales:0}";
        }
    }
}
