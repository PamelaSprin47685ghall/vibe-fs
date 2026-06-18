module VibeFs.Index

open Fable.Core
open VibeFs.Kernel.ToolPolicy

let createRegistration (deps: obj) : obj =
    VibeFs.MuxPlugin.Registration.createRegistration deps

/// Plugin tool names — kept in sync with MuxTools.createToolCatalog.
let private pluginToolNames =
    [| "coder"; "reader"; "meditator"; "browser"; "executor"
       "submit_review"; "websearch"; "webfetch"
       "fuzzy_find"; "fuzzy_grep"; "write"; "read" |]

/// Export canUse so the host can filter any tool name directly.
let canUseTool (agent: string) (tool: string) : bool =
    canUse agent tool

/// Return {add, remove} for the mux host's regex-filter pipeline.
/// Denied plugin tools are listed in `remove` so they never appear available.
let getPluginToolPolicy (_agentId: string) (role: string) : obj =
    let agent = if System.String.IsNullOrEmpty role then "manager" else role
    let remove = pluginToolNames |> Array.filter (fun t -> not (canUse agent t))
    box {| add = [||]; remove = remove |}

let buildCapsFileReadData (projectRoot: string) : JS.Promise<VibeFs.Mux.CapsFileRead.CapsFileReadEntry[]> =
    VibeFs.Mux.CapsFileRead.buildCapsFileReadData projectRoot

let deduplicateReadOutputs (messages: obj array) : obj array =
    VibeFs.Mux.Dedup.deduplicateReadOutputs messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj array =
    VibeFs.Mux.Dedup.deduplicateReadOutputsWithSeen (List.ofArray seenOutputs) messages |> snd

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj array =
    let seen, result = VibeFs.Mux.Dedup.deduplicateModelReadOutputsWithSeen (List.ofArray seenOutputs) messages
    Array.ofList seen, result

let deduplicateReadOutputsAgainstHistory (history: obj array) (messages: obj array) : obj array =
    let seenByPath = VibeFs.Mux.Dedup.collectReadOutputsByPath history
    VibeFs.Mux.Dedup.deduplicateReadOutputsWithSeenByPath seenByPath messages |> snd

let collectReadOutputs (messages: obj array) : string[] =
    VibeFs.Mux.Dedup.collectReadOutputs messages |> Array.ofList
