module Wanxiangshu.Hosts.Omp.FuzzyToolParameters

open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Hosts.Omp.Schema

let fuzzyFindParameters (tb: obj) =
    objectOf
        [| ("pattern",
            strArray
                (paramDoc "fuzzy_find" "pattern"
                 |> Result.defaultValue
                     """Plain fuzzy file path text to search for. Pass a real JSON array of strings for parallel search; never pass a stringified JSON string. Correct: ["src","build"]. Wrong: "[\"src\",\"build\"]" (a string, not an array).""")
                tb)
           ("path",
            opt
                (paramDoc "fuzzy_find" "path"
                 |> Result.defaultValue "Initial optional path constraint to narrow search scope")
                tb
                str)
           ("limit",
            opt
                (paramDoc "fuzzy_find" "limit"
                 |> Result.defaultValue "Maximum number of results to return per call (default: 30)")
                tb
                num) |]
        tb

let private fuzzyGrepExcludeParam (tb: obj) =
    ("exclude",
     optional
         (union
             [| str
                    (paramDoc "fuzzy_grep" "exclude"
                     |> Result.defaultValue "Initial exclude paths (e.g. 'test/,*.min.js')")
                    tb
                strArray
                    (paramDoc "fuzzy_grep" "exclude"
                     |> Result.defaultValue "Initial exclude path or glob")
                    tb |]
             tb)
         tb)

let fuzzyGrepParameters (tb: obj) =
    objectOf
        [| ("pattern",
            strArray
                (paramDoc "fuzzy_grep" "pattern"
                 |> Result.defaultValue
                     """Search pattern. Pass a real JSON array of strings for parallel search; never pass a stringified JSON string. Required on the first call. Correct: ["StateMachine","EventLog"]. Wrong: "[\"StateMachine","EventLog\"]" (a string, not an array).""")
                tb)
           ("path",
            opt
                (paramDoc "fuzzy_grep" "path" |> Result.defaultValue "Initial path constraint.")
                tb
                str)
           fuzzyGrepExcludeParam tb
           ("caseSensitive",
            opt
                (paramDoc "fuzzy_grep" "caseSensitive"
                 |> Result.defaultValue "Case-sensitivity override (smart-case by default).")
                tb
                bool_)
           ("searchIgnored",
            opt
                (paramDoc "fuzzy_grep" "searchIgnored"
                 |> Result.defaultValue
                      "Search git-ignored files such as node_modules by adding the fff git:ignored constraint.")
                tb
                bool_)
           ("context",
            opt
                (paramDoc "fuzzy_grep" "context"
                 |> Result.defaultValue "Number of context lines before and after each match")
                tb
                num)
           ("limit",
            opt
                (paramDoc "fuzzy_grep" "limit"
                 |> Result.defaultValue "Maximum number of matches to return per call.")
                tb
                num) |]
        tb
