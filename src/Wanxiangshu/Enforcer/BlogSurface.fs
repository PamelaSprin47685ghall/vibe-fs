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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
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

    let emptyTextError = Wanxiangshu.OpenCode.ChronicleTool.EmptyTextError
    let noLiveCycleError = Wanxiangshu.OpenCode.ChronicleTool.NoLiveCycleError

    /// Chronicle's canonical text gate.
    let canonicalText (value: obj) : obj =
        Wanxiangshu.OpenCode.ChronicleTool.tryCanonicalText (if isNullish value then null else string value)
        |> resultToJs box box

    /// Physical Blogger flight is the only live-cycle authority.
    let hasLiveCycle (hasFlight: bool) (_sessionId: string) : bool = hasFlight

    /// Pure semantic execute decision for the chronicle owner. The real Host
    /// supplies the physical abort; this boundary returns the exact observable
    /// consequence so tests do not construct ToolSpec/HostToolContext values.
    let execute (value: obj) : obj =
        let hasFlight = not (isNullish value?hasFlight) && unbox<bool> value?hasFlight
        let sessionId = text value?sessionId
        let entry = if isNullish value?entry then value?text else value?entry
        let tip = text value?tip

        if not hasFlight then
            box
                {| ok = false
                   error = noLiveCycleError
                   abortedSession =
                    if String.IsNullOrWhiteSpace sessionId then
                        null
                    else
                        box sessionId |}
        else
            match
                Wanxiangshu.OpenCode.ChronicleTool.tryCanonicalText (if isNullish entry then null else string entry)
            with
            | Error _ ->
                box
                    {| ok = true
                       text = "nothing-to-remember"
                       error = emptyTextError |}
            | Ok _ ->
                if String.IsNullOrWhiteSpace tip then
                    box
                        {| ok = true
                           text = "missing-tip"
                           error = "missing required argument: tip" |}
                elif
                    EnforcerCatalog.resolveByField tip (EnforcerCatalogResource.load ())
                    |> Option.isNone
                then
                    box
                        {| ok = true
                           text = "missing-tip"
                           error = "missing required argument: tip" |}
                else
                    box
                        {| ok = true
                           text = "remembered"
                           error = null |}

    let tipFieldNames () =
        EnforcerCatalog.fieldNames (EnforcerCatalogResource.load ()) |> List.toArray

    /// Live Blogger host owns the process-local flight/episode rendezvous.
    /// Tests hand the opaque PluginRuntimeScope; only the host crosses here.
    let private hostOf (value: obj) : IBloggerRuntimeHost =
        (unbox<Wanxiangshu.OpenCode.PluginRuntimeScope> value).BloggerRuntimeHost

    /// Real Blogger request context from a plain descriptor (Main/Squash shape
    /// shared with CompanionRuntimeSurface). Authority still comes from the
    /// durable profile; this only carries the exact live material.
    let private requestOf (value: obj) : BloggerRequestContext =
        match text value?kind with
        | "Squash" ->
            BloggerRequestContext.Squash
                { RequestId = BloggerRequestId.create (text value?requestId)
                  MainSessionId = SessionId.create (text value?mainSession)
                  BloggerSessionId = SessionId.create (text value?bloggerSession)
                  FrameEpochId = FrameEpochId.create (int64Value value?frameEpoch)
                  CoveredFrameCount = intValue value?coveredFrameCount
                  FrameDigests =
                    (if isNullish value?digests then
                         [||]
                     else
                         unbox<string array> value?digests)
                    |> Array.toList
                    |> List.map BlobDigest.create
                  ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedEpoch) }
        | _ ->
            let items =
                match BloggerDeltaItemWire.tryListOfJs value?items with
                | Ok parsed -> parsed
                | Error error -> invalidArg "items" error

            BloggerRequestContext.Main
                { RequestId = BloggerRequestId.create (text value?requestId)
                  MainSessionId = SessionId.create (text value?mainSession)
                  BloggerSessionId = SessionId.create (text value?bloggerSession)
                  Items = items
                  Toml = text value?toml
                  PreviousIngestedThroughSequence = int64Value value?previousIngested
                  NextIngestedThroughSequence = int64Value value?nextIngested
                  PreviousCoverableTurnCutoffExclusive = intValue value?previousCutoff
                  NextCoverableTurnCutoffExclusive = intValue value?nextCutoff
                  NextCoveredPrefixDigest = text value?nextDigest
                  FrameEpochId = FrameEpochId.create (int64Value value?frameEpoch)
                  DeltaDigest = BlobDigest.create (text value?deltaDigest)
                  ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedEpoch) }

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

    /// Blog-part status predicates used by continuation repair. The result is
    /// deliberately named and boolean rather than exposing a status DU.
    let classifyPart (part: obj) : obj =
        let isBlog =
            not (isNullish part)
            && (text part?tool = "chronicle" || text part?name = "chronicle")

        let state = if isNullish part?state then null else part?state
        let status = if isNullish state then "" else text state?status
        let metadata = if isNullish state then null else state?metadata

        let interrupted =
            if isNullish metadata then
                false
            else
                not (isNullish metadata?interrupted) && unbox<bool> metadata?interrupted

        box
            {| isBlogToolPart = isBlog
               status = if isNullish state then null else box status
               hasIncompleteBlogTool = isBlog && (status = "pending" || status = "running")
               hasFailedBlogAttempt = isBlog && (status = "error" || interrupted)
               blogPartInterrupted = isBlog && interrupted |}

    /// Coverage birth guard: sequence and cutoff advance together with the
    /// first durable frame; no synthetic zero/zero coverage is accepted.
    let coverageBirth (value: obj) : obj =
        let previousSequence = int64 (text value?previousIngestedThroughSequence)
        let nextSequence = int64 (text value?nextIngestedThroughSequence)
        let previousCutoff = int (text value?previousCoverableTurnCutoffExclusive)
        let nextCutoff = int (text value?nextCoverableTurnCutoffExclusive)

        if nextSequence <= previousSequence then
            box
                {| ok = false
                   error = "non-advancing ingested sequence" |}
        elif nextCutoff <= previousCutoff then
            box
                {| ok = false
                   error = "non-advancing coverable cutoff" |}
        elif String.IsNullOrWhiteSpace(text value?nextCoveredPrefixDigest) then
            box
                {| ok = false
                   error = "missing covered prefix digest" |}
        else
            box
                {| ok = true
                   ingestedThroughSequence = nextSequence
                   coverableTurnCutoffExclusive = nextCutoff |}

    /// Commit branch classification over semantic evidence. Each branch keeps
    /// the production failure meaning visible without leaking a Cycle DU.
    let classifyCommit (value: obj) : obj =
        let calls =
            if isNullish value?callCount then
                0
            else
                int (text value?callCount)

        let providerRun = text value?providerRun
        let tip = text value?tip

        if calls <> 1 then
            box
                {| branch = "ProtocolRepair"
                   ok = false
                   reason = "exactly one chronicle call required" |}
        elif String.IsNullOrWhiteSpace providerRun then
            box
                {| branch = "Fatal"
                   ok = false
                   reason = "no provable provider run" |}
        elif String.IsNullOrWhiteSpace tip then
            box
                {| branch = "ProtocolRepair"
                   ok = false
                   reason = "missing tip" |}
        elif
            EnforcerCatalog.tryFindByField tip (EnforcerCatalogResource.load ())
            |> Option.isNone
        then
            box
                {| branch = "ProtocolRepair"
                   ok = false
                   reason = "unknown tip" |}
        else
            box
                {| branch = "Committed"
                   ok = true
                   providerRun = providerRun
                   tipRuleId = tip |}

    /// Protocol transition for one terminal assistant step.
    let protocol (value: obj) : obj =
        let step = EnforcerSurface.classifyAssistantStep value
        let accepted = int step?acceptedCalls
        let messageId = text value?messageId

        if String.IsNullOrWhiteSpace messageId then
            box
                {| state = "ProjectMessages"
                   fatal = "no provable provider run" |}
        elif accepted = 0 then
            box
                {| state = "ProjectMessages"
                   fatal = null |}
        elif accepted = 1 then
            box
                {| state = "StopPhysicalRun"
                   fatal = null |}
        else
            box
                {| state = "ProjectMessages"
                   fatal = "exactly one chronicle call required" |}
