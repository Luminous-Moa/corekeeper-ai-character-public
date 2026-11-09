param(
    [Parameter(Mandatory=$true)][string]$Id,
    [Parameter(Mandatory=$true)][string]$Url,
    [ValidateSet('GET','POST')][string]$Method='GET',
    [string]$BodyFile=''
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$out=Join-Path $root ('raw/'+$Id)
$arguments=@('--silent','--show-error','--max-time','10','--request',$Method,'--dump-header',('"'+$out+'.headers.txt"'),'--output',('"'+$out+'.body.txt"'),'--write-out','%{http_code}')
if($BodyFile){$arguments+=@('--header','"Content-Type: application/json"','--data-binary',('"@'+$BodyFile+'"'))}
$arguments+=('"'+$Url+'"')
$started=(Get-Date).ToUniversalTime().ToString('o')
$p=Start-Process -FilePath 'curl.exe' -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput ($out+'.status.txt') -RedirectStandardError ($out+'.stderr.txt')
$status=(Get-Content -LiteralPath ($out+'.status.txt') -Raw).Trim()
@{startedUtc=$started;endedUtc=(Get-Date).ToUniversalTime().ToString('o');command='curl.exe';arguments=$arguments;method=$Method;url=$Url;requestBodyFile=$BodyFile;exitCode=$p.ExitCode;httpStatus=if($status -eq '000'){$null}else{[int]$status};clientStatus=$status;freshExecution=$true;replay=$false;mock=$false} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 ($out+'.meta.json')
Get-Content -LiteralPath ($out+'.meta.json') -Encoding UTF8
if(Test-Path -LiteralPath ($out+'.body.txt')){Get-Content -LiteralPath ($out+'.body.txt') -Encoding UTF8}
Get-Content -LiteralPath ($out+'.stderr.txt') -Encoding UTF8
