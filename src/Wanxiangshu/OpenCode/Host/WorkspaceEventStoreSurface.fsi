namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

/// Opaque capability for one workspace journal handle.
type WorkspaceJournalHandle =
    private new: journal: AgentJournal -> WorkspaceJournalHandle
    member internal Journal: AgentJournal
    member internal Release: unit -> unit
    static member internal Create: journal: AgentJournal -> WorkspaceJournalHandle

/// Workspace-host-owned shared journal surface.
/// Runtime paths and EventStore capabilities stay opaque; callers observe only
/// identity, keyed Current presence, and append outcomes.
[<RequireQualifiedAccess>]
module WorkspaceEventStoreSurface =
    /// Acquire a shared workspace journal, returning a boxed result object.
    val acquire: retiredDirectory: string -> commonDirectory: string -> processId: int -> startedAt: string -> Task<obj>

    /// Release a workspace journal handle.
    val release: handle: WorkspaceJournalHandle -> unit

    /// Test whether two handles refer to the same journal instance.
    val same: left: WorkspaceJournalHandle -> right: WorkspaceJournalHandle -> bool
