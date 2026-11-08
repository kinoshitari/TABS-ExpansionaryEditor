param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $root 'package\manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
$version=$manifest.version_number
if(!$SkipBuild){
    & dotnet build (Join-Path $root 'ExpansionaryEditor.csproj') -c Release --no-restore --nologo
    if($LASTEXITCODE -ne 0){throw '构建失败'}
}
$stage=Join-Path $root ('build\mod-'+$version)
$output=Join-Path $root ('dist\Local-ExpansionaryEditor-'+$version+'.zip')
if((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $output)){throw '当前版本打包目录已存在，未覆盖旧包。'}
New-Item -ItemType Directory -Path (Join-Path $stage 'BepInEx\plugins') -Force|Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $output) -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $root 'bin\Release\net472\ExpansionaryEditor.dll') -Destination (Join-Path $stage 'BepInEx\plugins\ExpansionaryEditor.dll')
foreach($name in @('manifest.json','README.md','CHANGELOG.md')){Copy-Item -LiteralPath (Join-Path $root ('package\'+$name)) -Destination $stage}
Copy-Item -LiteralPath (Join-Path $root '伤害类型.md') -Destination $stage

# A self-contained 256px icon; no external image, brand mark or network request.
Add-Type -AssemblyName System.Drawing
$image=[Drawing.Bitmap]::new(256,256)
$canvas=[Drawing.Graphics]::FromImage($image)
$canvas.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$canvas.Clear([Drawing.Color]::FromArgb(245,245,247))
$blue=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(0,108,246))
$white=[Drawing.SolidBrush]::new([Drawing.Color]::White)
$line=[Drawing.Pen]::new([Drawing.Color]::White,10)
$line.StartCap=[Drawing.Drawing2D.LineCap]::Round;$line.EndCap=[Drawing.Drawing2D.LineCap]::Round
try{
    $shape=[Drawing.Drawing2D.GraphicsPath]::new()
    $shape.AddArc(24,24,64,64,180,90);$shape.AddArc(168,24,64,64,270,90)
    $shape.AddArc(168,168,64,64,0,90);$shape.AddArc(24,168,64,64,90,90);$shape.CloseFigure()
    $canvas.FillPath($blue,$shape)
    foreach($y in @(84,128,172)){$canvas.DrawLine($line,70,$y,186,$y)}
    foreach($point in @(@(106,84),@(160,128),@(118,172))){$canvas.FillEllipse($white,($point[0]-13),($point[1]-13),26,26)}
    $image.Save((Join-Path $stage 'icon.png'),[Drawing.Imaging.ImageFormat]::Png)
    $shape.Dispose()
}finally{$line.Dispose();$blue.Dispose();$white.Dispose();$canvas.Dispose();$image.Dispose()}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$output,[IO.Compression.CompressionLevel]::Optimal,$false)
Write-Output $output
