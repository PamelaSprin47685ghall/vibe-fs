namespace Wanxiangshu.Context.Companion

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Persistence.Journal

/// Context-owned fold oracle for durable recovery laws.
/// It accepts plain fact/envelope data and returns plain projection summaries;
/// the typed fold and all Fable collections remain inside the production boundary.
[<RequireQualifiedAccess>]
module ContextFoldSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private sessionId (value: obj) = SessionId.create (text value)
    let private providerRun (value: obj) = ProviderRunIdentity.create (text value)
    let private blobRef (value: obj) = BlobRef.create (text value)
    let private blobDigest (value: obj) = BlobDigest.create (text value)

    let private frameEpoch (value: obj) =
        FrameEpochId.create (Int64.Parse(text value))

    let private prefixEpoch (value: obj) =
        PrefixEpochId.create (Int64.Parse(text value))

    let private bloggerRequest (value: obj) = BloggerRequestId.create (text value)
    let private promptKey (value: obj) = PromptKey.create (text value)

    let private optionalText (value: obj) =
        if isNull value then None else Some(text value)

    let private optionalBlobRef (value: obj) =
        if isNull value then None else Some(blobRef value)

    let private optionalProviderRun (value: obj) =
        if isNull value then None else Some(providerRun value)

    let private optionalToolCall (value: obj) =
        if isNull value then
            None
        else
            Some(ToolCallId.create (text value))

    let private optionalHostToolPart (value: obj) =
        if isNull value then
            None
        else
            Some(HostToolPartId.create (text value))

    let private int64Value (value: obj) = unbox<int64> value
    let private intValue (value: obj) = unbox<int> value

    let private stringList (value: obj) =
        if isNull value then
            []
        else
            unbox<string array> value |> Array.toList

    let private toolCallList (value: obj) =
        if isNull value then
            []
        else
            unbox<string array> value |> Array.toList |> List.map ToolCallId.create

    let private companionFact (caseName: string) (payload: obj) : AgentFact =
        match caseName with
        | "CompanionBloggerLinked" ->
            AgentFact.Companion(
                CompanionFactCases.CompanionBloggerLinked
                    {| SessionId = sessionId (payload?SessionId)
                       BloggerSessionId = sessionId (payload?BloggerSessionId)
                       BloggerAgent = text (payload?BloggerAgent) |}
            )
        | "CompanionBloggerClosed" ->
            AgentFact.Companion(
                CompanionFactCases.CompanionBloggerClosed {| SessionId = sessionId (payload?SessionId) |}
            )
        | "OpeningPromptCaptured" ->
            AgentFact.Companion(
                CompanionFactCases.OpeningPromptCaptured
                    {| SessionId = sessionId (payload?SessionId)
                       AssignmentText = text (payload?AssignmentText)
                       AuthoritativeRequirements = stringList (payload?AuthoritativeRequirements)
                       ProviderRun = optionalProviderRun (payload?ProviderRun) |}
            )
        | "XTracePartAppended" ->
            AgentFact.Companion(
                CompanionFactCases.XTracePartAppended
                    {| SessionId = sessionId (payload?SessionId)
                       CursorSequence = int64Value (payload?CursorSequence)
                       Role = text (payload?Role)
                       Turn = intValue (payload?Turn)
                       PartIndex = intValue (payload?PartIndex)
                       Kind = text (payload?Kind)
                       ToolName = optionalText (payload?ToolName)
                       TextRef = blobRef (payload?TextRef)
                       TextDigest = blobDigest (payload?TextDigest)
                       Provenance = text (payload?Provenance)
                       ProviderRun = optionalProviderRun (payload?ProviderRun)
                       ToolCallId = optionalToolCall (payload?ToolCallId)
                       HostToolPartId = optionalHostToolPart (payload?HostToolPartId) |}
            )
        | "TerminalOutputCaptured" ->
            AgentFact.Companion(
                CompanionFactCases.TerminalOutputCaptured
                    {| SessionId = sessionId (payload?SessionId)
                       TextRef = blobRef (payload?TextRef)
                       TextDigest = blobDigest (payload?TextDigest)
                       ProviderRun = providerRun (payload?ProviderRun) |}
            )
        | other -> failwith $"ContextFoldSurface: unknown Companion fact '{other}'"

    let private contextFact (caseName: string) (payload: obj) : AgentFact =
        match caseName with
        | "BlogObservationCommitted" ->
            AgentFact.Context(
                ContextFactCases.BlogObservationCommitted
                    {| SessionId = sessionId (payload?SessionId)
                       BloggerSessionId = sessionId (payload?BloggerSessionId)
                       RequestId = bloggerRequest (payload?RequestId)
                       FrameEpochId = frameEpoch (payload?FrameEpochId)
                       PreviousIngestedThroughSequence = int64Value (payload?PreviousIngestedThroughSequence)
                       NextIngestedThroughSequence = int64Value (payload?NextIngestedThroughSequence)
                       PreviousCoverableTurnCutoffExclusive = intValue (payload?PreviousCoverableTurnCutoffExclusive)
                       NextCoverableTurnCutoffExclusive = intValue (payload?NextCoverableTurnCutoffExclusive)
                       NextCoveredPrefixDigest = text (payload?NextCoveredPrefixDigest)
                       TextRef = blobRef (payload?TextRef)
                       TextDigest = blobDigest (payload?TextDigest)
                       ProviderRun = providerRun (payload?ProviderRun)
                       ToolCallIds = toolCallList (payload?ToolCallIds)
                       TipRuleId = text (payload?TipRuleId)
                       FieldNameAtCommit = optionalText (payload?FieldNameAtCommit)
                       EvidenceRef = optionalBlobRef (payload?EvidenceRef)
                       ObservedPrefixEpochId = prefixEpoch (payload?ObservedPrefixEpochId) |}
            )
        | "BlogObservationsSquashed" ->
            AgentFact.Context(
                ContextFactCases.BlogObservationsSquashed
                    {| SessionId = sessionId (payload?SessionId)
                       BloggerSessionId = sessionId (payload?BloggerSessionId)
                       RequestId = bloggerRequest (payload?RequestId)
                       PreviousFrameEpochId = frameEpoch (payload?PreviousFrameEpochId)
                       NextFrameEpochId = frameEpoch (payload?NextFrameEpochId)
                       CoveredFrameCount = intValue (payload?CoveredFrameCount)
                       TextRef = blobRef (payload?TextRef)
                       TextDigest = blobDigest (payload?TextDigest)
                       ProviderRun = providerRun (payload?ProviderRun) |}
            )
        | "PrefixRebaseCommitted" ->
            AgentFact.Context(
                ContextFactCases.PrefixRebaseCommitted
                    {| SessionId = sessionId (payload?SessionId)
                       PreviousEpochId = prefixEpoch (payload?PreviousEpochId)
                       NextEpochId = prefixEpoch (payload?NextEpochId)
                       FrozenRecordPrefixRef = blobRef (payload?FrozenRecordPrefixRef)
                       FrozenRecordPrefixDigest = blobDigest (payload?FrozenRecordPrefixDigest)
                       CutoffExclusive = intValue (payload?CutoffExclusive)
                       CoveredPrefixDigest = text (payload?CoveredPrefixDigest)
                       SealRoot = text (payload?SealRoot)
                       SyntheticMessageId = text (payload?SyntheticMessageId)
                       ProbeId = text (payload?ProbeId)
                       SolvingProviderRun = providerRun (payload?SolvingProviderRun) |}
            )
        | "ContextReanchored" ->
            AgentFact.Context(
                ContextFactCases.ContextReanchored
                    {| SessionId = sessionId (payload?SessionId)
                       PreviousEpochId = prefixEpoch (payload?PreviousEpochId)
                       NextEpochId = prefixEpoch (payload?NextEpochId)
                       ObservedCompactionRun = providerRun (payload?ObservedCompactionRun) |}
            )
        | other -> failwith $"ContextFoldSurface: unknown context fact '{other}'"

    let private agentFactOfJs (value: obj) : Fact =
        let family = text (value?family)
        let caseName = text (value?case)
        let payload = unbox<obj> (value?payload)

        match family with
        | "Companion" -> Fact.Agent(companionFact caseName payload)
        | "Context" -> Fact.Agent(contextFact caseName payload)
        | _ -> failwith $"ContextFoldSurface: unknown fact family '{family}'"

    let private streamOfJs (value: obj) =
        StreamId.Session(sessionId (value?session))

    let private envelopeOfJs (value: obj) : Envelope =
        let fact = agentFactOfJs (value?fact)

        { RuntimeId = RuntimeId.create (text (value?runtime))
          LocalSeq = LocalSeq.create (int64Value (value?seq))
          ObservedAt = DateTimeOffset.Parse(text (value?observedAt))
          EventId = EventId.create (text (value?id))
          Stream = streamOfJs value
          ProviderRun = optionalProviderRun (value?run)
          Fact = fact }

    let private prefixSnapshotToJs snapshot =
        match snapshot with
        | None -> null
        | Some value ->
            box
                {| FrozenRecordPrefixRef = BlobRef.value value.FrozenRecordPrefixRef
                   FrozenRecordPrefixDigest = BlobDigest.value value.FrozenRecordPrefixDigest
                   CutoffExclusive = value.CutoffExclusive
                   CoveredPrefixDigest = value.CoveredPrefixDigest
                   SealRoot = value.SealRoot
                   SyntheticMessageId = value.SyntheticMessageId |}

    let private blogToJs blog =
        match blog with
        | None -> null
        | Some state ->
            box
                {| FrameEpochId = FrameEpochId.value state.FrameEpochId
                   FrameKinds =
                    state
                    |> BlogProjection.frames
                    |> List.map (fun frame -> string frame.Kind)
                    |> List.toArray
                   FrameCount = BlogProjection.frameCount state
                   Coverage =
                    box
                        {| IngestedThroughSequence = state.Coverage.IngestedThroughSequence
                           CoverableTurnCutoffExclusive = state.Coverage.CoverableTurnCutoffExclusive
                           CoveredPrefixDigest = state.Coverage.CoveredPrefixDigest |} |}

    let private companionToJs (companion: CompanionProjection option) =
        match companion with
        | None -> null
        | Some state ->
            box
                {| BloggerSessionId =
                    match state.BloggerSessionId with
                    | None -> null
                    | Some blogger -> box (SessionId.value blogger) |}

    let private xTraceToJs (xTrace: XTraceProjectionState option) =
        match xTrace with
        | None -> null
        | Some state ->
            let opening =
                match XTraceProjection.openingEvidence state with
                | None -> null
                | Some value ->
                    box
                        {| AssignmentText = value.AssignmentText
                           AuthoritativeRequirements = value.AuthoritativeRequirements |> List.toArray |}

            let parts =
                XTraceProjection.orderedSemanticParts state
                |> List.map (fun part ->
                    box
                        {| CursorSequence = XTraceCursor.sequence part.Cursor
                           Provenance = part.Provenance
                           Role = part.Role
                           Kind = part.Kind
                           TextRef = BlobRef.value part.TextRef
                           TextDigest = BlobDigest.value part.TextDigest |})
                |> List.toArray

            let latestTerminal =
                match XTraceProjection.latestTerminalEvidence state with
                | None -> null
                | Some terminal ->
                    box
                        {| TextRef = BlobRef.value terminal.TextRef
                           TextDigest = BlobDigest.value terminal.TextDigest
                           ProviderRun = ProviderRunIdentity.value terminal.ProviderRun
                           FrontierSequence = XTraceCursor.sequence terminal.Frontier |}

            box
                {| Opening = opening
                   Parts = parts
                   LatestTerminal = latestTerminal |}

    let private sessionToJs (session: SessionAgentProjection) : obj =
        box
            {| Companion = companionToJs session.Companion
               XTrace = xTraceToJs session.XTrace
               Blog = blogToJs session.Blog
               PrefixEpoch =
                match session.PrefixEpoch with
                | None -> null
                | Some epoch ->
                    box
                        {| EpochId = PrefixEpochId.value epoch.EpochId
                           Snapshot = prefixSnapshotToJs epoch.Snapshot |}
               Handles = null
               Fallback = null
               PromptAuthority = null
               Enforcement = null
               BloggerCycles = null
               Relay = null
               Guidelines = null
               RequirementGrounding = null
               TipDelivery = null
               SessionStartedAt = null
               DelegatedToolEstimate = null |}

    let private projectionToJs (projection: ProjectionSet) : obj =
        projection.AgentProjections.Sessions
        |> Map.toList
        |> List.map (fun (sessionId, session) -> SessionId.value sessionId, sessionToJs session)
        |> createObj

    let private okState projection =
        box {| sessions = projectionToJs projection |}

    let private rejectionToJs (rejection: FoldRejection) : obj =
        box
            {| Fact = rejection.Fact
               Reason = rejection.Reason |}

    let private foldEnvelopes (envelopes: Envelope array) : obj =
        envelopes
        |> Array.fold
            (fun result envelope ->
                result
                |> Result.bind (fun current -> Wanxiangshu.Composition.Durable.Fold.foldEnvelope current envelope))
            (Ok Wanxiangshu.Composition.Durable.Fold.empty)
        |> function
            | Ok projection ->
                box
                    {| ok = true
                       value = okState projection |}
            | Error rejection ->
                box
                    {| ok = false
                       error = rejectionToJs rejection |}

    let fold (envelopes: obj array) : obj =
        envelopes |> Array.map envelopeOfJs |> foldEnvelopes

    /// Same fold, but each envelope crosses the canonical line codec first.
    let replay (envelopes: obj array) : obj =
        let decoded =
            envelopes
            |> Array.map (fun value ->
                let envelope = envelopeOfJs value

                match Envelope.deserialize (Envelope.serialize envelope) with
                | Ok roundTripped -> roundTripped
                | Error error -> failwith $"ContextFoldSurface: envelope round trip failed: {error}")

        foldEnvelopes decoded
