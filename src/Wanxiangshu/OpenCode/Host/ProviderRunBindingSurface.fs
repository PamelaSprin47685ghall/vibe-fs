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
                box {| ok = false; error = "NoBindableRun" |}
            | ProviderRunBinding.Rejection.AmbiguousRun count ->
                box {| ok = false; error = "AmbiguousRun"; count = count |}
            | ProviderRunBinding.Rejection.NotLatestRun ->
                box {| ok = false; error = "NotLatestRun" |}
