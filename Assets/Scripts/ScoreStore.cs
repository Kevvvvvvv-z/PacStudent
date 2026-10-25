using UnityEngine;

public static class ScoreStore
{
    private static string Prefix(string level) => "BeetleRun." + level + ".";
    public static int BestScore(string level) => PlayerPrefs.GetInt(Prefix(level) + "Score", 0);
    public static int BestTime(string level) => PlayerPrefs.GetInt(Prefix(level) + "Time", 0);
    public static string FormatTime(int hundredths) => $"{hundredths / 6000:00}:{hundredths / 100 % 60:00}:{hundredths % 100:00}";

    public static bool SaveResult(string level, int score, int hundredths)
    {
        string prefix = Prefix(level);
        bool exists = PlayerPrefs.HasKey(prefix + "Score");
        if (exists && (score < BestScore(level) || (score == BestScore(level) && hundredths >= BestTime(level)))) return false;
        PlayerPrefs.SetInt(prefix + "Score", score);
        PlayerPrefs.SetInt(prefix + "Time", hundredths);
        PlayerPrefs.Save();
        return true;
    }
}
