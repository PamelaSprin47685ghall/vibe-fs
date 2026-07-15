module Wanxiangshu.Kernel.EventLog.SubsessionProjection

/// Independent projection for subagent state.
///
/// Owner: Subagent / Subsession subsystem
/// Input events: subagent_spawned, subagent_continued
/// Query: ChildId → Agent, Title, ContinuedPrompts
///
/// Phase 6: Split from SessionState.

open Wanxiangshu.Kernel.EventLog.Types

type SubagentState =
    { ChildId: string
      Agent: string
      Title: string
      ContinuedPrompts: string list }

let private subagentFolder (current: Map<string, SubagentState>) (e: WanEvent) : Map<string, SubagentState> =
    match e.Kind with
    | k when k = eventKindSubagentSpawned ->
        let childId = defaultArg (e.Payload |> Map.tryFind "childId") ""

        if childId = "" then
            current
        else
            let agent = defaultArg (e.Payload |> Map.tryFind "agent") ""
            let title = defaultArg (e.Payload |> Map.tryFind "title") ""

            Map.add
                childId
                { ChildId = childId
                  Agent = agent
                  Title = title
                  ContinuedPrompts = [] }
                current
    | k when k = eventKindSubagentContinued ->
        let childId = defaultArg (e.Payload |> Map.tryFind "childId") ""

        if childId = "" then
            current
        else
            match Map.tryFind childId current with
            | Some state ->
                let prompt = defaultArg (e.Payload |> Map.tryFind "prompt") ""
                let nextList = prompt :: state.ContinuedPrompts

                Map.add
                    childId
                    { state with
                        ContinuedPrompts =
                            if nextList.Length > 5 then
                                List.truncate 5 nextList
                            else
                                nextList }
                    current
            | None ->
                let prompt = defaultArg (e.Payload |> Map.tryFind "prompt") ""

                Map.add
                    childId
                    { ChildId = childId
                      Agent = ""
                      Title = ""
                      ContinuedPrompts = [ prompt ] }
                    current
    | _ -> current

/// Fold a single subagent event (public for composite projection).
let foldSingleSubagentEvent (current: Map<string, SubagentState>) (e: WanEvent) : Map<string, SubagentState> =
    subagentFolder current e

let foldSubagents (sessionId: string) (events: WanEvent list) : Map<string, SubagentState> =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold subagentFolder Map.empty
