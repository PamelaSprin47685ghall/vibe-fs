module Wanxiangshu.Runtime.MessageTransform.CapsStage

open Fable.Core
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Stack
open Wanxiangshu.Runtime.RuntimeScope

/// Forget one-shot caps injection state after compaction (or equivalent context reset).
/// Next transform rebuilds the prefix from the current disk caps files.
let invalidateCapsAfterCompaction (scope: RuntimeScope) (sessionID: string) : unit =
    if sessionID = "" then
        ()
    else
        clearCapsSlot scope sessionID
        let prefix = sessionID + "\u0000"
        scope.ClearCapsFilesForSession prefix
        scope.ClearCapsInflightForSession prefix

let private resolveCapsSessionID (sessionID: string) (plan: MessageTransformPlan) : string =
    if sessionID <> "" then
        sessionID
    else
        plan.Cleaned
        |> List.tryFind (fun message -> message.source = Native && message.info.role = User)
        |> Option.map (fun message -> "anonymous-" + message.info.id)
        |> Option.defaultValue "anonymous-empty"

let private storeCapsPrefix
    (scope: RuntimeScope)
    (capsSessionID: string)
    (state: TransformState)
    (existingSlot: CapsSlot option)
    (prefix: obj array)
    : unit =
    let capsSlot: CapsSlot =
        match existingSlot with
        | Some slot ->
            { slot with
                Segment = Some prefix
                ScopeId = capsSessionID }
        | None ->
            { Segment = Some prefix
              ScopeId = capsSessionID
              CapsRevision = state.CapsRevision
              PolicyVersion = state.PolicyVersion }

    set scope capsSessionID { state with Caps = Some capsSlot }

let private buildAndCacheCaps
    (scope: RuntimeScope)
    (capsSessionID: string)
    (state: TransformState)
    (existingSlot: CapsSlot option)
    (encoded: obj array)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        let! capsFiles = loadCaps ()
        let result = buildCaps encoded capsFiles None
        let prefixLen = result.Length - encoded.Length

        if prefixLen > 0 then
            storeCapsPrefix scope capsSessionID state existingSlot result.[.. prefixLen - 1]

        return result
    }

let prependCapsWithState
    (scope: RuntimeScope)
    (sessionID: string)
    (plan: MessageTransformPlan)
    (encoded: obj array)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        let capsSessionID = resolveCapsSessionID sessionID plan
        let state = get scope capsSessionID

        match state.Caps with
        | Some capsSlot ->
            match capsSlot.Segment with
            | Some prefix -> return Array.append prefix encoded
            | None -> return! buildAndCacheCaps scope capsSessionID state (Some capsSlot) encoded loadCaps buildCaps
        | None -> return! buildAndCacheCaps scope capsSessionID state None encoded loadCaps buildCaps
    }
