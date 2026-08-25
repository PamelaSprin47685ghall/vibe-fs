namespace Wanxiangshu.OpenCode

/// JS-native boundary for HOST-BOUNDARY-008: the causal read that binds a
/// provider run identity to the unsealed assistant child of one physical user
/// message.
///
/// `ProviderRunBinding.bindableRun` returns `Result<SessionMessage, Rejection>`
/// where `Rejection` is a typed F# union (`NoBindableRun | AmbiguousRun of int
/// | NotLatestRun`). This surface translates that into JSON-shaped objects so
/// semantic tests observe the real binding law without touching Fable
/// representation (`.tag` / `.fields` / DU ordinals).
///
/// The projected message list is produced by `SessionSnapshotSurface.projectMessages`
/// as an opaque `ProjectedMessages` handle. This surface consumes that handle
/// (same-assembly access to the internal `Messages` field) — no Fable list
/// crosses the JS boundary.
module ProviderRunBindingSurface =

    let private rejectionToJs reads rejection : obj =
        match rejection with
        | ProviderRunBinding.Rejection.NoBindableRun ->
            box
                {| ok = false
                   error = "NoBindableRun"
                   reads = reads |}
        | ProviderRunBinding.Rejection.AmbiguousRun count ->
            box
                {| ok = false
                   error = "AmbiguousRun"
                   count = count
                   reads = reads |}
        | ProviderRunBinding.Rejection.NotLatestRun ->
            box
                {| ok = false
                   error = "NotLatestRun"
                   reads = reads |}

    /// HOST-BOUNDARY-008 causal read: bind the unsealed assistant child of one
    /// physical user message to a provider run identity.
    ///
    /// `messages` is the opaque `ProjectedMessages` handle from
    /// `SessionSnapshotSurface.projectMessages`.
    ///
    /// Returns `{ ok: true, id, parentId, completed }` when exactly one
    /// unsealed assistant message is the child of `physicalUserMessage` and is
    /// the latest assistant run; `{ ok: false, error, count? }` when 0 or ≥2
    /// candidates are observed (fail closed).
    let bindableRun (physicalUserMessage: string) (messages: SessionSnapshotSurface.ProjectedMessages) : obj =
        let typed = messages.Messages

        match ProviderRunBinding.bindableRun physicalUserMessage typed with
        | Ok run ->
            box
                {| ok = true
                   id = run.Id
                   parentId = run.ParentId |> Option.defaultValue null
                   completed = run.Completed |}
        | Error rejection ->
            match rejection with
            | ProviderRunBinding.Rejection.NoBindableRun ->
                box
                    {| ok = false
                       error = "NoBindableRun" |}
            | ProviderRunBinding.Rejection.AmbiguousRun count ->
                box
                    {| ok = false
                       error = "AmbiguousRun"
                       count = count |}
            | ProviderRunBinding.Rejection.NotLatestRun -> box {| ok = false; error = "NotLatestRun" |}

    /// Exercise the same typed observation policy used by the physical wire:
    /// `NoBindableRun` may advance to another public snapshot, while genuine
    /// identity rejection stops immediately. The sequence is capped by the
    /// production catch-up budget so tests cannot prove an unbounded policy.
    let observeSequence
        (physicalUserMessage: string)
        (snapshots: SessionSnapshotSurface.ProjectedMessages array)
        : obj =
        let capped =
            snapshots |> Array.truncate ProviderRunBinding.projectionCatchupMaxReads

        let rec loop index =
            if index >= capped.Length then
                rejectionToJs index ProviderRunBinding.Rejection.NoBindableRun
            else
                match ProviderRunBinding.observeBindableRun physicalUserMessage capped[index].Messages with
                | ProviderRunBinding.Observation.Bound run ->
                    box
                        {| ok = true
                           id = run.Id
                           reads = index + 1 |}
                | ProviderRunBinding.Observation.RunTerminal terminal ->
                    box
                        {| ok = false
                           error = "RunTerminal"
                           id = terminal.Id
                           reads = index + 1 |}
                | ProviderRunBinding.Observation.ProjectionNotVisibleYet -> loop (index + 1)
                | ProviderRunBinding.Observation.Rejected rejection -> rejectionToJs (index + 1) rejection

        loop 0
