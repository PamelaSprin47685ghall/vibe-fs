module Wanxiangshu.Runtime.Fallback.HostEventInspection

open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Subsession.Types

/// Search a prioritized list of objects for the first non-empty string value
/// matching any of the provided key names.
let findFirstStringValue (targets: obj array) (keys: string array) : string =
    targets
    |> Array.tryPick (fun target ->
        keys
        |> Array.tryPick (fun key ->
            let s = Dyn.str target key
            if s <> "" then Some s else None))
    |> Option.defaultValue ""

/// Search a prioritized list of objects for the first non-empty string value
/// matching any of the provided key names, returned as an option.
let tryFindFirstStringValue (targets: obj array) (keys: string array) : string option =
    match findFirstStringValue targets keys with
    | "" -> None
    | s -> Some s

/// Look for a turn/run identifier in a prioritized list of objects.
let tryFindTurnId (targets: obj array) : TurnId option =
    tryFindFirstStringValue targets [| "turnId"; "turnID"; "runId"; "runID" |]
    |> Option.map TurnId.create

/// Extract a host model string from an OMP/OpenCode-style `info.model` field.
/// Accepts either a plain model string or an object with providerID/modelID/variant.
let tryGetModelStringFromInfo (info: obj) : string option =
    if Dyn.isNullish info then
        None
    else
        let mv = Dyn.get info "model"

        if Dyn.isNullish mv then
            None
        elif Dyn.typeIs mv "string" then
            let s = string mv in if s = "" then None else Some s
        else
            let pID, mID, variant =
                Dyn.str mv "providerID", Dyn.str mv "modelID", Dyn.str mv "variant"

            let suffix = if variant <> "" then ":" + variant else ""

            if pID = "" || mID = "" then
                let idVal = Dyn.str mv "id" in if idVal <> "" then Some(idVal + suffix) else None
            else
                Some(sprintf "%s/%s%s" pID mID suffix)
