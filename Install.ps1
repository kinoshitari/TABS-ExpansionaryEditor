param([string]$ProfileRoot='D:\r2modman-data\TABS\profiles\Default')
$ErrorActionPreference='Stop'
if(Get-Process -Name TotallyAccurateBattleSimulator -ErrorAction SilentlyContinue){throw '请先退出 TABS，再安装或更新补丁。'}
if(!(Test-Path -LiteralPath (Join-Path $ProfileRoot 'BepInEx\core\BepInEx.dll'))){throw '指定配置中没有 BepInEx，请选择可启动模组版 TABS 的配置。'}
$source=Join-Path $PSScriptRoot 'ExpansionaryEditor.dll'
if(!(Test-Path -LiteralPath $source)){$source=Join-Path $PSScriptRoot 'bin\Release\net472\ExpansionaryEditor.dll'}
if(!(Test-Path -LiteralPath $source)){throw '没有找到补丁 DLL，请先构建或使用发布包。'}

# Preserve hand-installed usage. Refresh the manager record only when it already exists.
$manifestPath=Join-Path $PSScriptRoot 'manifest.json'
if(!(Test-Path -LiteralPath $manifestPath)){$manifestPath=Join-Path $PSScriptRoot 'package\manifest.json'}
$metadataRoot=$null;$newRegistry=$null
$registryPath=Join-Path $ProfileRoot 'mods.yml'
if(Test-Path -LiteralPath $manifestPath){
    $metadataRoot=Split-Path -Parent $manifestPath
    $manifest=Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8|ConvertFrom-Json
    if(Test-Path -LiteralPath $registryPath){
        $registryText=[IO.File]::ReadAllText($registryPath)
        $pattern='(?ms)^- manifestVersion: 1\r?\n  name: Local-ExpansionaryEditor\r?\n.*?(?=^- manifestVersion:|\z)'
        $entry=[regex]::Match($registryText,$pattern)
        if($entry.Success){
            $nl=if($entry.Value.Contains("`r`n")){"`r`n"}else{"`n"}
            function Set-ManifestField([string]$block,[string]$field,[string]$value){
                $fieldPattern='(?m)^  '+[regex]::Escape($field)+':[^\r\n]*(?:\r?\n    [^\r\n]*)*'
                if(![regex]::IsMatch($block,$fieldPattern)){throw "模组记录缺少字段：$field"}
                return [regex]::Replace($block,$fieldPattern,[Text.RegularExpressions.MatchEvaluator]{param($m) "  ${field}:$value"})
            }
            $block=$entry.Value
            $block=Set-ManifestField $block 'websiteUrl' (" '"+([string]$manifest.website_url).Replace("'","''")+"'")
            $block=Set-ManifestField $block 'description' (" '"+([string]$manifest.description).Replace("'","''")+"'")
            $dependencies=@($manifest.dependencies|ForEach-Object {'    - '+$_})-join $nl
            $block=Set-ManifestField $block 'dependencies' ($nl+$dependencies)
            $version=[version]$manifest.version_number
            $block=Set-ManifestField $block 'versionNumber' ($nl+'    major: '+$version.Major+$nl+'    minor: '+$version.Minor+$nl+'    patch: '+$version.Build)
            $newRegistry=$registryText.Substring(0,$entry.Index)+$block+$registryText.Substring($entry.Index+$entry.Length)
        }
    }
}
$destination=Join-Path $ProfileRoot 'BepInEx\plugins\Local-ExpansionaryEditor'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$target=Join-Path $destination 'ExpansionaryEditor.dll'
if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination ($target+'.bak')}
Copy-Item -LiteralPath $source -Destination $target -Force
if($metadataRoot){
    foreach($name in @('manifest.json','README.md','CHANGELOG.md','icon.png','伤害类型.md')){
        $file=Join-Path $metadataRoot $name
        if($name -eq '伤害类型.md' -and !(Test-Path -LiteralPath $file)){$file=Join-Path $PSScriptRoot $name}
        if(!(Test-Path -LiteralPath $file)){continue}
        $installed=Join-Path $destination $name
        if(Test-Path -LiteralPath $installed){Copy-Item -LiteralPath $installed -Destination ($installed+'.bak') -Force}
        Copy-Item -LiteralPath $file -Destination $installed -Force
    }
}
if($null -ne $newRegistry){
    $temp=$registryPath+'.ExpansionaryEditor.tmp'
    [IO.File]::WriteAllText($temp,$newRegistry,[Text.UTF8Encoding]::new($false))
    [IO.File]::Replace($temp,$registryPath,$registryPath+'.ExpansionaryEditor.bak')
}
Write-Host '已安装。用这个配置启动模组版游戏，按 F6 打开中文面板。'
