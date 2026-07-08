module Wanxiangshu.Kernel.MessageDedup

open Wanxiangshu.Kernel.Dedup

type ReadPayload = { path: string; content: string }

type DedupVerdict =
    | AlreadySeen
    | NewContent of ReadPayload

type DedupState = { seenContents: string list }

let createDedupState () : DedupState = { seenContents = [] }

let processDedup (state: DedupState) (payload: ReadPayload) : DedupVerdict * DedupState =
    let result = deduplicate state.seenContents payload.content

    if isNoChangeOutput result.output then
        AlreadySeen, state
    else
        NewContent payload,
        { state with
            seenContents = result.seenOutputs }

/// Tool names that represent a file-read operation across hosts.
let readToolNames = Set.ofList [ "read"; "file_read" ]

let dedupForPath
    (seenByPath: Map<string, string list>)
    (payload: ReadPayload)
    : Map<string, string list> * DedupVerdict =
    let pathSeen = Map.tryFind payload.path seenByPath |> Option.defaultValue []
    let verdict, nextState = processDedup { seenContents = pathSeen } payload
    (Map.add payload.path nextState.seenContents seenByPath), verdict

let foldDedup
    (seenByPath: Map<string, string list>)
    (payloads: ReadPayload list)
    : Map<string, string list> * (string list * bool list) =
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
            Map.add payload.path (next @ [ payload.content ]) map)
        Map.empty

let collectReadOutputs (payloads: ReadPayload list) : string list =
    payloads |> List.map (fun payload -> payload.content)
