namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Durable topology evidence that outlives the process: which child belongs to
/// which parent with which execution agent, and which fission lane belongs to
/// which owner. These are pure reads of the durable projections; process-local
/// tables are only a cache of what this process is currently driving.
module SessionBindingRecovery =

    /// Durable evidence for one child session: (parentSessionId, executionAgent).
    val evidenceFor: projections: AgentProjectionSet -> childSessionId: SessionId -> (string * string) option

    /// Durable evidence for one fission lane (owner, group and slot).
    val fissionLaneFor: projections: AgentProjectionSet -> laneSessionId: SessionId -> FissionLaneBinding option

    /// Load Phase: install the durable fission-lane resolver.
    val install: journal: AgentJournal -> unit
