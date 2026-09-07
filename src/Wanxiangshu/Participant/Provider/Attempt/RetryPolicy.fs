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

    /// Pure Blogger retry request selection based on material presence.
    /// A failed Squash always returns to Main; a failed Main attempts Squash if squash material exists.
    let nextRequest
        (failedKind: ProviderRequestKind)
        (hasSquashMaterial: bool)
        : Result<ProviderRequestKind, BloggerRetryError> =
        match failedKind, hasSquashMaterial with
        | ProviderRequestKind.BloggerMain, true -> Ok ProviderRequestKind.BloggerSquash
        | ProviderRequestKind.BloggerMain, false -> Ok ProviderRequestKind.BloggerMain
        | ProviderRequestKind.BloggerSquash, _ -> Ok ProviderRequestKind.BloggerMain
        | _ -> Error BloggerRetryError.NoActiveBloggerRun
