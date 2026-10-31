# TABS 数值编辑器

TABS 游戏内兵种数值编辑器，当前版本 **1.4.0**。按 **F6** 打开或关闭。

## 安装与更新

从本仓库的 **Releases → v1.4.0** 下载 `Local-ExpansionaryEditor-1.4.0.zip`。

在 r2modman 选择 TABS 和目标配置，打开 **设置 → Import local mod（导入本地模组）**，选择 ZIP，无需解压。使用原配置更新，原数值与预设继续保留。

本包仅包含编辑器。对应的兵种模组须在同一配置安装并启用；仅 BepInExPack_TABS 为硬依赖。

## 功能

- 编辑原版、HiddenUnits、臭臭、Expansionary、圣诞节（Holiday 2.2.1）、动物王国（AnimalKingdom 1.0.4）。
- 属性、左右手武器、弹丸及可识别技能字段。
- 乘算、设值、增减的批量预览与应用。
- 全局、Mod、派系预设，修改摘要与历史恢复。
- 浅色极简界面、大字号、派系折叠、修改与草稿标识。

价格立即更新，其他属性需重新摆放兵种或重开当前关卡。详细操作见 [使用说明](使用说明.md)。

## 配置与预设

配置保存在当前 r2modman 配置的 `BepInEx/config/ExpansionaryEditor`：

- `数值设置.json`：当前覆盖规则，格式版本仍为 1。
- `平衡方案`：全局、Mod、派系预设。
- `编辑历史.json`：操作历史。

插件 GUID 保持 `local.tabs.expansionaryeditor`。更新包不含个人配置、预设、备份、游戏或第三方模组文件。

## 从源码构建

在 Windows 上使用 .NET SDK 和 .NET Framework 4.7.2 Targeting Pack。引用程序集来自本机合法安装的 TABS 及 r2modman 配置；本仓库不分发这些程序集。

在仓库目录打开 PowerShell，将两个路径替换为实际位置：

```powershell
$gameManaged = 'G:\SteamLibrary\steamapps\common\Totally Accurate Battle Simulator\TotallyAccurateBattleSimulator_Data\Managed'
$bepInExCore = 'D:\r2modman-data\TABS\profiles\Default\BepInEx\core'

dotnet restore .\ExpansionaryEditor.csproj
dotnet build .\ExpansionaryEditor.csproj -c Release --no-restore `
  "-p:GameDir=$gameManaged" "-p:CoreDir=$bepInExCore"

.\Build-Mod.ps1 -SkipBuild
```

输出位于 `dist/Local-ExpansionaryEditor-1.4.0.zip`。打包脚本不会覆盖同版本的已有输出；重打包前先移走 `dist` 中的同名 ZIP 与 `build/mod-1.4.0`。

也可退出游戏后手工安装构建的 DLL：

```powershell
.\Install.ps1 -ProfileRoot 'D:\r2modman-data\TABS\profiles\Default'
```

## 1.4.0 交付状态

源码已构建并生成模组包。按本次要求，未运行测试、实际 r2modman 导入、界面预览或游戏验证。
