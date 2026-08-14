namespace Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace

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
