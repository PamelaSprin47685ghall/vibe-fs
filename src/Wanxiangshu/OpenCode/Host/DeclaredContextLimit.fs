namespace Wanxiangshu.OpenCode

open System.Collections.Generic

/// The provider's own declaration of how many context tokens the session's
/// model is configured for — read where opencode hands over the resolved model
/// (the chat.params barrier), never derived from our own measurements. It is
/// disclosed to the model as one datum about its state; nothing in the host
/// predicts overflow from it (context-compression [001]).
module DeclaredContextLimit =

    // DSL-MUTABLE: resource — sessionId → declared context tokens
    let private limits = Dictionary<string, int>()

    let record (sessionId: string) (contextTokens: int) : unit = limits.[sessionId] <- contextTokens

    let tryFind (sessionId: string) : int option =
        match limits.TryGetValue sessionId with
        | true, value -> Some value
        | false, _ -> None

    let clearSession (sessionId: string) : unit = limits.Remove sessionId |> ignore

    let clearAll () : unit = limits.Clear()
