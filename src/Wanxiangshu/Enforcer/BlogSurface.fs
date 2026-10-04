namespace Wanxiangshu.Enforcer

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources

/// JS-native owner boundary for the Blogger/chronicle contract and recovery
/// evidence. It exposes semantic outcomes only; Host tool records, journal
/// facts and typed identities stay private.
[<RequireQualifiedAccess>]
module BlogSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("$0[$1](...$2)")>]
    let private invokeRawTask (value: obj) (name: string) (args: obj array) : System.Threading.Tasks.Task<obj> =
        jsNative

    [<Emit("Promise.resolve($0($1, $2))")>]
    let private invokeTermination
        (terminate: obj)
        (sessionId: string)
        (reason: string)
        : System.Threading.Tasks.Task<obj> =
        jsNative

    [<Emit("$0[$1](...$2)")>]
    let private invokeRawDisposable (value: obj) (name: string) (args: obj array) : IDisposable = jsNative

    [<Emit("$0[$1](...$2)")>]
    let private invokeRawValue (value: obj) (name: string) (args: obj array) : obj = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private arrayOf (value: obj) : obj array =
        if isNullish value then [||] else unbox<obj array> value

    let private optionText (value: obj) : string option =
        if isNullish value then None else Some(text value)

    let private intValue (value: obj) : int = int (text value)
    let private int64Value (value: obj) : int64 = int64 (text value)

    let private resultToJs (ok: 'a -> obj) (error: 'e -> obj) (result: Result<'a, 'e>) : obj =
        match result with
        | Ok value -> box {| ok = true; value = ok value |}
        | Error reason -> box {| ok = false; error = error reason |}

    let emptyTextError = ChronicleExecution.EmptyTextError
    let noLiveCycleError = ChronicleExecution.NoLiveCycleError

    /// Chronicle's canonical text gate.
    let canonicalText (value: obj) : obj =
        ChronicleExecution.tryCanonicalText (if isNullish value then null else string value)
        |> resultToJs box box

    /// Live Blogger host owns the process-local flight/episode rendezvous.
    let private hostOf (value: obj) : IBloggerRuntimeHost = unbox<IBloggerRuntimeHost> value

    /// Real Blogger request context from a plain descriptor (Main/Squash shape
    /// shared with CompanionRuntimeSurface). Authority still comes from the
    /// durable profile; this only carries the exact live material.
    let private requestOf (value: obj) : BloggerRequestContext =
        match text value?kind with
        | "Squash" ->
            let mainSessionId = SessionId.create (text value?mainSession)
            let bloggerSessionId = SessionId.create (text value?bloggerSession)
            let frameEpoch = FrameEpochId.create (int64Value value?frameEpoch)
            let coveredFrameCount = intValue value?coveredFrameCount

            let frameDigests =
                (if isNullish value?digests then
                     [||]
                 else
                     unbox<string array> value?digests)
                |> Array.toList
                |> List.map BlobDigest.create

            let requestId =
                if System.String.IsNullOrWhiteSpace(text value?requestId) then
                    BloggerRequestContext.squashRequestId
                        mainSessionId
                        bloggerSessionId
                        frameEpoch
                        coveredFrameCount
                        frameDigests
                else
                    BloggerRequestId.create (text value?requestId)

            let candidate: BloggerSquashRequestInput =
                { RequestId = requestId
                  MainSessionId = mainSessionId
                  BloggerSessionId = bloggerSessionId
                  FrameEpochId = frameEpoch
                  CoveredFrameCount = coveredFrameCount
                  FrameDigests = frameDigests
                  ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedEpoch) }

            match BloggerRequestMaterial.createSquash candidate with
            | Ok verified -> BloggerRequestContext.Squash verified
            | Error rejection -> invalidArg "request" (sprintf "squash request rejected: %A" rejection)
        | _ ->
            let items =
                match BloggerDeltaItemWire.tryListOfJs value?items with
                | Ok parsed -> parsed
                | Error error -> invalidArg "items" error

            let mainSessionId = SessionId.create (text value?mainSession)
            let bloggerSessionId = SessionId.create (text value?bloggerSession)
            let toml = text value?toml
            let previousIngested = int64Value value?previousIngested
            let nextIngested = int64Value value?nextIngested
            let deltaDigest = BlobDigest.create (HostDigest.sha256Hex toml)

            let requestId =
                if System.String.IsNullOrWhiteSpace(text value?requestId) then
                    BloggerRequestContext.mainRequestId
                        mainSessionId
                        bloggerSessionId
                        deltaDigest
                        previousIngested
                        nextIngested
                else
                    BloggerRequestId.create (text value?requestId)

            let candidate: BloggerMainRequestInput =
                { RequestId = requestId
                  MainSessionId = mainSessionId
                  BloggerSessionId = bloggerSessionId
                  Items = items
                  Toml = toml
                  PreviousIngestedThroughSequence = previousIngested
                  NextIngestedThroughSequence = nextIngested
                  PreviousCoverableTurnCutoffExclusive = intValue value?previousCutoff
                  NextCoverableTurnCutoffExclusive = intValue value?nextCutoff
                  NextCoveredPrefixDigest = text value?nextDigest
                  FrameEpochId = FrameEpochId.create (int64Value value?frameEpoch)
                  DeltaDigest = deltaDigest
                  ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedEpoch) }

            match BloggerRequestMaterial.createMain candidate with
            | Ok verified -> BloggerRequestContext.Main verified
            | Error rejection -> invalidArg "request" (sprintf "main request rejected: %A" rejection)

    /// Opaque journal capability from JS. `JournalSurface_boot` hands out a
    /// handle whose `Journal` member is internal to this assembly, so the
    /// handle is read structurally: this file compiles before
    /// Persistence.Journal.Surface and cannot name its type.
    let private journalOf (value: obj) : AgentJournal option =
        if isNullish value then
            None
        else
            let nested = value?Journal

            if isNullish nested then
                Some(unbox<AgentJournal> value)
            else
                Some(unbox<AgentJournal> nested)

    let reloadRequest (journal: obj) (value: obj) : System.Threading.Tasks.Task<obj> =
        task {
            let durable = journalOf journal |> Option.get

            let openRequest: OpenBloggerRequest =
                { RequestId = BloggerRequestId.create (text value?requestId)
                  MainSessionId = SessionId.create (text value?mainSession)
                  BloggerSessionId = SessionId.create (text value?bloggerSession)
                  RequestKind = text value?requestKind
                  ContextRef = BlobRef.create (text value?contextRef)
                  ContextDigest = BlobDigest.create (text value?contextDigest)
                  ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedEpoch)
                  PreviousIngestedThroughSequence = int64Value value?previousIngested
                  NextIngestedThroughSequence = int64Value value?nextIngested
                  FrameEpochId = FrameEpochId.create (int64Value value?frameEpoch)
                  SelectedFrameDigests = arrayOf value?digests |> Array.map (text >> BlobDigest.create) |> Array.toList
                  PromptKey = None }

            match! EnforcerFrameRecovery.tryReloadRequestContextDetailed durable openRequest with
            | Error rejection ->
                return
                    box
                        {| ok = false
                           error = EnforcerFrameRecovery.reloadRejectionLabel rejection |}
            | Ok(BloggerRequestContext.Main request) ->
                return
                    box
                        {| ok = true
                           kind = "Main"
                           requestId = BloggerRequestId.value request.RequestId
                           toml = request.Toml
                           deltaDigest = BlobDigest.value request.DeltaDigest
                           previousIngested = int request.PreviousIngestedThroughSequence
                           nextIngested = int request.NextIngestedThroughSequence
                           frameEpoch = int (FrameEpochId.value request.FrameEpochId)
                           observedEpoch = int (PrefixEpochId.value request.ObservedPrefixEpochId) |}
            | Ok(BloggerRequestContext.Squash request) ->
                return
                    box
                        {| ok = true
                           kind = "Squash"
                           requestId = BloggerRequestId.value request.RequestId
                           coveredFrameCount = request.CoveredFrameCount
                           digests = request.FrameDigests |> List.map BlobDigest.value |> List.toArray
                           frameEpoch = int (FrameEpochId.value request.FrameEpochId)
                           observedEpoch = int (PrefixEpochId.value request.ObservedPrefixEpochId) |}
        }

    /// Drive the real Blogger continuation transform over a Host-shaped
    /// transcript: step position, ownership proof and the owned branches are
    /// the production ones.
    let continueTransform
        (scope: obj)
        (journal: obj)
        (bloggerSessionId: string)
        (rawMessages: obj)
        : System.Threading.Tasks.Task<obj> =
        task {
            let! outcome =
                EnforcerContinuation.handleContinuation
                    (hostOf scope)
                    (journalOf journal)
                    (SessionId.create bloggerSessionId)
                    (arrayOf rawMessages |> Array.toList)

            return
                match outcome with
                | EnforcerContinuation.ContinuationOutcome.ProjectMessages projected ->
                    box
                        {| kind = "ProjectMessages"
                           messages = List.toArray projected |}
                | EnforcerContinuation.ContinuationOutcome.StopPhysicalRun(projected, reason) ->
                    box
                        {| kind = "StopPhysicalRun"
                           messages = List.toArray projected
                           reason = reason |}
        }

    /// The owner binds its landed physical dispatch to the request through the
    /// production binder: exact flight claim plus the durable open request
    /// carrying the dispatch PromptKey.
    let bindRequestDispatch
        (scope: obj)
        (journal: obj)
        (request: obj)
        (promptKey: string)
        : System.Threading.Tasks.Task<obj> =
        task {
            match journalOf journal with
            | None ->
                return
                    box
                        {| ok = false
                           error = "journal required" |}
            | Some durable ->
                let! bound =
                    BloggerCoordinator.bindContinuationContext
                        (hostOf scope)
                        durable
                        (requestOf request)
                        (PromptKey.create promptKey)

                return resultToJs (fun () -> box true) box bound
        }

    /// Drive the real stop boundary: the admission barrier lands before the
    /// detached physical abort is requested. `terminate` is the Host
    /// termination capability as a JS function `sessionId -> reason ->
    /// Promise<null|{ok:true}|{ok:false,error:string}>`; the exact execution
    /// and session ids cross as plain strings.
    let applyPhysicalStop (terminate: obj) (sessionId: string) (physicalUserMessageId: string) (reason: string) =
        let terminateSession (sid: SessionId) (why: string) : System.Threading.Tasks.Task<Result<unit, string>> =
            task {
                try
                    let! result = invokeRawTask terminate "call" [| box terminate; box (SessionId.value sid); box why |]

                    if isNullish result || unbox<bool> result?ok then
                        return Ok()
                    else
                        return Error(text result?error)
                with ex ->
                    return Error ex.Message
            }

        EnforcerContinuation.applyPhysicalStop
            terminateSession
            (SessionId.create sessionId)
            sessionId
            (if String.IsNullOrWhiteSpace physicalUserMessageId then
                 None
             else
                 Some(PhysicalUserMessageId.create physicalUserMessageId))
            reason

    /// Complete reconciled turn for the idle repair entry. Only the fields the
    /// coordinator reads (session, physical message, run, directory,
    /// quiescence permit, delivery) cross the boundary; classification parts
    /// stay empty. JS sees only primitive identity strings: session/event
    /// callbacks cross as plain strings and plain outcome snapshots, and the
    /// typed Host contracts are rebuilt here so every decision still comes
    /// from BloggerCoordinator.
    let private terminalSnapshot (outcome: Wanxiangshu.OpenCode.TerminalOutcome) : obj =
        match outcome with
        | Wanxiangshu.OpenCode.TerminalOutcome.Completed result ->
            box
                {| kind = "Completed"
                   providerRun = ProviderRunIdentity.value result.ProviderRun
                   text = result.TerminalText |}
        | Wanxiangshu.OpenCode.TerminalOutcome.Aborted stop ->
            box
                {| kind = "Aborted"
                   text = stop.Reason
                   authorityRoot =
                    stop.AuthorityRootUserMessageId
                    |> Option.map AuthorityRootUserMessageId.value
                    |> Option.defaultValue "" |}
        | Wanxiangshu.OpenCode.TerminalOutcome.Failed stop ->
            box
                {| kind = "Failed"
                   text = stop.Reason
                   authorityRoot =
                    stop.AuthorityRootUserMessageId
                    |> Option.map AuthorityRootUserMessageId.value
                    |> Option.defaultValue "" |}

    let private terminalOfJs (value: obj) : Wanxiangshu.OpenCode.TerminalOutcome =
        match text value?kind with
        | "Completed" ->
            let sessionId = SessionId.create (text value?sessionId)
            let run = text value?providerRun

            Wanxiangshu.OpenCode.TerminalOutcome.Completed
                { SessionId = sessionId
                  AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (text value?authorityRoot)
                  ProviderRun =
                    ProviderRunIdentity.create (
                        if String.IsNullOrWhiteSpace run then
                            "unidentified"
                        else
                            run
                    )
                  Role = Role.Blogger
                  Directory = None
                  TerminalText = text value?text
                  TurnFormalText = text value?text }
        | "Aborted" ->
            Wanxiangshu.OpenCode.TerminalOutcome.Aborted(Wanxiangshu.OpenCode.TerminalStop.session (text value?text))
        | _ -> Wanxiangshu.OpenCode.TerminalOutcome.Failed(Wanxiangshu.OpenCode.TerminalStop.session (text value?text))

    /// Plain JS session port: session ids cross as strings, completions as
    /// snapshots. SendPrompt answers stay opaque production outcomes so the
    /// dispatcher keeps its real transport evidence.
    type private JsSessionPort(raw: obj) =
        interface Wanxiangshu.OpenCode.ISessionHostPort with
            member _.SubscribeTerminal(sessionId, listener) =
                let callback =
                    fun (rawSession: obj) (rawOutcome: obj) ->
                        listener (SessionId.create (text rawSession)) (terminalOfJs rawOutcome)

                invokeRawDisposable raw "SubscribeTerminal" [| box (SessionId.value sessionId); box callback |]

            member _.SubscribeFutureTerminal(sessionId, listener) =
                let callback =
                    fun (rawSession: obj) (rawOutcome: obj) ->
                        listener (SessionId.create (text rawSession)) (terminalOfJs rawOutcome)

                invokeRawDisposable raw "SubscribeFutureTerminal" [| box (SessionId.value sessionId); box callback |]

            member _.SendPrompt(sessionId, promptText, options) =
                task {
                    let! value =
                        invokeRawTask
                            raw
                            "SendPrompt"
                            [| box (SessionId.value sessionId); box promptText; box options |]

                    return unbox<Outcome.SendOutcome> value
                }

            member _.AbortSession _ =
                System.Threading.Tasks.Task.FromResult(Ok())

            member _.InterruptAttempt _ =
                System.Threading.Tasks.Task.FromResult(Ok())

            member _.IsManagedChild _ = false

            member _.AbortChildren _ : System.Threading.Tasks.Task =
                (task { return () } :> System.Threading.Tasks.Task)

            member _.CreateSiblingSession(_, _, _) =
                System.Threading.Tasks.Task.FromResult(Error "unsupported")

            member _.TryGetParentSession _ =
                System.Threading.Tasks.Task.FromResult(Ok None)

            member _.CreateChildSession(_, _) =
                System.Threading.Tasks.Task.FromResult(Error "unsupported")

            member _.ListChildren _ =
                System.Threading.Tasks.Task.FromResult(Ok [])

            member _.FamilyRootOf(sessionId) = sessionId

    /// Plain JS event port: NotifyTerminal receives the string session id and
    /// a primitive snapshot, never a Fable DU.
    type private JsEventPort(raw: obj) =
        interface Wanxiangshu.OpenCode.IEventObservationPort with
            member _.SubscribeTerminalListener(listener) =
                let callback =
                    fun (rawSession: obj) (rawOutcome: obj) ->
                        listener (SessionId.create (text rawSession)) (terminalOfJs rawOutcome)

                invokeRawDisposable raw "SubscribeTerminalListener" [| box callback |]

            member _.SubscribeFutureTerminalListener(listener) =
                let callback =
                    fun (rawSession: obj) (rawOutcome: obj) ->
                        listener (SessionId.create (text rawSession)) (terminalOfJs rawOutcome)

                invokeRawDisposable raw "SubscribeFutureTerminalListener" [| box callback |]

            member _.NotifyTerminal sessionId outcome =
                unbox<bool> (
                    invokeRawValue raw "NotifyTerminal" [| box (SessionId.value sessionId); terminalSnapshot outcome |]
                )

    let private rootReaderOf (raw: obj) : Wanxiangshu.OpenCode.IRootWorkspaceReader =
        { new Wanxiangshu.OpenCode.IRootWorkspaceReader with
            member _.TryRead() =
                let value = invokeRawValue raw "TryRead" [||]
                if isNullish value then None else Some(string value) }

    let private turnContextOf (value: obj) permit : Wanxiangshu.Composition.Turn.ReconciledTurnContext =
        if isNullish value then
            invalidArg "context" "idle repair observation requires a reconciled turn context"

        let turn: Wanxiangshu.Composition.Turn.ReconciledTurn =
            { SessionId = SessionId.create (text value?sessionId)
              PhysicalUserMessageId = PhysicalUserMessageId.create (text value?physicalUserMessageId)
              AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (text value?authorityRoot)
              ProviderRun = ProviderRunIdentity.create (text value?providerRun)
              Role = None
              Directory = optionText value?directory
              Parts = [||]
              Finish = None
              ErrorName = None
              Model = None
              Outcome = Wanxiangshu.Composition.Turn.ReconcileProgram.TurnCompleted
              Observation = None }

        let delivery =
            let d =
                if isNullish value?delivery then
                    value?Delivery
                else
                    value?delivery

            match text d with
            | "IdleRevisit" -> Wanxiangshu.Composition.Turn.ReconciledTurnDelivery.IdleRevisit
            | _ -> Wanxiangshu.Composition.Turn.ReconciledTurnDelivery.Observation

        { Turn = turn
          Failure = None
          Quiescence = permit
          Delivery = delivery }

    /// Coordinator repair verdict rendered as a plain outcome object. The name
    /// is the production result; no stage is reconstructed here.
    let private repairOutcomeToJs (outcome: BloggerRepairOutcome) : obj =
        match outcome with
        | BloggerRepairOutcome.NudgeSent key ->
            box
                {| outcome = "NudgeSent"
                   promptKey = key |> Option.map PromptKey.value |> Option.toObj |}
        | BloggerRepairOutcome.AabbSent key ->
            box
                {| outcome = "AabbSent"
                   promptKey = key |> Option.map PromptKey.value |> Option.toObj |}
        | BloggerRepairOutcome.RepairInjected messages ->
            box
                {| outcome = "RepairInjected"
                   messages = messages |> List.toArray |}
        | BloggerRepairOutcome.PendingRepairWait -> box {| outcome = "PendingRepairWait" |}
        | BloggerRepairOutcome.UnownedIdleIgnored -> box {| outcome = "UnownedIdleIgnored" |}
        | BloggerRepairOutcome.SupersededIgnored -> box {| outcome = "SupersededIgnored" |}
        | BloggerRepairOutcome.UnprovenIgnored -> box {| outcome = "UnprovenIgnored" |}
        | BloggerRepairOutcome.AbandonedExhausted -> box {| outcome = "AbandonedExhausted" |}
        | BloggerRepairOutcome.Completed -> box {| outcome = "Completed" |}

    /// Drive the real transform repair entry: observed terminal/tool facts in,
    /// coordinator verdict out. The exact live request and terminal run cross
    /// explicitly; `rawMessages` is the plain Host transcript.
    let observeTransformRepair
        (scope: obj)
        (journal: obj)
        (request: obj)
        (terminalRun: string)
        (rawMessages: obj)
        : System.Threading.Tasks.Task<obj> =
        task {
            let! outcome =
                BloggerCoordinator.observeTransformRepair
                    (hostOf scope)
                    (journalOf journal)
                    (requestOf request)
                    (ProviderRunIdentity.create terminalRun)
                    (arrayOf rawMessages |> Array.toList)

            return repairOutcomeToJs outcome
        }

    /// Drive the real idle repair entry. The observation states quiescence
    /// explicitly: when quiescent the surface
    /// begins the exact provider attempt on a real SessionQuiescenceGate,
    /// observes its idle, and places the resulting permit in the reconciled
    /// turn; otherwise the turn carries no permit. Session, workspace and
    /// event ports arrive as plain JS stubs over primitive strings; only
    /// their exercised calls take effect. The run is read from the turn itself.
    let observeIdleRepair
        (scope: obj)
        (journal: obj)
        (request: obj)
        (observation: obj)
        : System.Threading.Tasks.Task<obj> =
        task {
            let contextValue = observation?context
            let sessionId = SessionId.create (text contextValue?sessionId)
            let gate = Wanxiangshu.OpenCode.SessionQuiescenceGate()

            let permit =
                if isNullish observation?quiescent || not (unbox<bool> observation?quiescent) then
                    None
                else
                    gate.BeginProviderAttempt sessionId
                    Some(gate.ObserveIdle sessionId)

            let! outcome =
                BloggerCoordinator.observeIdleRepair
                    (hostOf scope)
                    (journalOf journal)
                    (requestOf request)
                    (gate :> Wanxiangshu.OpenCode.ISessionQuiescenceGate)
                    (turnContextOf contextValue permit)
                    (JsSessionPort(observation?sessionPort) :> Wanxiangshu.OpenCode.ISessionHostPort)
                    (rootReaderOf observation?rootWorkspace)
                    (JsEventPort(observation?eventPort) :> Wanxiangshu.OpenCode.IEventObservationPort)

            return repairOutcomeToJs outcome
        }

    /// Durable repair-claim facts read through the production probe. No stage
    /// is derived here; the coordinator owns repair sequencing.
    let repairClaimedForKind
        (journal: obj)
        (bloggerSessionId: string)
        (requestId: string)
        (terminalRun: string)
        (repairKind: string)
        : bool =
        match journalOf journal with
        | None -> false
        | Some durable ->
            BloggerRecoveryProbe.repairClaimedForKind
                durable
                (SessionId.create bloggerSessionId)
                (BloggerRequestId.create requestId)
                (ProviderRunIdentity.create terminalRun)
                repairKind

    let repairIssuedForKind
        (journal: obj)
        (bloggerSessionId: string)
        (requestId: string)
        (terminalRun: string)
        (repairKind: string)
        : bool =
        match journalOf journal with
        | None -> false
        | Some durable ->
            BloggerRecoveryProbe.repairIssuedForKind
                durable
                (SessionId.create bloggerSessionId)
                (BloggerRequestId.create requestId)
                (ProviderRunIdentity.create terminalRun)
                repairKind

    let private id (value: obj) = text value

    let private observationPayload (value: obj) : ContextFactCases =
        let toolCalls = arrayOf value?toolCallIds |> Array.map id
        let evidence = optionText value?evidenceRef

        ContextFactCases.BlogObservationCommitted
            {| SessionId = SessionId.create (text value?sessionId)
               BloggerSessionId = SessionId.create (text value?bloggerSessionId)
               RequestId = BloggerRequestId.create (text value?requestId)
               FrameEpochId = FrameEpochId.create (int64 (text value?frameEpoch))
               PreviousIngestedThroughSequence = int64 (text value?previousIngestedThroughSequence)
               NextIngestedThroughSequence = int64 (text value?nextIngestedThroughSequence)
               PreviousCoverableTurnCutoffExclusive = int (text value?previousCoverableTurnCutoffExclusive)
               NextCoverableTurnCutoffExclusive = int (text value?nextCoverableTurnCutoffExclusive)
               NextCoveredPrefixDigest = text value?nextCoveredPrefixDigest
               TextRef = BlobRef.create (text value?textRef)
               TextDigest = BlobDigest.create (text value?textDigest)
               ProviderRun = ProviderRunIdentity.create (text value?run)
               ToolCallIds = toolCalls |> Array.toList |> List.map ToolCallId.create
               TipRuleId = text value?tipRuleId
               FieldNameAtCommit = optionText value?fieldNameAtCommit
               EvidenceRef = evidence |> Option.map BlobRef.create
               ObservedPrefixEpochId = PrefixEpochId.create (int64 (text value?observedPrefixEpoch)) |}

    let private observationSquashPayload (value: obj) : ContextFactCases =
        ContextFactCases.BlogObservationsSquashed
            {| SessionId = SessionId.create (text value?sessionId)
               BloggerSessionId = SessionId.create (text value?bloggerSessionId)
               RequestId = BloggerRequestId.create (text value?requestId)
               PreviousFrameEpochId = FrameEpochId.create (int64 (text value?previousFrameEpoch))
               NextFrameEpochId = FrameEpochId.create (int64 (text value?nextFrameEpoch))
               CoveredFrameCount = int (text value?coveredFrameCount)
               TextRef = BlobRef.create (text value?textRef)
               TextDigest = BlobDigest.create (text value?textDigest)
               ProviderRun = ProviderRunIdentity.create (text value?run) |}

    let private factOfJs (value: obj) : Fact =
        match text value?case with
        | "BlogObservationCommitted" -> Fact.Agent(AgentFact.Context(observationPayload value))
        | "BlogObservationsSquashed" -> Fact.Agent(AgentFact.Context(observationSquashPayload value))
        | other -> failwith $"BlogSurface: unknown fact '{other}'"

    let private factCase (value: Fact) : string =
        match value with
        | Fact.Agent(AgentFact.Context(ContextFactCases.BlogObservationCommitted _)) -> "BlogObservationCommitted"
        | Fact.Agent(AgentFact.Context(ContextFactCases.BlogObservationsSquashed _)) -> "BlogObservationsSquashed"
        | _ -> "Unknown"

    /// Serialize the two observation facts with the production FactCodec.
    let serializeFact (value: obj) : string =
        FactCodec.serializeFact (factOfJs value)

    /// Decode a fact line and expose only its normalized bytes and semantic case.
    let deserializeFact (line: string) : obj =
        match FactCodec.deserializeFact line with
        | Error error -> box {| ok = false; error = error |}
        | Ok fact ->
            box
                {| ok = true
                   case = factCase fact
                   line = FactCodec.serializeFact fact |}

    let containsLegacyScoreVectorEntry (line: string) =
        FactCodec.containsLegacyScoreVectorEntry line

    let tipV2CleanBreakMessage = FactCodec.tipV2CleanBreakMessage

    let private streamOf (value: obj) : StreamId =
        match text value?kind with
        | "Session" -> StreamId.Session(SessionId.create (text value?id))
        | "Workspace" -> StreamId.Workspace
        | other -> failwith $"BlogSurface: unknown stream '{other}'"

    let private envelopeOfJs (value: obj) : Envelope =
        { RuntimeId = RuntimeId.create (text value?runtimeId)
          LocalSeq = LocalSeq.create (int64 (text value?localSeq))
          ObservedAt = DateTimeOffset.Parse(text value?observedAt)
          EventId = EventId.create (text value?eventId)
          Stream = streamOf (value?stream)
          ProviderRun = optionText value?providerRun |> Option.map ProviderRunIdentity.create
          Fact = factOfJs (value?fact) }

    let private envelopeToJs (value: Envelope) : obj =
        box
            {| runtimeId = RuntimeId.value value.RuntimeId
               localSeq = LocalSeq.value value.LocalSeq
               observedAt = value.ObservedAt.ToOffset(TimeSpan.Zero).ToString("O")
               eventId = EventId.value value.EventId
               case = factCase value.Fact
               line = Envelope.serialize value |}

    let serializeEnvelope (value: obj) : string = Envelope.serialize (envelopeOfJs value)

    let deserializeEnvelope (line: string) : obj =
        match Envelope.deserialize line with
        | Error error -> box {| ok = false; error = error |}
        | Ok value ->
            box
                {| ok = true
                   value = envelopeToJs value |}

    let serializeObservationFact (value: obj) : string = serializeFact value
    let deserializeObservationFact (line: string) : obj = deserializeFact line

    let private sha256 (value: string) : string = HostDigest.sha256Hex value

    /// Build the complete Blogger projection plan from semantic frame/tip
    /// inputs. The builder retains pairing, physical-delta ordering and
    /// squash instruction placement behind the Blog owner boundary.
    let buildProjectionPlan (value: obj) : obj =
        let kind =
            match text value?kind with
            | "Squash" -> CompanionRequestKind.Squash(int (text value?count))
            | _ -> CompanionRequestKind.Normal

        let frameBodies =
            arrayOf value?frameBodies
            |> Array.toList
            |> List.map (fun item -> BlobDigest.create (text item?digest), text item?body)

        let physicalDelta =
            if isNullish value?physicalDelta then
                None
            else
                let items =
                    match BloggerDeltaItemWire.tryListOfJs value?physicalDelta?items with
                    | Ok parsed -> parsed
                    | Error error -> invalidArg "physicalDelta.items" error

                Some(text value?physicalDelta?id, items)

        let previousTips =
            arrayOf value?previousTips
            |> Array.toList
            |> List.map (fun item -> text item?tipName, text item?cycleId)

        let lines (item: obj) =
            arrayOf item |> Array.toList |> List.map text

        let plan =
            CompanionProjectionBuilder.build
                sha256
                (SessionId.create (text value?bloggerSessionId))
                (FrameEpochId.create (int64 (text value?frameEpoch)))
                kind
                frameBodies
                physicalDelta
                previousTips
                (lines value?normalInstructionLines)
                (lines value?squashInstructionLines)

        let messages =
            plan.Messages
            |> List.map (fun message ->
                box
                    {| id = message.MessageId
                       role = message.Role
                       text = message.Text
                       isPhysical = message.IsPhysical |})
            |> List.toArray

        box
            {| messages = messages
               isFirstTurnShape = CompanionProjectionBuilder.isFirstTurnShape plan |}

    /// Explicit trace evidence drives the real fold and coverage birth decision.
    let coverageBirth (value: obj) : obj =
        let previousSequence = int64 (text value?previousIngestedThroughSequence)
        let nextSequence = int64 (text value?nextIngestedThroughSequence)
        let previousCutoff = int (text value?previousCoverableTurnCutoffExclusive)
        let nextCutoff = int (text value?nextCoverableTurnCutoffExclusive)

        let traceSequences =
            arrayOf value?traceSequences
            |> Array.toList
            |> List.map (fun item -> int64 (text item))

        let xTrace =
            traceSequences
            |> List.fold
                (fun state sequence ->
                    XTraceProjection.applyPart
                        sequence
                        "user"
                        "g:0/coverage-birth"
                        (int sequence)
                        0
                        "text"
                        None
                        None
                        None
                        None
                        (BlobRef.create "coverage-birth")
                        (BlobDigest.create "coverage-birth")
                        state
                    |> Result.defaultWith (string >> invalidOp))
                XTraceProjection.empty

        let blog =
            { BlogProjection.empty with
                Coverage =
                    { BlogProjection.empty.Coverage with
                        IngestedThroughSequence = previousSequence
                        CoverableTurnCutoffExclusive = previousCutoff
                        CoveredPrefixDigest = text value?nextCoveredPrefixDigest } }

        let projection: ProviderProjection.ProviderSemanticProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = [] }

        let chunk: BloggerDeltaChunk =
            { Items = []
              Toml = "coverage-birth"
              NextCursor =
                { TurnIndex = int nextSequence + 1
                  PartIndex = 0 }
              NextCoverableTurnCutoffExclusive = nextCutoff }

        let mainSessionId = SessionId.create "coverage-birth-main"
        let bloggerSessionId = SessionId.create "coverage-birth-blogger"

        match
            BloggerMainContext.mainContextFromChunk
                mainSessionId
                bloggerSessionId
                PrefixEpochId.initial
                blog
                xTrace
                projection
                chunk
        with
        | Some(BloggerRequestContext.Main context) ->
            box
                {| ok = true
                   ingestedThroughSequence = context.NextIngestedThroughSequence
                   coverableTurnCutoffExclusive = context.NextCoverableTurnCutoffExclusive
                   nextCoveredPrefixDigest = context.NextCoveredPrefixDigest |}
        | Some(BloggerRequestContext.Squash _) ->
            box
                {| ok = false
                   error = "unexpected squash context" |}
        | None when nextSequence <= previousSequence ->
            box
                {| ok = false
                   error = "non-advancing ingested sequence" |}
        | None ->
            box
                {| ok = false
                   error = "unmapped next cursor" |}

    /// Convert the production decoder's result without reimplementing its protocol.
    let decodeCycle (messages: obj array) : obj =
        match EnforcerCycleDecode.latestAssistant (Array.toList messages) with
        | None -> null
        | Some step ->
            let calls = EnforcerCycleDecode.callsOf (fun _ _ -> ()) step
            let rawCallCount = EnforcerCycleDecode.chronicleCallCount step

            let decision =
                EnforcerCycleDecode.validateCycle step.MessageId rawCallCount calls
                |> resultToJs
                    (fun (cycle, identities) ->
                        box
                            {| text = cycle.MergedText
                               evidence = cycle.MergedEvidence
                               ruleId = cycle.CanonicalTip.RuleId
                               toolCallIds = identities |> List.map ToolCallId.value |> List.toArray |})
                    box

            box
                {| messageId = step.MessageId
                   completed = step.Completed
                   chronicleCallCount = rawCallCount
                   decodedCalls = List.length calls
                   decision = decision |}
