namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Identity

type AgentLinkageProjection =
    { LinkedChildren: Map<ChildId, string>
      LinkedRoles: Map<ChildId, string>
      ForkedChildren: Map<ChildId, string> }

/// Durable family linkage. Runtime ownership is not inferred from envelope RuntimeId.
module LinkageProjection =

    let empty =
        { LinkedChildren = Map.empty
          LinkedRoles = Map.empty
          ForkedChildren = Map.empty }

    let private normalizedRole role =
        role |> Option.bind (fun value -> if String.IsNullOrWhiteSpace value then None else Some value)

    let link childId targetAgent role current =
        let existing = defaultArg current empty

        { existing with
            LinkedChildren = Map.add childId targetAgent existing.LinkedChildren
            LinkedRoles =
                match normalizedRole role with
                | Some value -> Map.add childId value existing.LinkedRoles
                | None -> existing.LinkedRoles }

    let fork childId targetAgent role current =
        let linked = link childId targetAgent role current

        { linked with
            ForkedChildren = Map.add childId targetAgent linked.ForkedChildren }

    let unlink childId current =
        let existing = defaultArg current empty

        { LinkedChildren = Map.remove childId existing.LinkedChildren
          LinkedRoles = Map.remove childId existing.LinkedRoles
          ForkedChildren = Map.remove childId existing.ForkedChildren }
