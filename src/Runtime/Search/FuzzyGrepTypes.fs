module Wanxiangshu.Runtime.FuzzyGrepTypes

open Wanxiangshu.Runtime.FuzzySearchSupport

type ResolvedGrep =
    { matches: GrepMatch list
      total: int option
      regexError: string option
      cursor: obj }
