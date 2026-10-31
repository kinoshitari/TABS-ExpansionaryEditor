param([string]$ProfileRoot='D:\r2modman-data\TABS\profiles\Default')
$ErrorActionPreference='Stop'
if(Get-Process -Name TotallyAccurateBattleSimulator -ErrorAction SilentlyContinue){throw '请先退出 TABS，再安装或更新补丁。'}
if(!(Test-Path -LiteralPath (Join-Path $ProfileRoot 'BepInEx\core\BepInEx.dll'))){throw '指定配置中没有 BepInEx，请选择可启动模组版 TABS 的配置。'}
$source=Join-Path $PSScriptRoot 'ExpansionaryEditor.dll'
if(!(Test-Path -LiteralPath $source)){$source=Join-Path $PSScriptRoot 'bin\Release\net472\ExpansionaryEditor.dll'}
if(!(Test-Path -LiteralPath $source)){throw '没有找到补丁 DLL，请先构建或使用发布包。'}
$destination=Join-Path $ProfileRoot 'BepInEx\plugins\Local-ExpansionaryEditor'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$target=Join-Path $destination 'ExpansionaryEditor.dll'
if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination ($target+'.bak')}
Copy-Item -LiteralPath $source -Destination $target -Force
Write-Host '已安装。用这个配置启动模组版游戏，按 F6 打开中文面板。'
