module Wanxiangshu.Runtime.OpencodeAgentConfigCodec

open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField

type PermissionOverrides = Map<string, string>
type ToolsOverrides = Map<string, bool>

type UserAgentScalars =
    { Prompt: string
      Mode: string
      Permission: PermissionOverrides option
      Tools: ToolsOverrides option
      Mcps: string array option }

let private emptyUserAgentScalars () : UserAgentScalars =
    { Prompt = ""
      Mode = ""
      Permission = None
      Tools = None
      Mcps = None }

let private mcpsFromField (userAgent: obj) : string array option =
    let v = Dyn.get userAgent "mcps"

    if Dyn.isNullish v then
        None
    elif Dyn.isArray v then
        Some((v :?> obj array) |> Array.map string)
    else
        Some [| string v |]

let private permissionMapFromObj (v: obj) : PermissionOverrides option =
    if Dyn.isNullish v then
        None
    else
        Dyn.keys v
        |> Array.fold
            (fun acc key ->
                let value = string (Dyn.get v key)
                Map.add key value acc)
            Map.empty
        |> Some

let toolsMapFromObj (v: obj) : ToolsOverrides option =
    if Dyn.isNullish v then
        None
    else
        Dyn.keys v
        |> Array.fold
            (fun acc key ->
                let raw = Dyn.get v key
                let enabled = if Dyn.isNullish raw then false else Dyn.truthy raw
                Map.add key enabled acc)
            Map.empty
        |> Some

let permissionMapToObj (m: PermissionOverrides) : obj =
    m |> Map.toList |> List.map (fun (k, v) -> k, box v) |> createObj

let toolsMapToObj (m: ToolsOverrides) : obj =
    m |> Map.toList |> List.map (fun (k, v) -> k, box v) |> createObj

let private mergePermissionMaps
    (defaults: PermissionOverrides)
    (user: PermissionOverrides option)
    : PermissionOverrides =
    match user with
    | None -> defaults
    | Some u -> Map.fold (fun acc k v -> Map.add k v acc) defaults u

let private mergeToolsMaps (defaults: ToolsOverrides) (user: ToolsOverrides option) : ToolsOverrides =
    match user with
    | None -> defaults
    | Some u -> Map.fold (fun acc k v -> Map.add k v acc) defaults u

let mergePermissionOverrides (defaults: PermissionOverrides) (user: PermissionOverrides option) : PermissionOverrides =
    mergePermissionMaps defaults user

let mergeToolsOverrides (defaults: ToolsOverrides) (user: ToolsOverrides option) : ToolsOverrides =
    mergeToolsMaps defaults user

let decodeUserAgentScalars (userAgent: obj) : UserAgentScalars =
    if Dyn.isNullish userAgent then
        emptyUserAgentScalars ()
    else
        let prompt = strField userAgent "prompt" |> Option.defaultValue ""
        let mode = strField userAgent "mode" |> Option.defaultValue ""

        { Prompt = prompt
          Mode = mode
          Permission = permissionMapFromObj (Dyn.get userAgent "permission")
          Tools = toolsMapFromObj (Dyn.get userAgent "tools")
          Mcps = mcpsFromField userAgent }

let encodeAgentScalarsRecord (scalars: UserAgentScalars) : obj =
    let pairs =
        [ "prompt", box scalars.Prompt; "mode", box scalars.Mode ]
        @ (match scalars.Permission with
           | None -> []
           | Some m -> [ "permission", permissionMapToObj m ])
        @ (match scalars.Tools with
           | None -> []
           | Some m -> [ "tools", toolsMapToObj m ])
        @ (match scalars.Mcps with
           | None -> []
           | Some arr -> [ "mcps", box arr ])

    createObj pairs
