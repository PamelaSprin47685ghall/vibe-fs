module Wanxiangshu.Kernel.ToolCatalog.Search

open Wanxiangshu.Kernel.ToolCatalog.ToolSpec

let internal fuzzyFindSpec: ToolSpec =
    { name = "fuzzy_find"
      description =
        "Search for files by fuzzy path text matching. Returns file paths ranked by relevance and frecency. Regex and glob syntax are not supported. When more results exist, the tool output carries an `iterator` field for the next page."
      paramDocs =
        map
            [ "pattern",
              """Plain fuzzy file path text to search for. Pass a real JSON array of strings for parallel search; never pass a stringified JSON string. Correct: ["src","build"]. Wrong: "[\"src\",\"build\"]" (a string, not an array)."""
              "path", "Initial optional path constraint to narrow search scope"
              "limit", "Maximum number of results to return per call (default: 30)" ]
      requiredFields = [ "pattern" ] }

let internal fuzzyGrepSpec: ToolSpec =
    { name = "fuzzy_grep"
      description =
        "Search file contents using fuzzy-aware content search. Smart-case, git-aware, frecency-ranked. Supports automatic regex mode detection. Use mode=fuzzy explicitly for fuzzy matching when exact regex yields no results. When more results exist, the tool output carries an `iterator` field for the next page."
      paramDocs =
        map
            [ "pattern",
              """Search pattern. Pass a real JSON array of strings for parallel search; never pass a stringified JSON string. Required on the first call. Correct: ["StateMachine","EventLog"]. Wrong: "[\"StateMachine\",\"EventLog\"]" (a string, not an array)."""
              "path", "Initial path constraint."
              "exclude", "Initial exclude paths (e.g. 'test/,*.min.js')"
              "searchIgnored", "Search git-ignored files such as node_modules by adding the fff git:ignored constraint."
              "caseSensitive", "Case-sensitivity override (smart-case by default)."
              "context", "Number of context lines before and after each match"
              "limit", "Maximum number of matches to return per call" ]
      requiredFields = [ "pattern" ] }

let internal fuzzyContinueSpec: ToolSpec =
    { name = "fuzzy_continue"
      description = "Continue a previously running fuzzy_find or fuzzy_grep session. Returns the next page of results."
      paramDocs = map [ "iterator", "Opaque single-use iterator from a previous search result." ]
      requiredFields = [ "iterator" ] }
