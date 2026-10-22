namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Host.Ports

/// The live host config, shared between the adapter (which owns the host object)
/// and the product (which reads and writes settings through `IHostConfigPort`).
///
/// The host delivers its config object through the `config` hook, long after the
/// ports were built. The holder therefore keeps the current object mutable and
/// swaps it in place: the product keeps the same `IHostConfigPort` instance and
/// sees the live object, never a stale snapshot.
type LiveHostConfig(config: obj) =

    let mutable current = config

    let writeLeaf (node: obj) (leaf: string) (value: HostSettingValue) : unit =
        match value with
        | HostSettingValue.Bool b -> node?(leaf) <- box b
        | HostSettingValue.Text t -> node?(leaf) <- box t

    let ensureChild (node: obj) (head: string) : obj =
        if isNull node?(head) then
            node?(head) <- createObj []

        node?(head)

    let rec descendWrite (node: obj) (path: string list) (value: HostSettingValue) : unit =
        match path with
        | [] -> ()
        | [ leaf ] -> writeLeaf node leaf value
        | head :: rest -> descendWrite (ensureChild node head) rest value

    let readLeaf (node: obj) (leaf: string) : obj option =
        if isNull node || isNull node?(leaf) then
            None
        else
            Some node?(leaf)

    let tryUnboxBool (act: obj) (expected: bool) : bool =
        try
            unbox<bool> act = expected
        with _ ->
            false

    let tryUnboxString (act: obj) (expected: string) : bool =
        try
            unbox<string> act = expected
        with _ ->
            false

    let valueMatches (expected: HostSettingValue) (actual: obj option) : bool =
        match expected, actual with
        | HostSettingValue.Bool b, Some act -> tryUnboxBool act b
        | HostSettingValue.Text t, Some act -> tryUnboxString act t
        | _, None -> false

    let rec descendStep (node: obj) (path: string list) : obj option =
        match path with
        | [] -> None
        | [ leaf ] -> readLeaf node leaf
        | head :: rest -> descendRead node?(head) rest

    and descendRead (node: obj) (path: string list) : obj option =
        if isNull node then None else descendStep node path

    let toSettingValue (objVal: obj) : HostSettingValue =
        if emitJsExpr objVal "typeof $0 === 'boolean'" then
            HostSettingValue.Bool(unbox<bool> objVal)
        else
            HostSettingValue.Text(string objVal)

    let readFromObject (cfg: obj) (path: string list) : Result<HostSettingValue option, HostError> =
        descendRead cfg path |> Option.map toSettingValue |> Ok

    let toSettingGap (gap: RequiredSetting) : SettingGap =
        { Path = gap.Path
          Reason = "the host did not accept or preserve the configured setting" }

    let applyToObject (cfg: obj) (settings: RequiredSetting list) : Result<SettingGap option, HostError> =
        settings
        |> List.tryFind (fun setting ->
            descendWrite cfg setting.Path setting.Value
            not (valueMatches setting.Value (descendRead cfg setting.Path)))
        |> Option.map toSettingGap
        |> Ok

    /// Decides how a rejected config write becomes a port verdict: a host config
    /// can be frozen or otherwise non-writable, the write then throws instead of
    /// being ignored, and the round trip did not complete — so the port reports
    /// it and the product owns the verdict.
    let applyToLiveConfig (cfg: obj) (settings: RequiredSetting list) : Result<SettingGap option, HostError> =
        try
            applyToObject cfg settings
        with ex ->
            Error(HostError.Refused("the host config rejected the write: " + ex.Message))

    /// Adopt the config object the host just delivered. The port instance stays
    /// the same, so the product keeps reading the live config.
    member this.Accept(nextConfig: obj) : unit = current <- nextConfig

    interface IHostConfigPort with
        member this.Read(path: string list) : Result<HostSettingValue option, HostError> =
            if isNull current then
                Error(HostError.Refused "the host supplied no config object")
            else
                readFromObject current path

        member this.Apply(settings: RequiredSetting list) : Result<SettingGap option, HostError> =
            if isNull current then
                Error(HostError.Refused "the host supplied no config object")
            else
                applyToLiveConfig current settings
