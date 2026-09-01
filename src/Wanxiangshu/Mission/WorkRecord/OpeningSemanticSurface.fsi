namespace Wanxiangshu.Mission.WorkRecord

[<RequireQualifiedAccess>]
module OpeningSemanticSurface =
    val opening: assignment: string -> requirements: string array -> constitutive: string -> obj
    val withConstitutive: opening: obj -> constitutiveBody: string -> obj
    val materialize: opening: obj -> frames: string array -> renderedGap: string -> includeOpening: bool -> string
