using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AntiGravityRepeater {
public class Endpoint : IDisposable {
 public int Pid,Port;
 private string token;
 private HttpClient http;
 public Endpoint(int pid,int port,string csrf){Pid=pid;Port=port;token=csrf;http=new HttpClient(new HttpClientHandler{UseProxy=false});http.Timeout=TimeSpan.FromSeconds(20);}
 public async Task<Dictionary<string,object>> Call(string method,object body){
  if(!Regex.IsMatch(method,"^[A-Za-z]+$"))throw new ArgumentException("Invalid method");
  using(var req=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:"+Port+"/exa.language_server_pb.LanguageServerService/"+method)){
   req.Headers.TryAddWithoutValidation("x-codeium-csrf-token",token);req.Headers.TryAddWithoutValidation("Connect-Protocol-Version","1");req.Content=new StringContent(Json.Encode(body),Encoding.UTF8,"application/json");
   using(var res=await http.SendAsync(req)){string text=await res.Content.ReadAsStringAsync();if(!res.IsSuccessStatusCode)throw new InvalidOperationException("AntiGravity local service returned HTTP "+(int)res.StatusCode+" for "+method+".");return Json.Decode(text);}
  }
 }
 public async Task<List<Conversation>> Conversations(){var r=await Call("GetAllCascadeTrajectories",new {});var list=new List<Conversation>();foreach(var kv in Json.Map(Json.Get(r,"trajectorySummaries"))){var v=kv.Value;list.Add(new Conversation{Id=kv.Key,Title=Json.Str(v,"summary"),Status=Json.Str(v,"status"),Steps=Json.Int(v,"stepCount"),Modified=Json.Str(v,"lastModifiedTime"),LastInput=Json.Str(v,"lastUserInputTime"),LastInputIndex=Json.Int(v,"lastUserInputStepIndex"),Endpoint=this});}return list;}
 public async Task<Conversation> Get(string id){return (await Conversations()).FirstOrDefault(c=>c.Id==id);}
 public async Task<object[]> RecentSteps(Conversation c){var r=await Call("GetCascadeTrajectorySteps",new {cascadeId=c.Id,stepOffset=Math.Max(0,c.Steps-32)});return Json.Array(Json.Get(r,"steps"));}
 public async Task<string> GetBlockReason(Conversation c){
  return CompletionBlockReason(await RecentSteps(c));
 }
 public static object LastRelevantStep(object[] steps){
  for(int i=steps.Length-1;i>=0;i--){var step=steps[i];if(Json.Get(step,"userInput")!=null||Json.Str(step,"type")=="CORTEX_STEP_TYPE_USER_INPUT")return null;
   string type=Json.Str(step,"type"),status=Json.Str(step,"status");
   // A checkpoint can follow a terminal failure. It does not prove recovery.
   if(status=="CORTEX_STEP_STATUS_DONE"&&(type=="CORTEX_STEP_TYPE_CHECKPOINT"||type=="CORTEX_STEP_TYPE_CONVERSATION_HISTORY"))continue;
   return step;
  }
  return null;
 }
 public static string CompletionBlockReason(object[] steps){
  if(steps.Length==0)return "No trajectory steps were returned; waiting for a valid conversation.";
  var last=LastRelevantStep(steps);if(last==null)return null;string status=Json.Str(last,"status");
  if(HasErrorInLatestTurn(steps))return "The last agent step reports an error. Resolve it in AntiGravity, then start again.";
  if(status.IndexOf("WAITING",StringComparison.OrdinalIgnoreCase)>=0 || status.IndexOf("PENDING",StringComparison.OrdinalIgnoreCase)>=0 || status.IndexOf("BLOCKED",StringComparison.OrdinalIgnoreCase)>=0)return "The agent is waiting for input or approval. Resolve it in AntiGravity, then start again.";
  return null;
 }
 public static bool HasErrorInLatestTurn(object[] steps){
  // Tool failures can be recovered within the same turn. Only the final
  // relevant step decides whether the agent actually ended with an error.
  var s=LastRelevantStep(steps);return s!=null&&(Json.Str(s,"type").Contains("ERROR")||Json.Str(s,"status").Contains("ERROR")||Json.Get(s,"errorMessage")!=null||Json.Get(s,"error")!=null);
 }
 public static object FindConfig(object[] steps){
  for(int i=steps.Length-1;i>=0;i--){var input=Json.Get(steps[i],"userInput");foreach(string key in new[]{"userConfig","lastUserConfig"}){var config=Json.Get(input,key);var planner=Json.Get(config,"plannerConfig");if(Json.Get(planner,"requestedModel")!=null || !string.IsNullOrEmpty(Json.Str(planner,"modelName")))return config;}}
  return null;
 }
 public async Task<object> PrepareSend(Conversation c,string prompt){
  // This IDE requires the existing config explicitly, including model and policies.
  var latest=await Call("GetCascadeTrajectorySteps",new{cascadeId=c.Id,stepOffset=Math.Max(0,c.LastInputIndex)});
  object config=FindConfig(Json.Array(Json.Get(latest,"steps")));
  if(config==null){var trajectory=await Call("GetCascadeTrajectory",new{cascadeId=c.Id});config=FindConfig(Json.Array(Json.Get(Json.Get(trajectory,"trajectory"),"steps")));}
  if(config==null)throw new InvalidOperationException("No existing model configuration found. Send one prompt from AntiGravity first.");
  return new {cascadeId=c.Id,items=new[]{new{text=prompt}},cascadeConfig=config};
 }
 public async Task Send(Conversation c,string prompt){await Call("SendUserCascadeMessage",await PrepareSend(c,prompt));}
 public void Dispose(){http.Dispose();token=null;}
}
public static class Discovery {
 [DllImport("iphlpapi.dll",SetLastError=true)]static extern uint GetExtendedTcpTable(IntPtr table,ref int size,bool order,int family,int tableClass,uint reserved);
 public static Dictionary<int,List<int>> Ports(){int size=0;GetExtendedTcpTable(IntPtr.Zero,ref size,false,2,5,0);var p=Marshal.AllocHGlobal(size);try{uint result=GetExtendedTcpTable(p,ref size,false,2,5,0);if(result!=0)throw new InvalidOperationException("Cannot enumerate local TCP listeners.");int count=Marshal.ReadInt32(p);var map=new Dictionary<int,List<int>>();for(int i=0;i<count;i++){IntPtr row=IntPtr.Add(p,4+i*24);int state=Marshal.ReadInt32(row),pid=Marshal.ReadInt32(row,20);if(state!=2)continue;byte a=Marshal.ReadByte(row,8),b=Marshal.ReadByte(row,9);int port=(a<<8)|b;List<int> ports;if(!map.TryGetValue(pid,out ports)){ports=new List<int>();map[pid]=ports;}if(!ports.Contains(port))ports.Add(port);}return map;}finally{Marshal.FreeHGlobal(p);}}
 public static async Task<List<Endpoint>> Find(){
  var candidates=await Task.Run(()=>{var ports=Ports();var output=new List<Endpoint>();using(var search=new ManagementObjectSearcher("SELECT ProcessId, CommandLine, ExecutablePath FROM Win32_Process WHERE Name = 'language_server_windows_x64.exe'")){foreach(ManagementObject p in search.Get()){string cmd=Convert.ToString(p["CommandLine"]),path=Convert.ToString(p["ExecutablePath"]);if(path.IndexOf("Antigravity",StringComparison.OrdinalIgnoreCase)<0 || !cmd.Contains("--csrf_token"))continue;var match=Regex.Match(cmd,"--csrf_token(?:=|\\s+)([^\\s\\\"]+)");if(!match.Success)continue;int pid=Convert.ToInt32(p["ProcessId"]);List<int> ps;if(ports.TryGetValue(pid,out ps))foreach(int port in ps)output.Add(new Endpoint(pid,port,match.Groups[1].Value));}}return output;});
  var checks=candidates.Select(async e=>{try{await e.Conversations();return e;}catch{e.Dispose();return null;}}).ToArray();return (await Task.WhenAll(checks)).Where(e=>e!=null).ToList();
 }
}
}
