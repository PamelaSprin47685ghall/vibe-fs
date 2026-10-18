namespace Wanxiangshu.Participant.Provider.Attempt

/// Why a Blogger retry request cannot be selected.
[<RequireQualifiedAccess>]
type BloggerRetryError =
    /// The boundary could not project a physical request kind.
    | MissingProjection
    /// The projected request does not belong to an active Blogger run.
    | NoActiveBloggerRun

/// What one provider attempt produced.
[<RequireQualifiedAccess>]
type AttemptOutcome =
    | Completed
    | CompletedInvalid
    | Failed
    | Aborted

[<RequireQualifiedAccess>]
module BloggerRetryPolicy =

    /// Pure Blogger retry request selection.
    /// A failed BloggerMain always retries as BloggerMain.
    let nextRequest
        (failedKind: ProviderRequestKind)
        (hasSquashMaterial: bool)
        : Result<ProviderRequestKind, BloggerRetryError> =
        ignore hasSquashMaterial

        match failedKind with
        | ProviderRequestKind.BloggerMain -> Ok ProviderRequestKind.BloggerMain
        | _ -> Error BloggerRetryError.NoActiveBloggerRun
