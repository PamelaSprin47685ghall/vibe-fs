module Wanxiangshu.Runtime.DelegatedAiSettings

type DelegatedAiSettings =
    { modelString: string option
      thinkingLevel: string option }

let emptySettings: DelegatedAiSettings =
    { modelString = None
      thinkingLevel = None }
