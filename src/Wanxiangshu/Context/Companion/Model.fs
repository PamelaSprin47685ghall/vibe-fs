namespace Wanxiangshu.Context.Companion

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity

type BlogText = string

/// DSL-state-combination: domain — this durable Companion projection combines
/// frame history, optional effective text, session identity and XTrace evidence;
/// none of these fields is a process continuation cursor.
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
/// written by the recovery path against `Context/Prefix/Epoch`, which is the
/// single owner. A recovery slot either has a candidate or does not, so there is no
/// enable flag left to persist.
type ICompanionDurablePort =
    abstract Load: SessionId -> Task<Result<CompanionMemory option, string>>

    /// COMPANION-003. Takes the Blogger's own SessionId, not a `ChildId` plus a
    /// `"blogger"` target string: the previous shape recorded an EXEC-009 handle
    /// link and then recovered Y by searching for the literal target `"blogger"`,
    /// which is agent-string matching standing in for an identity.
    abstract LinkBlogger: SessionId * SessionId * string -> Task<Result<unit, string>>

    abstract CloseBlogger: SessionId -> Task<Result<unit, string>>
