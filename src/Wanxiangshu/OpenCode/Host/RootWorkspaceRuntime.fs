namespace Wanxiangshu.OpenCode

type RootWorkspaceRuntime() =
    let gate = obj ()
    // DSL-MUTABLE: resource — process-lifetime first-bind cell
    let mutable workspaceDirectory: string option = None

    let reader =
        { new IRootWorkspaceReader with
            member _.TryRead() =
                lock gate (fun () -> workspaceDirectory) }

    let binder =
        { new IRootWorkspaceBinder with
            member _.TryBind candidate =
                lock gate (fun () ->
                    match workspaceDirectory, candidate |> Option.filter (System.String.IsNullOrWhiteSpace >> not) with
                    | None, Some path ->
                        workspaceDirectory <- Some path
                        true
                    | _ -> false) }

    member _.Binder = binder
    member _.Reader = reader

module RootWorkspaceProcess =
    let private runtime = RootWorkspaceRuntime()

    let local () = runtime
