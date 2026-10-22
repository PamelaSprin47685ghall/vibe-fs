namespace Wanxiangshu.OpenCode

open Wanxiangshu.Host.Ports

/// HNI P-10: the OpenCode adapter for the neutral persistence touchpoints.
///
/// The product owns its journals, ledgers and leases; the host only supplies
/// stable roots and a project identity. `StateRoot` is the product's existing
/// durable-state root (`RuntimePath.stateRoot`, `<primary worktree>/.Herdchestra`),
/// resolved by the composition root and handed in as a memoized function because
/// the port is a plain property while the resolution is async. `ProjectRoot` is
/// the plugin's `input.directory`; `ProjectId` is the host-supplied
/// `input.project.id` with the directory as the stable fallback.
module PathsPort =

    /// The paths for a resolved state-root thunk, a project root and a project id.
    type HostPaths =
        new: stateRoot: (unit -> string) * projectRoot: string * projectId: string -> HostPaths
        interface IHostPaths

    /// The workspace directory the plugin runs in (`input.directory`) — the
    /// single reading of the host's workspace field.
    val workspaceDirectory: input: obj -> string option

    /// The project root string carried by resolved paths.
    val projectRootOf: paths: IHostPaths option -> string option

    /// The state root string carried by resolved paths.
    val stateRootOf: paths: IHostPaths option -> string option

    /// The host-supplied project identity, with the given fallback when the
    /// host wiring carries no project object.
    val projectIdOf: input: obj -> fallback: string -> string

    /// The paths for the given host wiring, or `None` when the input names no
    /// project root.
    val create: stateRoot: (unit -> string) -> input: obj -> IHostPaths option
