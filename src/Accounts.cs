using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Globalization;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace AntiGravityRepeater {
// Minimal protobuf reader for the IDE's persisted Topic and UserStatus messages.
public static class Proto {
 public class Field {public int Id,Wire;public byte[] Bytes;public ulong Number;}
 public static List<Field> Read(byte[] b){var r=new List<Field>();int p=0;while(p<b.Length){ulong tag=Var(b,ref p);var f=new Field{Id=(int)(tag>>3),Wire=(int)(tag&7)};if(f.Id==0)throw new InvalidDataException("Invalid protobuf field.");if(f.Wire==0)f.Number=Var(b,ref p);else{int n=f.Wire==2?checked((int)Var(b,ref p)):f.Wire==5?4:f.Wire==1?8:-1;if(n<0||n>b.Length-p)throw new InvalidDataException("Invalid protobuf data.");f.Bytes=new byte[n];Buffer.BlockCopy(b,p,f.Bytes,0,n);p+=n;}r.Add(f);}return r;}
 static ulong Var(byte[] b,ref int p){ulong v=0;for(int s=0;s<64;s+=7){if(p>=b.Length)throw new InvalidDataException();byte x=b[p++];v|=(ulong)(x&127)<<s;if((x&128)==0)return v;}throw new InvalidDataException();}
 public static byte[] Bytes(byte[] b,int id){var f=Read(b).FirstOrDefault(x=>x.Id==id);return f==null?new byte[0]:f.Bytes??new byte[0];}
 public static string Text(byte[] b,int id){return Encoding.UTF8.GetString(Bytes(b,id));}
 public static ulong Num(byte[] b,int id){var f=Read(b).FirstOrDefault(x=>x.Id==id);return f==null?0:f.Number;}
 public static byte[] TopicValue(string raw,string key){foreach(var entry in Read(Convert.FromBase64String(raw)).Where(x=>x.Id==1)){if(Text(entry.Bytes,1)==key)return Convert.FromBase64String(Text(Bytes(entry.Bytes,2),1));}return new byte[0];}
}
public class ModelQuota {
 public string Label,ModelKey,ResetUtc;public double? Remaining;public bool Disabled;
 public string RemainingText {get{return Remaining.HasValue?(Remaining.Value*100).ToString("0.#",CultureInfo.InvariantCulture)+"%":"Not reported";}}
 public string UsedText {get{return Remaining.HasValue?((1-Remaining.Value)*100).ToString("0.#",CultureInfo.InvariantCulture)+"%":"Not reported";}}
 public string ResetText {get{DateTime t;if(!DateTime.TryParse(ResetUtc,null,DateTimeStyles.RoundtripKind,out t))return "Not reported";return UsageClock.India(t.ToUniversalTime()).ToString("dd MMM, HH:mm")+" IST";}}
}
public class UsageSnapshot {
 public string Email,Name,Plan;public int? PromptCredits,FlowCredits;public DateTime CheckedUtc;public List<ModelQuota> Models=new List<ModelQuota>();
 public static string ModelKeyFromRequest(object request){var body=Json.Decode(Json.Encode(request));return Json.Encode(Json.Get(Json.Get(Json.Get(body,"cascadeConfig"),"plannerConfig"),"requestedModel"));}
 public static double? Fraction(object value,bool quotaExists){if(!quotaExists)return null;if(value==null)return 0;double n;if(!double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out n)||double.IsNaN(n)||double.IsInfinity(n))return null;return Math.Max(0,Math.Min(1,n));}
 public static UsageSnapshot FromJson(object response){var u=Json.Get(response,"userStatus");var plan=Json.Get(u,"planStatus");var result=new UsageSnapshot{Email=Json.Str(u,"email"),Name=Json.Str(u,"name"),Plan=Json.Str(Json.Get(plan,"planInfo"),"planName"),CheckedUtc=DateTime.UtcNow};int n;if(int.TryParse(Json.Str(plan,"availablePromptCredits"),out n))result.PromptCredits=n;if(int.TryParse(Json.Str(plan,"availableFlowCredits"),out n))result.FlowCredits=n;
 foreach(var model in Json.Array(Json.Get(Json.Get(u,"cascadeModelConfigData"),"clientModelConfigs"))){var qi=Json.Get(model,"quotaInfo");result.Models.Add(new ModelQuota{Label=Json.Str(model,"label"),ModelKey=Json.Encode(Json.Get(model,"modelOrAlias")),Remaining=Fraction(Json.Get(qi,"remainingFraction"),qi!=null),ResetUtc=Json.Str(qi,"resetTime"),Disabled=Json.Get(model,"disabled") is bool&&(bool)Json.Get(model,"disabled")});}return result;}
 public static UsageSnapshot FromTopic(string topic){var b=Proto.TopicValue(topic,"userStatusSentinelKey");if(b.Length==0)throw new InvalidDataException("No signed-in account found.");var p=Proto.Bytes(b,13);var result=new UsageSnapshot{Email=Proto.Text(b,7),Name=Proto.Text(b,3),Plan=Proto.Text(Proto.Bytes(p,1),2),CheckedUtc=DateTime.UtcNow};foreach(var m in Proto.Read(Proto.Bytes(b,33)).Where(x=>x.Id==1)){var qb=Proto.Bytes(m.Bytes,15);var f=Proto.Read(qb).FirstOrDefault(x=>x.Id==1);double? remaining=qb.Length==0?(double?)null:f==null?0:UsageSnapshot.Fraction(BitConverter.ToSingle(f.Bytes,0),true);var ts=Proto.Bytes(qb,2);string reset="";try{reset=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(Proto.Num(ts,1)).ToString("o");}catch{}result.Models.Add(new ModelQuota{Label=Proto.Text(m.Bytes,1),ModelKey=Proto.Num(Proto.Bytes(m.Bytes,2),1).ToString(),Remaining=remaining,ResetUtc=reset,Disabled=Proto.Num(m.Bytes,4)!=0});}return result;}
}
public static class UsageClock {public static DateTime India(DateTime utc){return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));}public static string Day(DateTime utc){return India(utc).ToString("yyyy-MM-dd");}}
public class BudgetLedger {
 public string Day="";public int Attempted;
 public static BudgetLedger Load(){try{return Json.Serializer().Deserialize<BudgetLedger>(File.ReadAllText(Path.Combine(Settings.Folder,"budget.json")));}catch{return new BudgetLedger();}}
 public int Used(DateTime now){if(Day!=UsageClock.Day(now)){Day=UsageClock.Day(now);Attempted=0;}return Attempted;}
 public bool CanSend(int limit,DateTime now){return limit<=0||Used(now)<limit;}
 public void Reserve(DateTime now){Used(now);Attempted++;Directory.CreateDirectory(Settings.Folder);Atomic.Write(Path.Combine(Settings.Folder,"budget.json"),Encoding.UTF8.GetBytes(Json.Encode(this)));}
}
public static class Atomic {public static void Write(string path,byte[] bytes){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string tmp=path+".tmp";File.WriteAllBytes(tmp,bytes);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}}
public class LoginAccount {
 public string Id=Guid.NewGuid().ToString("N"),Email,Name,SourceDatabase;public DateTime SavedUtc;public Dictionary<string,string> Records=new Dictionary<string,string>();public UsageSnapshot Usage;
 public override string ToString(){return (string.IsNullOrEmpty(Name)?Email:Name+"  ·  "+Email);}
}
public class AccountVault {
 public List<LoginAccount> Accounts=new List<LoginAccount>();
 static byte[] entropy=Encoding.UTF8.GetBytes("AntiGravityRepeater.Accounts.v1");
 public static string DefaultPath {get{return Path.Combine(Settings.Folder,"accounts.dpapi");}}
 public static AccountVault Load(string path=null){path=path??DefaultPath;if(!File.Exists(path))return new AccountVault();try{byte[] data=ProtectedData.Unprotect(File.ReadAllBytes(path),entropy,DataProtectionScope.CurrentUser);try{return Json.Serializer().Deserialize<AccountVault>(Encoding.UTF8.GetString(data));}finally{Array.Clear(data,0,data.Length);}}catch{throw new InvalidDataException("Saved account vault could not be decrypted by this Windows user. It has not been overwritten.");}}
 public void Save(string path=null){byte[] bytes=Encoding.UTF8.GetBytes(Json.Encode(this));try{Atomic.Write(path??DefaultPath,ProtectedData.Protect(bytes,entropy,DataProtectionScope.CurrentUser));}finally{Array.Clear(bytes,0,bytes.Length);}}
 public void Upsert(LoginAccount account){int i=Accounts.FindIndex(a=>string.Equals(a.Email,account.Email,StringComparison.OrdinalIgnoreCase));if(i>=0){account.Id=Accounts[i].Id;Accounts[i]=account;}else Accounts.Add(account);}
}
// Windows ships this SQLite library. Only the three login-related rows may be written.
public sealed class LoginState : IDisposable {
 public const string Prefix="antigravityUnifiedStateSync.";
 public static readonly string[] AllowedKeys={Prefix+"oauthToken",Prefix+"userStatus",Prefix+"modelCredits"};
 IntPtr db;
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open_v2(byte[] path,out IntPtr db,int flags,IntPtr vfs);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr db);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int len,out IntPtr stmt,IntPtr tail);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_step(IntPtr stmt);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_finalize(IntPtr stmt);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_text(IntPtr stmt,int col);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_bytes(IntPtr stmt,int col);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_text(IntPtr stmt,int index,byte[] value,int len,IntPtr destructor);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_busy_timeout(IntPtr db,int ms);
 static byte[] Utf(string s){return Encoding.UTF8.GetBytes(s+"\0");}
 public LoginState(string path,bool writable=false,bool create=false){int rc=sqlite3_open_v2(Utf(path),out db,writable?(create?6:2):1,IntPtr.Zero);if(rc!=0){Dispose();throw new IOException("Cannot open AntiGravity account database.");}sqlite3_busy_timeout(db,5000);}
 IntPtr Prepare(string sql){IntPtr st;if(sqlite3_prepare_v2(db,Utf(sql),-1,out st,IntPtr.Zero)!=0)throw new IOException("Account database operation failed.");return st;}
 public void Execute(string sql){IntPtr st=Prepare(sql);try{if(sqlite3_step(st)!=101)throw new IOException("Account database transaction failed.");}finally{sqlite3_finalize(st);}}
 public string Get(string key){IntPtr st=Prepare("SELECT value FROM ItemTable WHERE key=?1");try{Bind(st,1,key);int rc=sqlite3_step(st);if(rc==101)return null;if(rc!=100)throw new IOException("Account database read failed.");int n=sqlite3_column_bytes(st,0);var bytes=new byte[n];Marshal.Copy(sqlite3_column_text(st,0),bytes,0,n);return Encoding.UTF8.GetString(bytes);}finally{sqlite3_finalize(st);}}
 static void Bind(IntPtr st,int index,string value){byte[] b=Utf(value);if(sqlite3_bind_text(st,index,b,b.Length-1,new IntPtr(-1))!=0)throw new IOException("Account database binding failed.");}
 public void Set(string key,string value){IntPtr st=Prepare(value==null?"DELETE FROM ItemTable WHERE key=?1":"INSERT OR REPLACE INTO ItemTable(key,value) VALUES(?1,?2)");try{Bind(st,1,key);if(value!=null)Bind(st,2,value);if(sqlite3_step(st)!=101)throw new IOException("Account database update failed.");}finally{sqlite3_finalize(st);}}
 public void ReplaceLogin(Dictionary<string,string> records,Action afterFirstWrite=null){if(records.Keys.Any(k=>!AllowedKeys.Contains(k))||!records.ContainsKey(Prefix+"oauthToken")||!records.ContainsKey(Prefix+"userStatus"))throw new InvalidDataException("Incomplete or invalid saved login.");Execute("BEGIN IMMEDIATE");try{int count=0;foreach(string key in AllowedKeys){string value;records.TryGetValue(key,out value);Set(key,value);if(++count==1&&afterFirstWrite!=null)afterFirstWrite();}Execute("COMMIT");}catch{try{Execute("ROLLBACK");}catch{}throw;}}
 public void Dispose(){if(db!=IntPtr.Zero){sqlite3_close(db);db=IntPtr.Zero;}}
 public static string DefaultDatabase {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Antigravity IDE","User","globalStorage","state.vscdb");}}
 public static LoginAccount Capture(string database=null){database=database??DefaultDatabase;var records=new Dictionary<string,string>();using(var s=new LoginState(database)){foreach(string key in AllowedKeys){string value=s.Get(key);if(value!=null)records[key]=value;}}
 string oauth,user;if(!records.TryGetValue(Prefix+"oauthToken",out oauth)||!records.TryGetValue(Prefix+"userStatus",out user))throw new InvalidDataException("Finish signing in to AntiGravity first.");var token=Proto.TopicValue(oauth,"oauthTokenInfoSentinelKey");if(string.IsNullOrEmpty(Proto.Text(token,1))||string.IsNullOrEmpty(Proto.Text(token,3)))throw new InvalidDataException("The login does not include a reusable session. Sign in through AntiGravity again.");var usage=UsageSnapshot.FromTopic(user);if(string.IsNullOrEmpty(usage.Email))throw new InvalidDataException("Signed-in account identity is missing.");return new LoginAccount{Email=usage.Email,Name=usage.Name,SavedUtc=DateTime.UtcNow,Records=records,SourceDatabase=database,Usage=usage};}
}
public static class RotationPolicy {
 public static LoginAccount Next(IEnumerable<LoginAccount> accounts,string currentEmail,string modelLabel,DateTime now,ISet<string> attempted=null){return accounts.Where(a=>!string.Equals(a.Email,currentEmail,StringComparison.OrdinalIgnoreCase)&&a.Usage!=null&&(now-a.SavedUtc).TotalHours<24&&(attempted==null||!attempted.Contains(a.Id))).Select(a=>new{Account=a,Quota=a.Usage.Models.FirstOrDefault(m=>m.Label==modelLabel&&!m.Disabled&&m.Remaining.HasValue&&m.Remaining.Value>0)}).Where(x=>x.Quota!=null).OrderByDescending(x=>x.Quota.Remaining.Value).Select(x=>x.Account).FirstOrDefault();}
 public static bool CanSwitch(IEnumerable<Conversation> conversations){return conversations.All(c=>c.IsIdle);}
 public static bool Exhausted(ModelQuota q){return q!=null&&!q.Disabled&&q.Remaining.HasValue&&q.Remaining.Value<=0;}
}
public class AccountSwitcher {
 public static string Executable {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Antigravity IDE","Antigravity IDE.exe");}}
 public static string NewLogin(string existingDatabase=null){string basePath=Path.GetFullPath(Path.Combine(Settings.Folder,"login-profiles"));string path=existingDatabase==null?Path.Combine(basePath,Guid.NewGuid().ToString("N")):Path.GetFullPath(Path.Combine(Path.GetDirectoryName(existingDatabase),"..",".."));if(!path.StartsWith(basePath+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Invalid login profile path.");Directory.CreateDirectory(path);Process.Start(new ProcessStartInfo(Executable,"--user-data-dir \""+path+"\" --new-window"){UseShellExecute=true});return Path.Combine(path,"User","globalStorage","state.vscdb");}
 public static void CommitAndLaunch(string database,LoginAccount target,Action launch){
  var previous=new Dictionary<string,string>();using(var state=new LoginState(database)){foreach(string key in LoginState.AllowedKeys){var value=state.Get(key);if(value!=null)previous[key]=value;}}
  using(var state=new LoginState(database,true))state.ReplaceLogin(target.Records);
  try{launch();}catch{using(var state=new LoginState(database,true))state.ReplaceLogin(previous);throw new IOException("The IDE could not be launched. The previous login was restored.");}
 }
 static bool IsProcessAlive(int pid){try{using(var p=Process.GetProcessById(pid))return !p.HasExited;}catch{return false;}}
 public static async Task Apply(LoginAccount target,CancellationToken cancellation=default(CancellationToken)){
  // Do not close any IDE until every reachable agent reports an explicit idle state.
  var endpoints=await Discovery.Find();var serverIds=endpoints.Select(e=>e.Pid).Distinct().ToArray();if(endpoints.Count==0)throw new InvalidOperationException("AntiGravity is not connected.");try{DateTime idleDeadline=DateTime.UtcNow.AddMinutes(10);for(;;){cancellation.ThrowIfCancellationRequested();bool idle=true;foreach(var ep in endpoints){var cs=await ep.Conversations();if(!RotationPolicy.CanSwitch(cs))idle=false;}if(idle)break;if(DateTime.UtcNow>=idleDeadline)throw new InvalidOperationException("Account switching timed out while waiting for other agents to become idle. The current login is unchanged.");await Task.Delay(2500,cancellation);}}finally{foreach(var ep in endpoints)ep.Dispose();}
  cancellation.ThrowIfCancellationRequested();var previous=LoginState.Capture();var recovery=new AccountVault();recovery.Upsert(previous);recovery.Save(Path.Combine(Settings.Folder,"last-login-backup.dpapi"));
  var processes=Process.GetProcessesByName("Antigravity IDE");foreach(var p in processes){try{if(p.MainWindowHandle!=IntPtr.Zero)p.CloseMainWindow();}finally{p.Dispose();}}
  DateTime deadline=DateTime.UtcNow.AddSeconds(20);while(Process.GetProcessesByName("Antigravity IDE").Length>0&&DateTime.UtcNow<deadline)await Task.Delay(400);
  if(Process.GetProcessesByName("Antigravity IDE").Length>0)throw new InvalidOperationException("AntiGravity did not close cleanly. Login records were left unchanged; no process was forced to close.");
  DateTime serverDeadline=DateTime.UtcNow.AddSeconds(10);while(serverIds.Any(IsProcessAlive)&&DateTime.UtcNow<serverDeadline)await Task.Delay(300);if(serverIds.Any(IsProcessAlive)){Process.Start(new ProcessStartInfo(Executable){UseShellExecute=true});throw new InvalidOperationException("A persistent language server is still running. Login records were left unchanged.");}
  if(cancellation.IsCancellationRequested){Process.Start(new ProcessStartInfo(Executable){UseShellExecute=true});cancellation.ThrowIfCancellationRequested();}
  try{CommitAndLaunch(LoginState.DefaultDatabase,target,()=>{Process.Start(new ProcessStartInfo(Executable){UseShellExecute=true});});}catch{try{Process.Start(new ProcessStartInfo(Executable){UseShellExecute=true});}catch{}throw;}
 }
}
}
