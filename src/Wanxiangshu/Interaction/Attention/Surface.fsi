namespace Wanxiangshu.Interaction.Attention

[<RequireQualifiedAccess>]
module AttentionSurface =
    val empty: unit -> obj
    val record: session: string -> occurrence: string -> text: string -> state: obj -> obj

    val resurface: session: string -> learningOccurrence: string -> workIds: string array -> state: obj -> obj

    val pending: session: string -> state: obj -> obj
