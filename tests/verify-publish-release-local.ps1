$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
Add-Type -ReferencedAssemblies System.Net.Http -TypeDefinition @'
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
public class PublishLocalHandler : HttpMessageHandler {
 public string Body="[]"; public int Status=200; public int Requests;
 public long SentLength; public byte[] SentBytes;
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel) {
  Requests++;
  if(request.Content!=null){SentLength=request.Content.Headers.ContentLength??-1;SentBytes=request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();}
  var response=new HttpResponseMessage((HttpStatusCode)Status);
  response.Content=new StringContent(Body,Encoding.UTF8,"application/json");
  return Task.FromResult(response);
 }
}
'@
$taskTokens=$null;$taskParseErrors=$null
$taskSource=Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts/publish-release.ps1'
$taskAst=[Management.Automation.Language.Parser]::ParseFile($taskSource,[ref]$taskTokens,[ref]$taskParseErrors)
if($taskParseErrors.Count){throw 'Publisher syntax errors'}
$taskFunctions=$taskAst.FindAll({param($a) $a -is [Management.Automation.Language.FunctionDefinitionAst]},$false)
foreach($taskFunction in $taskFunctions){Invoke-Expression $taskFunction.Extent.Text}
$taskHandler=New-Object PublishLocalHandler
$taskClient=New-Object Net.Http.HttpClient($taskHandler)
$taskChecks=0
function Assert-Test([bool]$condition,[string]$name){if(!$condition){throw $name};$script:taskChecks++}
try{
 $taskHandler.Body='[{"name":"first","size":1},{"name":"second","size":2}]'
 $taskAssets=@(Invoke-GitHubRequest 'GET' ([uri]'https://api.github.com/repos/hyronguyen/hyrolauncher/releases/1/assets'))
 Assert-Test ($taskAssets.Count -eq 2 -and $taskAssets[0].name -eq 'first' -and $taskAssets[1].name -eq 'second') 'JSON array enumeration'
 $taskHandler.Body='[]'
 $taskAssets=@(Invoke-GitHubRequest 'GET' ([uri]'https://api.github.com/repos/hyronguyen/hyrolauncher/releases/1/assets'))
 Assert-Test ($taskAssets.Count -eq 0) 'Empty array'
 $taskHandler.Body='[{"name":"first","size":1}]'
 $taskAssets=@(Invoke-GitHubRequest 'GET' ([uri]'https://api.github.com/repos/hyronguyen/hyrolauncher/releases/1/assets'))
 Assert-Test ($taskAssets.Count -eq 1 -and $taskAssets.name -eq 'first') 'Single array'
 $taskHandler.Status=404
 Assert-Test ($null -eq (Invoke-GitHubRequest 'GET' ([uri]'https://api.github.com/repos/hyronguyen/hyrolauncher/releases/tags/v1.0.0') -AllowNotFound)) 'Normal 404'
 $taskHandler.Status=503;$taskHandler.Body='sensitive server response must not be emitted'
 $taskMessage='';try{Invoke-GitHubRequest 'GET' ([uri]'https://api.github.com/repos/hyronguyen/hyrolauncher/releases/1')}catch{$taskMessage=$_.Exception.Message}
 Assert-Test ($taskMessage -match 'HTTP 503' -and $taskMessage -notmatch 'sensitive server') 'Redacted API errors'
 $taskRequestsBefore=$taskHandler.Requests
 $taskRejected=$false;try{Invoke-GitHubRequest 'GET' ([uri]'https://invalid.example/repos/hyronguyen/hyrolauncher/releases/1')}catch{$taskRejected=$true}
 Assert-Test ($taskRejected -and $taskHandler.Requests -eq $taskRequestsBefore) 'Foreign host blocked'
 $taskRejected=$false;try{Assert-GitHubEndpoint ([uri]'https://uploads.github.com/repos/other/repository/releases/1/assets') -Upload}catch{$taskRejected=$true}
 Assert-Test $taskRejected 'Foreign upload repo blocked'
 $taskHandler.Status=201;$taskHandler.Body='{"name":"fixture.zip","size":8192,"state":"uploaded"}'
 $taskUpload=New-Object IO.MemoryStream(,(New-Object byte[] 8192))
 $taskUploaded=Invoke-GitHubRequest 'POST' ([uri]'https://uploads.github.com/repos/hyronguyen/hyrolauncher/releases/1/assets?name=fixture.zip') -Stream $taskUpload -ContentType 'application/zip'
 Assert-Test ($taskHandler.SentLength -eq 8192 -and $taskHandler.SentBytes.Length -eq 8192 -and $taskUploaded.name -eq 'fixture.zip') 'StreamContent upload'
 $taskLocal=[pscustomobject]@{Name='fixture.zip';Length=8192;Sha256=('a'*64)}
 $taskRemote=[pscustomobject]@{name='fixture.zip';size=8192;state='uploaded';digest=('sha256:'+('a'*64))}
 Assert-RemoteAsset $taskRemote $taskLocal;Assert-Test $true 'Matching digest'
 $taskRemote.digest=$null;Assert-RemoteAsset $taskRemote $taskLocal;Assert-Test $true 'Missing digest accepts size'
 $taskRemote.size=999;$taskRejected=$false;try{Assert-RemoteAsset $taskRemote $taskLocal}catch{$taskRejected=$true};Assert-Test $taskRejected 'Changed size rejects without deletion'
 $taskRemote.size=8192;$taskRemote.digest='sha256:'+('b'*64);$taskRejected=$false;try{Assert-RemoteAsset $taskRemote $taskLocal}catch{$taskRejected=$true};Assert-Test $taskRejected 'Changed digest rejects without deletion'
 $taskPlain=New-Object IO.MemoryStream(,[Text.Encoding]::UTF8.GetBytes('hello'))
 Assert-Test ((Get-StreamSha256 $taskPlain) -eq '2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824' -and $taskPlain.Position -eq 0) 'Stream SHA256'
 $taskPlain.Dispose()
 [ordered]@{Passed=$true;Checks=$taskChecks;NetworkUsed=$false} | ConvertTo-Json -Compress
}finally{$taskClient.Dispose()}
