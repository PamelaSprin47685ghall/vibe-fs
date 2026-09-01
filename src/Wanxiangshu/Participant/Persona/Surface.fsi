namespace Wanxiangshu.Participant.Persona

module PersonaSurface =
    val allRoleLabels: string array
    val allPublicRoleLabels: string array
    val allInternalRoleLabels: string array
    val requiredNames: string array
    val legacyNames: string array
    val nameOf: string -> string -> string
    val peerTierLabel: string -> string
    val peerName: string -> string
    val isManagedName: string -> bool
    val isLegacyName: string -> bool
    val roleName: string -> string
    val persona: string -> string -> string
    val bookkeeperPersona: string -> string
    val formatLegacyNameNotSupported: string -> string
    val formatLegacyNameInConfig: string -> string
    val resolveParticipantIdentityAtRoot: string -> obj
    val inheritParticipantIdentityFromOwner: string -> string -> obj
    val rehydrateParticipantIdentity: string -> string -> string -> string -> string -> string -> int -> string -> obj
