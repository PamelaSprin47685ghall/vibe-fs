namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Foundation.Identity

/// Durable Companion link. Blog frames and coverage belong to `BlogProjection`.
type CompanionProjection =
    {
        /// COMPANION-003: the companion Blogger Session Y, so a restart rebinds the
        /// same one instead of creating a second Y for the same X.
        BloggerSessionId: SessionId option
    }

/// Durable Companion cache facts. In-flight Blogger work remains runtime-only.
///
/// Every function takes an already-resolved `CompanionProjection`, not an option:
/// `Fold.updateCompanion` is the single caller and it defaults the absent case to
/// `empty` before applying. A second `defaultArg` here would let a caller pass
/// `None` and silently discard the session's existing companion state.
module CompanionProjection =

    let empty = { BloggerSessionId = None }

    let linkBlogger (bloggerSessionId: SessionId) (current: CompanionProjection) =
        { current with
            BloggerSessionId = Some bloggerSessionId }

    /// The Blogger was aborted. `None` again, so the next transform creates a
    /// fresh Y rather than prompting an aborted session forever.
    let closeBlogger (current: CompanionProjection) =
        { current with BloggerSessionId = None }
