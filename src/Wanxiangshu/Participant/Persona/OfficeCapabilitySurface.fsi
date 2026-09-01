namespace Wanxiangshu.Participant.Persona

[<RequireQualifiedAccess>]
module OfficeCapabilitySurface =
    val managerForkableOffices: unit -> string array
    val permissions: roleLabel: string -> string array
    val isAllowed: roleLabel: string -> permissionLabel: string -> bool
