namespace Wanxiangshu.Composition.Plugin

open Wanxiangshu.Host
open Wanxiangshu.Host.Ports

module ConfigPolicy =

    let establish (config: IHostConfigPort) : SettingGap option =
        let required =
            HostCompactionPolicy.requiredSettings
            |> List.map (fun setting ->
                { Path = setting.Path
                  Value = HostSettingValue.Bool setting.Required })

        match config.Apply required with
        | Ok maybeGap -> maybeGap
        | Error _ ->
            // Host returned an error: we assume the first required compaction setting
            // could not be verified/applied.
            HostCompactionPolicy.requiredSettings
            |> List.tryHead
            |> Option.map (fun setting ->
                { Path = setting.Path
                  Reason = "the host refused or failed to apply required config settings" })

    let settingOfGap (gap: SettingGap) : CompactionSetting option =
        HostCompactionPolicy.requiredSettings
        |> List.tryFind (fun setting -> setting.Path = gap.Path)
        |> Option.orElse (HostCompactionPolicy.requiredSettings |> List.tryHead)
