namespace Wanxiangshu.Participant.Provider.Projection

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Strength

/// JS-native semantic boundary for provider projection algebra.
///
/// ProjectionIntent, ProjectionSnapshot, ProviderWireProjection and the
/// Strength payloads remain F# values behind this module. Tests and Host
/// adapters cross the boundary with plain objects, arrays, strings and
/// numbers; no Fable union or collection representation is exposed.
module ProjectionSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private arrayOf (value: obj) : obj array =
        if isNullish value then [||] else unbox<obj array> value

    let private stringOf (value: obj) : string =
        if isNullish value then "" else string value

    let private intOf (value: obj) : int =
        if isNullish value then 0 else int (string value)

    let private optionalString (value: obj) : string option =
        if isNullish value then None else Some(string value)

    let private int64Of (value: obj) : int64 =
        if isNullish value then 0L else int64 (string value)

    let private boolOf (value: obj) : bool =
        if isNullish value then false else unbox<bool> value

    let private optionObj (value: string option) : obj =
        match value with
        | None -> null
        | Some text -> box text

    let private wirePartOf (value: obj) : ProviderProjection.WirePart =
        match stringOf value?kind with
        | "text"
        | "Text" -> ProviderProjection.WireText(stringOf value?text)
        | "reasoning"
        | "Reasoning" -> ProviderProjection.WireReasoning(stringOf value?text)
        | "tool-call"
        | "ToolCall" ->
            let arguments =
                if isNullish value?args then
                    stringOf value?text
                else
                    stringOf value?args

            let callId = ToolCallId.create (stringOf value?callId)
            let name = stringOf value?name
            ProviderProjection.WireToolCall(callId, name, arguments)
        | "tool-result"
        | "ToolResult" ->
            let result =
                if isNullish value?result then
                    stringOf value?text
                else
                    stringOf value?result

            let callId = ToolCallId.create (stringOf value?callId)
            ProviderProjection.WireToolResult(callId, result)
        | "media"
        | "Media" -> ProviderProjection.WireMedia(optionalString value?mediaType, stringOf value?contentDigest)
        | other -> failwithf "ProjectionSurface: unknown wire part kind %s" other

    let private wirePartToJs (part: ProviderProjection.WirePart) : obj =
        match part with
        | ProviderProjection.WireText text -> box {| kind = "text"; text = text |}
        | ProviderProjection.WireReasoning text -> box {| kind = "reasoning"; text = text |}
        | ProviderProjection.WireToolCall(callId, name, args) ->
            box
                {| kind = "tool-call"
                   callId = ToolCallId.value callId
                   name = name
                   args = args |}
        | ProviderProjection.WireToolResult(callId, result) ->
            box
                {| kind = "tool-result"
                   callId = ToolCallId.value callId
                   result = result |}
        | ProviderProjection.WireMedia(mediaType, digest) ->
            box
                {| kind = "media"
                   mediaType = optionObj mediaType
                   contentDigest = digest |}

    let private wireMessageOf (value: obj) : ProviderProjection.WireMessage =
        { Role = stringOf value?role
          Parts = arrayOf value?parts |> Array.toList |> List.map wirePartOf }

    let private wireMessageToJs (message: ProviderProjection.WireMessage) : obj =
        box
            {| role = message.Role
               parts = message.Parts |> List.map wirePartToJs |> List.toArray |}

    let private wireProjectionOf (value: obj) : ProviderProjection.ProviderWireProjection =
        { ProviderId = optionalString value?providerId
          ModelId = optionalString value?modelId
          Variant = optionalString value?variant
          Tools = arrayOf value?tools |> Array.map stringOf |> Array.toList
          System = arrayOf value?system |> Array.map stringOf |> Array.toList
          Messages = arrayOf value?messages |> Array.toList |> List.map wireMessageOf }

    let private wireProjectionToJs (projection: ProviderProjection.ProviderWireProjection) : obj =
        box
            {| providerId = optionObj projection.ProviderId
               modelId = optionObj projection.ModelId
               variant = optionObj projection.Variant
               tools = projection.Tools |> List.toArray
               system = projection.System |> List.toArray
               messages = projection.Messages |> List.map wireMessageToJs |> List.toArray |}

    let private semanticPartToJs (part: ProviderProjection.SemanticPart) : obj =
        match part with
        | ProviderProjection.SemanticText text -> box {| kind = "text"; text = text |}
        | ProviderProjection.SemanticReasoning text -> box {| kind = "reasoning"; text = text |}
        | ProviderProjection.SemanticToolCall(name, args) ->
            box
                {| kind = "tool-call"
                   name = name
                   args = args |}
        | ProviderProjection.SemanticToolResult result ->
            box
                {| kind = "tool-result"
                   result = result |}
        | ProviderProjection.SemanticMedia(mediaType, digest) ->
            box
                {| kind = "media"
                   mediaType = optionObj mediaType
                   contentDigest = digest |}

    let private semanticMessageToJs (message: ProviderProjection.SemanticMessage) : obj =
        box
            {| role = message.Role
               parts = message.Parts |> List.map semanticPartToJs |> List.toArray |}

    let private semanticProjectionToJs (projection: ProviderProjection.ProviderSemanticProjection) : obj =
        box
            {| providerId = optionObj projection.ProviderId
               modelId = optionObj projection.ModelId
               variant = optionObj projection.Variant
               tools = projection.Tools |> List.toArray
               system = projection.System |> List.toArray
               messages = projection.Messages |> List.map semanticMessageToJs |> List.toArray |}

    let private semanticPartOf (value: obj) : ProviderProjection.SemanticPart =
        match stringOf value?kind with
        | "text"
        | "Text" -> ProviderProjection.SemanticText(stringOf value?text)
        | "reasoning"
        | "Reasoning" -> ProviderProjection.SemanticReasoning(stringOf value?text)
        | "tool-call"
        | "ToolCall" -> ProviderProjection.SemanticToolCall(stringOf value?name, stringOf value?args)
        | "tool-result"
        | "ToolResult" -> ProviderProjection.SemanticToolResult(stringOf value?result)
        | "media"
        | "Media" -> ProviderProjection.SemanticMedia(optionalString value?mediaType, stringOf value?contentDigest)
        | other -> failwithf "ProjectionSurface: unknown semantic part kind %s" other

    let private semanticProjectionOf (value: obj) : ProviderProjection.ProviderSemanticProjection =
        { ProviderId = optionalString value?providerId
          ModelId = optionalString value?modelId
          Variant = optionalString value?variant
          Tools = arrayOf value?tools |> Array.map stringOf |> Array.toList
          System = arrayOf value?system |> Array.map stringOf |> Array.toList
          Messages =
            arrayOf value?messages
            |> Array.toList
            |> List.map (fun message ->
                { Role = stringOf message?role
                  Parts = arrayOf message?parts |> Array.toList |> List.map semanticPartOf }) }

    let private prefixSnapshotOf (value: obj) : PrefixSnapshot =
        { FrozenRecordPrefixRef = BlobRef.create (stringOf value?frozenRecordPrefixRef)
          FrozenRecordPrefixDigest = BlobDigest.create (stringOf value?frozenRecordPrefixDigest)
          CutoffExclusive = intOf value?cutoffExclusive
          CoveredPrefixDigest = stringOf value?coveredPrefixDigest
          SealRoot = stringOf value?sealRoot
          SyntheticMessageId = stringOf value?syntheticMessageId }

    let private prefixSnapshotToJs (value: PrefixSnapshot) : obj =
        box
            {| frozenRecordPrefixRef = BlobRef.value value.FrozenRecordPrefixRef
               frozenRecordPrefixDigest = BlobDigest.value value.FrozenRecordPrefixDigest
               cutoffExclusive = value.CutoffExclusive
               coveredPrefixDigest = value.CoveredPrefixDigest
               sealRoot = value.SealRoot
               syntheticMessageId = value.SyntheticMessageId |}

    let private blogFrameOf (value: obj) : ResolvedBlogFrame =
        { Kind =
            match stringOf value?kind with
            | "Squash" -> ProjectionBlogFrameKind.Squash
            | _ -> ProjectionBlogFrameKind.Entry
          Digest = stringOf value?digest
          Body = stringOf value?body }

    let private blogFrameToJs (value: ResolvedBlogFrame) : obj =
        box
            {| kind =
                match value.Kind with
                | ProjectionBlogFrameKind.Entry -> "Entry"
                | ProjectionBlogFrameKind.Squash -> "Squash"
               digest = value.Digest
               body = value.Body |}

    let blogFrame (value: obj) : obj = value |> blogFrameOf |> blogFrameToJs

    let private pairOf (value: obj) : string * string =
        if emitJsExpr value "Array.isArray($0)" then
            let fields = arrayOf value
            (stringOf fields[0], stringOf fields[1])
        elif not (isNullish value?field) || not (isNullish value?cycleId) then
            stringOf value?field, stringOf value?cycleId
        else
            stringOf value?messageId, stringOf value?toml

    let private physicalDeltaOf (value: obj) =
        if isNullish value then
            None
        else
            match BloggerDeltaItemWire.tryListOfJs value?items with
            | Ok items -> Some(stringOf value?messageId, items)
            | Error error -> invalidArg "physicalDelta.items" error

    let private blogFramesIntentOf (value: obj) : BlogFramesIntent =
        { RequestKind = stringOf value?requestKind
          SquashFrameCount = intOf value?squashFrameCount
          BloggerSessionId = stringOf value?bloggerSessionId
          FrameEpoch = int64Of value?frameEpoch
          PhysicalDelta = physicalDeltaOf value?physicalDelta
          PreviousTips = arrayOf value?previousTips |> Array.toList |> List.map pairOf
          NormalInstructionLines = arrayOf value?normalInstructionLines |> Array.map stringOf |> Array.toList
          SquashInstructionLines = arrayOf value?squashInstructionLines |> Array.map stringOf |> Array.toList }

    let private activationOf (value: obj) : PrefixActivation =
        { SyntheticMessageId = stringOf value?syntheticMessageId
          Memory = stringOf value?memory
          CutoffExclusive = intOf value?dropLeading }

    let private strengthExchangeOf (value: obj) : StrengthToolExchange =
        { ToolName = stringOf value?toolName
          CanonicalArguments = stringOf value?canonicalArguments
          CanonicalResult = stringOf value?canonicalResult }

    let private strengthBundleOf (value: obj) : StrengthFrameBundle =
        let batches =
            arrayOf value?batches
            |> Array.toList
            |> List.map (fun batch ->
                { RequestOrdinal = intOf batch?requestOrdinal
                  Exchanges = arrayOf batch?exchanges |> Array.toList |> List.map strengthExchangeOf })

        { Batches = batches
          Digest = stringOf value?digest
          ByteLength = intOf value?byteLength }

    let private strengthIntentOf (value: obj) : ProjectionIntent =
        let ownerSession = SessionId.create (stringOf value?ownerSessionId)
        let decisionId = StrengthDecisionId.create (stringOf value?decisionId)

        match stringOf value?kind with
        | "strength-mirror" ->
            ProjectionIntent.useStrengthMirror
                decisionId
                (ProviderRunIdentity.create (stringOf value?targetProviderRun))
                (stringOf value?semanticDigest)
                (arrayOf value?messages |> Array.toList |> List.map wireMessageOf)
        | "strength-candidate" ->
            let bundle = strengthBundleOf value?bundle

            ProjectionIntent.strengthCandidate
                ownerSession
                decisionId
                (ProviderRunIdentity.create (stringOf value?targetProviderRun))
                (ProviderRunIdentity.create (stringOf value?currentProviderRun))
                bundle
        | "strength-promoted" ->
            let bundle = strengthBundleOf value?bundle

            ProjectionIntent.strengthPromoted
                ownerSession
                decisionId
                (ProviderRunIdentity.create (stringOf value?targetProviderRun))
                (intOf value?beforeIndex)
                (boolOf value?isReplicaRequest)
                bundle
        | "strength-replica-local" ->
            let bundle = strengthBundleOf value?bundle
            ProjectionIntent.strengthReplicaLocal ownerSession decisionId bundle
        | other -> failwithf "ProjectionSurface: unknown Strength intent kind %s" other

    let private intentOf (value: obj) : ProjectionIntent =
        match stringOf value?kind with
        | "KeepPhysicalPrefix" -> ProjectionIntent.KeepPhysicalPrefix
        | "ActivatePrefixEpoch" -> ProjectionIntent.ActivatePrefixEpoch(activationOf value?activation)
        | "InsertBlogFrames" -> ProjectionIntent.InsertBlogFrames(blogFramesIntentOf value?payload)
        | "InsertRepair" -> ProjectionIntent.InsertRepair { RequestKey = stringOf value?requestKey }
        | "UseStrengthMirror"
        | "strength-mirror"
        | "strength-candidate"
        | "strength-promoted"
        | "strength-replica-local" -> strengthIntentOf value
        | "InsertStrengthFrames" -> strengthIntentOf value?payload
        | "SuppressTransportOnly" -> ProjectionIntent.SuppressTransportOnly
        | "ReanchorAfterCompaction" -> ProjectionIntent.ReanchorAfterCompaction
        | other -> failwithf "ProjectionSurface: unknown intent kind %s" other

    let private intentKind (intent: ProjectionIntent) : string =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix -> "KeepPhysicalPrefix"
        | ProjectionIntent.ActivatePrefixEpoch _ -> "ActivatePrefixEpoch"
        | ProjectionIntent.InsertBlogFrames _ -> "InsertBlogFrames"
        | ProjectionIntent.InsertRepair _ -> "InsertRepair"
        | ProjectionIntent.UseStrengthMirror _ -> "UseStrengthMirror"
        | ProjectionIntent.InsertStrengthFrames _ -> "InsertStrengthFrames"
        | ProjectionIntent.SuppressTransportOnly -> "SuppressTransportOnly"
        | ProjectionIntent.ReanchorAfterCompaction -> "ReanchorAfterCompaction"

    let private activationToJs (activation: PrefixActivation) : obj =
        box
            {| syntheticMessageId = activation.SyntheticMessageId
               memory = activation.Memory
               dropLeading = activation.CutoffExclusive |}

    let private intentToJs (intent: ProjectionIntent) : obj =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix -> box {| kind = "KeepPhysicalPrefix" |}
        | ProjectionIntent.ActivatePrefixEpoch activation ->
            box
                {| kind = "ActivatePrefixEpoch"
                   activation = activationToJs activation |}
        | ProjectionIntent.InsertBlogFrames _ -> box {| kind = "InsertBlogFrames" |}
        | ProjectionIntent.InsertRepair _ -> box {| kind = "InsertRepair" |}
        | ProjectionIntent.UseStrengthMirror _ -> box {| kind = "UseStrengthMirror" |}
        | ProjectionIntent.InsertStrengthFrames _ -> box {| kind = "InsertStrengthFrames" |}
        | ProjectionIntent.SuppressTransportOnly -> box {| kind = "SuppressTransportOnly" |}
        | ProjectionIntent.ReanchorAfterCompaction -> box {| kind = "ReanchorAfterCompaction" |}

    let private conflictName (conflict: ProjectionConflict) : string =
        match conflict with
        | ProjectionConflict.ConflictingPrefixSelection _ -> "ConflictingPrefixSelection"
        | ProjectionConflict.ConflictingBlogFrames -> "ConflictingBlogFrames"
        | ProjectionConflict.ConflictingRepair -> "ConflictingRepair"
        | ProjectionConflict.ConflictingPrefixLifecycle -> "ConflictingPrefixLifecycle"
        | ProjectionConflict.ConflictingStrengthFrames _ -> "ConflictingStrengthFrames"
        | ProjectionConflict.StrengthCandidateWrongTarget _ -> "StrengthCandidateWrongTarget"
        | ProjectionConflict.StrengthPromotedReplicaReflection _ -> "StrengthPromotedReplicaReflection"
        | ProjectionConflict.InvalidStrengthAnchor _ -> "InvalidStrengthAnchor"
        | ProjectionConflict.StrengthFrameDigestMismatch _ -> "StrengthFrameDigestMismatch"

    let private snapshotOfJs (value: obj) : ProjectionSnapshot =
        { CurrentProjection = semanticProjectionOf value?currentProjection
          CommittedPrefix =
            if isNullish value?committedPrefix then
                None
            else
                Some(prefixSnapshotOf value?committedPrefix)
          BlogFrames = arrayOf value?blogFrames |> Array.toList |> List.map blogFrameOf
          TransportMessages = arrayOf value?transportMessages |> Array.map stringOf |> Set.ofArray
          HostReanchor =
            if isNullish value?hostReanchor then
                None
            else
                Some
                    { PreviousEpochId = stringOf value?hostReanchor?previousEpochId
                      NextEpochId = stringOf value?hostReanchor?nextEpochId
                      ObservedCompactionRunId = stringOf value?hostReanchor?observedCompactionRunId } }

    let private snapshotToJs (snapshot: ProjectionSnapshot) : obj =
        box
            {| currentProjection = semanticProjectionToJs snapshot.CurrentProjection
               committedPrefix =
                snapshot.CommittedPrefix
                |> Option.map prefixSnapshotToJs
                |> Option.defaultValue null
               blogFrames = snapshot.BlogFrames |> List.map blogFrameToJs |> List.toArray
               transportMessages = snapshot.TransportMessages |> Set.toArray
               hostReanchor =
                snapshot.HostReanchor
                |> Option.map (fun value ->
                    box
                        {| previousEpochId = value.PreviousEpochId
                           nextEpochId = value.NextEpochId
                           observedCompactionRunId = value.ObservedCompactionRunId |})
                |> Option.defaultValue null |}

    let private renderedPrefixOf (value: obj) : RenderedPrefix =
        match stringOf value?name with
        | "PhysicalPrefix" -> RenderedPrefix.PhysicalPrefix
        | "SyntheticPrefix" -> RenderedPrefix.SyntheticPrefix(activationOf value?activation)
        | other -> failwithf "ProjectionSurface: unknown rendered prefix %s" other

    let private renderedPrefixToJs (value: RenderedPrefix) : obj =
        match value with
        | RenderedPrefix.PhysicalPrefix ->
            box
                {| name = "PhysicalPrefix"
                   activation = null |}
        | RenderedPrefix.SyntheticPrefix activation ->
            box
                {| name = "SyntheticPrefix"
                   activation = activationToJs activation |}

    /// Constructor for the physical-prefix intent.
    let keepPhysicalPrefix: obj = box {| kind = "KeepPhysicalPrefix" |}

    /// Constructor for the synthetic-prefix intent.
    let activatePrefixEpoch (activation: obj) : obj =
        box
            {| kind = "ActivatePrefixEpoch"
               activation = activation |}

    let insertBlogFrames (payload: obj) : obj =
        box
            {| kind = "InsertBlogFrames"
               payload = payload |}

    let insertRepair (requestKey: string) : obj =
        box
            {| kind = "InsertRepair"
               requestKey = requestKey |}

    [<Emit("Object.assign({}, $0, { kind: $1 })")>]
    let private withKind (payload: obj) (kind: string) : obj = jsNative

    let useStrengthMirror (payload: obj) : obj = withKind payload "strength-mirror"

    let strengthCandidate (payload: obj) : obj = withKind payload "strength-candidate"

    let strengthPromoted (payload: obj) : obj = withKind payload "strength-promoted"

    let strengthReplicaLocal (payload: obj) : obj =
        withKind payload "strength-replica-local"

    let suppressTransportOnly: obj = box {| kind = "SuppressTransportOnly" |}

    let reanchorAfterCompaction: obj = box {| kind = "ReanchorAfterCompaction" |}

    /// Construct and normalize an attempt-local projection snapshot.
    let projectionSnapshot (currentProjection: obj) (options: obj) : obj =
        let value =
            if isNullish options then
                box
                    {| currentProjection = currentProjection
                       committedPrefix = null
                       blogFrames = [||]
                       transportMessages = [||]
                       hostReanchor = null |}
            else
                box
                    {| currentProjection = currentProjection
                       committedPrefix = options?committedPrefix
                       blogFrames = options?blogFrames
                       transportMessages = options?transportMessages
                       hostReanchor = options?hostReanchor |}

        snapshotOfJs value |> snapshotToJs

    /// Decode raw Host messages through the production codec into wire data.
    let decodeMessages (rawMessages: obj array) : obj =
        ProviderWireCapture.decodeMessageView (Array.toList rawMessages)
        |> wireProjectionToJs

    /// Apply a rendered prefix through the production write-back adapter.
    let applyRenderedPrefix (rawMessages: obj array) (rendered: obj) : obj array =
        ProjectionMessageEdit.applyRenderedPrefix (Array.toList rawMessages) (renderedPrefixOf rendered)
        |> List.toArray

    /// Resolve a committed prefix snapshot into the production projection intent.
    let prefixForSnapshot (snapshot: obj) (memoryPreamble: string) (body: string) : obj =
        let committed =
            if isNullish snapshot?committedPrefix then
                None
            else
                Some(prefixSnapshotOf snapshot?committedPrefix)

        XPrefixProjection.forSnapshot committed memoryPreamble body |> intentToJs

    /// Plan a JSON intent array with canonical rank and explicit conflict data.
    let plan (intents: obj array) : obj =
        match ProjectionPlanner.plan (Array.toList intents |> List.map intentOf) with
        | Ok ordered ->
            box
                {| ok = true
                   intents = ordered |> List.map intentKind |> List.toArray |}
        | Error conflict ->
            match conflict with
            | ProjectionConflict.ConflictingPrefixSelection(first, second) ->
                box
                    {| ok = false
                       conflict = conflictName conflict
                       first = intentKind first
                       second = intentKind second |}
            | _ ->
                box
                    {| ok = false
                       conflict = conflictName conflict |}

    /// Render a prefix intent set to a write-back instruction.
    let renderPrefix (intents: obj array) : obj =
        ProjectionRenderer.renderPrefix (Array.toList intents |> List.map intentOf)
        |> renderedPrefixToJs

    /// Render wire messages with the canonical projection renderer.
    let renderMessages (snapshot: obj) (baseMessages: obj array) (intents: obj array) : obj array =
        ProjectionRenderer.renderMessagesWithIntents
            (snapshotOfJs snapshot)
            (Array.toList baseMessages |> List.map wireMessageOf)
            (Array.toList intents |> List.map intentOf)
        |> List.map wireMessageToJs
        |> List.toArray

    /// Render wire messages and Host side-channel ids with a caller-supplied hash.
    let renderMessagesWithHostIds
        (sha256: string -> string)
        (snapshot: obj)
        (baseMessages: obj array)
        (intents: obj array)
        : obj =
        let rendered =
            ProjectionRenderer.renderMessagesWithHostIds
                sha256
                (snapshotOfJs snapshot)
                (Array.toList baseMessages |> List.map wireMessageOf)
                (Array.toList intents |> List.map intentOf)

        box
            {| messages = rendered.Messages |> List.map wireMessageToJs |> List.toArray
               hostMessageIds = rendered.HostMessageIds |> List.map optionObj |> List.toArray
               hostIsPhysical = rendered.HostIsPhysical |> List.toArray |}

    let renderWire (messages: obj array) : string =
        let projection: ProviderProjection.ProviderWireProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = Array.toList messages |> List.map wireMessageOf }

        ProviderProjection.renderWire projection

    /// Return the semantic projection for plain wire messages, dropping wire ids.
    let semanticProjection (messages: obj array) : obj =
        let projection: ProviderProjection.ProviderWireProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = Array.toList messages |> List.map wireMessageOf }

        projection |> ProviderProjection.toSemantic |> semanticProjectionToJs

    /// ARCH-004: the sole append-only prefix authority at the JS boundary.
    let isAppendOnlyPrefix (previous: obj) (next: obj) : bool =
        ProviderProjection.isAppendOnlyPrefix (wireProjectionOf previous) (wireProjectionOf next)

    let renderSemantic (projection: obj) : string =
        wireProjectionOf projection
        |> ProviderProjection.toSemantic
        |> ProviderProjection.renderSemantic

    let semanticallyEqual (left: obj) (right: obj) : bool =
        renderSemantic left = renderSemantic right

    let sealDigest (sha256: string -> string) (projection: obj) : string =
        wireProjectionOf projection |> ProviderProjection.renderWire |> sha256

    let toolResultDigests (sha256: string -> string) (projection: obj) : string array =
        wireProjectionOf projection
        |> fun value -> value.Messages
        |> List.collect (fun message ->
            message.Parts
            |> List.choose (function
                | ProviderProjection.WireToolResult(_, result) -> Some(sha256 result)
                | _ -> None))
        |> List.toArray

    /// The owner constant used by the InsertRepair projection.
    let repairInstruction: string = ProjectionConstants.RepairInstruction

    /// Explicitly exposed pure API names used by the lifecycle-boundary contract.
    let pureContractNames: string array =
        [| "plan"
           "renderPrefix"
           "renderMessages"
           "renderMessagesWithHostIds"
           "renderWire"
           "renderSemantic"
           "isAppendOnlyPrefix"
           "sealDigest"
           "toolResultDigests" |]
