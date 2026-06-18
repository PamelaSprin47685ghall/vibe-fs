module VibeFs.Opencode.Actors

open Fable.Core
open VibeFs.Kernel.Domain

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

    member this.ResolveSubsessionParentID(sessionID: string option) =
        let rec resolve visited current resolved =
            if Set.contains current visited then
                Some (Id.sessionIdValue resolved)
            else
                match this.parseChildId (Id.sessionIdValue current) with
                | None -> Some (Id.sessionIdValue resolved)
                | Some childId ->
                    match Map.tryFind childId state.Value.childSessions with
                    | None -> Some (Id.sessionIdValue resolved)
                    | Some meta ->
                        match meta.parentSessionId with
                        | None -> Some (Id.sessionIdValue current)
                        | Some parent -> resolve (Set.add current visited) parent parent

        match sessionID with
        | None -> None
        | Some raw ->
            match this.parseSessionId raw with
            | None -> None
            | Some sid ->
                match this.parseChildId raw with
                | Some childId when Map.containsKey childId state.Value.childSessions ->
                    resolve Set.empty sid sid
                | _ -> Some raw

    member this.UnregisterChildAgent(sessionID: string) =
        match this.parseChildId sessionID with
        | None -> ()
        | Some childId ->
            state.Value <- reduce state.Value (ChildUnregistered childId)

type private ExecutorEnvelope =
    { work: unit -> Async<string>
      reply: AsyncReplyChannel<Result<string, exn>> }

type ExecutorActor() =
    let agents = System.Collections.Generic.Dictionary<string, MailboxProcessor<ExecutorEnvelope>>()

    let createSessionAgent () =
        MailboxProcessor.Start(fun inbox ->
            let rec loop () = async {
                let! message = inbox.Receive()
                let! outcome = Async.Catch(message.work())
                match outcome with
                | Choice1Of2 result -> message.reply.Reply(Ok result)
                | Choice2Of2 error -> message.reply.Reply(Error error)
                return! loop ()
            }
            loop ())

    let getSessionAgent (sessionID: string) =
        match agents.TryGetValue sessionID with
        | true, agent -> agent
        | false, _ ->
            let agent = createSessionAgent ()
            agents.[sessionID] <- agent
            agent

    member _.Post(sessionID: string, work: unit -> JS.Promise<string>) : JS.Promise<string> =
        async {
            let agent = getSessionAgent sessionID
            let! outcome = agent.PostAndAsyncReply(fun reply ->
                { work = fun () -> work () |> Async.AwaitPromise
                  reply = reply })
            match outcome with
            | Ok result -> return result
            | Error error -> return raise error
        }
        |> Async.StartAsPromise

let private actor = ExecutorActor()

let post (sessionID: string) (work: unit -> JS.Promise<string>) : JS.Promise<string> =
    actor.Post(sessionID, work)
