module VibeFs.Kernel.ToolCatalog.Web

open VibeFs.Kernel.ToolCatalog.ToolSpec

let internal websearchSpec: ToolSpec =
    { name = "websearch"
      description =
        "Search the web for any topic; raw results are rewritten by a summarizer subagent focused on what_to_summarize, returning clean, ready-to-use content."
      paramDocs =
        map
            [ "query",
              "Natural language search query. Should be a semantically rich description of the ideal page, not just keywords."
              "numResults", "Number of search results to return (default: 10)"
              "what_to_summarize", "The question or intent the search should answer." ]
      requiredFields = [ "query"; "what_to_summarize" ] }

let internal webfetchSpec: ToolSpec =
    { name = "webfetch"
      description =
        "Fetch a URL with better extraction for static/docs pages. Supports llms.txt probing, content-focused HTML extraction, metadata, and redirects."
      paramDocs =
        map
            [ "url", "The URL to fetch"
              "extract_main", "Extract main content from the page, removing navigation, ads, etc. (default: true)"
              "prefer_llms_txt", "Probe for llms.txt files before fetching full page (default: auto)"
              "prompt", "Optional extraction task to run on the fetched content using a cheap secondary model"
              "timeout", "Timeout in seconds (max: 120)" ]
      requiredFields = [ "url" ] }
