# TABS 数值编辑器

TABS 游戏内兵种数值编辑器，当前版本 **1.10.0**。按 **F6** 打开或关闭。

同款双持武器可将当前一侧修改同步到另一只手；左右手新增“特殊效果”，分别开关并调整武器已有的寒冷、燃烧／持续伤害等词条。

## 安装与更新

从 [v1.10.0 Release](https://github.com/kinoshitari/TABS-ExpansionaryEditor/releases/tag/v1.10.0) 下载 `Local-ExpansionaryEditor-1.10.0.zip`。

在 r2modman 选择 TABS 和目标配置，打开 **设置 → Import local mod（导入本地模组）**，选择 ZIP，无需解压。使用原配置更新，原数值与预设继续保留。

本包仅包含编辑器。管理器前置与纯原版用法见下方；原有数值、预设和菜单排序不随包分发。

## 功能

- 编辑原版、HiddenUnits、臭臭、Expansionary、圣诞节（Holiday 2.2.1）、动物王国（AnimalKingdom 1.0.4）、中国神话（Chinese_myth 0.2.1）。
- 属性、独立承伤页、左右手武器、弹丸及可识别技能字段。
- 同款双持在右手点“同步到左手”，或在左手点“同步到右手”；包含整只手的草稿、特殊效果和还原，仍需保存。
- 左右手“特殊效果”支持寒冷、燃烧／持续伤害、流血、雷电、生化、酸蚀、感染、石化等实际附加词条；开关不关闭普通命中，未识别数值的状态只提供开关。
- 隐藏、臭臭、圣诞节及动物王国兵种补充中文名称；衍生兵种显示主兵种，支持按主兵种搜索。
- 承伤页集中四类原生伤害、燃烧／持续伤害、爆炸、冰冻积累、模组实际扣血来源、部位系数及护甲与冲击。
- 来源项下的“注释”默认收起，展开查看模组、机制和已确认的预制体关联兵种；备注以“（…）”显示，“调用入口”列出实际接入的组件与方法。
- 数值框 `<< / < / > / >>` 分级增减：单箭头 1%，双箭头 10%，整数至少变化 1。
- 乘算、设值、增减的批量预览与应用。
- 打开“更多派系”后拖动派系内兵种排序，显示插入线并支持边缘滚动；顶部派系标签沿用游戏原生拖拽。拖后就地更新现有按钮，保留当前页和选中兵种。
- 全局、Mod、派系预设，修改摘要与历史恢复。
- 中国神话独立分组，支持大圣护甲强度、金箍棒伸缩冷却及旋转／推进速度。
- 设置中即时切换浅色极简与经典深色界面，记住风格；两种风格均保留大字号、派系折叠和修改标识。

价格立即更新，其他属性需重新摆放兵种或重开当前关卡。详细操作见 [使用说明](使用说明.md)。

## 前置与原版模式

管理器包声明以下前置：

- `BepInEx-BepInExPack_TABS-5.4.16`
- `Grad-HiddenUnits-2.0.9`
- `Chou_Chou_Team-ChouChou_s_Unit-0.5.0`
- `Geezt-Expansionary-1.2.9`
- `Grad-Holiday-2.2.1`
- `Grad-AnimalKingdom-1.0.4`
- `Chou_Chou_Team-Chinese_myth-0.2.1`

运行时，扩展来源及其核心均使用 BepInEx **软依赖**。只对实际启用的来源等待加载；不安装或停用这些来源，编辑器仍可加载原版目录。项目不引用第三方 Mod DLL。

只编辑原版时，保留 BepInEx 与编辑器即可。若不希望管理器按前置列表安装扩展，可将 ZIP 内的 `BepInEx/plugins/ExpansionaryEditor.dll` 放入当前配置的 `BepInEx/plugins/Local-ExpansionaryEditor`，或使用下方手工安装脚本。不要另留一份同 GUID 的编辑器。

声明的管理器前置会参与安装依赖解析；插件能否独立加载由软依赖决定。见 [Thunderstore 包格式](https://wiki.thunderstore.io/mods/creating-a-package) 与 [BepInEx 依赖说明](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/2_plugin_start.html#specifying-dependencies-on-other-plugins)。

伤害机制和覆盖范围见 [伤害类型](伤害类型.md)。

## 配置与预设

配置保存在当前 r2modman 配置的 `BepInEx/config/ExpansionaryEditor`：

- `数值设置.json`：当前覆盖规则，格式版本仍为 1。
- `平衡方案`：全局、Mod、派系预设。
- `编辑历史.json`：操作历史。
- `菜单排序.json`：独立保存派系内兵种的显示顺序，不参与数值预设。

界面风格另存于 BepInEx 的插件 cfg；“设置 → 浅色 / 经典”只改变界面，不清除草稿、数值或预设。

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

输出位于 `dist/Local-ExpansionaryEditor-1.10.0.zip`。打包脚本不会覆盖同版本的已有输出；重打包前先移走 `dist` 中的同名 ZIP 与 `build/mod-1.10.0`。

也可退出游戏后手工安装构建的 DLL。已有 r2modman 登记时，脚本同步本编辑器的版本与前置清单；不修改其他模组的启停状态：

```powershell
.\Install.ps1 -ProfileRoot 'D:\r2modman-data\TABS\profiles\Default'
```

## 1.10.0 交付状态

源码已构建并生成模组包。按本次要求，未运行测试、实际 r2modman 导入、界面预览或游戏验证。
