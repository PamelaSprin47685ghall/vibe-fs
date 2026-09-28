namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Restart drops every process-local execution binding; the durable handle
/// projection is the evidence that outlives it. This module installs that
/// evidence behind the binding cache so resolution happens on demand instead of
/// relying on a load-order pre-population step.
module SessionBindingRecovery =

    /// Durable evidence for one child session: (parentSessionId, executionAgent).
    val evidenceFor: projections: AgentProjectionSet -> childSessionId: SessionId -> (string * string) option

    /// Install the resolver over a projection source.
    val installFrom: projections: (unit -> AgentProjectionSet) -> unit

    /// Load Phase: install the durable resolver behind the binding cache.
    val install: journal: AgentJournal -> unit
