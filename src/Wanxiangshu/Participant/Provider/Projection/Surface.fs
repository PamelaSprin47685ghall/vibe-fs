namespace Wanxiangshu.Participant.Provider.Projection

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

/// Plain-JavaScript boundary for the generic provider projection algebra.
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

            ProviderProjection.WireToolCall(ToolCallId.create (stringOf value?callId), stringOf value?name, arguments)
        | "tool-result"
        | "ToolResult" ->
            let result =
                if isNullish value?result then
                    stringOf value?text
                else
                    stringOf value?result

            ProviderProjection.WireToolResult(ToolCallId.create (stringOf value?callId), result)
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

    let private semanticProjectionToJs (projection: ProviderProjection.ProviderSemanticProjection) : obj =
        box
            {| providerId = optionObj projection.ProviderId
               modelId = optionObj projection.ModelId
               variant = optionObj projection.Variant
               tools = projection.Tools |> List.toArray
               system = projection.System |> List.toArray
               messages =
                projection.Messages
                |> List.map (fun message ->
                    box
                        {| role = message.Role
                           parts = message.Parts |> List.map semanticPartToJs |> List.toArray |})
                |> List.toArray |}

    let private messageAnchorOf (value: obj) : ProjectionMessageAnchor =
        match stringOf value?kind with
        | "Append" -> ProjectionMessageAnchor.Append
        | "BeforeMessageIndex" -> ProjectionMessageAnchor.BeforeMessageIndex(intOf value?index)
        | other -> failwithf "ProjectionSurface: unknown message anchor %s" other

    let private messageAnchorToJs (anchor: ProjectionMessageAnchor) : obj =
        match anchor with
        | ProjectionMessageAnchor.Append -> box {| kind = "Append" |}
        | ProjectionMessageAnchor.BeforeMessageIndex index ->
            box
                {| kind = "BeforeMessageIndex"
                   index = index |}

    let private messageRowOf (value: obj) : ProjectionMessageRow =
        { Message = wireMessageOf value?message
          HostMessageId = optionalString value?hostMessageId
          HostIsPhysical = boolOf value?hostIsPhysical }

    let private messageRowToJs (row: ProjectionMessageRow) : obj =
        box
            {| message = wireMessageToJs row.Message
               hostMessageId = optionObj row.HostMessageId
               hostIsPhysical = row.HostIsPhysical |}

    let private messageBaseOf (value: obj) : ProjectionMessageBase =
        { Key = stringOf value?key
          Rows = arrayOf value?rows |> Array.toList |> List.map messageRowOf }

    let private messageInsertionOf (value: obj) : ProjectionMessageInsertion =
        { Key = stringOf value?key
          Anchor = messageAnchorOf value?anchor
          Rows = arrayOf value?rows |> Array.toList |> List.map messageRowOf }

    let private intentOf (value: obj) : ProjectionIntent =
        match stringOf value?kind with
        | "ReplaceMessageBase" -> ProjectionIntent.ReplaceMessageBase(messageBaseOf value)
        | "InsertMessageRows" -> ProjectionIntent.InsertMessageRows(messageInsertionOf value)
        | other -> failwithf "ProjectionSurface: unknown intent kind %s" other

    let private intentKind (intent: ProjectionIntent) : string =
        match intent with
        | ProjectionIntent.ReplaceMessageBase _ -> "ReplaceMessageBase"
        | ProjectionIntent.InsertMessageRows _ -> "InsertMessageRows"

    /// Convert an internal intent to plain surface data without exposing an F# union.
    let internal intentToSurfaceValue (intent: ProjectionIntent) : obj =
        match intent with
        | ProjectionIntent.ReplaceMessageBase replacement ->
            box
                {| kind = "ReplaceMessageBase"
                   key = replacement.Key
                   rows = replacement.Rows |> List.map messageRowToJs |> List.toArray |}
        | ProjectionIntent.InsertMessageRows insertion ->
            box
                {| kind = "InsertMessageRows"
                   key = insertion.Key
                   anchor = messageAnchorToJs insertion.Anchor
                   rows = insertion.Rows |> List.map messageRowToJs |> List.toArray |}

    let private conflictName (conflict: ProjectionConflict) : string =
        match conflict with
        | ProjectionConflict.ConflictingMessageBase -> "ConflictingMessageBase"
        | ProjectionConflict.ConflictingMessageRows _ -> "ConflictingMessageRows"

    let private snapshotOfJs (value: obj) : ProjectionSnapshot =
        { CurrentProjection = semanticProjectionOf value?currentProjection }

    let private snapshotToJs (snapshot: ProjectionSnapshot) : obj =
        box {| currentProjection = semanticProjectionToJs snapshot.CurrentProjection |}

    [<Emit("Object.assign({}, $0, { kind: $1 })")>]
    let private withKind (payload: obj) (kind: string) : obj = jsNative

    let replaceMessageBase (payload: obj) : obj = withKind payload "ReplaceMessageBase"

    let insertMessageRows (payload: obj) : obj = withKind payload "InsertMessageRows"

    /// Construct and normalize an attempt-local projection snapshot.
    let projectionSnapshot (currentProjection: obj) : obj =
        box {| currentProjection = currentProjection |} |> snapshotOfJs |> snapshotToJs

    /// Decode raw Host messages through the production codec into wire data.
    let decodeMessages (rawMessages: obj array) : obj =
        ProviderWireCapture.decodeMessageView (Array.toList rawMessages)
        |> wireProjectionToJs

    /// Plan a plain intent array with canonical ordering and explicit conflict data.
    let plan (intents: obj array) : obj =
        match ProjectionPlanner.plan (Array.toList intents |> List.map intentOf) with
        | Ok ordered ->
            box
                {| ok = true
                   intents = ordered |> List.map intentKind |> List.toArray |}
        | Error(ProjectionConflict.ConflictingMessageRows key as conflict) ->
            box
                {| ok = false
                   conflict = conflictName conflict
                   key = key |}
        | Error conflict ->
            box
                {| ok = false
                   conflict = conflictName conflict |}

    /// Render canonical wire messages.
    let renderMessages (snapshot: obj) (baseMessages: obj array) (intents: obj array) : obj array =
        ProjectionRenderer.renderMessagesWithIntents
            (snapshotOfJs snapshot)
            (Array.toList baseMessages |> List.map wireMessageOf)
            (Array.toList intents |> List.map intentOf)
        |> List.map wireMessageToJs
        |> List.toArray

    /// Render canonical wire messages and aligned Host metadata.
    let renderMessagesWithHostIds (snapshot: obj) (baseMessages: obj array) (intents: obj array) : obj =
        let rendered =
            ProjectionRenderer.renderMessagesWithHostIds
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

    let isAppendOnlyPrefix (previous: obj) (next: obj) : bool =
        ProviderProjection.isAppendOnlyPrefix (wireProjectionOf previous) (wireProjectionOf next)

    let renderSemantic (projection: obj) : string =
        wireProjectionOf projection
        |> ProviderProjection.toSemantic
        |> ProviderProjection.renderSemantic

    let semanticallyEqual (left: obj) (right: obj) : bool =
        renderSemantic left = renderSemantic right

    let cutoffDigest (sha256: string -> string) (snapshot: obj) (cutoff: int) : string =
        ProjectionRenderer.cutoffDigest sha256 (snapshotOfJs snapshot) cutoff

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

    let pureContractNames: string array =
        [| "plan"
           "renderMessages"
           "renderMessagesWithHostIds"
           "renderWire"
           "renderSemantic"
           "cutoffDigest"
           "isAppendOnlyPrefix"
           "sealDigest"
           "toolResultDigests" |]
