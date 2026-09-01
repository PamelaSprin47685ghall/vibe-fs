namespace Wanxiangshu.Interaction.Concern

[<RequireQualifiedAccess>]
module ConcernSurface =
    val empty: unit -> obj

    val subscribe<'a> : owner: string -> occurrence: string -> id: string -> concern: string -> state: 'a -> obj

    val publish<'a> : sender: string -> occurrence: string -> id: string -> message: string -> state: 'a -> obj

    val applyPublishedClaim<'a> :
        sender: string -> occurrence: string -> id: string -> generation: string -> message: string -> state: 'a -> obj

    val applySubscribedClaim<'a> :
        owner: string -> occurrence: string -> id: string -> concern: string -> state: 'a -> obj

    val retire<'a> : owner: string -> id: string -> generation: string -> state: 'a -> obj
    val prepare<'a> : recipient: string -> state: 'a -> obj

    val place<'a> :
        recipient: string -> announcedGenerations: string array -> deliveredMessages: string array -> state: 'a -> obj
