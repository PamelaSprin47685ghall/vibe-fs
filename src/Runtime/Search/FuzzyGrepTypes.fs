module Wanxiangshu.Runtime.FuzzyGrepTypes

open Wanxiangshu.Kernel.FuzzyFormat

type ResolvedGrep =
    { matches: GrepMatch list
      total: int option
      regexError: string option
      cursor: obj }
