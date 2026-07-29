namespace Wanxiangshu.Next.Journal

open System
open Wanxiangshu.Next.Kernel.Identity
open AgentFactsFoldHelpers

module AgentFactsLinkage =

    let foldLinked proj parentId childId targetAgent role =
        let role =
            role
            |> Option.bind (fun value -> if String.IsNullOrWhiteSpace value then None else Some value)

        let sessions =
            updateSession
                parentId
                (fun s ->
                    let link =
                        match s.Linkage with
                        | Some existing ->
                            { LinkedChildren = Map.add childId targetAgent existing.LinkedChildren
                              LinkedRoles =
                                match role with
                                | Some value -> Map.add childId value existing.LinkedRoles
                                | None -> existing.LinkedRoles
                              ForkedChildren = existing.ForkedChildren }
                        | None ->
                            { LinkedChildren = Map.ofList [ (childId, targetAgent) ]
                              LinkedRoles =
                                role
                                |> Option.map (fun value -> Map.ofList [ (childId, value) ])
                                |> Option.defaultValue Map.empty
                              ForkedChildren = Map.empty }

                    { s with Linkage = Some link })
                proj.Sessions

        { proj with Sessions = sessions }

    /// Record a direct fork in addition to the generic session association.
    /// Only this projection is eligible to repopulate a ForkRuntime after restart.
    let foldForked proj parentId childId targetAgent role =
        let linked = foldLinked proj parentId childId targetAgent role

        let sessions =
            updateSession
                parentId
                (fun s ->
                    match s.Linkage with
                    | Some existing ->
                        { s with
                            Linkage =
                                Some
                                    { existing with
                                        ForkedChildren = Map.add childId targetAgent existing.ForkedChildren } }
                    | None -> s)
                linked.Sessions

        { linked with Sessions = sessions }

    let foldUnlinked proj parentId childId =
        let sessions =
            updateSession
                parentId
                (fun s ->
                    let link =
                        match s.Linkage with
                        | Some existing ->
                            { LinkedChildren = Map.remove childId existing.LinkedChildren
                              LinkedRoles = Map.remove childId existing.LinkedRoles
                              ForkedChildren = Map.remove childId existing.ForkedChildren }
                        | None ->
                            { LinkedChildren = Map.empty
                              LinkedRoles = Map.empty
                              ForkedChildren = Map.empty }

                    { s with Linkage = Some link })
                proj.Sessions

        { proj with Sessions = sessions }
