param([string]$Owner='vikrant-project',[string]$Repo='antigravity-auto-continue')
$ErrorActionPreference='Stop'
if(-not $env:GH_TOKEN){throw 'Supply GH_TOKEN in process environment.'}
$headers=@{Authorization=('Bearer '+$env:GH_TOKEN);Accept='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28';'User-Agent'='AntiGravity-Repeater-Publisher'}
function Api($Method,$Path,$Body=$null){
  $args=@{Method=$Method;Uri=('https://api.github.com'+$Path);Headers=$headers}
  if($null -ne $Body){$args.ContentType='application/json';$args.Body=($Body|ConvertTo-Json -Depth 10)}
  Invoke-RestMethod @args
}
$identity=Api GET '/user'
if($identity.login -ne $Owner){throw 'Authenticated account does not match requested owner.'}
$existing=$null
try{$existing=Api GET "/repos/$Owner/$Repo"}catch{if($_.Exception.Response.StatusCode.value__ -ne 404){throw}}
if($existing){if($existing.size -gt 0 -or $existing.private){throw 'Repository is populated or private; refusing to overwrite.'}}
else{$created=Api POST '/user/repos' @{name=$Repo;description='AntiGravity IDE auto continue for Windows: repeat prompts in the same agent conversation, with idle detection, usage tracking and daily budgets. Portable EXE + C# source.';private=$false;auto_init=$false;has_issues=$true;has_wiki=$false}}
$null=Api PUT "/repos/$Owner/$Repo/topics" @{names=@('antigravity','antigravity-ide','antigravity-auto-continue','auto-continue','ai-agents','agent-automation','windows','csharp','dotnet-framework','desktop-app','prompt-automation','portable')}
git init -b main | Out-Null
git config user.name $Owner
git config user.email "$Owner@users.noreply.github.com"
git add README.md TEST-REPORT.md SECURITY.md .gitignore .github docs src tests tools build.ps1 dist
git diff --cached --check
if($LASTEXITCODE -ne 0){throw 'Staged whitespace validation failed.'}
git commit -m 'Publish AntiGravity Repeater: Windows app, diagnostics and visual documentation' | Out-Null
if($LASTEXITCODE -ne 0){throw 'Commit failed.'}
git remote add origin "https://github.com/$Owner/$Repo.git"
$env:GIT_TERMINAL_PROMPT='0'
$env:GIT_CONFIG_COUNT='1'
$env:GIT_CONFIG_KEY_0='http.https://github.com/.extraheader'
$env:GIT_CONFIG_VALUE_0='AUTHORIZATION: basic '+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('x-access-token:'+$env:GH_TOKEN))
try{git push -u origin main; if($LASTEXITCODE -ne 0){throw 'Git push failed.'}}finally{Remove-Item Env:GIT_CONFIG_COUNT,Env:GIT_CONFIG_KEY_0,Env:GIT_CONFIG_VALUE_0 -ErrorAction SilentlyContinue}
$release=Api POST "/repos/$Owner/$Repo/releases" @{tag_name='v1.0.0';target_commitish='main';name='AntiGravity Repeater v1.0.0 — Windows portable';body="Portable Windows application with source and recorded test evidence.`n`nDownload the ZIP for the app, diagnostics and documentation, or download the standalone EXE. The EXE is unsigned. Offline regression: 85 checks passed. Recorded live test: five repeats and six verified replies. Live two-account switching remains unverified.`n`nSelect your actual work conversation before Start; test chats are separate histories.";draft=$false;prerelease=$false}
foreach($asset in @('AntiGravityRepeater-Windows.zip','dist/AntiGravityRepeater.exe','dist/RepeaterDiagnostics.exe','dist/SHA256SUMS.txt')){
 $name=[IO.Path]::GetFileName($asset)
 $uri=($release.upload_url -replace '\{.*$','')+'?name='+[Uri]::EscapeDataString($name)
 $null=Invoke-RestMethod -Method POST -Uri $uri -Headers $headers -ContentType 'application/octet-stream' -InFile $asset
}
$verified=Api GET "/repos/$Owner/$Repo"
$assets=Api GET "/repos/$Owner/$Repo/releases/$($release.id)/assets"
if($verified.private -or @($assets).Count -ne 4){throw 'Publication verification failed.'}
Write-Output ('Public repository: '+$verified.html_url)
Write-Output ('Release: '+$release.html_url)
Write-Output ('Verified assets: '+@($assets).Count)
