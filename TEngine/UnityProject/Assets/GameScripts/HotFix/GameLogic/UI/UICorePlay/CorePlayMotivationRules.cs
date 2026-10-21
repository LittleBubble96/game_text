using GameLogic.Localization;

namespace GameLogic
{
    /// <summary>按本关累计找到数触发；完成关卡优先于普通节点。</summary>
    internal static class CorePlayMotivationRules
    {
        private static readonly string[] Third = { LanguageKey.motivation_nice, 
            LanguageKey.motivation_accurate , 
            LanguageKey.motivation_before1 ,
            LanguageKey.motivation_before2 ,
            LanguageKey.motivation_before3 ,
            LanguageKey.motivation_before4 ,
            LanguageKey.motivation_before5 ,
            LanguageKey.motivation_before6 ,
            LanguageKey.motivation_before7 ,
        };
        private static readonly string[] Fifth =
        {
            LanguageKey.motivation_great, 
            LanguageKey.motivation_sharp_eyes,
            LanguageKey.motivation5_before1,
            LanguageKey.motivation5_before2,
            LanguageKey.motivation5_before3,
            LanguageKey.motivation5_before4,
            LanguageKey.motivation5_before5,
            LanguageKey.motivation5_before6,
            LanguageKey.motivation5_before7,
            LanguageKey.motivation5_before9,
        };
        private static readonly string[] Eighth =
        {
            LanguageKey.motivation_on_a_roll,
            LanguageKey.motivation_keep_it_up, 
            LanguageKey.motivation_awesome,
            LanguageKey.motivation8_before1,
            LanguageKey.motivation8_before2,
            LanguageKey.motivation8_before3,
            LanguageKey.motivation8_before4,
            LanguageKey.motivation8_before5,
            LanguageKey.motivation8_before6,

        };
        private static readonly string[] Later =
        {
            LanguageKey.motivation_amazing, LanguageKey.motivation_hidden, LanguageKey.motivation_thats_you,
            LanguageKey.motivation_incredible, LanguageKey.motivation_impossible,            
            LanguageKey.motivation_after1,
            LanguageKey.motivation_after2,
            LanguageKey.motivation_after3,
            LanguageKey.motivation_after4,
            LanguageKey.motivation_after5,
            LanguageKey.motivation_after6,
            LanguageKey.motivation_after7,
        };
        private static readonly string[] Complete =
        {
            LanguageKey.motivation_all_found, 
            LanguageKey.motivation_success1,
            LanguageKey.motivation_success2,
            LanguageKey.motivation_success3,
            LanguageKey.motivation_success4,
            LanguageKey.motivation_success5,
            LanguageKey.motivation_success6,
            LanguageKey.motivation_success7,
        };

        public static string[] GetKeys(int foundCount, int requiredCount)
        {
            if (foundCount <= 0 || requiredCount <= 0) return null;
            if (foundCount >= requiredCount) return Complete;
            if (foundCount == 3) return Third;
            if (foundCount == 5) return Fifth;
            if (foundCount == 8) return Eighth;
            return foundCount > 8 && (foundCount - 8) % 3 == 0 ? Later : null;
        }
    }
}
