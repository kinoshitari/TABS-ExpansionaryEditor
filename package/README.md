# 数值编辑器

游戏内按 **F6**。

## 导入 r2modman

1. 选择 TABS 和目标配置。
2. 打开“设置”，选择“导入本地模组 / Import local mod”。
3. 选择 **Local-ExpansionaryEditor-1.4.0.zip**，不要解压或改名。
4. 启动模组版游戏。

若之前使用手工安装版，请使用原来的配置。包名仍为 Local-ExpansionaryEditor，插件 GUID 和数值目录不变。

## 六个来源

原版、HiddenUnits、臭臭、Expansionary、Holiday、AnimalKingdom。

圣诞节对应 **Holiday 2.2.1**，动物王国对应 **AnimalKingdom 1.0.4**。这些来源模组是可选项，需要在同一配置中安装并启用；本包只包含编辑器，不捆绑第三方兵种模组。

## 编辑

- 属性、左右手、可识别技能。
- 批量乘算、设值、增减与预览。
- 全局、Mod、派系预设及历史恢复。
- 支持日文输入法的组合输入与全角数字。

价格立即更新，其他数值需重新摆放兵种或重开关卡。

## 数据

原有配置仍位于当前配置的 `BepInEx/config/ExpansionaryEditor`。主数值格式保持版本 1，已有全局与局部预设继续读取。本包不含数值、预设或界面配置，不会用一份空配置覆盖现有内容。

## 更新边界

1.4.0 按作者要求只构建和打包，没有进行测试、实际导入、界面预览或游戏验证。

包装依据：[Thunderstore 包格式](https://wiki.thunderstore.io/mods/creating-a-package)、[BepInEx 文件布局](https://wiki.thunderstore.io/mods/packaging-your-mods)。

