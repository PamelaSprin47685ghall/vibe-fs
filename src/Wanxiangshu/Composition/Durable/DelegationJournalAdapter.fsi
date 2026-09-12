namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module DelegationJournalAdapter =
    /// DELEG-029: durable composition wraps delegation fact cases into the outer routing union.
    val fromAgentJournal: journal: AgentJournal -> AgentJournalPort
