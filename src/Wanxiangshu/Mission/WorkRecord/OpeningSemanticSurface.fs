namespace Wanxiangshu.Mission.WorkRecord

open Fable.Core.JsInterop
open Wanxiangshu.Context.Trace

/// JS-native boundary for the WorkRecord-owned composition of already-rendered
/// XTrace evidence. Semantic item decoding, selection, and rendering stay on the
/// XTrace owner surface.
[<RequireQualifiedAccess>]
module OpeningSemanticSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private openingOf (value: obj) : XTraceOpeningEvidence =
        { AssignmentText = text (value?assignment)
          AuthoritativeRequirements =
            if isNull (value?requirements) then
                []
            else
                (value?requirements) |> unbox<string array> |> Array.toList
          ConstitutiveBody = text (value?constitutive) }

    let private openingView (value: XTraceOpeningEvidence) : obj =
        box
            {| assignment = value.AssignmentText
               requirements = List.toArray value.AuthoritativeRequirements
               constitutive = value.ConstitutiveBody |}

    let opening (assignment: string) (requirements: string array) (constitutive: string) : obj =
        box
            {| assignment = assignment
               requirements = if isNull requirements then [||] else requirements
               constitutive = constitutive |}

    let withConstitutive (opening: obj) (constitutiveBody: string) : obj =
        LifecycleWorkRecord.withConstitutive (openingOf opening) constitutiveBody
        |> openingView

    let materialize
        (opening: obj)
        (frames: string array)
        (renderedGap: string)
        (includeOpening: bool)
        : string =
        LifecycleWorkRecord.materialize
            (openingOf opening)
            (if isNull frames then [] else Array.toList frames)
            renderedGap
            includeOpening
