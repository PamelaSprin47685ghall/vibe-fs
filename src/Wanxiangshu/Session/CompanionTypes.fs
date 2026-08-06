namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Journal

type BlogText = string

type BloggerCompletion =
    { BloggerSessionId: SessionId
      ProviderRun: ProviderRunIdentity
      Text: BlogText
      NextCursor: SemanticCursor
      NextCoverableTurnCutoffExclusive: int
      NextCoveredPrefixDigest: string }



type CompanionMemory =
    {
        Blog: BlogProjectionState
        EffectiveFrames: BlogText option
        /// COMPANION-003: the durable companion Blogger Session Y.
        BloggerSessionId: SessionId option
        /// COMPANION-003: the session's XTrace projection. The chunker maps the
        /// RecordCoverage (XTrace cursor sequence) back to semantic coordinates
        /// through it, so the Blog projection and the XTrace stay in step without
        /// a second copy of either.
        XTrace: XTraceProjectionState
    }

/// The durable Companion writes this runtime cache mirrors.
///
/// Two members are gone with the old prefix mechanism: `AppendEpochSwitched`, which
/// wrote the FrozenRecordPrefix text inline from a token-budget comparison, and
/// `EnableReplacement`, which flipped a per-session opt-in flag. COMPANION-009's epoch
/// now has exactly two movers — `PrefixRebaseCommitted` (CTX-012) and
/// `ContextReanchored` (HOST-006) — and neither goes through this port: they are
/// written by the recovery path against `Journal/PrefixEpochProjection`, which is the
/// single owner. A recovery slot either has a candidate or does not, so there is no
/// enable flag left to persist.
type ICompanionDurablePort =
    abstract Load: SessionId -> Result<CompanionMemory option, string>

    /// COMPANION-003. Takes the Blogger's own SessionId, not a `ChildId` plus a
    /// `"blogger"` target string: the previous shape recorded an EXEC-009 handle
    /// link and then recovered Y by searching for the literal target `"blogger"`,
    /// which is agent-string matching standing in for an identity.
    abstract LinkBlogger: SessionId * SessionId * string -> Result<unit, string>

    abstract CloseBlogger: SessionId -> Result<unit, string>
