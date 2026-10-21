using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AntiGravityRepeater {
public static partial class Diagnostics {
 static int assertions;
 static void Assert(bool value,string message){assertions++;if(!value)throw new Exception("FAIL: "+message);}
 public static int Main(string[] args){try{Run(args).GetAwaiter().GetResult();return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
 static Conversation C(string status,int steps,string input){return new Conversation{Id="test",Status="CASCADE_RUN_STATUS_"+status,Steps=steps,Modified=steps.ToString(),LastInput=input,LastInputIndex=steps-2};}
 static void UnitTests(){
  DateTime t=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);var e=new RepeatEngine{DelaySeconds=5,MaxRepeats=2};e.Start();
  Assert(!e.Observe(C("RUNNING",3,"a"),t),"never send while running");Assert(!e.Observe(C("IDLE",4,"a"),t),"idle requires debounce");Assert(!e.Observe(C("IDLE",4,"a"),t.AddSeconds(4)),"delay enforced");Assert(e.Observe(C("IDLE",4,"a"),t.AddSeconds(5)),"send after stable completion");
  var sent=C("IDLE",4,"a");e.MarkSending(sent,t.AddSeconds(5));e.MarkAccepted();Assert(!e.Observe(sent,t.AddSeconds(40)),"unchanged idle cannot duplicate send");Assert(!e.Observe(C("RUNNING",7,"b"),t.AddSeconds(41)),"acknowledged running cycle waits");Assert(!e.Observe(C("IDLE",8,"b"),t.AddSeconds(42)),"second cycle debounces");Assert(e.Observe(C("IDLE",8,"b"),t.AddSeconds(47)),"second completed cycle triggers");e.MarkSending(C("IDLE",8,"b"),t.AddSeconds(47));e.MarkAccepted();Assert(!e.Observe(C("IDLE",11,"c"),t.AddSeconds(70))&&!e.Armed,"repeat limit stops loop");
  e=new RepeatEngine{DelaySeconds=5,SendInitially=false};e.Start();Assert(!e.Observe(C("IDLE",2,"a"),t.AddSeconds(100)),"wait-for-run mode does not send initially");e.Observe(C("RUNNING",3,"a"),t);e.Observe(C("IDLE",4,"a"),t);Assert(e.Observe(C("IDLE",4,"a"),t.AddSeconds(5)),"wait-for-run releases after completion");
  e=new RepeatEngine{DelaySeconds=5};e.Start();e.Observe(C("IDLE",4,"a"),t);Assert(!e.Observe(C("UNSPECIFIED",4,"a"),t.AddSeconds(8)),"unknown status is never idle");Assert(!e.Observe(C("IDLE",4,"a"),t.AddSeconds(9)),"unknown resets debounce");Assert(!e.Observe(null,t.AddSeconds(20)),"disconnect does not send");Assert(!e.Observe(C("IDLE",4,"a"),t.AddSeconds(21)),"reconnect resets debounce");e.Stop("stop");Assert(!e.Observe(C("IDLE",4,"a"),t.AddSeconds(99)),"stop prevents later send");
  e=new RepeatEngine();e.Start();e.MarkSending(C("IDLE",4,"a"),t);e.MarkAccepted();e.Observe(C("IDLE",4,"a"),t.AddSeconds(91));Assert(!e.Armed,"missing acknowledgement stops instead of repeating");
  e=new RepeatEngine();e.Start();e.SendFailed();Assert(!e.Armed,"uncertain send stops");
  e=new RepeatEngine{DelaySeconds=5};e.Start();e.Observe(C("IDLE",4,"a"),t);Assert(!e.Observe(C("IDLE",5,"a"),t.AddSeconds(5)),"new idle progress restarts stability delay");Assert(e.Observe(C("IDLE",5,"a"),t.AddSeconds(10)),"stable updated idle sends");
  e.MarkSending(C("IDLE",5,"a"),t.AddSeconds(10));e.MarkAccepted();Assert(!e.Observe(C("IDLE",8,"b"),t.AddSeconds(11)),"fast response acknowledgement still debounces");Assert(e.Observe(C("IDLE",8,"b"),t.AddSeconds(16)),"fast completed response can repeat without observed running");
  string sample="continue\nहैलो \"quoted\"";Assert(Json.Str(Json.Decode(Json.Encode(new{text=sample})),"text")==sample,"multiline Unicode JSON round trip");
  object input=Json.Decode("{\"userInput\":{\"userConfig\":{\"plannerConfig\":{\"requestedModel\":{\"model\":\"test-model\"},\"toolConfig\":{\"policy\":\"unchanged\"}}}}}");
  object err=Json.Decode("{\"type\":\"CORTEX_STEP_TYPE_ERROR_MESSAGE\",\"status\":\"CORTEX_STEP_STATUS_DONE\",\"errorMessage\":{\"message\":\"failure\"}}");
  Assert(Endpoint.HasErrorInLatestTurn(new[]{input,err}),"DONE error-message steps are failures");
  Assert(!Endpoint.HasErrorInLatestTurn(new[]{err,input}),"previous turn errors do not block a recovered turn");
  var preserved=Endpoint.FindConfig(new[]{input,Json.Decode("{\"userInput\":{}}")});
  Assert(Json.Str(Json.Get(Json.Get(preserved,"plannerConfig"),"requestedModel"),"model")=="test-model","follow-ups retain the model");
  Assert(Json.Str(Json.Get(Json.Get(preserved,"plannerConfig"),"toolConfig"),"policy")=="unchanged","follow-ups retain permission settings");
  e.Stop("User stopped");e.MarkAccepted();Assert(e.State=="User stopped"&&!e.Armed,"late send callback preserves Stop");
  Console.WriteLine("PASS: "+assertions+" assertions");
 }
 static async Task Run(string[] args){UnitTests();AccountUnitTests();RecoveryUnitTests();if(args.Length>0&&args[0]=="--recovery-probe"){await RecoveryProbe();return;}if(args.Length>0&&args[0]=="--accounts-test"){await LiveAccountsTest();return;}if(args.Length==0 || args[0]=="--self-test")return;
  var eps=await Discovery.Find();try{var all=new List<Conversation>();foreach(var foundEndpoint in eps)all.AddRange(await foundEndpoint.Conversations());Console.WriteLine("Local endpoints: "+eps.Count+"; conversations: "+all.Count);foreach(var item in all)Console.WriteLine(item.Id+" | "+item.Title+" | "+item.Status+" | "+item.Steps+" steps");
   if(args[0]=="--probe")return;if(args[0]!="--live-test")throw new Exception("Use --self-test, --probe or --live-test");
   var seed=all.OrderByDescending(item=>item.Modified).FirstOrDefault();if(seed==null)throw new Exception("No conversation available to identify model configuration.");var ep=seed.Endpoint;
   var tr=await ep.Call("GetCascadeTrajectory",new{cascadeId=seed.Id});var steps=Json.Array(Json.Get(Json.Get(tr,"trajectory"),"steps"));
   object model=null;foreach(var s in steps){var uc=Json.Get(Json.Get(s,"userInput"),"userConfig");var m=Json.Get(Json.Get(uc,"plannerConfig"),"requestedModel");if(m!=null)model=m;}
   if(model==null)throw new Exception("Cannot identify the existing model for isolated test.");
   Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory,"tests","live-workspace"));
   var start=await ep.Call("StartCascade",new{source="CORTEX_TRAJECTORY_SOURCE_CASCADE_CLIENT",trajectoryType="CORTEX_TRAJECTORY_TYPE_CASCADE",workspaceUris=new[]{new Uri(Path.Combine(Environment.CurrentDirectory,"tests","live-workspace")+Path.DirectorySeparatorChar).AbsoluteUri}});
   string id=Json.Str(start,"cascadeId");if(string.IsNullOrEmpty(id))throw new Exception("StartCascade returned no ID");Console.WriteLine("Isolated test conversation: "+id);
   string prompt="This is a harmless desktop controller integration test. Reply with exactly REPEATER_OK and nothing else. Do not use tools, access files, run commands, browse, or perform any task.";
   await ep.Call("SendUserCascadeMessage",new{cascadeId=id,items=new[]{new{text=prompt}},cascadeConfig=new{plannerConfig=new{requestedModel=model}}});Console.WriteLine("Initial test prompt accepted.");
   int repeatTarget=args.Length>1?int.Parse(args[1]):2;if(repeatTarget<1||repeatTarget>8)throw new Exception("Repeat test target must be 1 to 8.");var eng=new RepeatEngine{DelaySeconds=3,MaxRepeats=repeatTarget,SendInitially=true};eng.Start();DateTime deadline=DateTime.UtcNow.AddMinutes(8);var events=new List<object>();string state="";Conversation c=null;
   while(DateTime.UtcNow<deadline && eng.Armed){c=await ep.Get(id);if(c==null)throw new Exception("Test conversation disappeared");if(eng.Observe(c,DateTime.UtcNow)){
     string blocked=await ep.GetBlockReason(c);if(blocked!=null)throw new Exception(blocked);var fresh=await ep.Get(id);if(fresh.IsIdle&&fresh.Fingerprint==c.Fingerprint){eng.MarkSending(fresh,DateTime.UtcNow);try{await ep.Send(fresh,prompt);eng.MarkAccepted();events.Add(new{time=DateTime.UtcNow.ToString("o"),action="repeat",id=id,steps=fresh.Steps});Console.WriteLine("Repeat "+eng.Sent+" accepted in SAME conversation.");}catch{eng.SendFailed();throw;}}}
    if(state!=eng.State){state=eng.State;Console.WriteLine(state);}await Task.Delay(1000);
   }
   Assert(eng.Sent==repeatTarget,"live repeat count");Assert(!eng.Armed,"live repeat limit");
   // Wait for the final response, then prove all three user messages and replies exist in this ID.
   deadline=DateTime.UtcNow.AddMinutes(4);do{c=await ep.Get(id);if(c.IsIdle)break;await Task.Delay(1000);}while(DateTime.UtcNow<deadline);
   Assert(c.IsIdle,"final test response completed");var final=await ep.Call("GetCascadeTrajectory",new{cascadeId=id});var fs=Json.Array(Json.Get(Json.Get(final,"trajectory"),"steps"));int inputs=fs.Count(x=>Json.Get(x,"userInput")!=null);int replies=fs.Count(x=>Json.Str(Json.Get(x,"plannerResponse"),"response").Contains("REPEATER_OK")||Json.Str(Json.Get(x,"plannerResponse"),"modifiedResponse").Contains("REPEATER_OK"));
   Assert(inputs==repeatTarget+1,"same history contains the initial prompt and every repeat");Assert(replies>=repeatTarget+1,"every agent reply verified");
   var report=new{testedAtUtc=DateTime.UtcNow.ToString("o"),result="PASS",conversationId=id,initialMessages=1,repeats=eng.Sent,totalUserMessages=inputs,verifiedReplies=replies,finalStatus=c.Status,events=events,assertions=assertions};Directory.CreateDirectory("tests");File.WriteAllText("tests/live-test-result.json",Json.Encode(report));Console.WriteLine(Json.Encode(report));
  }finally{foreach(var ep in eps)ep.Dispose();}
 }
}
}
