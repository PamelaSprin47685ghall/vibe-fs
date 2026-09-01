namespace Wanxiangshu.Execution.Session

[<RequireQualifiedAccess>]
module AssociationSurface =
    val empty: obj
    val link: pair: obj -> state: obj -> obj
    val unlink: mainSessionId: string -> state: obj -> obj
    val entry: sessionId: string -> state: obj -> obj
    val classify: sessionId: string -> state: obj -> obj
    val ids: state: obj -> string array
    val bloggerOf: sessionId: string -> state: obj -> obj
    val mainSessionOf: sessionId: string -> state: obj -> obj
    val isCompanion: sessionId: string -> state: obj -> bool
    val executionClass: kind: string -> obj
    val ownershipRoot: obj
    val ownershipAttached: owner: string -> attachment: string -> obj
    val attachment: kind: string -> obj
    val bookkeeperAttachment: transactionId: string -> obj
    val dedicatedExecutionClass: string
    val dedicatedOwnership: owner: string -> role: string -> obj
    val dedicatedAttachment: role: string -> string
    val strengthExecutionClass: string
    val strengthOwnership: owner: string -> obj
    val isStrengthReplicaAttachment: kind: string -> bool
    val satelliteKinds: string array
