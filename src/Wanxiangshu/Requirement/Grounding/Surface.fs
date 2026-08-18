namespace Wanxiangshu.Requirement.Grounding

module Surface =

    let discoverPackages workspace : string array =
        GroundingCatalog.discover workspace |> List.map _.Name |> List.toArray

    let resolvePackages workspace path : string array =
        GroundingCatalog.resolve workspace path |> List.map _.Name |> List.toArray

    let materializePackage workspace packageName : obj =
        let snapshot = GroundingCatalog.materialize workspace packageName

        box
            {| workspace = snapshot.Workspace
               name = snapshot.PackageName
               digest = snapshot.Digest
               materials =
                snapshot.Materials
                |> List.map (fun material ->
                    box
                        {| path = material.Path
                           result = material.ResultBytes |})
                |> List.toArray |}
