namespace Wanxiangshu.OpenCode

/// Physical Host binding for the assistant run created for one user message.
/// This is transport identity only; it carries no Review semantics.
module ProviderRunBinding =

    [<RequireQualifiedAccess>]
    type Rejection =
        | NoBindableRun
        | AmbiguousRun of count: int
        | NotLatestRun
        | InsufficientSequence

    [<RequireQualifiedAccess>]
    type Observation =
        | Bound of SessionMessage
        | ProjectionNotVisibleYet
        | Rejected of Rejection

    /// Maximum public-snapshot reads used by the physical Host seam when the
    /// only evidence missing is the not-yet-projected assistant message.
    val projectionCatchupMaxReads: int

    val projectionCatchupDelayMilliseconds: int

    /// Bind one physical user message to exactly one unsealed assistant child
    /// run. Returns the latest assistant run when there is exactly one
    /// non-compaction child; otherwise returns a typed rejection.
    val bindableRun: physicalUserMessage: string -> messages: SessionMessage list -> Result<SessionMessage, Rejection>

    /// Split a physical snapshot visibility gap from a genuine identity
    /// rejection without weakening `bindableRun` itself.
    val observeBindableRun: physicalUserMessage: string -> messages: SessionMessage list -> Observation
