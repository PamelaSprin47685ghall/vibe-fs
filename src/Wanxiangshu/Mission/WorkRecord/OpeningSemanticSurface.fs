namespace Wanxiangshu.Mission.WorkRecord

open Fable.Core.JsInterop
open Wanxiangshu.Context.Trace
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

/// JS-native Opening/LWR owner used by obligation semantic proofs.
/// XTrace parts and lifecycle materialization are decoded at this boundary;
/// no semantic union representation is exposed to tests.
[<RequireQualifiedAccess>]
module OpeningSemanticSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private partOf (value: obj) : SemanticPart =
        match text (value?kind) with
        | "text" -> SemanticText(text (value?text))
        | "reasoning" -> SemanticReasoning(text (value?text))
        | "tool-call"
        | "tool_call" -> SemanticToolCall(text (value?name), text (value?args))
        | "tool-result"
        | "tool_result" -> SemanticToolResult(text (value?result))
        | "media" -> SemanticMedia(None, text (value?digest))
        | other -> failwith $"OpeningSemanticSurface: unknown XTrace part '{other}'"

    let private itemOf (value: obj) : XTraceItem =
        { Cursor = { Sequence = int64 (unbox<int> (value?sequence)) }
          Provenance = text (value?provenance)
          Role = text (value?role)
          Part = partOf (value?part) }

    let private itemsOf (values: obj array) =
        if isNull values then [] else values |> Array.toList |> List.map itemOf

    let private itemView (value: XTraceItem) : obj =
        let part =
            match value.Part with
            | SemanticText text -> box {| kind = "text"; text = text |}
            | SemanticReasoning text -> box {| kind = "reasoning"; text = text |}
            | SemanticToolCall(name, args) -> box {| kind = "tool-call"; name = name; args = args |}
            | SemanticToolResult result -> box {| kind = "tool-result"; result = result |}
            | SemanticMedia(mediaType, digest) ->
                box
                    {| kind = "media"
                       digest = digest
                       mediaType = mediaType |> Option.map (fun item -> box item) |> Option.toObj |}

        box
            {| sequence = int value.Cursor.Sequence
               provenance = value.Provenance
               role = value.Role
               part = part |}

    let private openingOf (value: obj) : OpeningMaterial =
        { AssignmentText = text (value?assignment)
          AuthoritativeRequirements =
            if isNull (value?requirements) then [] else (value?requirements) |> unbox<string array> |> Array.toList
          ConstitutiveBody = text (value?constitutive) }

    let private openingView (value: OpeningMaterial) : obj =
        box
            {| assignment = value.AssignmentText
               requirements = List.toArray value.AuthoritativeRequirements
               constitutive = value.ConstitutiveBody |}

    let item (sequence: int) (role: string) (part: obj) : obj =
        itemView
            { Cursor = { Sequence = int64 sequence }
              Provenance = ""
              Role = role
              Part = partOf part }

    let textPart (value: string) : obj = box {| kind = "text"; text = value |}
    let reasoningPart (value: string) : obj = box {| kind = "reasoning"; text = value |}
    let toolCallPart (name: string) (args: string) : obj = box {| kind = "tool-call"; name = name; args = args |}
    let toolResultPart (value: string) : obj = box {| kind = "tool-result"; result = value |}

    let opening (assignment: string) (requirements: string array) (constitutive: string) : obj =
        box
            {| assignment = assignment
               requirements = if isNull requirements then [||] else requirements
               constitutive = constitutive |}

    let withConstitutive (opening: obj) (items: obj array) : obj =
        openingOf opening
        |> fun value -> LifecycleWorkRecord.withConstitutive value (itemsOf items)
        |> openingView

    let materialize
        (opening: obj)
        (frames: string array)
        (trace: obj array)
        (coverageSequence: int)
        (openingEndSequence: int)
        (includeOpening: bool)
        : string =
        LifecycleWorkRecord.materialize
            (openingOf opening)
            (if isNull frames then [] else Array.toList frames)
            (itemsOf trace)
            { IngestedThrough = { Sequence = int64 coverageSequence } }
            { Sequence = int64 openingEndSequence }
            includeOpening

    let forOpening (items: obj array) : obj array =
        itemsOf items |> XTrace.forOpening |> List.map itemView |> List.toArray

    let forWorkRecord (items: obj array) : obj array =
        itemsOf items |> XTrace.forWorkRecord |> List.map itemView |> List.toArray

    let render (items: obj array) : string = itemsOf items |> XTrace.render
