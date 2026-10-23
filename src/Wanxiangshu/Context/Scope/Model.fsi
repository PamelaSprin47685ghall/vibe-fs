namespace Wanxiangshu.Context.Scope

open Wanxiangshu.Foundation.Identity

type ScopeSessionState =
    { TurnBudget: int
      ScopeInitialized: bool
      CurrentPromptKey: string
      CurrentPromptTimestamp: float option }

type ScopeResetParams =
    { OldScopeVerboseVerbatimExperience: string option
      NewScopeObjective: string
      NewScopeTurnBudget: int }
