namespace Wanxiangshu.Interaction.Dispatch

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal

/// Dispatch-owned JavaScript boundary. Host ports stay opaque and durable
/// JournalHandle capabilities never cross as Fable records; only transport
/// constructors, send observations, and claim counts are plain values.
[<RequireQualifiedAccess>]
module DispatchSurface =

    type private PlainSessionPort(raw: obj) =
        let typed = unbox<Wanxiangshu.OpenCode.ISessionHostPort> raw
        let sendPrompt = raw?``SendPrompt``
        // DSL-MUTABLE: buffer — latest physical send observation for the JS result
        let mutable lastObservation: obj = null

        member _.LastObservation = lastObservation

        interface Wanxiangshu.OpenCode.ISessionHostPort with
            member _.SubscribeTerminal(sessionId, listener) =
                typed.SubscribeTerminal(sessionId, listener)

            member _.SendPrompt(sessionId, text, options) =
                lastObservation <-
                    box
                        {| session = SessionId.value sessionId
                           text = text
                           agent = options.Agent |> Option.defaultValue null
                           model =
                            options.Model
                            |> Option.map (fun model ->
                                box
                                    {| providerID = model.providerID
                                       modelID = model.modelID
                                       variant = model.variant |> Option.defaultValue null |})
                            |> Option.defaultValue null
                           directory = options.Directory |> Option.defaultValue null
                           metadata = options.Metadata |> Option.defaultValue null |}

                emitJsExpr (sendPrompt, SessionId.value sessionId, text, options) "$0($1,$2,$3)"

            member _.AbortSession(sessionId) = typed.AbortSession sessionId
            member _.InterruptAttempt(sessionId) = typed.InterruptAttempt sessionId

            member _.TerminateAttempt(sessionId: SessionId, reason: string) : Task<Result<unit, string>> =
                typed.TerminateAttempt(sessionId, reason)

            member _.TryTakeAttemptTermination(sessionId: SessionId) : string option =
                typed.TryTakeAttemptTermination sessionId

            member _.AbortChildren(sessionId) = typed.AbortChildren sessionId

            member _.CreateSiblingSession(owner, parent, options) =
                typed.CreateSiblingSession(owner, parent, options)

            member _.TryGetParentSession(sessionId) = typed.TryGetParentSession sessionId

            member _.CreateChildSession(parent, options) =
                typed.CreateChildSession(parent, options)

            member _.ListChildren(parent) = typed.ListChildren parent
            member _.FamilyRootOf(sessionId) = typed.FamilyRootOf sessionId

    let internal sessionPort (port: obj) : Wanxiangshu.OpenCode.ISessionHostPort =
        PlainSessionPort(port) :> Wanxiangshu.OpenCode.ISessionHostPort

    let admittedWithReceipt (value: string) : Outcome.SendOutcome =
        Outcome.SendOutcome.AdmittedWithReceipt(TransportReceipt.create value)

    let admittedWithPhysicalMessage (value: string) : Outcome.SendOutcome =
        Outcome.SendOutcome.AdmittedWithPhysicalMessage(PhysicalUserMessageId.create value)

    let retryable (reason: string) : Outcome.SendOutcome = Outcome.SendOutcome.Retryable reason

    let acceptanceUnknown (reason: string) : Outcome.SendOutcome =
        Outcome.SendOutcome.AcceptanceUnknown reason

    let fatal (reason: string) : Outcome.SendOutcome = Outcome.SendOutcome.Fatal reason

    let private appendResult result =
        match result with
        | Ok _ -> box {| ok = true; error = null |}
        | Error failure ->
            box
                {| ok = false
                   error = JournalAppendFailure.describe failure |}

    /// Seed the durable AgentOwnerRoot needed by a continuation owner. This is
    /// the same PromptFact writer used by production ingress; the returned value
    /// contains no AgentFact/union representation.
    let appendAuthorityRoot (handle: JournalHandle) (session: string) (agent: string) : Task<obj> =
        task {
            match PromptAuthority.parseAgentName agent with
            | Error error -> return box {| ok = false; error = error |}
            | Ok(_, role, tier, peer) ->
                let sessionId = SessionId.create session

                let fact =
                    PromptFact.AuthorityRootAccepted
                        {| SessionId = sessionId
                           LogicalRunId = LogicalRunId.create (sprintf "run-%s" session)
                           AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (sprintf "root-%s" session)
                           AuthorityKind = "AgentOwnerRoot"
                           SelectedAgent = agent
                           PeerAgent = peer
                           CanonicalRole = PromptAuthority.roleLabel role
                           SelectedTier = PromptAuthority.tierLabel tier |}

                let! result = AgentJournal.appendAgent (StreamId.Session sessionId) None fact handle.Journal
                return appendResult result
        }

    /// Real PROMPT-002 Detached send through the production dispatcher. The
    /// transport port is adapted only at this JS boundary; claim/persist/send
    /// semantics remain PromptDispatcher.Runtime.
    let sendAgentOwnerRoot
        (port: obj)
        (handle: JournalHandle)
        (session: string)
        (text: string)
        (agent: string)
        : Task<obj> =
        task {
            let runtime = PromptDispatcher.forJournal handle.Journal
            let adapter = PlainSessionPort(port)

            let! result =
                runtime.SendAgentOwnerRoot
                    (adapter :> Wanxiangshu.OpenCode.ISessionHostPort)
                    (SessionId.create session)
                    text
                    agent
                    None
                    PromptDispatcher.AwaitMode.Detached
                    None

            return
                match result with
                | Ok key ->
                    box
                        {| ok = true
                           key = PromptKey.value key
                           error = null
                           observation = adapter.LastObservation |}
                | Error error ->
                    box
                        {| ok = false
                           key = null
                           error = error
                           observation = adapter.LastObservation |}
        }

    let private profileOf (value: obj) : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        let selectedAgent =
            if isNull value?selectedAgent then
                ""
            else
                string value?selectedAgent

        match PromptAuthority.parseAgentName selectedAgent with
        | Error error -> Error error
        | Ok(_, role, tier, peer) ->
            let peerAgent =
                if isNull value?peerAgent then
                    peer
                else
                    string value?peerAgent

            match string value?authorityKind with
            | "AgentOwnerRoot" ->
                Ok
                    { SessionId = SessionId.create (string value?session)
                      LogicalRunId = LogicalRunId.create (string value?logicalRun)
                      AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (string value?authorityRoot)
                      AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                      SelectedAgent = selectedAgent
                      PeerAgent = peerAgent
                      CanonicalRole = role
                      SelectedTier = tier }
            | "HumanRoot" ->
                Ok
                    { SessionId = SessionId.create (string value?session)
                      LogicalRunId = LogicalRunId.create (string value?logicalRun)
                      AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (string value?authorityRoot)
                      AuthorityKind = PromptAuthority.RootAuthorityKind.HumanRoot
                      SelectedAgent = selectedAgent
                      PeerAgent = peerAgent
                      CanonicalRole = role
                      SelectedTier = tier }
            | unknown -> Error(sprintf "Unknown authority root kind: %s" unknown)

    let private awaitModeOf (value: string) =
        if isNull value then
            PromptDispatcher.AwaitMode.Await
        else
            match value with
            | "Detached" -> PromptDispatcher.AwaitMode.Detached
            | _ -> PromptDispatcher.AwaitMode.Await

    let sendContinuation
        (port: obj)
        (handle: JournalHandle)
        (session: string)
        (text: string)
        (continuation: string)
        (profile: obj)
        (effectiveAgent: string)
        (awaitMode: string)
        : Task<obj> =
        task {
            match PromptAuthority.tryParseContinuationKind continuation, profileOf profile with
            | Some kind, Ok authorityProfile ->
                let runtime = PromptDispatcher.forJournal handle.Journal
                let adapter = PlainSessionPort(port)

                let! result =
                    runtime.SendContinuation
                        (adapter :> Wanxiangshu.OpenCode.ISessionHostPort)
                        (SessionId.create session)
                        text
                        kind
                        authorityProfile
                        effectiveAgent
                        None
                        (awaitModeOf awaitMode)
                        None

                return
                    match result with
                    | Ok key ->
                        box
                            {| ok = true
                               key = PromptKey.value key
                               error = null
                               observation = adapter.LastObservation |}
                    | Error error ->
                        box
                            {| ok = false
                               key = null
                               error = error
                               observation = adapter.LastObservation |}
            | None, _ ->
                return
                    box
                        {| ok = false
                           key = null
                           error = sprintf "Unknown continuation kind: %s" continuation
                           observation = null |}
            | _, Error error ->
                return
                    box
                        {| ok = false
                           key = null
                           error = error
                           observation = null |}
        }

    /// HOST-004 / DISPATCH-PROTOCOL-002: exercise the dispatch-owned final
    /// physical-send admission without exposing Quiescence internals to this
    /// package's JS tests. Crash-reconciliation proves when the admission turns
    /// stale; this surface proves that stale evidence closes the durable claim
    /// and never reaches the Host SendPrompt boundary.
    let sendIdleContinuation
        (port: obj)
        (handle: JournalHandle)
        (session: string)
        (text: string)
        (continuation: string)
        (profile: obj)
        (effectiveAgent: string)
        (physicalAdmission: bool)
        : Task<obj> =
        task {
            match PromptAuthority.tryParseContinuationKind continuation, profileOf profile with
            | Some kind, Ok authorityProfile ->
                let runtime = PromptDispatcher.forJournal handle.Journal
                let adapter = PlainSessionPort(port)

                let! outcome =
                    runtime.SendIdleContinuation
                        (adapter :> Wanxiangshu.OpenCode.ISessionHostPort)
                        (SessionId.create session)
                        text
                        kind
                        authorityProfile
                        effectiveAgent
                        None
                        PromptDispatcher.AwaitMode.Await
                        None
                        (fun () -> physicalAdmission)

                return
                    match outcome with
                    | PromptDispatcher.SendAttemptOutcome.Sent key ->
                        box
                            {| outcome = "Sent"
                               key = PromptKey.value key
                               error = null
                               observation = adapter.LastObservation |}
                    | PromptDispatcher.SendAttemptOutcome.Superseded ->
                        box
                            {| outcome = "Superseded"
                               key = null
                               error = null
                               observation = adapter.LastObservation |}
                    | PromptDispatcher.SendAttemptOutcome.Failed error ->
                        box
                            {| outcome = "Failed"
                               key = null
                               error = error
                               observation = adapter.LastObservation |}
            | None, _ ->
                return
                    box
                        {| outcome = "Failed"
                           key = null
                           error = sprintf "Unknown continuation kind: %s" continuation
                           observation = null |}
            | _, Error error ->
                return
                    box
                        {| outcome = "Failed"
                           key = null
                           error = error
                           observation = null |}
        }

    let private profileView (profile: PromptAuthority.AuthorityExecutionProfile) : obj =
        box
            {| session = SessionId.value profile.SessionId
               logicalRun = LogicalRunId.value profile.LogicalRunId
               authorityRoot = AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId
               authorityKind =
                match profile.AuthorityKind with
                | PromptAuthority.RootAuthorityKind.AgentOwnerRoot -> "AgentOwnerRoot"
                | PromptAuthority.RootAuthorityKind.HumanRoot -> "HumanRoot"
               selectedAgent = profile.SelectedAgent
               peerAgent = profile.PeerAgent
               canonicalRole = PromptAuthority.roleLabel profile.CanonicalRole
               selectedTier = PromptAuthority.tierLabel profile.SelectedTier |}

    /// PROMPT-004/005: prove one dispatched AgentOwnerRoot at a physical message
    /// boundary. The Dispatcher writes PhysicalAccepted before registering the
    /// authority profile; only the normalized profile crosses this boundary.
    let acceptAgentOwnerRoot
        (handle: JournalHandle)
        (session: string)
        (promptKey: string)
        (physicalMessageId: string)
        : Task<obj> =
        task {
            let! result =
                (PromptDispatcher.forJournal handle.Journal).AcceptAgentOwnerRoot
                    (PromptKey.create promptKey)
                    (SessionId.create session)
                    (PhysicalUserMessageId.create physicalMessageId)

            return
                match result with
                | Ok profile ->
                    box
                        {| ok = true
                           profile = profileView profile
                           error = null |}
                | Error error ->
                    box
                        {| ok = false
                           profile = null
                           error = error |}
        }

    /// PROMPT-004: accept the external HumanRoot through the same Dispatcher
    /// writer used by chat.message. The physical id is supplied by the caller as
    /// host-boundary evidence; this surface never invents an alias.
    let acceptHumanRoot
        (handle: JournalHandle)
        (session: string)
        (physicalMessageId: string)
        (agent: string)
        : Task<obj> =
        task {
            let! result =
                (PromptDispatcher.forJournal handle.Journal).AcceptHumanRoot
                    (SessionId.create session)
                    (PhysicalUserMessageId.create physicalMessageId)
                    (Some agent)

            return
                match result with
                | Ok profile ->
                    box
                        {| ok = true
                           profile = profileView profile
                           error = null |}
                | Error error ->
                    box
                        {| ok = false
                           profile = null
                           error = error |}
        }

    let private claimView (claim: PromptAuthority.PromptClaim) : obj =
        box
            {| promptKey = PromptKey.value claim.PromptKey
               session = SessionId.value claim.SessionId
               origin = PromptAuthority.originLabel claim.Origin
               logicalRun = claim.LogicalRunId |> Option.map LogicalRunId.value |> Option.defaultValue null
               authorityRoot =
                claim.AuthorityRootUserMessageId
                |> Option.map AuthorityRootUserMessageId.value
                |> Option.defaultValue null
               effectiveAgent = claim.EffectiveAgent |> Option.defaultValue null
               payloadDigest = claim.PayloadDigest
               receipt = claim.Receipt |> Option.map TransportReceipt.value |> Option.defaultValue null
               claimedAtRuntimeStartCount = claim.ClaimedAtRuntimeStartCount |}

    let projectionObservation (handle: JournalHandle) (session: string) : obj =
        let runtime = PromptDispatcher.forJournal handle.Journal
        let projection = runtime.ProjectionFor(SessionId.create session)
        let snapshot = AgentJournal.snapshot handle.Journal

        box
            {| runtimeStartCount = snapshot.AgentProjections.RuntimeStartCount
               activeLogicalRun =
                projection.ActiveLogicalRun
                |> Option.map profileView
                |> Option.defaultValue null
               pendingClaims =
                projection.PendingClaims
                |> Map.toArray
                |> Array.map (fun (_, claim) -> claimView claim)
               claimSequences =
                projection.ClaimSequences
                |> Map.toArray
                |> Array.map (fun (scope, count) -> box {| scope = scope; count = count |}) |}

    let sendMemberObservation () : obj =
        box
            {| owner = "PromptDispatcher.Runtime"
               members =
                [| "SendAgentOwnerRoot"
                   "SendAgentOwnerRootDetachedObserved"
                   "SendAgentOwnerRootWithTools"
                   "SendContinuation"
                   "SendContinuationWithTools"
                   "SendRepairFamily"
                   "SendInteractionRepair"
                   "SendManagerIdleEncouragement" |]
               standaloneFireAndForget = false |}

    let awaitModeObservation () : obj =
        box
            {| await = "Await"
               detached = "Detached" |}

    let runtimeStartPolicy () : obj =
        box
            {| claimStamp = "workspace-runtime-start-count"
               advancesWorkspaceWatermark = true
               restartRecoveryAuthority = false |}

    let private watermarkText (value: obj) =
        if isNull value then "" else string value

    let private watermarkEnvelope (value: obj) : Envelope =
        let kind = watermarkText value?kind
        let sequence = Int64.Parse(watermarkText value?seq)
        let runtime = watermarkText value?runtime

        let observedAt =
            if isNull value?observedAt then
                DateTimeOffset.Parse("1970-01-01T00:00:00Z").AddTicks sequence
            else
                DateTimeOffset.Parse(watermarkText value?observedAt)

        let stream, fact =
            match kind with
            | "runtime-start" ->
                StreamId.Workspace,
                Fact.Runtime(
                    Fact.RuntimeFact.RuntimeStarted
                        {| RuntimeId = RuntimeId.create runtime
                           ProcessId = 0
                           StartedAt = observedAt |}
                )
            | "claim" ->
                let session = SessionId.create (watermarkText value?session)
                let logicalRun = watermarkText value?logicalRun
                let authorityRoot = watermarkText value?authorityRoot

                StreamId.Session session,
                Fact.Agent(
                    AgentFact.Prompt(
                        PromptFactCases.PluginPromptClaimed
                            {| PromptKey = PromptKey.create (watermarkText value?promptKey)
                               SessionId = session
                               ContinuationKind = watermarkText value?continuationKind
                               LogicalRunId =
                                if System.String.IsNullOrWhiteSpace logicalRun then
                                    None
                                else
                                    Some(LogicalRunId.create logicalRun)
                               AuthorityRootUserMessageId =
                                if System.String.IsNullOrWhiteSpace authorityRoot then
                                    None
                                else
                                    Some(AuthorityRootUserMessageId.create authorityRoot)
                               EffectiveAgent =
                                let agent = watermarkText value?effectiveAgent

                                if System.String.IsNullOrWhiteSpace agent then
                                    None
                                else
                                    Some agent
                               PayloadDigest = watermarkText value?payloadDigest |}
                    )
                )
            | other -> invalidArg "kind" (sprintf "unknown watermark event kind: %s" other)

        { RuntimeId = RuntimeId.create runtime
          LocalSeq = LocalSeq.create sequence
          ObservedAt = observedAt
          EventId = EventId.create (sprintf "dispatch-watermark-%d" sequence)
          Stream = stream
          ProviderRun = None
          Fact = fact }

    let foldRuntimeStartWatermark (events: obj array) : obj =
        let rec loop current remaining =
            match remaining with
            | [] ->
                let claims =
                    current.AgentProjections.Sessions
                    |> Map.toArray
                    |> Array.collect (fun (sessionId, session) ->
                        match session.PromptAuthority with
                        | None -> [||]
                        | Some authority ->
                            authority.PendingClaims
                            |> Map.toArray
                            |> Array.map (fun (_, claim) ->
                                box
                                    {| session = SessionId.value sessionId
                                       promptKey = PromptKey.value claim.PromptKey
                                       claimedAtRuntimeStartCount = claim.ClaimedAtRuntimeStartCount |}))

                box
                    {| ok = true
                       value =
                        box
                            {| runtimeStartCount = current.AgentProjections.RuntimeStartCount
                               claims = claims |}
                       error = null |}
            | value :: tail ->
                match Fold.foldEnvelope current (watermarkEnvelope value) with
                | Ok updated -> loop updated tail
                | Error rejection ->
                    box
                        {| ok = false
                           value = null
                           error =
                            box
                                {| fact = rejection.Fact
                                   reason = rejection.Reason |} |}

        loop Fold.empty (events |> Array.toList)

    let pendingClaimCount (handle: JournalHandle) (session: string) : int =
        let projection =
            (PromptDispatcher.forJournal handle.Journal)
                .ProjectionFor(SessionId.create session)

        projection.PendingClaims |> Map.count
