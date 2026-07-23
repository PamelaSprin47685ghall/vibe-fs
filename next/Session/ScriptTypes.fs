namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel

type TodoView =
    { Unfinished: bool
      ProgressStamp: int64 }

type ReviewView =
    { Required: bool
      Round: int
      MaxRound: int
      Verdict: Fact.ReviewVerdict option }

type SessionScriptConfig =
    { FallbackModels: string list
      MaxRetriesPerModel: int
      MaxInvalidRetries: int }
