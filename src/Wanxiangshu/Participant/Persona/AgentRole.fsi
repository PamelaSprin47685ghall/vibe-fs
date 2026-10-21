namespace Wanxiangshu.Participant.ManagedAgents

open Wanxiangshu.Foundation

/// Host agent name -> participant role (managed `fast-ROLE` / `deep-ROLE` names or a canonical role).
/// Pure roster law; moved out of the OpenCode adapter's HostSessionContext (CUT-34).
module AgentRole =
    val roleOf: agent: string -> Role option
