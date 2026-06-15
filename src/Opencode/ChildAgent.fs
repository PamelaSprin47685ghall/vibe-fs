module VibeFs.Opencode.ChildAgent

open VibeFs.Kernel.Boundary
open VibeFs.Kernel.WorkspaceState

let private workspace = ref empty

let private parseChildId input =
    match Id.childId input with
    | Ok id -> Some id
    | Error _ -> None

let private parseSessionId input =
    match Id.sessionId input with
    | Ok id -> Some id
    | Error _ -> None

let registerChildAgent sessionID agent parentSessionID =
    match parseChildId sessionID with
    | None -> ()
    | Some childId ->
        let meta =
            { agent = agent
              parentSessionId = Option.bind parseSessionId parentSessionID }
        workspace.Value <- reduce workspace.Value (ChildRegistered(childId, meta))

let lookupChildAgent sessionID =
    parseChildId sessionID
    |> Option.bind (fun childId -> Map.tryFind childId (workspace.Value).childSessions)
    |> Option.map (fun meta -> meta.agent)

let resolveSubsessionParentID sessionID =
    let rec resolve visited current resolved =
        if Set.contains current visited then
            Some (Id.sessionIdValue resolved)
        else
            match parseChildId (Id.sessionIdValue current) with
            | None -> Some (Id.sessionIdValue resolved)
            | Some childId ->
                match Map.tryFind childId (workspace.Value).childSessions with
                | None -> Some (Id.sessionIdValue resolved)
                | Some meta ->
                    match meta.parentSessionId with
                    | None -> Some (Id.sessionIdValue current)
                    | Some parent -> resolve (Set.add current visited) parent parent

    match sessionID with
    | None -> None
    | Some raw ->
        match parseSessionId raw with
        | None -> None
        | Some sid ->
            match parseChildId raw with
            | Some childId when Map.containsKey childId (workspace.Value).childSessions ->
                resolve Set.empty sid sid
            | _ -> Some raw

let unregisterChildAgent sessionID =
    match parseChildId sessionID with
    | None -> ()
    | Some childId ->
        workspace.Value <- reduce workspace.Value (ChildUnregistered childId)
