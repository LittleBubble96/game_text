using System;
using System.Collections.Generic;
using GameLogic;

internal static class LevelCompletionRewardTests
{
    private static int _checks;
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); _checks++; }
    public static void Main()
    {
        var session = new LevelCompletionReward();
        Check(!session.CanClaim(1, 0), "not completed");
        var map = new Dictionary<int, int> { {10000,20}, {10001,1} };
        session.Begin(1, map);
        int first = session.Version;
        map[10000] = 999;
        Check(!session.TryClaim(1,first,false,out _), "interrupted or failed ad gives no reward");
        Check(session.CanClaim(1,first), "failed ad can retry");
        Check(!session.TryClaim(2,first,true,out _), "wrong level rejected");
        Check(!session.TryClaim(1,first-1,true,out _), "wrong completion rejected");
        Check(session.TryClaim(1,first,true,out var reward), "completed ad grants");
        Check(reward[10000]==20 && reward[10001]==1, "one additional snapshot including props");
        Check(20+reward[10000]==40, "coin total 40");
        Check(!session.TryClaim(1,first,true,out _), "duplicate callback rejected");
        Check(session.HasClaimed(1,first), "reopened finish remembers claim");
        session.Begin(2,new Dictionary<int,int>{{10000,20}});
        Check(!session.TryClaim(1,first,true,out _), "late callback cannot claim next level");
        Check(session.TryClaim(2,session.Version,true,out reward), "next completion eligible");
        session.Begin(2,new Dictionary<int,int>{{10000,20}});
        Check(session.TryClaim(2,session.Version,true,out reward), "new attempt same level eligible");
        session.Begin(3,new Dictionary<int,int>{{10000,0},{10001,-1}});
        Check(!session.CanClaim(3,session.Version), "nonpositive reward ignored");
        session.Begin(4,null);
        Check(!session.CanClaim(4,session.Version), "missing reward ignored");
        Console.WriteLine("LevelCompletionReward: " + _checks + " checks passed.");
    }
}
