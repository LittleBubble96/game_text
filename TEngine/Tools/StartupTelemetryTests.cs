// Standalone harness: compile with SdkSession, ISdk, IRewardedVideoAd and StartupTelemetry.
using System;
using System.Collections.Generic;
using System.Linq;
using GameSDK;

namespace GameSDK
{
    public static class SDK
    {
        internal static SdkSession Session;
        public static bool ReportEvent(string name, Dictionary<string,string> data) => Session.ReportEvent(name,data);
    }
}
internal sealed class TestPlatform : ISdk
{
    public readonly List<Action<string>> Success = new List<Action<string>>();
    public readonly List<Action<string>> Failure = new List<Action<string>>();
    public readonly List<KeyValuePair<string,Dictionary<string,string>>> Sent = new List<KeyValuePair<string,Dictionary<string,string>>>();
    public Action<string,Dictionary<string,string>> OnSend;
    public int NativeStarts;
    public void Login(Action<string> ok, Action<string> fail) { Success.Add(ok); Failure.Add(fail); }
    public void ReportEvent(string name, Dictionary<string,string> data) { OnSend?.Invoke(name,data); Sent.Add(new KeyValuePair<string,Dictionary<string,string>>(name,data)); }
    public IRewardedVideoAd CreateRewardedVideoAd(string id,Action loaded,Action<int,string> error,Action<bool> closed) => null;
    public string GetOpenId() => "cached_id_must_not_grant_login";
    public void ShareAppMessage(string title) { }
    public void CreateGameClubButton() { }
    public void OpenGameClub() { }
    public void ReportGameStart() { NativeStarts++; }
}
internal static class StartupTelemetryTests
{
    private static int checks;
    private static void Check(bool condition,string name) { if(!condition) throw new Exception(name); checks++; }
    public static void Main()
    {
        double now=0;
        var p=new TestPlatform();
        var s=new SdkSession(()=>now,_=>{});
        SDK.Session=s;
        var input=new Dictionary<string,string>{{"value","original"}};
        Check(s.ReportEvent("before_init",input),"accept before init");
        input["value"]="changed";
        Check(!s.IsLoggedIn && s.OpenId=="" && p.Sent.Count==0,"cached identity cannot log in");
        s.Initialize(p);s.Initialize(p);s.RetryLogin();
        Check(p.Success.Count==1,"idempotent init and no concurrent login");
        StartupTelemetry.Begin("1.0","WebPlayMode");StartupTelemetry.Success(1);
        StartupTelemetry.Start(2);StartupTelemetry.Start(2);StartupTelemetry.Fail(2,"failed",new string('x',300));
        StartupTelemetry.Start(2);StartupTelemetry.Success(2);StartupTelemetry.Success(2);
        StartupTelemetry.Skip(5,"web");StartupTelemetry.Skip(5,"web");StartupTelemetry.Skip(2,"invalid_skip");
        StartupTelemetry.Start(10);
        StartupTelemetry.Complete();StartupTelemetry.Complete();
        Check(p.Sent.Count==0,"all startup BI blocked before successful login");
        p.Success[0]("user");
        Check(s.IsLoggedIn && s.OpenId=="user" && s.PendingCount==0,"login unlocks and drains");
        Check(p.Sent[0].Value["value"]=="original","snapshot before caller mutation");
        Check(p.Sent.Select(x=>long.Parse(x.Value["event_seq"])).SequenceEqual(Enumerable.Range(1,p.Sent.Count).Select(x=>(long)x)),"FIFO including login events");
        var steps=p.Sent.Where(x=>x.Key=="startup_step").ToList();
        Check(steps.Count==7,"startup start finish skip dedup");
        Check(steps.Count(x=>x.Value["index"]=="10")==0,"index 10 reserved");
        Check(steps.Last(x=>x.Value["index"]=="2").Value["attempt"]=="2","fixed index retry counter");
        Check(steps.Single(x=>x.Value["status"]=="fail").Value["error_msg"].Length==256,"bounded error");
        Check(p.Sent.Count(x=>x.Key=="startup_complete")==1,"one overall completion");
        int sent=p.Sent.Count;p.Success[0]("duplicate");p.Failure[0]("late");
        Check(p.Sent.Count==sent && s.OpenId=="user","duplicate callbacks ignored");
        s.ReportEvent("live",null);Check(p.Sent.Last().Key=="live","postlogin immediate delivery");
        Check(!s.ReportEvent(" ",null)&&!s.ReportEvent("invalid",new Dictionary<string,string>{{" ","x"}}),"invalid payload rejected");

        p=new TestPlatform();s=new SdkSession(()=>now,_=>{});now=0;s.Initialize(p);
        now=60;s.Tick();Check(s.LoginState==SdkLoginState.Failed,"login timeout");
        now=61;s.Tick();Check(p.Success.Count==1,"backoff honored");
        now=62;s.Tick();Check(p.Success.Count==2,"automatic retry");
        p.Success[0]("stale");Check(!s.IsLoggedIn,"stale success cannot unlock");
        p.Failure[1]("network");now=66;s.Tick();p.Failure[2]("network");now=200;s.Tick();
        Check(p.Success.Count==3 && p.Sent.Count==0,"three login attempts max and queue retained");
        s.RetryLogin();p.Success[3]("recovered");Check(s.IsLoggedIn&&s.PendingCount==0,"manual login retry recovers buffered failures");

        p=new TestPlatform();s=new SdkSession(()=>now,_=>{});s.Initialize(p);p.Success[0]("");
        Check(!s.IsLoggedIn&&s.LoginState==SdkLoginState.Failed,"empty openid rejected");
        s.RetryLogin();p.Success[1]("id");
        int calls=0;string seq=null,time=null;
        p.OnSend=(name,data)=>{if(name=="broken"){calls++; if(seq==null){seq=data["event_seq"];time=data["event_time_ms"];}else Check(seq==data["event_seq"]&&time==data["event_time_ms"],"retry keeps identity and occurrence time");data["mutated"]="yes";throw new Exception("send failed");}};
        now=0;s.ReportEvent("broken",null);s.ReportEvent("tail",null);s.Tick();Check(calls==1&&s.PendingCount==2,"send failure retains FIFO without busy retry");
        now=2;s.Tick();now=6;s.Tick();now=100;s.Tick();Check(calls==3&&s.PendingCount==2,"report retries bounded");
        p.OnSend=null;s.RetryPendingEvents();Check(s.PendingCount==0&&p.Sent.Last().Key=="tail","manual report retry resumes ordered drain");
        Check(!p.Sent.First(x=>x.Key=="broken").Value.ContainsKey("mutated"),"platform mutation cannot change cached payload");
        p.OnSend=(name,data)=>{if(name=="outer")s.ReportEvent("inner",null);};s.ReportEvent("outer",null);
        Check(p.Sent[p.Sent.Count-2].Key=="outer"&&p.Sent.Last().Key=="inner","reentrant enqueue preserves order");
        p=new TestPlatform();s=new SdkSession(()=>now,_=>{});
        for(int i=0;i<512;i++)s.ReportEvent("queued",null);
        Check(!s.ReportEvent("overflow",null)&&s.PendingCount==512,"512 item capacity rejects newest");
        s.Initialize(p);p.Success[0]("id");Check(s.PendingCount==0&&p.Sent.Count==512,"full queue still drains on login");
        p=new TestPlatform();s=new SdkSession(()=>now,_=>{});s.Initialize(p);s.ReportGameStart();
        Check(p.NativeStarts==0,"native game start also waits for login");
        p.Success[0]("id");Check(p.NativeStarts==1,"native game start delivered after login");
        Console.WriteLine("SDK/startup: "+checks+" checks passed.");
    }
}
