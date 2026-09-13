namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

/// Recovery-owned JS boundary. Host transcript evidence is projected here;
/// PromptRecovery's typed claims and outcomes never cross into JavaScript.
[<RequireQualifiedAccess>]
module RecoverySurface =
    /// Reconcile all currently unsettled claims against raw Host messages. The
    /// same production SessionSnapshotPort projection used by the Host path is
    /// applied before PromptRecovery searches role=user + PromptKey evidence.
    val reconcile: handle: JournalHandle -> rawMessages: obj array -> Task<obj array>
    /// snapshot-unreadable ambiguity state: reconcile against a failing Host
    /// snapshot port; production reconcile must report Unreadable.
    val reconcileWithUnreadableSnapshot: handle: JournalHandle -> reason: string -> Task<obj array>
