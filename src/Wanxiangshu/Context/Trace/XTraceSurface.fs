namespace Wanxiangshu.Context.Trace

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Companion
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Persistence.Journal

/// JS-native semantic owner for XTrace capture and durable projection proofs.
///
/// Message parts, fold envelopes and projection snapshots cross this boundary
/// as plain values. JournalHandle remains the only resource capability.
[<RequireQualifiedAccess>]
module XTraceSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private field (value: obj) (name: string) : obj =
        if isNullish value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(text value)

    let private stringList (value: obj) : string list =
        if isNullish value then
            []
        else
            unbox<obj array> value |> Array.toList |> List.map text

    let private intValue (value: obj) : int =
        if isNullish value then 0 else int (text value)

    let private int64Value (value: obj) : int64 =
        if isNullish value then 0L else int64 (text value)

    let private providerRunOf (value: obj) : ProviderRunIdentity option =
        optionalText value
        |> Option.filter (fun value -> not (String.IsNullOrWhiteSpace value))
        |> Option.map ProviderRunIdentity.create

    let private requiredProviderRunOf (value: obj) : ProviderRunIdentity =
        match providerRunOf value with
        | Some run -> run
        | None -> failwith "XTraceSurface: terminal capture requires providerRun"

    let private toolCallOf (value: obj) : ToolCallId option =
        optionalText value |> Option.map ToolCallId.create

    let private hostToolPartOf (value: obj) : HostToolPartId option =
        optionalText value |> Option.map HostToolPartId.create

    let private messagePartOf (value: obj) : MessagePart =
        match text (field value "kind") with
        | "text" -> MessagePart.Text(text (field value "text"))
        | "reasoning" -> MessagePart.Reasoning(text (field value "text"))
        | "tool-call"
        | "tool_call" ->
            MessagePart.ToolCall(text (field value "callId"), text (field value "name"), text (field value "args"))
        | "tool-result"
        | "tool_result" -> MessagePart.ToolResult(text (field value "callId"), text (field value "result"))
        | "activity" -> MessagePart.Activity(text (field value "activity"))
        | other -> failwith $"XTraceSurface: unknown capture part '{other}'"

    let private semanticPartView (part: SemanticPart) : obj =
        match part with
        | SemanticText value -> box {| kind = "text"; text = value |}
        | SemanticReasoning value -> box {| kind = "reasoning"; text = value |}
        | SemanticToolCall(name, args) ->
            box
                {| kind = "tool-call"
                   name = name
                   args = args |}
        | SemanticToolResult value ->
            box
                {| kind = "tool-result"
                   result = value |}
        | SemanticMedia(mediaType, digest) ->
            box
                {| kind = "media"
                   mediaType = mediaType |> Option.map box |> Option.toObj
                   digest = digest |}

    /// Plain capture-part constructors. Call identities stay in the input only
    /// long enough for the owner mapper to discard them from SemanticPart.
    let textPart (value: string) : obj = box {| kind = "text"; text = value |}

    let reasoningPart (value: string) : obj =
        box {| kind = "reasoning"; text = value |}

    let toolCallPart (callId: string) (name: string) (args: string) : obj =
        box
            {| kind = "tool-call"
               callId = callId
               name = name
               args = args |}

    let toolResultPart (callId: string) (result: string) : obj =
        box
            {| kind = "tool-result"
               callId = callId
               result = result |}

    let activityPart (kind: string) : obj =
        box {| kind = "activity"; activity = kind |}

    /// The sole MessagePart → SemanticPart mapper, represented as plain JS.
    let mapPart (value: obj) : obj option =
        match XTraceCapture.semanticPart (messagePartOf value) with
        | None -> None
        | Some mapped ->
            let tag =
                match mapped with
                | SemanticText _ -> "SemanticText"
                | SemanticReasoning _ -> "SemanticReasoning"
                | SemanticToolCall _ -> "SemanticToolCall"
                | SemanticToolResult _ -> "SemanticToolResult"
                | SemanticMedia _ -> "SemanticMedia"

            Some(
                box
                    {| kind = tag
                       part = semanticPartView mapped |}
            )

    let semantic (value: obj) : obj =
        let rawMessages = field value "messages"

        let messages =
            if isNullish rawMessages then
                [||]
            else
                unbox<obj array> rawMessages

        box {| messages = messages |}

    let private semanticProjectionOf (value: obj) : ProviderSemanticProjection =
        let messages = field value "messages"

        let semanticMessage (message: obj) : SemanticMessage =
            let parts = field message "parts"

            { Role = text (field message "role")
              Parts =
                if isNullish parts then
                    []
                else
                    unbox<obj array> parts
                    |> Array.toList
                    |> List.choose (fun part -> XTraceCapture.semanticPart (messagePartOf part)) }

        { ProviderId = None
          ModelId = None
          Variant = None
          Tools = []
          System = []
          Messages =
            if isNullish messages then
                []
            else
                unbox<obj array> messages |> Array.toList |> List.map semanticMessage }

    let private optionalStringView (value: 'a option) (render: 'a -> string) : obj =
        match value with
        | None -> null
        | Some item -> box (render item)

    let private openingView (opening: OpeningMaterial) : obj =
        box
            {| assignmentText = opening.AssignmentText
               authoritativeRequirements = List.toArray opening.AuthoritativeRequirements
               constitutiveBody = opening.ConstitutiveBody |}

    let private partView (part: XTracePartRef) : obj =
        box
            {| cursor = {| sequence = int part.Cursor.Sequence |}
               provenance = part.Provenance
               generation = part.Generation
               role = part.Role
               turn = part.Turn
               partIndex = part.PartIndex
               kind = part.Kind
               toolName = optionalStringView part.ToolName id
               providerRun = optionalStringView part.ProviderRun ProviderRunIdentity.value
               toolCallId = optionalStringView part.ToolCallId ToolCallId.value
               hostToolPartId = optionalStringView part.HostToolPartId HostToolPartId.value
               textRef = BlobRef.value part.TextRef
               textDigest = BlobDigest.value part.TextDigest |}

    let private projectionView (projection: XTraceProjectionState) : obj =
        box
            {| opening = projection.Opening |> Option.map openingView |> Option.toObj
               parts = projection |> XTraceProjection.parts |> List.map partView |> List.toArray
               terminal =
                projection.Terminal
                |> Option.map (fun (reference, digest) ->
                    box
                        {| textRef = BlobRef.value reference
                           textDigest = BlobDigest.value digest |})
                |> Option.toObj
               head = int (XTraceProjection.head projection) |}

    let private prefixEpochView (epoch: ActivePrefixEpoch) : obj =
        box
            {| epochId = int (PrefixEpochId.value epoch.EpochId)
               snapshot =
                epoch.Snapshot
                |> Option.map (fun snapshot ->
                    box
                        {| cutoffExclusive = snapshot.CutoffExclusive
                           coveredPrefixDigest = snapshot.CoveredPrefixDigest |})
                |> Option.toObj
               reanchoredRuns = epoch.ReanchoredRuns |> Set.toArray |> Array.map ProviderRunIdentity.value |}

    let private sessionView (session: SessionAgentProjection) : obj =
        box
            {| xTrace = session.XTrace |> Option.map projectionView |> Option.toObj
               prefixEpoch = session.PrefixEpoch |> Option.map prefixEpochView |> Option.toObj |}

    let private allSessionsView (projection: ProjectionSet) : obj =
        let sessions =
            projection.AgentProjections.Sessions
            |> Map.toList
            |> List.map (fun (sessionId, session) ->
                box
                    {| sessionId = SessionId.value sessionId
                       value = sessionView session |})
            |> List.toArray

        box {| sessions = sessions |}

    /// Oldest-first XTrace part references from a plain projection snapshot.
    let parts (projection: obj) : obj array =
        let value = field projection "parts"
        if isNullish value then [||] else unbox<obj array> value

    /// One-past-last cursor from a plain projection snapshot.
    let head (projection: obj) : int =
        if isNullish projection then
            0
        else
            intValue (field projection "head")

    /// Plain semantic-part constructors for cursor, flatten and rendering proofs.
    /// The capture constructors above retain source call ids; these constructors
    /// describe the already-owned semantic value and never expose a DU.
    let semanticText (value: string) : obj = box {| kind = "text"; text = value |}

    let semanticReasoning (value: string) : obj =
        box {| kind = "reasoning"; text = value |}

    let semanticToolCall (name: string) (args: string) : obj =
        box
            {| kind = "tool-call"
               name = name
               args = args |}

    let semanticToolResult (value: string) : obj =
        box
            {| kind = "tool-result"
               result = value |}

    let semanticMedia (mediaType: obj) (digest: string) : obj =
        box
            {| kind = "media"
               mediaType = mediaType
               digest = digest |}

    let private semanticPartOfPlain (value: obj) : SemanticPart =
        match text (field value "kind") with
        | "text"
        | "Text" -> SemanticText(text (field value "text"))
        | "reasoning"
        | "Reasoning" -> SemanticReasoning(text (field value "text"))
        | "tool-call"
        | "tool_call"
        | "ToolCall" -> SemanticToolCall(text (field value "name"), text (field value "args"))
        | "tool-result"
        | "tool_result"
        | "ToolResult" -> SemanticToolResult(text (field value "result"))
        | "media"
        | "Media" ->
            let mediaType = optionalText (field value "mediaType")
            let digestValue = field value "digest"

            let digest =
                if isNullish digestValue then
                    text (field value "contentDigest")
                else
                    text digestValue

            SemanticMedia(mediaType, digest)
        | other -> failwith $"XTraceSurface: unknown semantic part '{other}'"

    let private itemSequence (value: obj) : int64 =
        let direct = field value "sequence"

        if isNullish direct then
            int64Value (field (field value "cursor") "sequence")
        else
            int64Value direct

    let private semanticItemOfPlain (value: obj) : XTraceItem =
        { Cursor = { Sequence = itemSequence value }
          Provenance = text (field value "provenance")
          Role =
            let role = text (field value "role")
            if String.IsNullOrWhiteSpace role then "user" else role
          Part = semanticPartOfPlain (field value "part") }

    let private semanticItemView (item: XTraceItem) : obj =
        box
            {| cursor = {| sequence = int item.Cursor.Sequence |}
               provenance = item.Provenance
               role = item.Role
               part = semanticPartView item.Part |}

    /// Construct one JSON-shaped XTrace item from a cursor/role/semantic part.
    let item (value: obj) : obj =
        semanticItemOfPlain value |> semanticItemView

    let private itemList (values: obj array) : XTraceItem list =
        if isNullish values then
            []
        else
            values |> Array.toList |> List.map semanticItemOfPlain

    /// Explicitly normalize item descriptors before passing them to an owner.
    let toItems (values: obj array) : obj array =
        itemList values |> List.map semanticItemView |> List.toArray

    let private cursorOfPlain (value: obj) : XTraceCursor =
        { Sequence = int64Value (field value "sequence") }

    let private cursorView (cursor: XTraceCursor) : obj =
        box {| sequence = int cursor.Sequence |}

    let originCursor: obj = cursorView XTrace.originCursor

    let next (cursor: obj) : obj =
        cursorOfPlain cursor |> XTrace.nextCursor |> cursorView

    let isAfter (nextCursor: obj) (previous: obj) : bool =
        XTrace.isAfter (cursorOfPlain nextCursor) (cursorOfPlain previous)

    let sliceBetween (start: obj) (endExclusive: obj) (values: obj array) : obj array =
        XTrace.sliceBetween (cursorOfPlain start) (cursorOfPlain endExclusive) (itemList values)
        |> List.map semanticItemView
        |> List.toArray

    let sliceFrom (start: obj) (values: obj array) : obj array =
        XTrace.sliceFrom (cursorOfPlain start) (itemList values)
        |> List.map semanticItemView
        |> List.toArray

    let itemHead (values: obj array) : obj =
        itemList values |> XTrace.head |> cursorView

    let flatten (messages: obj array) : obj array =
        let semanticMessages =
            if isNullish messages then
                []
            else
                messages
                |> Array.toList
                |> List.map (fun message ->
                    { Role = text (field message "role")
                      Parts =
                        let parts = field message "parts"

                        if isNullish parts then
                            []
                        else
                            unbox<obj array> parts |> Array.toList |> List.map semanticPartOfPlain })

        XTrace.flatten semanticMessages
        |> List.map (fun entry ->
            box
                {| role = entry.Role
                   part = semanticPartView entry.Part |})
        |> List.toArray

    let render (values: obj array) : string = itemList values |> XTrace.render

    let renderItem (value: obj) : string =
        value |> semanticItemOfPlain |> XTrace.renderItem

    let forWorkRecord (values: obj array) : obj array =
        XTrace.forWorkRecord (itemList values)
        |> List.map semanticItemView
        |> List.toArray

    let forOpening (values: obj array) : obj array =
        XTrace.forOpening (itemList values) |> List.map semanticItemView |> List.toArray

    let isWorkRecordPart (value: obj) : bool =
        semanticPartOfPlain value |> XTrace.isWorkRecordPart


    let fact (caseName: string) (payload: obj) : obj =
        box
            {| caseName = caseName
               payload = payload |}

    let envelope (value: obj) : obj =
        box
            {| sequence = intValue (field value "seq")
               sessionId = text (field value "session")
               providerRun = optionalText (field value "run")
               fact = field value "fact" |}

    let private agentFactOf (value: obj) : AgentFact =
        let caseName = text (field value "caseName")
        let payload = field value "payload"
        let sessionId = SessionId.create (text (field payload "sessionId"))

        match caseName with
        | "CompanionBloggerLinked" ->
            CompanionFact.CompanionBloggerLinked
                {| SessionId = sessionId
                   BloggerSessionId = SessionId.create (text (field payload "bloggerSessionId"))
                   BloggerAgent = text (field payload "bloggerAgent") |}
        | "OpeningPromptCaptured" ->
            CompanionFact.OpeningPromptCaptured
                {| SessionId = sessionId
                   AssignmentText = text (field payload "assignmentText")
                   AuthoritativeRequirements = stringList (field payload "authoritativeRequirements")
                   ProviderRun = providerRunOf (field payload "providerRun") |}
        | "XTracePartAppended" ->
            CompanionFact.XTracePartAppended
                {| SessionId = sessionId
                   CursorSequence = int64Value (field payload "sequence")
                   Role = text (field payload "role")
                   Turn = intValue (field payload "turn")
                   PartIndex = intValue (field payload "partIndex")
                   Kind = text (field payload "kind")
                   ToolName = optionalText (field payload "toolName")
                   TextRef = BlobRef.create (text (field payload "textRef"))
                   TextDigest = BlobDigest.create (text (field payload "textDigest"))
                   Provenance = text (field payload "provenance")
                   ProviderRun = providerRunOf (field payload "providerRun")
                   ToolCallId = toolCallOf (field payload "toolCallId")
                   HostToolPartId = hostToolPartOf (field payload "hostToolPartId") |}
        | "TerminalOutputCaptured" ->
            CompanionFact.TerminalOutputCaptured
                {| SessionId = sessionId
                   TextRef = BlobRef.create (text (field payload "textRef"))
                   TextDigest = BlobDigest.create (text (field payload "textDigest"))
                   ProviderRun = requiredProviderRunOf (field payload "providerRun") |}
        | "ContextReanchored" ->
            ContextFact.ContextReanchored
                {| SessionId = sessionId
                   PreviousEpochId = PrefixEpochId.create (int64Value (field payload "previousEpochId"))
                   NextEpochId = PrefixEpochId.create (int64Value (field payload "nextEpochId"))
                   ObservedCompactionRun = requiredProviderRunOf (field payload "observedCompactionRun") |}
        | other -> failwith $"XTraceSurface: unsupported fold fact '{other}'"

    let private envelopeOf (value: obj) : Envelope =
        let sessionId = SessionId.create (text (field value "sessionId"))
        let providerRun = providerRunOf (field value "providerRun")
        let sequence = int64Value (field value "sequence")
        let factDescriptor = field value "fact"

        { RuntimeId = RuntimeId.create "rt-xtrace-surface"
          LocalSeq = LocalSeq.create sequence
          ObservedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
          EventId = EventId.create ($"xtrace-{sequence}")
          Stream = StreamId.Session sessionId
          ProviderRun = providerRun
          Fact = Fact.Agent(agentFactOf factDescriptor) }

    let private foldTyped (values: Envelope list) : Result<ProjectionSet, FoldRejection> =
        let rec loop current remaining =
            match remaining with
            | [] -> Ok current
            | envelope :: tail ->
                match Fold.foldEnvelope current envelope with
                | Ok next -> loop next tail
                | Error failure -> Error failure

        loop Fold.empty values

    let private foldEnvelopes (values: obj array) : Result<ProjectionSet, FoldRejection> =
        values |> Array.toList |> List.map envelopeOf |> foldTyped

    let private foldResult (result: Result<ProjectionSet, FoldRejection>) : obj =
        match result with
        | Ok projection ->
            box
                {| ok = true
                   value = allSessionsView projection |}
        | Error failure ->
            box
                {| ok = false
                   error =
                    box
                        {| Fact = failure.Fact
                           Reason = failure.Reason |} |}

    /// Apply an ordered sequence of plain envelope descriptors through the
    /// production durable fold.
    let fold (values: obj array) : obj = foldEnvelopes values |> foldResult

    /// Replay the same envelope descriptors through the production NDJSON codec
    /// before folding, proving that the persisted shape remains semantic.
    let replay (values: obj array) : obj =
        let decoded =
            values
            |> Array.toList
            |> List.map envelopeOf
            |> List.map (fun envelope -> Envelope.deserialize (Envelope.serialize envelope))

        match
            decoded
            |> List.tryFind (function
                | Error _ -> true
                | Ok _ -> false)
        with
        | Some(Error reason) ->
            box
                {| ok = false
                   error = box {| Fact = "Envelope"; Reason = reason |} |}
        | _ ->
            decoded
            |> List.choose (function
                | Ok envelope -> Some envelope
                | Error _ -> None)
            |> foldTyped
            |> foldResult

    /// Read one session from the plain projection returned by `fold`/`replay`.
    let session (projection: obj) (sessionId: string) : obj option =
        let values = field projection "sessions"

        if isNullish values then
            None
        else
            unbox<obj array> values
            |> Array.tryPick (fun entry ->
                if text (field entry "sessionId") = sessionId then
                    Some(field entry "value")
                else
                    None)

    let private appendResult (result: Result<'a, JournalAppendFailure>) : obj =
        match result with
        | Ok _ -> box {| ok = true |}
        | Error failure ->
            box
                {| ok = false
                   error = JournalAppendFailure.describe failure |}

    /// Capture a plain semantic projection through the opaque journal handle.
    let captureProjection (handle: JournalHandle) (sessionId: string) (projection: obj) : Task<obj option> =
        task {
            let! result =
                XTraceCapture.captureProjection
                    (Some handle.Journal)
                    (SessionId.create sessionId)
                    (semanticProjectionOf projection)

            return result |> Option.map projectionView
        }

    let captureOpening
        (handle: JournalHandle)
        (sessionId: string)
        (assignmentText: string)
        (authoritativeRequirements: string array)
        : Task<unit> =
        XTraceCapture.captureOpening
            (Some handle.Journal)
            (SessionId.create sessionId)
            assignmentText
            (if isNull authoritativeRequirements then
                 []
             else
                 Array.toList authoritativeRequirements)

    let captureTerminalText
        (handle: JournalHandle)
        (sessionId: string)
        (value: string)
        (providerRun: string)
        : Task<unit> =
        XTraceCapture.captureTerminalText
            (Some handle.Journal)
            (SessionId.create sessionId)
            value
            (ProviderRunIdentity.create providerRun)

    let captureLastWords
        (handle: JournalHandle)
        (sessionId: string)
        (textRef: string)
        (textDigest: string)
        (providerRun: string)
        : Task<unit> =
        XTraceCapture.captureLastWords
            (Some handle.Journal)
            (SessionId.create sessionId)
            (BlobRef.create textRef)
            (BlobDigest.create textDigest)
            (ProviderRunIdentity.create providerRun)

    /// Append the Host reanchor fact used by the capture-generation contract.
    /// The fact remains owned and folded by the production Context boundary.
    let appendReanchor
        (handle: JournalHandle)
        (sessionId: string)
        (previousEpoch: int)
        (nextEpoch: int)
        (observedCompactionRun: string)
        : Task<obj> =
        task {
            let! result =
                AgentJournal.appendAgent
                    (StreamId.Session(SessionId.create sessionId))
                    (Some(ProviderRunIdentity.create observedCompactionRun))
                    (ContextFact.ContextReanchored
                        {| SessionId = SessionId.create sessionId
                           PreviousEpochId = PrefixEpochId.create (int64 previousEpoch)
                           NextEpochId = PrefixEpochId.create (int64 nextEpoch)
                           ObservedCompactionRun = ProviderRunIdentity.create observedCompactionRun |})
                    handle.Journal

            return appendResult result
        }

    /// Journal-backed lifecycle work record projection used by capture proofs.
    let lifecycleWorkRecord (handle: JournalHandle) (sessionId: string) (includeOpening: bool) : Task<string option> =
        LifecycleWorkRecordProjection.lifecycleWorkRecord
            (Some handle.Journal)
            (SessionId.create sessionId)
            includeOpening

    /// Provider-run/Host-tool locality over a plain snapshot and envelope list.
    /// The raw Host message decoder, durable fold and locality resolver remain
    /// production-owned; only a normalized result crosses this boundary.
    let private stateView state : obj =
        match state with
        | SnapshotToolPartState.Pending -> box {| status = "pending" |}
        | SnapshotToolPartState.Completed output ->
            box
                {| status = "completed"
                   outputCanonical = output |}
        | SnapshotToolPartState.Failed error ->
            box
                {| status = "failed"
                   errorCanonical = error |}

    let private localizedView (value: MagicTodoLocality.LocalizedToolCall) : obj =
        box
            {| providerRun = ProviderRunIdentity.value value.ProviderRun
               hostToolPartId = HostToolPartId.value value.HostToolPartId
               toolCallId = ToolCallId.value value.ToolCallId
               toolName = value.ToolName
               inputCanonical = value.InputCanonical
               state = stateView value.State
               todowriteCallIdsInMessage = value.TodowriteCallIdsInMessage |> List.map ToolCallId.value |> List.toArray
               toolPartOrdinal = value.ToolPartOrdinal
               reviewFrontier = cursorView value.ReviewFrontier
               range =
                box
                    {| start = cursorView value.Range.Start
                       endExclusive = cursorView value.Range.EndExclusive |} |}

    let private localityErrorCode error =
        match error with
        | MagicTodoLocality.LocalityRejection.Snapshot _ -> "Snapshot"
        | MagicTodoLocality.LocalityRejection.XTraceUnavailable -> "XTraceUnavailable"
        | MagicTodoLocality.LocalityRejection.XTraceMissing _ -> "XTraceMissing"
        | MagicTodoLocality.LocalityRejection.XTraceAmbiguous _ -> "XTraceAmbiguous"

    let resolveLocality (sessionId: string) (messages: obj array) (envelopes: obj array) (toolCallId: string) : obj =
        match foldEnvelopes (if isNullish envelopes then [||] else envelopes) with
        | Error failure ->
            box
                {| ok = false
                   error =
                    box
                        {| code = "Fold"
                           fact = failure.Fact
                           reason = failure.Reason |} |}
        | Ok projection ->
            let projectedMessages =
                SessionSnapshotPort.projectMessages (if isNullish messages then [||] else messages)

            match
                MagicTodoLocality.resolve
                    (SessionId.create sessionId)
                    projectedMessages
                    projection
                    (ToolCallId.create toolCallId)
            with
            | Ok localized ->
                box
                    {| ok = true
                       value = localizedView localized |}
            | Error error ->
                box
                    {| ok = false
                       error = box {| code = localityErrorCode error |} |}

    /// Capture a decoded Host message view while retaining provider/tool identity.
    let captureMessageView (handle: JournalHandle) (sessionId: string) (messages: obj array) : Task<obj option> =
        task {
            let captured =
                ProviderWireCapture.decodeCapturedMessageView (if isNullish messages then [] else Array.toList messages)

            let! result = XTraceCapture.captureMessageView (Some handle.Journal) (SessionId.create sessionId) captured
            return result |> Option.map projectionView
        }

    let captureSessionMessages (handle: JournalHandle) (sessionId: string) (messages: obj array) : Task<obj> =
        task {
            let sessionIdentity = SessionId.create sessionId

            let projected =
                SessionSnapshotPort.projectMessages (if isNullish messages then [||] else messages)

            match! XTraceCapture.captureSessionMessages (Some handle.Journal) sessionIdentity projected with
            | Error error -> return box {| ok = false; error = error |}
            | Ok() ->
                let projection = AgentJournal.snapshot handle.Journal

                let trace =
                    projection.AgentProjections.Sessions
                    |> Map.tryFind sessionIdentity
                    |> Option.bind (fun session -> session.XTrace)
                    |> Option.defaultValue XTraceProjection.empty

                return
                    box
                        {| ok = true
                           value = projectionView trace |}
        }

    let lifecycleWorkRecordBounded
        (handle: JournalHandle)
        (sessionId: string)
        (startInclusive: int)
        (endExclusive: int)
        : Task<string option> =
        LifecycleWorkRecordProjection.lifecycleWorkRecordBounded
            (Some handle.Journal)
            (SessionId.create sessionId)
            { StartInclusive = { Sequence = int64 startInclusive }
              EndExclusive = { Sequence = int64 endExclusive } }
