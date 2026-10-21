using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.IO;
using System.Text;

namespace AntiGravityRepeater {
public static class Json {
 public static JavaScriptSerializer Serializer(){return new JavaScriptSerializer {MaxJsonLength=64*1024*1024,RecursionLimit=200};}
 public static string Encode(object o){return Serializer().Serialize(o);}
 public static Dictionary<string,object> Decode(string s){return Serializer().Deserialize<Dictionary<string,object>>(s);}
 public static Dictionary<string,object> Map(object o){return o as Dictionary<string,object> ?? new Dictionary<string,object>();}
 public static object Get(object o,string key){object v;return Map(o).TryGetValue(key,out v)?v:null;}
 public static string Str(object o,string key){return Convert.ToString(Get(o,key)) ?? "";}
 public static int Int(object o,string key){int v;return int.TryParse(Str(o,key),out v)?v:0;}
 public static object[] Array(object o){var a=o as object[];if(a!=null)return a;var l=o as ArrayList;return l!=null?l.ToArray():new object[0];}
}
public class Conversation {
 public string Id,Title,Status,Modified,LastInput;
 public int Steps,LastInputIndex;
 public Endpoint Endpoint;
 public bool IsIdle {get {return Status=="CASCADE_RUN_STATUS_IDLE";}}
 public bool IsBusy {get {return Status=="CASCADE_RUN_STATUS_RUNNING" || Status=="CASCADE_RUN_STATUS_BUSY" || Status=="CASCADE_RUN_STATUS_CANCELING";}}
 public string Fingerprint {get{return Steps+"|"+LastInput+"|"+LastInputIndex+"|"+Modified;}}
 public override string ToString(){return Title+"  ["+Status.Replace("CASCADE_RUN_STATUS_","").ToLowerInvariant()+"]  "+Id.Substring(0,Math.Min(8,Id.Length));}
}
public class Settings {
 public string Prompt="continue";
 public string ConversationId="";
 public int DelaySeconds=10,MaxRepeats=0;
 public int DailySendBudget=0;public bool AutoSwitchAccounts=true;public string PendingLoginDatabase;
 public bool SendWhenInitiallyIdle=true;
 public static string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AntiGravityRepeater");
 public static Settings Load(){try{return Json.Serializer().Deserialize<Settings>(File.ReadAllText(Path.Combine(Folder,"settings.json")));}catch{return new Settings();}}
 public void Save(){Directory.CreateDirectory(Folder);string p=Path.Combine(Folder,"settings.json"),t=p+".tmp";File.WriteAllText(t,Json.Encode(this),Encoding.UTF8);if(File.Exists(p))File.Replace(t,p,null);else File.Move(t,p);}
}
// Pure transition logic. Only explicit IDLE after a stable delay can cause a send.
public class RepeatEngine {
 public bool Armed {get;private set;}
 public int Sent {get;private set;}
 public string State {get;private set;}
 public bool Pending {get;private set;}
 public bool SendInitially=true;
 public int DelaySeconds=10,MaxRepeats;
 private DateTime? idleSince,pendingSince;
 private string idleFingerprint,lastSentFingerprint,pendingInput;
 private int pendingInputIndex;
 private bool observedBusy,completedCycle,awaiting;
 public void Start(){Armed=true;Sent=0;Pending=false;idleSince=null;idleFingerprint=null;lastSentFingerprint=null;pendingSince=null;observedBusy=false;completedCycle=false;awaiting=false;State="Monitoring";}
 public void Stop(string reason){Armed=false;State=reason;idleSince=null;}
 public void ResumeAfterAccountSwitch(){Armed=true;Pending=false;idleSince=null;idleFingerprint=null;pendingSince=null;observedBusy=true;completedCycle=false;awaiting=false;State="Monitoring new account";}
 public bool Observe(Conversation c,DateTime now){
  if(!Armed)return false;
  if(c==null){idleSince=null;State="Conversation unavailable; reconnecting";return false;}
  if(MaxRepeats>0 && Sent>=MaxRepeats){Stop("Repeat limit reached");return false;}
  if(Pending){
   bool inputAdvanced=c.LastInput!=pendingInput || c.LastInputIndex>pendingInputIndex;
   if(inputAdvanced){Pending=false;awaiting=true;completedCycle=true;State="Prompt acknowledged";}
   else {if(pendingSince.HasValue && (now-pendingSince.Value).TotalSeconds>=90)Stop("Stopped: send acknowledgement was not observed. Check AntiGravity before restarting.");else State="Waiting for send acknowledgement";return false;}
  }
  if(c.IsBusy){observedBusy=true;idleSince=null;State="Agent is working";return false;}
  if(!c.IsIdle){idleSince=null;State="Unknown agent status; waiting";return false;}
  if(awaiting && !completedCycle){State="Waiting for a new agent cycle";return false;}
  if(!SendInitially && !observedBusy && !completedCycle){State="Waiting for the agent to run first";return false;}
  if(c.Fingerprint==lastSentFingerprint){idleSince=null;State="Waiting for conversation progress";return false;}
  if(!idleSince.HasValue || idleFingerprint!=c.Fingerprint){idleSince=now;idleFingerprint=c.Fingerprint;}
  double left=DelaySeconds-(now-idleSince.Value).TotalSeconds;
  if(left>0){State="Agent idle; next prompt in "+Math.Ceiling(left)+"s";return false;}
  return true;
 }
 public void MarkSending(Conversation c,DateTime now){Pending=true;pendingSince=now;pendingInput=c.LastInput;pendingInputIndex=c.LastInputIndex;lastSentFingerprint=c.Fingerprint;idleSince=null;observedBusy=false;completedCycle=false;awaiting=true;State="Sending prompt";}
 public void MarkAccepted(){Sent++;if(Armed)State="Sent; verifying acknowledgement";}
 public void SendFailed(){if(Armed)Stop("Stopped: send result is uncertain. Check conversation before restarting.");}
}
}
