module Wanxiangshu.Shell.ChildAgentRegistry

open Wanxiangshu.Kernel.Domain

type ChildAgentRegistry private (state: WorkspaceState ref) =

    static member Create() =
        ChildAgentRegistry({ contents = empty })

    member private _.parseChildId(input: string) =
        match Id.childId input with
        | Ok id -> Some id
        | Error _ -> None

    member private _.parseSessionId(input: string) =
        match Id.sessionId input with
        | Ok id -> Some id
        | Error _ -> None

    member this.RegisterChildAgent(sessionID: string, agent: string, parentSessionID: string option) =
        match this.parseChildId sessionID with
        | None -> ()
        | Some childId ->
            let meta =
                { agent = agent
                  parentSessionId = Option.bind this.parseSessionId parentSessionID }

            state.Value <- reduce state.Value (ChildRegistered(childId, meta))

    member this.LookupChildAgent(sessionID: string) =
        this.parseChildId sessionID
        |> Option.bind (fun childId -> Map.tryFind childId state.Value.childSessions)
        |> Option.map (fun meta -> meta.agent)

    member this.GetChildSessions() =
        state.Value.childSessions
        |> Map.toList
        |> List.map (fun (k, v) -> Id.childIdValue k, v.agent)

    member this.ResolveSubsessionParentID(sessionID: string option) =
        let rec resolve visited current resolved =
            if Set.contains current visited then
                Some(Id.sessionIdValue resolved)
            else
                match this.parseChildId (Id.sessionIdValue current) with
                | None -> Some(Id.sessionIdValue resolved)
                | Some childId ->
                    match Map.tryFind childId state.Value.childSessions with
                    | None -> Some(Id.sessionIdValue resolved)
                    | Some meta ->
                        match meta.parentSessionId with
                        | None -> Some(Id.sessionIdValue current)
                        | Some parent -> resolve (Set.add current visited) parent parent

        match sessionID with
        | None -> None
        | Some raw ->
            match this.parseSessionId raw with
            | None -> None
            | Some sid ->
                match this.parseChildId raw with
                | Some childId when Map.containsKey childId state.Value.childSessions -> resolve Set.empty sid sid
                | _ -> Some raw

    member this.UnregisterChildAgent(sessionID: string) =
        match this.parseChildId sessionID with
        | None -> ()
        | Some childId -> state.Value <- reduce state.Value (ChildUnregistered childId)
