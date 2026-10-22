namespace Wanxiangshu.Participant.Persona

[<RequireQualifiedAccess>]
module OfficeCapabilitySurface =
    val managerForkableOffices: unit -> string array
    val permissions: roleLabel: string -> string array
    val isAllowed: roleLabel: string -> permissionLabel: string -> bool

    /// The canonical permission labels, in catalog order. The single
    /// authority: no consumer keeps its own copy of the label set.
    val permissionLabels: unit -> string array

    /// The canonical label for a permission label, or None when the label is
    /// not a permission at all.
    val permissionOfLabel: label: string -> string option

    /// Manager gate facts as a plain JS object: hasActiveIncumbency, hasAssessment,
    /// isFinalIncumbent, cleanupBlockerDigest.
    val managerFacts:
        hasActiveIncumbency: bool ->
        hasAssessment: bool ->
        isFinalIncumbent: bool ->
        cleanupBlockerDigest: string ->
            obj

    val managerFactsAllowed: facts: obj -> permissionLabel: string -> bool
