namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host.Ports

/// HNI P-10: the OpenCode adapter for the neutral persistence touchpoints.
///
/// The product owns its journals, ledgers and leases (Persistence/*); the host
/// only supplies stable roots and a project identity. On this host:
///
///  - `StateRoot` is the product's existing durable-state root,
///    `RuntimePath.stateRoot` — `<primary worktree>/.Herdchestra`
///    (src/Wanxiangshu/Persistence/Journal/RuntimePath.fs:86-118), NOT a new
///    directory and NOT host storage. Resolving it is async (it runs
///    `git worktree list --porcelain`), while the port is a plain property, so
///    the composition root resolves it once and hands the adapter a thunk.
///  - `ProjectRoot` is the plugin's workspace directory, `input.directory`
///    (read once here, the single authority for the host's workspace field).
///  - `ProjectId` is host-supplied: the plugin input's `input.project.id`
///    (node_modules/@opencode-ai/plugin/dist/index.d.ts `PluginInput.project`
///    of SDK type `Project { id, worktree, … }`). No product code keys on a
///    project id today, so when the host omits it the directory is used as the
///    stable fallback key.
module PathsPort =

    /// A host value is absent when it is `null` or `undefined`; a missing
    /// host property reads as `undefined`.
    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    /// A host object field; a missing field reads as `undefined`.
    [<Emit("$0 == null ? undefined : $0[$1]")>]
    let private field (value: obj) (name: string) : obj = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isJsString (value: obj) : bool = jsNative

    /// The text of a host string value; a non-string, absent or blank value is
    /// reported as absent rather than coerced.
    let private readText (value: obj) : string option =
        if isNullish value || not (isJsString value) then
            None
        elif System.String.IsNullOrWhiteSpace(unbox<string> value) then
            None
        else
            Some(unbox<string> value)

    /// The workspace directory the plugin runs in (`input.directory`). This is
    /// the single reading of the host's workspace field: every consumer
    /// receives the value through this port instead of re-reading the raw
    /// plugin input.
    let workspaceDirectory (input: obj) : string option =
        if isNullish input then
            None
        else
            readText (field input "directory")

    /// The project root string carried by resolved paths.
    let projectRootOf (paths: IHostPaths option) : string option =
        paths
        |> Option.map (fun p ->
            let (WorkspacePath root) = p.ProjectRoot
            root)

    /// The state root string carried by resolved paths.
    let stateRootOf (paths: IHostPaths option) : string option =
        paths
        |> Option.map (fun p ->
            let (WorkspacePath root) = p.StateRoot
            root)

    /// The host-supplied project identity (`input.project.id`), absent when the
    /// host wiring carries no project object. `projectID` / `projectId` are
    /// accepted as the flat spellings some hosts use.
    let private projectId (input: obj) : string option =
        if isNullish input then
            None
        else
            let projectObj = field input "project"

            readText (field projectObj "id")
            |> Option.orElseWith (fun () -> readText (field input "projectID"))
            |> Option.orElseWith (fun () -> readText (field input "projectId"))

    /// The host-supplied project identity, with the given fallback when the
    /// host wiring carries no project object.
    let projectIdOf (input: obj) (fallback: string) : string =
        projectId input |> Option.defaultValue fallback

    /// The durable-state root thunk. `StateRoot` is a plain property (the port
    /// is not async), so the async `RuntimePath.stateRoot` resolution happens
    /// in the composition root and is handed in as a memoized function.
    type HostPaths(stateRoot: (unit -> string), projectRoot: string, projectId: string) =
        interface IHostPaths with
            member _.StateRoot = WorkspacePath(stateRoot ())
            member _.ProjectRoot = WorkspacePath projectRoot
            member _.ProjectId = projectId

    /// The paths for the given host wiring, or `None` when the input names no
    /// project root. The state root is supplied already resolved; the project
    /// root is `input.directory` and the project id is `input.project.id` with
    /// the directory as the stable fallback.
    let create (stateRoot: unit -> string) (input: obj) : IHostPaths option =
        let directory = workspaceDirectory input
        let id = projectId input

        match directory with
        | None -> None
        | Some dir -> Some(HostPaths(stateRoot, dir, Option.defaultValue dir id) :> IHostPaths)
