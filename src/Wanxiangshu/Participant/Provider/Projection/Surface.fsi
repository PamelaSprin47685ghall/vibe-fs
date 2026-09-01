namespace Wanxiangshu.Participant.Provider.Projection

[<RequireQualifiedAccess>]
module ProjectionSurface =
    val internal intentToSurfaceValue: intent: ProjectionIntent -> obj
    val replaceMessageBase: payload: obj -> obj
    val insertMessageRows: payload: obj -> obj
    val projectionSnapshot: currentProjection: obj -> obj
    val decodeMessages: rawMessages: obj array -> obj
    val plan: intents: obj array -> obj
    val renderMessages: snapshot: obj -> baseMessages: obj array -> intents: obj array -> obj array
    val renderMessagesWithHostIds: snapshot: obj -> baseMessages: obj array -> intents: obj array -> obj
    val renderWire: messages: obj array -> string
    val semanticProjection: messages: obj array -> obj
    val isAppendOnlyPrefix: previous: obj -> next: obj -> bool
    val renderSemantic: projection: obj -> string
    val semanticallyEqual: left: obj -> right: obj -> bool
    val cutoffDigest: sha256: (string -> string) -> snapshot: obj -> cutoff: int -> string
    val sealDigest: sha256: (string -> string) -> projection: obj -> string
    val toolResultDigests: sha256: (string -> string) -> projection: obj -> string array
    val pureContractNames: string array
