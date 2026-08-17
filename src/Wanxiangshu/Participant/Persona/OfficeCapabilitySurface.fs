namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

/// JS-native office-capability owner. The manager's forkable office law is
/// projected as labels so callers never decode Role unions or F# collections.
[<RequireQualifiedAccess>]
module OfficeCapabilitySurface =

    let private officeName role =
        match role with
        | Role.Coder -> "Coder"
        | Role.Inspector -> "Inspector"
        | Role.DevOps -> "DevOps"
        | Role.Browser -> "Browser"
        | Role.Inquiry -> "Inquiry"
        | _ -> failwith "OfficeCapabilitySurface: catalog contains a non-forkable role"

    /// OFF-002 / ARCH-017: the canonical manager fork office consequence set.
    let managerForkableOffices () : string array =
        ManagedAgentCatalog.managerForkableRoles |> List.map officeName |> List.toArray
