namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Execution.Session
open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
type BookkeeperRequest =
    | CaseRefresh
    | CaseFinalize

/// Physical Bookkeeper leaf: one CreateChildSession per transaction, js-bookkeeper
/// against process-local staging, then AbortSession.
module BookkeeperRuntime =

    type private LiveAttachment =
        { TxId: string
          OwnerSessionId: string
          Attachment: AttachmentKind }

    let private gate = obj ()
    // DSL-MUTABLE: resource
    let mutable private sessionPort: ISessionHostPort option = None
    let private live = Dictionary<string, LiveAttachment>()

    [<Literal>]
    let CompletionTimeoutMs = 600_000

    let setSessionPort (port: ISessionHostPort) : unit =
        lock gate (fun () -> sessionPort <- Some port)

    let resetSessionPort () : unit =
        lock gate (fun () ->
            sessionPort <- None
            live.Clear())

    let bindSession (sessionId: string) (txId: string) (ownerSessionId: string) : unit =
        lock gate (fun () ->
            live.[sessionId] <-
                { TxId = txId
                  OwnerSessionId = ownerSessionId
                  Attachment = AttachmentKind.Bookkeeper txId })

    let unbindSession (sessionId: string) : unit =
        lock gate (fun () -> live.Remove sessionId |> ignore)

    let tryTxId (sessionId: string) : string option =
        lock gate (fun () ->
            match live.TryGetValue sessionId with
            | true, attachment -> Some attachment.TxId
            | false, _ -> None)

    let txIdFor (sessionId: string) : string =
        match tryTxId sessionId with
        | Some txId -> txId
        | None -> ""

    let isAttached (sessionId: string) : bool =
        lock gate (fun () -> live.ContainsKey sessionId)

    let private currentPort () : ISessionHostPort option = lock gate (fun () -> sessionPort)

    let private systemPrompt (ownerSessionId: string) =
        PromptResources.loadBookkeeperSystemFor (ProviderProse.languageOf (SessionId.create ownerSessionId))

    let private table (name: string) (fields: string list) : string =
        String.concat "\n" (("[" + name + "]") :: fields)

    let private evidencePatch (observations: Observation list) : string =
        observations
        |> Observations.normalize
        |> List.map (fun observation ->
            match observation with
            | Observation.FileRead(path, hash) -> "read " + path + " " + hash
            | Observation.GlobResult(pattern, paths) ->
                "glob " + pattern + " " + (paths |> List.sort |> String.concat ",")
            | Observation.GrepResult(pattern, matches) ->
                let flat =
                    matches
                    |> List.map (fun (path, index, text) -> path + "@" + string index + ":" + text)
                    |> List.sort
                    |> String.concat "|"

                "grep " + pattern + " " + flat)
        |> String.concat "\n"

    let private evidenceBlocks (observations: Observation list) : string list =
        observations
        |> Observations.normalize
        |> List.map (fun observation ->
            match observation with
            | Observation.FileRead(path, hash) ->
                SyntheticToml.tableArrayEntry
                    "evidence"
                    [ SyntheticToml.field "kind" (SyntheticToml.renderString "file_read")
                      SyntheticToml.field "path" (SyntheticToml.renderString path)
                      SyntheticToml.field "hash" (SyntheticToml.renderString hash) ]
            | Observation.GlobResult(pattern, paths) ->
                SyntheticToml.tableArrayEntry
                    "evidence"
                    [ SyntheticToml.field "kind" (SyntheticToml.renderString "glob")
                      SyntheticToml.field "pattern" (SyntheticToml.renderString pattern)
                      SyntheticToml.field
                          "paths"
                          (SyntheticToml.renderString (paths |> List.sort |> String.concat "\n")) ]
            | Observation.GrepResult(pattern, matches) ->
                let flat =
                    matches
                    |> List.map (fun (path, index, text) -> sprintf "%s:%d:%s" path index text)
                    |> String.concat "\n"

                SyntheticToml.tableArrayEntry
                    "evidence"
                    [ SyntheticToml.field "kind" (SyntheticToml.renderString "grep")
                      SyntheticToml.field "pattern" (SyntheticToml.renderString pattern)
                      SyntheticToml.field "matches" (SyntheticToml.renderString flat) ])

    let private envelope
        (kind: BookkeeperRequest)
        (ownerSessionId: string)
        (q: string)
        (a: string)
        (observations: Observation list)
        (extraTranscript: string option)
        : string =
        let kindLabel =
            match kind with
            | BookkeeperRequest.CaseRefresh -> "CaseRefresh"
            | BookkeeperRequest.CaseFinalize -> "CaseFinalize"

        let transcriptBlock =
            match kind, extraTranscript with
            | BookkeeperRequest.CaseFinalize, Some text when not (String.IsNullOrWhiteSpace text) ->
                [ table "transcript" [ SyntheticToml.field "content" (SyntheticToml.renderString text) ] ]
            | _ -> []

        SyntheticToml.document
            [ systemPrompt ownerSessionId ]
            ([ table "request" [ SyntheticToml.field "kind" (SyntheticToml.renderString kindLabel) ]
               table "case" [ SyntheticToml.field "session_id" (SyntheticToml.renderString ownerSessionId) ]
               table "question" [ SyntheticToml.field "content" (SyntheticToml.renderString q) ]
               table "answer" [ SyntheticToml.field "content" (SyntheticToml.renderString a) ]
               table
                   "repository_change"
                   [ SyntheticToml.field "patch" (SyntheticToml.renderString (evidencePatch observations)) ] ]
             @ evidenceBlocks observations
             @ transcriptBlock)

    let private childOptions (txId: string) : OpenCodeChildOptions =
        { Title = Some("bookkeeper:" + txId)
          Agent = Some "fast-inspector"
          Directory = None }

    let private promptOptions: OpenCodePromptOptions =
        { Model = None
          Agent = Some "fast-inspector"
          Directory = None
          Metadata = None
          Tools = Some(Map.ofList [ "*", false; "js-bookkeeper", true ])
          BindingIntent = SessionBindingIntent.Preserve }

    let private retire (sessions: ISessionHostPort) (childId: SessionId) : Task<unit> =
        task {
            try
                let! _ = sessions.AbortSession childId
                ()
            with _ ->
                ()

            unbindSession (SessionId.value childId)
        }

    let private completeOnOutcome
        (completion: TaskCompletionSource<Result<unit, string>>)
        (outcome: TerminalOutcome)
        : unit =
        match outcome with
        | TerminalOutcome.Completed _ -> AsyncSupport.trySetResult completion (Ok()) |> ignore
        | TerminalOutcome.Failed error -> AsyncSupport.trySetResult completion (Error error) |> ignore
        | TerminalOutcome.Aborted reason -> AsyncSupport.trySetResult completion (Error reason) |> ignore

    let private sendFailureOf =
        function
        | Retryable reason -> Some reason
        | Fatal reason -> Some reason
        | AcceptanceUnknown reason -> Some reason
        | AdmittedWithReceipt _
        | AdmittedWithPhysicalMessage _ -> None

    let private awaitCompletion
        (sessions: ISessionHostPort)
        (txId: string)
        (childId: SessionId)
        (completion: TaskCompletionSource<Result<unit, string>>)
        (disposeSub: unit -> unit)
        : Task<Result<string * string, string>> =
        task {
            let timedOut: Task<Result<unit, string>> =
                emitJsExpr
                    CompletionTimeoutMs
                    "new Promise(function (resolve) { var t = setTimeout(function () { resolve({ tag: 1, fields: ['bookkeeper transaction timed out'] }); }, $0); if (t && typeof t.unref === 'function') t.unref(); })"

            let finished = completion.Task

            let! waited = (emitJsExpr (finished, timedOut) "Promise.race([$0, $1])": Task<Result<unit, string>>)

            disposeSub ()

            match waited with
            | Error err ->
                BookkeeperStaging.abort txId
                do! retire sessions childId
                return Error err
            | Ok() ->
                let taken = BookkeeperStaging.take txId
                do! retire sessions childId
                return taken
        }

    let private runChild
        (sessions: ISessionHostPort)
        (txId: string)
        (childId: SessionId)
        (kind: BookkeeperRequest)
        (ownerSessionId: string)
        (q: string)
        (a: string)
        (observations: Observation list)
        (extraTranscript: string option)
        : Task<Result<string * string, string>> =
        task {
            let childKey = SessionId.value childId
            bindSession childKey txId ownerSessionId

            let completion =
                TaskCompletionSource<Result<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

            // DSL-MUTABLE: subscription — bookkeeper terminal subscription
            let mutable subscription: System.IDisposable option = None

            subscription <-
                Some(sessions.SubscribeTerminal(childId, (fun _ outcome -> completeOnOutcome completion outcome)))

            let promptText = envelope kind ownerSessionId q a observations extraTranscript
            let! sent = sessions.SendPrompt(childId, promptText, promptOptions)

            let disposeSub () =
                subscription |> Option.iter (fun active -> active.Dispose())
                subscription <- None

            match sendFailureOf sent with
            | Some reason ->
                disposeSub ()
                BookkeeperStaging.abort txId
                do! retire sessions childId
                return Error reason
            | None -> return! awaitCompletion sessions txId childId completion disposeSub
        }

    let private runWithPort
        (sessions: ISessionHostPort)
        (kind: BookkeeperRequest)
        (ownerSessionId: string)
        (q: string)
        (a: string)
        (observations: Observation list)
        (extraTranscript: string option)
        : Task<Result<string * string, string>> =
        task {
            let txId = Guid.NewGuid().ToString("N")
            BookkeeperStaging.beginTransaction txId q a

            let! created = sessions.CreateSiblingSession(SessionId.create ownerSessionId, None, childOptions txId)

            match created with
            | Error err ->
                BookkeeperStaging.abort txId
                return Error err
            | Ok childId -> return! runChild sessions txId childId kind ownerSessionId q a observations extraTranscript
        }

    let runTransaction
        (kind: BookkeeperRequest)
        (ownerSessionId: string)
        (q: string)
        (a: string)
        (observations: Observation list)
        (extraTranscript: string option)
        : Task<Result<string * string, string>> =
        task {
            match currentPort () with
            | None -> return Error "bookkeeper session port unavailable"
            | Some sessions -> return! runWithPort sessions kind ownerSessionId q a observations extraTranscript
        }
