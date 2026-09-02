namespace Wanxiangshu.Context.Prefix

[<RequireQualifiedAccess>]
module XWireSurface =

    val coveredPrefixDigest: projection: obj -> cutoff: int -> string

    val presentationHorizon: hasProbe: bool -> string

    val retiredRetryMessageIds: horizon: string -> rawMessages: obj array -> string array

    val replacePrefixByHostIds:
        rawMessages: obj array ->
        coveredHostMessageIds: string array ->
        openingHostMessageId: obj ->
        syntheticMessageId: string ->
        memory: string ->
            obj array

    val suppressHostMessagesByIds: rawMessages: obj array -> hostMessageIds: string array -> obj array

    val transform: input: obj -> obj

    val reconcile: input: obj -> obj
