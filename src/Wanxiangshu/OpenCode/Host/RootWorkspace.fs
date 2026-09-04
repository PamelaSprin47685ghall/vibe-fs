namespace Wanxiangshu.OpenCode

type IRootWorkspaceReader =
    abstract TryRead: unit -> string option

type IRootWorkspaceBinder =
    abstract TryBind: workspaceDirectory: string option -> bool

module RootWorkspaceDirectory =
    let private valid path =
        not (System.String.IsNullOrWhiteSpace path)

    let select
        (pathExists: string -> bool)
        (rootWorkspace: IRootWorkspaceReader)
        (candidate: string option)
        : string option =
        candidate
        |> Option.filter valid
        |> Option.filter pathExists
        |> Option.orElseWith (fun () -> rootWorkspace.TryRead() |> Option.filter valid)
