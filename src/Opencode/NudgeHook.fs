module VibeFs.Opencode.NudgeHook

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.PromptFragments
open VibeFs.Shell
open VibeFs.Shell.Dyn
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Opencode.NudgeEffect
open VibeFs.Opencode.NudgeEventCodec
open VibeFs.Opencode.MagicTodo

type NudgeHook(host: Host, ctx: obj, reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore, registry: ChildAgentRegistry) =
    let client () = Dyn.get ctx "client"
    let holder = StateHolder<NudgeShellState>(emptyState)

    member _.handleChatMessage(sessionID: SessionId, agent: string, parts: obj) : JS.Promise<unit> =
        holder.Mutate(fun state ->
            let text = getPartsText parts
            let sid = Id.sessionIdValue sessionID
            if isNudgePrompt text then state, ()
            else
                let agentOpt = if agent <> "" then Some agent else None
                resumeSession (rememberAgent state sid agentOpt) sid, ())
        resolvedUnitPromise ()

    member _.handleCommandExecuteBefore(input: obj) (_output: obj) : JS.Promise<unit> =
        let sessionIDStr = Dyn.str input "sessionID"
        holder.Mutate(fun (state: NudgeShellState) -> resumeSession state sessionIDStr, ())
        resolvedUnitPromise ()

    member _.handleToolExecuteAfter(input: obj) (output: obj) : JS.Promise<unit> =
        promise {
            if normalizeToolName host (Dyn.str input "tool") = "todowrite" then
                let out = Dyn.get output "output"
                if not (Dyn.isNullish out) && Dyn.typeIs out "string" then
                    let s = string out
                    if not (s.Contains meditatorNudge) then
                        setOutput output (s + "\n" + meditatorNudge)
        }

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        let claimed =
            holder.Mutate(fun state ->
                try
                    let event = Dyn.get input "event"
                    let eventType = Dyn.str event "type"
                    let rawProps = Dyn.get event "properties"
                    let props = if Dyn.isNullish rawProps then event else rawProps
                    let sessionIDStr = getSessionID eventType props
                    match Id.trySessionId sessionIDStr with
                    | None -> state, None
                    | Some sessionID ->
                        let nudgeEvent = decodeNudgeHostEvent eventType props
                        let nextState, wantsNudge = NudgeState.handleEvent state (Id.sessionIdValue sessionID) nudgeEvent
                        nextState, (if wantsNudge then Some sessionID else None)
                with _ ->
                    state, None)
        match claimed with
        | Some sessionID -> startNudgeFlow holder (client ()) reviewStore registry sessionID
        | None -> ()
        resolvedUnitPromise ()

let createNudgeHook (host: Host) (ctx: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (registry: ChildAgentRegistry) : NudgeHook =
    NudgeHook(host, ctx, reviewStore, registry)
