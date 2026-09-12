namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

/// Workspace-host-owned shared journal surface.
/// Runtime paths and EventStore capabilities stay opaque; callers observe only
/// identity, keyed Current presence, and append outcomes.
[<RequireQualifiedAccess>]
module WorkspaceEventStoreSurface =
    /// Acquire the process-local workspace journal for raw directories.
    val acquire: retiredDirectory: string -> commonDirectory: string -> processId: int -> startedAt: string -> Task<obj>

    /// Acquire the plugin's process-local workspace journal through the same
    /// runtime-path owner as the composition root. Returns a standard
    /// `JournalHandle` suited for `JournalSurface` operations.
    val acquireSharedForWorkspace: workspace: string -> processId: int -> startedAt: string -> Task<obj>

    /// Release a workspace journal handle.
    val release: handle: JournalHandle -> unit

    /// Test whether two handles refer to the same journal instance.
    val same: left: JournalHandle -> right: JournalHandle -> bool
