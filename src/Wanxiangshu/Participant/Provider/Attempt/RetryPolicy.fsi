namespace Wanxiangshu.Participant.Provider.Attempt

/// Why a Blogger retry request cannot be selected.
[<RequireQualifiedAccess>]
type BloggerRetryError =
    | MissingProjection
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
    val nextRequest:
        failedKind: ProviderRequestKind -> hasSquashMaterial: bool -> Result<ProviderRequestKind, BloggerRetryError>
