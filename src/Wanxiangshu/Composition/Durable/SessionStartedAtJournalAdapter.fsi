namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module SessionStartedAtJournalAdapter =
    val forSessionStartedAt: journal: AgentJournal -> SessionStartedAtPort
    val forDelegatedToolEstimate: journal: AgentJournal -> DelegatedToolEstimatePort
