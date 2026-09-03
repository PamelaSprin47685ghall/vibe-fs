namespace Wanxiangshu.Foundation

/// JS-native semantic surface for participant identity. Role
/// crosses the boundary as canonical wire labels; JS callers never touch
/// Fable representation (JS-SEMANTIC-SURFACE-003/005).
module RolesSurface =

    let private labelsOf (predicate: Role -> bool) : string array =
        Roles.all
        |> List.filter predicate
        |> List.map Roles.roleLabel
        |> List.sort
        |> List.toArray

    /// The ten canonical role labels, sorted.
    let allRoleLabels: string array = labelsOf (fun _ -> true)

    /// AGENT-008 public fork / horizon vocabulary (no Distiller, no Blogger).
    let allPublicRoleLabels: string array = labelsOf (Roles.isInternal >> not)

    /// Private runtimes: Distiller (map/reduce) and Blogger (companion).
    let allInternalRoleLabels: string array = labelsOf Roles.isInternal
