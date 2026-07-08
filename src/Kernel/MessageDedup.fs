module Wanxiangshu.Kernel.MessageDedup

open Wanxiangshu.Kernel.Dedup

type ReadPayload = { path: string; content: string }

type DedupVerdict =
    | AlreadySeen
    | NewContent of ReadPayload

let createDedupState () : DedupState = emptyState

let processDedup (state: DedupState) (payload: ReadPayload) : DedupVerdict * DedupState =
    let result = deduplicate state payload.content

    if isNoChangeOutput result.output then
        AlreadySeen, state
    else
        NewContent payload, result.state

/// Tool names that represent a file-read operation across hosts.
let readToolNames = Set.ofList [ "read"; "file_read" ]

let dedupForPath
    (seenByPath: Map<string, DedupState>)
    (payload: ReadPayload)
    : Map<string, DedupState> * DedupVerdict =
    let pathState = Map.tryFind payload.path seenByPath |> Option.defaultValue emptyState
    let verdict, nextState = processDedup pathState payload
    (Map.add payload.path nextState seenByPath), verdict

let foldDedup
    (seenByPath: Map<string, DedupState>)
    (payloads: ReadPayload list)
    : Map<string, DedupState> * (string list * bool list) =
    let (nextSeen, (outputsRev, replacedRev)) =
        payloads
        |> List.fold
            (fun (state, (outs, reps)) payload ->
                let seen', verdict = dedupForPath state payload

                match verdict with
                | AlreadySeen -> seen', (outs, true :: reps)
                | NewContent _ -> seen', (payload.content :: outs, false :: reps))
            (seenByPath, ([], []))

    nextSeen, (List.rev outputsRev, List.rev replacedRev)

let collectReadOutputsByPath (payloads: ReadPayload list) : Map<string, string list> =
    payloads
    |> List.fold
        (fun map payload ->
            let next = Map.tryFind payload.path map |> Option.defaultValue []
            Map.add payload.path (payload.content :: next) map)
        Map.empty
    |> Map.map (fun _ v -> List.rev v)

let collectReadOutputs (payloads: ReadPayload list) : string list =
    payloads |> List.map (fun payload -> payload.content)
