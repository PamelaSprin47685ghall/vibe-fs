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
                                | None -> existing.LinkedRoles }
                        | None ->
                            { LinkedChildren = Map.ofList [ (childId, targetAgent) ]
                              LinkedRoles =
                                role
                                |> Option.map (fun value -> Map.ofList [ (childId, value) ])
                                |> Option.defaultValue Map.empty }

                    { s with Linkage = Some link })
                proj.Sessions

        { proj with Sessions = sessions }

    let foldUnlinked proj parentId childId =
        let sessions =
            updateSession
                parentId
                (fun s ->
                    let link =
                        match s.Linkage with
                        | Some existing ->
                            { LinkedChildren = Map.remove childId existing.LinkedChildren
                              LinkedRoles = Map.remove childId existing.LinkedRoles }
                        | None ->
                            { LinkedChildren = Map.empty
                              LinkedRoles = Map.empty }

                    { s with Linkage = Some link })
                proj.Sessions

        { proj with Sessions = sessions }
