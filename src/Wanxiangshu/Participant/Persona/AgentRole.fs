namespace Wanxiangshu.Participant.ManagedAgents

open System
open Wanxiangshu.Foundation

module AgentRole =

    let private parseRole (agent: string) : Role option =
        match ManagedAgent.tryParse agent with
        | Some managed -> Some managed.Role
        | None -> Roles.tryParseRole (agent.Trim())

    /// Resolve Role from Host Agent identity (fast-ROLE / deep-ROLE) or Canonical Role.
    /// build/plan aliases remain rejected.
    let roleOf (agent: string) : Role option =
        Option.ofObj agent
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Option.bind parseRole
