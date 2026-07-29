namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Identity

type ProjectionSnapshot = string
type BlogText = string

type ActivePrefixEpochProjection =
    { EpochId: string
      FrozenB: BlogText
      CutoffMessageIndex: int
      CoveredPrefixDigest: string }

type CompanionProjection =
    {
        LastSuccessfulProjection: ProjectionSnapshot option
        LatestB: BlogText option
        ActivePrefixEpoch: ActivePrefixEpochProjection option
        /// COMPANION-003: the companion Blogger Session Y, so a restart rebinds the
        /// same one instead of creating a second Y for the same X.
        BloggerSessionId: SessionId option
        ReplacementActive: bool
    }

    member this.PrefixReplacementEnabled = this.ReplacementActive

/// Durable Companion cache facts. In-flight Blogger work remains runtime-only.
module CompanionProjection =

    let empty =
        { LastSuccessfulProjection = None
          LatestB = None
          ActivePrefixEpoch = None
          BloggerSessionId = None
          ReplacementActive = false }

    let baseline projection current =
        { defaultArg current empty with
            LastSuccessfulProjection = Some projection }

    let checkpoint content current =
        { defaultArg current empty with
            LatestB = Some content }

    let recordBlogAdvance projection content current =
        { defaultArg current empty with
            LastSuccessfulProjection = Some projection
            LatestB = Some content }

    let switchEpoch epochId frozenB cutoff digest current =
        { defaultArg current empty with
            ActivePrefixEpoch =
                Some
                    { EpochId = epochId
                      FrozenB = frozenB
                      CutoffMessageIndex = cutoff
                      CoveredPrefixDigest = digest }
            ReplacementActive = true }

    let setReplacement active current =
        { defaultArg current empty with
            ReplacementActive = active }

    let linkBlogger (bloggerSessionId: SessionId) current =
        { defaultArg current empty with
            BloggerSessionId = Some bloggerSessionId }

    /// The Blogger was aborted. `None` again, so the next transform creates a
    /// fresh Y rather than prompting an aborted session forever.
    let closeBlogger current =
        { defaultArg current empty with
            BloggerSessionId = None }
