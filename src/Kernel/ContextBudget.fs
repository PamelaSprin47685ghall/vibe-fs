module Wanxiangshu.Kernel.ContextBudget

type ContextState =
    { phaseBaseTokens: int64
      backlogTokensAtPhaseStart: int64 }

let F (a: int64) (b: int64) (c: int64) (s: int64) : bool =
    2L * a >= b + s + c

let beginPhase (totalTokens: int64) (totalBytes: int64) (backlogBytes: int64) : ContextState =
    let backlogTokens =
        if totalBytes <= 0L then 0L
        else totalTokens * backlogBytes / totalBytes
    { phaseBaseTokens = totalTokens
      backlogTokensAtPhaseStart = backlogTokens }

let afterSuccessfulTodo (totalTokens: int64) (totalBytes: int64) (backlogBytes: int64) : ContextState =
    beginPhase totalTokens totalBytes backlogBytes

let estimateTokens (currentTextBytes: int) (lastUsage: {| tokenCount: int; textBytes: int |} option) : int option =
    match lastUsage with
    | Some u when u.textBytes > 0 && currentTextBytes >= 0 ->
        let estimated = (int64 u.tokenCount * int64 currentTextBytes) / int64 u.textBytes
        Some (int estimated)
    | _ -> None
