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
/// facts, typed identities and BloggerToolRecovery stay private.
[<RequireQualifiedAccess>]
module BlogSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private arrayOf (value: obj) : obj array =
        if isNullish value then [||] else unbox<obj array> value

    let private optionText (value: obj) : string option =
        if isNullish value then None else Some(text value)

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
                           text = "unknown-tip"
                           error = "missing required argument: tip" |}
                elif
                    EnforcerCatalog.tryFindByField tip (EnforcerCatalogResource.load ())
                    |> Option.isNone
                then
                    box
                        {| ok = true
                           text = "unknown-tip"
                           error = sprintf "UnknownTip %s" tip |}
                else
                    box
                        {| ok = true
                           text = "remembered"
                           error = null |}

    let tipFieldNames () =
        EnforcerCatalog.fieldNames (EnforcerCatalogResource.load ()) |> List.toArray

    /// Rejudge transcript evidence. One completed chronicle only proves
    /// recovery; any other terminal is still the nudge stage.
    let rejudgeFromEvidence (claimedRun: obj) (terminals: obj array) : obj =
        let claimed = optionText claimedRun

        let evidence =
            terminals
            |> Array.toList
            |> List.map (fun item -> text item?id, not (isNullish item?hasChronicle) && unbox<bool> item?hasChronicle)

        BloggerRecoveryProbe.rejudgeFromEvidence claimed evidence
        |> function
            | BloggerToolRecovery.NoRecovery -> box {| state = "NoRecovery"; run = null |}
            | BloggerToolRecovery.InteractionNudgeIssued run ->
                box
                    {| state = "InteractionNudgeIssued"
                       run = ProviderRunIdentity.value run |}
            | BloggerToolRecovery.AabbRepairIssued run ->
                box
                    {| state = "AabbRepairIssued"
                       run = ProviderRunIdentity.value run |}

    /// Rejudge named chronicle tool-part evidence from a compact semantic
    /// transcript. `chronicleCount` counts raw named calls, while
    /// `completedChronicleCount` proves exactly-one completion.
    let rejudgeChronicleEvidence (claimedRun: obj) (terminals: obj array) : obj =
        let normalized =
            terminals
            |> Array.map (fun item ->
                box
                    {| id = text item?id
                       hasChronicle =
                        let count = int (text item?chronicleCount)
                        let completed = int (text item?completedChronicleCount)
                        count = 1 && completed = 1 |})

        rejudgeFromEvidence claimedRun normalized

    /// Compact request-scoped recovery evidence. A claim is active only when
    /// its request matches and it was not abandoned; an older request cannot
    /// consume a new request's repair budget.
    let repairState (value: obj) : obj =
        let request = text value?requestId
        let claims = arrayOf value?claims

        let activeClaim kind =
            claims
            |> Array.tryFind (fun claim ->
                text claim?requestId = request
                && text claim?kind = kind
                && text claim?run <> ""
                && text claim?status <> "Abandoned")

        match activeClaim "blogger-aabb" with
        | Some claim ->
            box
                {| state = "AabbRepairIssued"
                   run = text claim?run |}
        | None ->
            match activeClaim "blogger-missing-tool" with
            | Some claim ->
                box
                    {| state = "InteractionNudgeIssued"
                       run = text claim?run |}
            | None -> box {| state = "NoRecovery"; run = null |}

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
                Some(text value?physicalDelta?id, text value?physicalDelta?toml)

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

    /// Bounded repair transition. A pure terminal first receives one nudge;
    /// subsequent different invalid terminals stay in AABB until the shared
    /// provider fallback budget is actually exhausted.
    let repairProtocol (value: obj) : obj =
        let prior = text value?priorState
        let terminal = text value?terminalRun

        let nudgeSucceeded =
            not (isNullish value?nudgeSucceeded) && unbox<bool> value?nudgeSucceeded

        let sameTerminal = text value?repairTerminalRun = terminal

        let fallbackExhausted =
            not (isNullish value?fallbackExhausted) && unbox<bool> value?fallbackExhausted

        match prior with
        | "NoRecovery" ->
            if nudgeSucceeded then
                box
                    {| state = "InteractionNudgeIssued"
                       run = terminal |}
            else
                box
                    {| state = "AabbRepairIssued"
                       run = terminal |}
        | "InteractionNudgeIssued" ->
            if sameTerminal then
                box
                    {| state = "InteractionNudgeIssued"
                       run = terminal |}
            else
                box
                    {| state = "AabbRepairIssued"
                       run = terminal |}
        | "AabbRepairIssued" ->
            if sameTerminal then
                box
                    {| state = "AabbRepairIssued"
                       run = terminal |}
            elif fallbackExhausted then
                box
                    {| state = "ProtocolExhausted"
                       run = null |}
            else
                box
                    {| state = "AabbRepairIssued"
                       run = terminal |}
        | _ -> box {| state = "NoRecovery"; run = null |}
