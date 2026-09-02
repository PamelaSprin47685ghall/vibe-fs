namespace Wanxiangshu.OpenCode

/// JS-native boundary for HOST-BOUNDARY-008: the causal read that binds a
/// provider run identity to the unsealed assistant child of one physical user
/// message.
///
/// `ProviderRunBinding.bindableRun` returns `Result<SessionMessage, Rejection>`
/// where `Rejection` is a typed F# union. This surface translates that into
/// JSON-shaped objects so semantic tests observe the real binding law without
/// touching Fable representation (`.tag` / `.fields` / DU ordinals).
///
/// The projected message list is produced by `SessionSnapshotSurface.projectMessages`
/// as an opaque `ProjectedMessages` handle. This surface consumes that handle
/// (same-assembly access to the internal `Messages` field) — no Fable list
/// crosses the JS boundary.
module ProviderRunBindingSurface =

    /// HOST-BOUNDARY-008 causal read: bind the unsealed assistant child of one
    /// physical user message to a provider run identity.
    ///
    /// `messages` is the opaque `ProjectedMessages` handle from
    /// `SessionSnapshotSurface.projectMessages`.
    val bindableRun: physicalUserMessage: string -> messages: SessionSnapshotSurface.ProjectedMessages -> obj

    /// Exercise the same typed observation policy used by the physical wire:
    /// `NoBindableRun` may advance to another public snapshot, while genuine
    /// identity rejection stops immediately. The sequence is capped by the
    /// production catch-up budget so tests cannot prove an unbounded policy.
    val observeSequence: physicalUserMessage: string -> snapshots: SessionSnapshotSurface.ProjectedMessages array -> obj
