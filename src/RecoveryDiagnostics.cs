using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
namespace AntiGravityRepeater {
public static partial class Diagnostics {
 static object RecoveryStep(string type,string status){return Json.Decode("{\"type\":\"CORTEX_STEP_TYPE_"+type+"\",\"status\":\"CORTEX_STEP_STATUS_"+status+"\"}");}
 static void RecoveryUnitTests(){
  var failedTool=RecoveryStep("CODE_ACTION","ERROR");var reply=RecoveryStep("PLANNER_RESPONSE","DONE");var edit=RecoveryStep("CODE_ACTION","DONE");var checkpoint=RecoveryStep("CHECKPOINT","DONE");var input=Json.Decode("{\"userInput\":{}}");
  Assert(Endpoint.CompletionBlockReason(new[]{failedTool,reply,edit,reply})==null,"recovered file-edit failure does not stop the next continuation");
  Assert(!Endpoint.HasErrorInLatestTurn(new[]{input,failedTool,reply}),"a successful response supersedes an earlier tool error");
  Assert(Endpoint.CompletionBlockReason(new[]{failedTool,reply,checkpoint})==null,"checkpoint after recovered failure remains successful");
  Assert(Endpoint.CompletionBlockReason(new[]{failedTool,checkpoint})!=null,"checkpoint cannot mask an unresolved terminal failure");
  Assert(Endpoint.CompletionBlockReason(new[]{reply,failedTool})!=null,"new terminal failure still stops instead of being hidden by old success");
  var terminalError=Json.Decode("{\"type\":\"CORTEX_STEP_TYPE_ERROR_MESSAGE\",\"status\":\"CORTEX_STEP_STATUS_DONE\",\"errorMessage\":{\"message\":\"Authentication required\"}}");
  Assert(Endpoint.CompletionBlockReason(new[]{terminalError,checkpoint})!=null,"terminal authentication error remains visible");
  Assert(Endpoint.CompletionBlockReason(new[]{RecoveryStep("RUN_COMMAND","WAITING_FOR_APPROVAL")})!=null,"pending approval is not treated as completed work");
  Assert(Endpoint.CompletionBlockReason(new[]{RecoveryStep("RUN_COMMAND","BLOCKED")})!=null,"blocked approval remains blocked");
  Assert(Endpoint.CompletionBlockReason(new[]{terminalError,input,reply})==null,"an older failed turn does not contaminate the new completed turn");
  DateTime t=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);var engine=new RepeatEngine{DelaySeconds=3};engine.Start();var first=C("IDLE",4,"a");engine.Observe(first,t);Assert(engine.Observe(first,t.AddSeconds(3)),"first completed cycle schedules a continuation");engine.MarkSending(first,t.AddSeconds(3));engine.MarkAccepted();engine.Observe(C("RUNNING",7,"b"),t.AddSeconds(4));var next=C("IDLE",9,"b");Assert(!engine.Observe(next,t.AddSeconds(5)),"second completion starts its own timer");Assert(Endpoint.CompletionBlockReason(new[]{failedTool,reply})==null&&engine.Observe(next,t.AddSeconds(8)),"second timer fires despite the recovered intermediate error");engine.MarkSending(next,t.AddSeconds(8));engine.MarkAccepted();Assert(engine.Sent==2&&engine.Armed,"repeat loop remains armed after the second prompt");
  Console.WriteLine("PASS: "+assertions+" assertions including recovered-error continuation regression");
 }
 static async Task RecoveryProbe(){var endpoints=await Discovery.Find();try{foreach(var ep in endpoints){try{var c=await ep.Get("cc577ebe-91e0-4b75-860d-27ed425c147f");if(c==null)continue;var response=await ep.Call("GetCascadeTrajectorySteps",new{cascadeId=c.Id,stepOffset=1289});var steps=Json.Array(Json.Get(response,"steps")).Take(32).ToArray();if(steps.Length!=32)continue;bool earlierError=steps.Take(steps.Length-1).Any(s=>Json.Str(s,"status").Contains("ERROR"));Assert(earlierError,"reported real trajectory contains the intermediate tool failure");Assert(Json.Str(steps[steps.Length-1],"status")=="CORTEX_STEP_STATUS_DONE","reported real trajectory subsequently completed successfully");Assert(Endpoint.CompletionBlockReason(steps)==null,"reported real trajectory now allows continuation");var result=new{result="PASS",testedAtUtc=DateTime.UtcNow.ToString("o"),reportedConversationId=c.Id,examinedStepOffset=1289,examinedSteps=steps.Length,intermediateErrorFound=earlierError,completedSuccessfully=true,continuationAllowed=true,assertions=assertions};Directory.CreateDirectory("tests");File.WriteAllText("tests/recovered-error-test-result.json",Json.Encode(result));Console.WriteLine(Json.Encode(result));return;}catch(InvalidOperationException){}}throw new Exception("The recorded conversation could not be read for the recovery probe.");}finally{foreach(var ep in endpoints)ep.Dispose();}}
}
}
