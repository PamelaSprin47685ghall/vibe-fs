namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type AgentVisibility =
    | Public
    | Internal

type ManagedAgent =
    { Name: string
      Role: Role
      Tier: AgentTier
      Visibility: AgentVisibility }

[<RequireQualifiedAccess>]
type ManagedAgentParseError =
    | UnknownManagedAgent of string
    | LegacyAgentName of string
    | Malformed of string

module ManagedAgent =
    val roleName: (Role -> string)
    val tierName: (AgentTier -> string)
    val nameOf: (AgentTier -> Role -> string)
    val visibilityOf: role: Role -> AgentVisibility
    val make: tier: AgentTier -> role: Role -> ManagedAgent
    val allPublicRoles: Role list
    val allInternalRoles: Role list
    val allRoles: Role list
    val requiredNames: string list
    val managerForkableNames: string list
    val orchestratorForkableNames: string list
    val inspectorToolNames: string list
    val coderToolNames: string list
    val parse: value: string -> Result<ManagedAgent, ManagedAgentParseError>
    val tryParse: value: string -> ManagedAgent option
    val peer: agent: ManagedAgent -> ManagedAgent
    val isInternal: agent: ManagedAgent -> bool
    val formatParseError: err: ManagedAgentParseError -> string
