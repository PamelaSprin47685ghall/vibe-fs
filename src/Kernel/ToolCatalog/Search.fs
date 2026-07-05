module Wanxiangshu.Kernel.ToolCatalog.Search

open Wanxiangshu.Kernel.ToolCatalog.ToolSpec

let internal fuzzyFindSpec: ToolSpec =
    { name = "fuzzy_find"
      description =
        "Search for files by fuzzy path text matching. Returns file paths ranked by relevance and frecency. Regex and glob syntax are not supported. When more results exist, the YAML front matter includes an iterator item for the next page."
      paramDocs =
        map
            [ "pattern", "Plain fuzzy file path text to search for. Accepts a single string or an array of strings for parallel search."
              "path", "Initial optional path constraint to narrow search scope"
              "limit", "Maximum number of results to return per call (default: 30)"
              "iterator", "Opaque single-use iterator from a previous fuzzy_find result." ]
      requiredFields = [] }

let internal fuzzyGrepSpec: ToolSpec =
    { name = "fuzzy_grep"
      description =
        "Search file contents using fuzzy-aware content search. Smart-case, git-aware, frecency-ranked. Supports automatic regex mode detection. Use mode=fuzzy explicitly for fuzzy matching when exact regex yields no results. When more results exist, the YAML front matter includes an iterator item for the next page."
      paramDocs =
        map
            [ "pattern", "Search pattern. Accepts a single string or an array of strings for parallel search. Required on the first call."
              "path", "Initial path constraint."
              "exclude", "Initial exclude paths (e.g. 'test/,*.min.js')"
              "searchIgnored", "Search git-ignored files such as node_modules by adding the fff git:ignored constraint."
              "caseSensitive", "Case-sensitivity override (smart-case by default)."
              "context", "Number of context lines before and after each match"
              "limit", "Maximum number of matches to return per call"
              "iterator", "Opaque single-use iterator from a previous fuzzy_grep result." ]
      requiredFields = [] }
