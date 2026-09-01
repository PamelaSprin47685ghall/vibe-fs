namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
module ManagedAgentCatalog =
    val peerTier: AgentTier -> AgentTier
    val nameOf: AgentTier -> Role -> string
    val peerNameOf: AgentTier -> Role -> string
    val personaCallingName: Persona -> string
    val tryParsePersonaCallingName: string -> Persona option
    val allRoles: Role list
    val allPublicRoles: Role list
    val allInternalRoles: Role list
    val managerForkableRoles: Role list
    val bookkeeperNames: string list
    val isBookkeeperName: string -> bool
    val tryParseBookkeeperTier: string -> AgentTier option
    val bookkeeperNameOf: AgentTier -> string
    val bookkeeperPeerName: string -> string option
    val requiredNames: string list
    val managerForkableNames: string list
    val orchestratorForkableNames: string list
    val inspectorToolNames: string list
    val coderToolNames: string list
    val legacyAgentNames: Set<string>
    val isLegacyAgentName: string -> bool
    val formatLegacyNameNotSupported: string -> string
    val formatLegacyNameInConfig: string -> string
