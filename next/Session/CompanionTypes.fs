namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Journal

type BlogText = string
type ProjectionSnapshot = ProviderSemanticProjection

type BloggerCompletion =
    { BloggerSessionId: SessionId
      ProviderRun: ProviderRunIdentity
      Text: BlogText
      NextCursor: SemanticCursor
      NextCoverableTurnCutoffExclusive: int
      NextCoveredPrefixDigest: string }

type CompanionOutcome =
    | Submitted
    | SkippedBusy

type CompanionMemory =
    {
        Blog: BlogProjectionState
        LatestB: BlogText option
        /// COMPANION-003: the durable companion Blogger Session Y.
        BloggerSessionId: SessionId option
    }

/// The durable Companion writes this runtime cache mirrors.
///
/// Two members are gone with the old prefix mechanism: `AppendEpochSwitched`, which
/// wrote the FrozenB text inline from a token-budget comparison, and
/// `EnableReplacement`, which flipped a per-session opt-in flag. COMPANION-009's epoch
/// now has exactly two movers — `PrefixRebaseCommitted` (CTX-012) and
/// `ContextReanchored` (HOST-006) — and neither goes through this port: they are
/// written by the recovery path against `Journal/PrefixEpochProjection`, which is the
/// single owner. A recovery slot either has a candidate or does not, so there is no
/// enable flag left to persist.
type ICompanionDurablePort =
    abstract Load: SessionId -> Result<CompanionMemory option, string>
    abstract AppendSuccessful: SessionId * BloggerCompletion -> Result<BlogProjectionState, string>

    /// COMPANION-003. Takes the Blogger's own SessionId, not a `ChildId` plus a
    /// `"blogger"` target string: the previous shape recorded an EXEC-009 handle
    /// link and then recovered Y by searching for the literal target `"blogger"`,
    /// which is agent-string matching standing in for an identity.
    abstract LinkBlogger: SessionId * SessionId * string -> Result<unit, string>

    abstract CloseBlogger: SessionId -> Result<unit, string>
