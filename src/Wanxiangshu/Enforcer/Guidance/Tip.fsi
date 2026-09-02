namespace Wanxiangshu.Enforcer.Guidance

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

/// Tip guidance body for Main auto-injected marker (without pair-programming trailer).
[<RequireQualifiedAccess>]
type TipGuidance =
    {
        TipName: string
        Presentation: TipPresentation
        /// Marker tip half only (Full = name header + main.md; Identity = tip: name).
        Text: string
    }

/// Current Main tip guidance: which tip text should the Main see (ENFORCER-*)?
/// Only answers that question — no continuation parking, no blog commit, no repair.
module EnforcerTipGuidance =
    val resolveTipGuidance:
        journal: AgentJournal -> mainOrBloggerSession: SessionId -> System.Threading.Tasks.Task<TipGuidance option>

    val latestTipGuidance:
        journal: AgentJournal -> mainOrBloggerSession: SessionId -> System.Threading.Tasks.Task<string option>

    val latestTipNudge:
        journal: AgentJournal -> mainOrBloggerSession: SessionId -> System.Threading.Tasks.Task<string option>
