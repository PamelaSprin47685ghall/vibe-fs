namespace Wanxiangshu.Composition.Plugin

open Wanxiangshu.Host
open Wanxiangshu.Host.Ports

module ConfigPolicy =
    val establish: config: IHostConfigPort -> SettingGap option
    val settingOfGap: gap: SettingGap -> CompactionSetting option
