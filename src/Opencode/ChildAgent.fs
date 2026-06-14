module VibeFs.Opencode.ChildAgent

open Fable.Core
open VibeFs.Kernel

/// In-memory registry mapping a child session id to the agent that owns it and
/// the parent session it was spawned from.  This lets nested subagents resolve
/// their lineage and lets hooks identify which agent is running in a session.
let private records = System.Collections.Generic.Dictionary<string, obj>()

type private Record =
    { agent: string
      parentSessionID: string option }

let private toRecord (o: obj) : Record =
    let parent = Dyn.get o "parentSessionID"
    { agent = string (Dyn.get o "agent")
      parentSessionID = if Dyn.isNullish parent then None else Some (string parent) }

/// Register a newly-created child session so future lookups know its agent and
/// its parent.
let registerChildAgent (sessionID: string) (agent: string) (parentSessionID: string option) : unit =
    records.[sessionID] <- box {| agent = agent; parentSessionID = (match parentSessionID with Some s -> box s | None -> box null) |}

/// Look up the agent name associated with a session id.
let lookupChildAgent (sessionID: string) : string option =
    match records.TryGetValue(sessionID) with
    | true, o -> Some (toRecord o).agent
    | false, _ -> None

/// Resolve the ultimate parent session id for a subagent.  If the session is
/// not a known child, the session itself is the parent.  This is the id that
/// must be passed to `client.session.create` as `parentID`.
let resolveSubsessionParentID (sessionID: string option) : string option =
    match sessionID with
    | None -> None
    | Some id when id = "" -> None
    | Some id ->
        if not (records.ContainsKey(id)) then Some id
        else
            let visited = System.Collections.Generic.HashSet<string>()
            let mutable current = id
            let mutable resolved = id
            let mutable cycling = false
            while not cycling && not (visited.Contains(current)) do
                visited.Add(current) |> ignore
                match records.TryGetValue(current) with
                | true, o ->
                    let parent = (toRecord o).parentSessionID
                    match parent with
                    | None -> cycling <- true
                    | Some p ->
                        resolved <- p
                        if not (records.ContainsKey(p)) then
                            cycling <- true
                        else
                            current <- p
                | false, _ -> cycling <- true
            Some resolved

/// Remove a child session record (used when a session is aborted/destroyed).
let unregisterChildAgent (sessionID: string) : unit =
    records.Remove(sessionID) |> ignore
