namespace Wanxiangshu.Repository.Knowledge.Casebook

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

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

    type private Runtime =
        { Sessions: ISessionHostPort
          ResolveActiveOwner: SessionId -> PromptAuthority.AuthorityExecutionProfile option }

    let private gate = obj ()
    // DSL-MUTABLE: resource
    let mutable private runtime: Runtime option = None
    let private live = Dictionary<string, LiveAttachment>()
    let private pendingPromptAuthorizations = Dictionary<string, string * string>()

    let private pendingCompletions =
        Dictionary<string, TaskCompletionSource<Result<unit, string>>>()

    let setRuntime
        (sessions: ISessionHostPort)
        (resolveActiveOwner: SessionId -> PromptAuthority.AuthorityExecutionProfile option)
        : unit =
        lock gate (fun () ->
            runtime <-
                Some
                    { Sessions = sessions
                      ResolveActiveOwner = resolveActiveOwner })

    let resetRuntime () : unit =
        lock gate (fun () ->
            runtime <- None
            live.Clear()
            pendingPromptAuthorizations.Clear()
            pendingCompletions.Clear())

    let bindSession (sessionId: string) (txId: string) (ownerSessionId: string) : unit =
        lock gate (fun () ->
            live.[sessionId] <-
                { TxId = txId
                  OwnerSessionId = ownerSessionId
                  Attachment = AttachmentKind.Bookkeeper txId })

    let unbindSession (sessionId: string) : unit =
        lock gate (fun () ->
            live.Remove sessionId |> ignore
            pendingPromptAuthorizations.Remove sessionId |> ignore
            pendingCompletions.Remove sessionId |> ignore)

    let private authorizePrompt (sessionId: SessionId) (agent: string) (text: string) =
        lock gate (fun () -> pendingPromptAuthorizations.[SessionId.value sessionId] <- agent, text)

    let tryConsumePromptAuthorization
        (sessionId: SessionId)
        (explicitAgent: string option)
        (text: string option)
        : bool =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match pendingPromptAuthorizations.TryGetValue key, explicitAgent, text with
            | (true, (expectedAgent, expectedText)), Some agent, Some payload when
                agent = expectedAgent && payload = expectedText
                ->
                pendingPromptAuthorizations.Remove key |> ignore
                true
            | _ -> false)

    let completePhysical (sessionId: SessionId) (outcome: Result<unit, string>) : unit =
        let completion =
            lock gate (fun () ->
                match pendingCompletions.TryGetValue(SessionId.value sessionId) with
                | true, pending -> Some pending
                | false, _ -> None)

        completion
        |> Option.iter (fun pending -> AsyncSupport.trySetResult pending outcome |> ignore)

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

    let private currentRuntime () : Runtime option = lock gate (fun () -> runtime)

    let private systemInstructions (ownerSessionId: string) =
        PromptResources.bookkeeperInstructionTextsFor (ProviderProse.languageOf (SessionId.create ownerSessionId))

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

    let private evidenceBlocks (observations: Observation list) : LlmFacing.DataBlock list =
        observations
        |> Observations.normalize
        |> List.map (fun observation ->
            match observation with
            | Observation.FileRead(path, hash) ->
                LlmFacing.Data.tableArray
                    "evidence"
                    [ LlmFacing.Data.stringMember "kind" "file_read"
                      LlmFacing.Data.stringMember "path" path
                      LlmFacing.Data.stringMember "hash" hash ]
            | Observation.GlobResult(pattern, paths) ->
                LlmFacing.Data.tableArray
                    "evidence"
                    [ LlmFacing.Data.stringMember "kind" "glob"
                      LlmFacing.Data.stringMember "pattern" pattern
                      LlmFacing.Data.stringMember "paths" (paths |> List.sort |> String.concat "\n") ]
            | Observation.GrepResult(pattern, matches) ->
                let flat =
                    matches
                    |> List.map (fun (path, index, text) -> sprintf "%s:%d:%s" path index text)
                    |> String.concat "\n"

                LlmFacing.Data.tableArray
                    "evidence"
                    [ LlmFacing.Data.stringMember "kind" "grep"
                      LlmFacing.Data.stringMember "pattern" pattern
                      LlmFacing.Data.stringMember "matches" flat ])

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
                [ LlmFacing.Data.table "transcript" [ LlmFacing.Data.stringMember "content" text ] ]
            | _ -> []

        LlmFacing.instructions (systemInstructions ownerSessionId)
        |> LlmFacing.withData (
            [ LlmFacing.Data.table "request" [ LlmFacing.Data.stringMember "kind" kindLabel ]
              LlmFacing.Data.table "case" [ LlmFacing.Data.stringMember "session_id" ownerSessionId ]
              LlmFacing.Data.table "question" [ LlmFacing.Data.stringMember "content" q ]
              LlmFacing.Data.table "answer" [ LlmFacing.Data.stringMember "content" a ]
              LlmFacing.Data.table
                  "repository_change"
                  [ LlmFacing.Data.stringMember "patch" (evidencePatch observations) ] ]
            @ evidenceBlocks observations
            @ transcriptBlock
        )
        |> LlmFacing.render

    let private canonicalAgent = ManagedAgentCatalog.bookkeeperName

    let private childOptions (txId: string) : OpenCodeChildOptions =
        { Title = Some("bookkeeper:" + txId)
          Agent = Some canonicalAgent
          Directory = None }

    let private exactTools = Map.ofList [ "*", false; "js-bookkeeper", true ]

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
        | TerminalOutcome.Failed stop -> AsyncSupport.trySetResult completion (Error stop.Reason) |> ignore
        | TerminalOutcome.Aborted stop -> AsyncSupport.trySetResult completion (Error stop.Reason) |> ignore

    let private awaitCompletion
        (sessions: ISessionHostPort)
        (txId: string)
        (childId: SessionId)
        (completion: TaskCompletionSource<Result<unit, string>>)
        (disposeSub: unit -> unit)
        : Task<Result<string * string, string>> =
        task {
            let! waited = completion.Task
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

    [<RequireQualifiedAccess>]
    type private BookkeeperPromptReceiptDecision =
        | AwaitTerminal
        | Reject of string

    let private decideBookkeeperPromptReceipt (outcome: SendOutcome) : BookkeeperPromptReceiptDecision =
        match outcome with
        | Retryable reason
        | Fatal reason
        | AcceptanceUnknown reason -> BookkeeperPromptReceiptDecision.Reject reason
        | AdmittedWithReceipt _
        | AdmittedWithPhysicalMessage _ -> BookkeeperPromptReceiptDecision.AwaitTerminal

    let private settleBookkeeperPromptReceipt
        (sessions: ISessionHostPort)
        (txId: string)
        (childId: SessionId)
        (disposeSub: unit -> unit)
        (decision: BookkeeperPromptReceiptDecision)
        : Task<Result<unit, string>> =
        match decision with
        | BookkeeperPromptReceiptDecision.AwaitTerminal -> Task.FromResult(Ok())
        | BookkeeperPromptReceiptDecision.Reject reason ->
            task {
                disposeSub ()
                BookkeeperStaging.abort txId
                do! retire sessions childId
                return Error reason
            }

    let private sendBookkeeperPrompt
        (sessions: ISessionHostPort)
        (txId: string)
        (childId: SessionId)
        (completion: TaskCompletionSource<Result<unit, string>>)
        (disposeSub: unit -> unit)
        (promptText: string)
        (promptOptions: OpenCodePromptOptions)
        : Task<Result<string * string, string>> =
        taskResult {
            let! receipt = sessions.SendPrompt(childId, promptText, promptOptions) |> TaskResultCE.ofTask

            let receiptDecision = decideBookkeeperPromptReceipt receipt
            do! settleBookkeeperPromptReceipt sessions txId childId disposeSub receiptDecision
            return! awaitCompletion sessions txId childId completion disposeSub
        }

    let private runChild
        (runtime: Runtime)
        (identitySeed: PromptAuthority.IdentitySeed)
        (txId: string)
        (childId: SessionId)
        (kind: BookkeeperRequest)
        (ownerSessionId: SessionId)
        (q: string)
        (a: string)
        (observations: Observation list)
        (extraTranscript: string option)
        : Task<Result<string * string, string>> =
        task {
            let sessions = runtime.Sessions
            let childKey = SessionId.value childId
            let ownerKey = SessionId.value ownerSessionId
            bindSession childKey txId ownerKey

            let completion =
                TaskCompletionSource<Result<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

            lock gate (fun () -> pendingCompletions.[childKey] <- completion)

            // DSL-MUTABLE: subscription — bookkeeper terminal subscription
            let mutable subscription: System.IDisposable option = None

            subscription <-
                Some(sessions.SubscribeTerminal(childId, (fun _ outcome -> completeOnOutcome completion outcome)))

            let disposeSub () =
                subscription |> Option.iter (fun active -> active.Dispose())
                subscription <- None
                lock gate (fun () -> pendingCompletions.Remove childKey |> ignore)

            let activeOwner = runtime.ResolveActiveOwner ownerSessionId

            match PromptAuthority.validateInheritedIdentitySeedAgainstActiveOwner activeOwner identitySeed with
            | Error rejection ->
                disposeSub ()
                BookkeeperStaging.abort txId
                do! retire sessions childId
                return Error(sprintf "bookkeeper identity seed rejected: %A" rejection)
            | Ok participantIdentity ->
                let promptOptions: OpenCodePromptOptions =
                    { Model = None
                      Agent = Some(ParticipantIdentity.selectedAgent participantIdentity)
                      Directory = None
                      Metadata = None
                      Tools = Some exactTools
                      BindingIntent = SessionBindingIntent.Preserve }

                let promptText = envelope kind ownerKey q a observations extraTranscript
                authorizePrompt childId (ParticipantIdentity.selectedAgent participantIdentity) promptText

                return! sendBookkeeperPrompt sessions txId childId completion disposeSub promptText promptOptions
        }

    let private activeOwnerProfile (runtime: Runtime) (ownerSessionId: SessionId) =
        match runtime.ResolveActiveOwner ownerSessionId with
        | Some profile -> Ok profile
        | None -> Error "bookkeeper owner has no active authority profile"

    let private runWithRuntime
        (runtime: Runtime)
        (kind: BookkeeperRequest)
        (ownerSessionId: SessionId)
        (q: string)
        (a: string)
        (observations: Observation list)
        (extraTranscript: string option)
        : Task<Result<string * string, string>> =
        task {
            match
                activeOwnerProfile runtime ownerSessionId
                |> Result.bind (fun profile ->
                    PromptAuthority.issueInheritedIdentitySeed canonicalAgent profile
                    |> Result.mapError (sprintf "invalid bookkeeper identity seed: %A"))
            with
            | Error error -> return Error error
            | Ok identitySeed ->
                let txId = Guid.NewGuid().ToString("N")
                BookkeeperStaging.beginTransaction txId q a

                match! runtime.Sessions.CreateSiblingSession(ownerSessionId, None, childOptions txId) with
                | Error error ->
                    BookkeeperStaging.abort txId
                    return Error error
                | Ok childId ->
                    return!
                        runChild runtime identitySeed txId childId kind ownerSessionId q a observations extraTranscript
        }

    let runTransaction
        (kind: BookkeeperRequest)
        (ownerSessionId: SessionId)
        (q: string)
        (a: string)
        (observations: Observation list)
        (extraTranscript: string option)
        : Task<Result<string * string, string>> =
        task {
            match currentRuntime () with
            | None -> return Error "bookkeeper runtime unavailable"
            | Some runtime -> return! runWithRuntime runtime kind ownerSessionId q a observations extraTranscript
        }
