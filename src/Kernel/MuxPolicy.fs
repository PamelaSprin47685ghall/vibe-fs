module VibeFs.Kernel.MuxPolicy

open VibeFs.Kernel.AgentPolicy
open VibeFs.Kernel.AgentRole

/// How a host should adjust a role's tool set: add or remove by pattern.
/// Using string arrays so the shape is a plain JS array at the host boundary.
type MuxPluginToolPolicy = { add: string array; remove: string array }

/// Map canonical permission names to the host's actual tool-name patterns.
let private muxToolPatterns: Map<string, string list> =
    Map.ofList
        [ "read", [ "read" ]
          "write", [ "write" ]
          "edit", [ "file_edit_.*" ]
          "executor", [ "executor" ]
          "glob", [ "glob" ]
          "fuzzy_find", [ "fuzzy_find" ]
          "fuzzy_grep", [ "fuzzy_grep" ]
          "grep", [ "grep" ]
          "editor", [ "editor" ]
          "greper", [ "greper" ]
          "reverie", [ "reverie" ]
          "submit_review", [ "submit_review" ]
          "submit_review_result", [ "submit_review_result" ]
          "webfetch", [ "webfetch"; "web_fetch" ]
          "websearch", [ "websearch"; "web_search" ]
          "browser", [ "browser" ]
          "task", [ "task"; "task_.*" ]
          "todowrite", [ "todo_read"; "todoread"; "todo_write"; "todowrite" ]
          "stealth-browser-mcp_*", [ "stealth_browser_mcp_.*" ]
          "bash", [ "bash"; "bash_.*" ]
          "question", [ "ask_user_question" ] ]

/// Expand canonical names into the host's glob patterns, deduplicated.
let expandPatterns (names: string seq) : string list =
    names
    |> Seq.collect (fun name -> Map.tryFind name muxToolPatterns |> Option.defaultValue [ name ])
    |> Set.ofSeq |> Set.toList

/// Return the denied canonical tool names for a given role string.
let disabledToolsFor (toolName: string) : string list =
    match AgentRole.ofString toolName with
    | Ok role -> (effectivePolicy role).deniedTools
    | Error _ -> []

/// Resolve a role string into the host tool-policy delta.  Unknown roles yield None.
let getPluginToolPolicy (role: string option) : MuxPluginToolPolicy option =
    match effectivePolicyOfStr (defaultArg role "orchestrator") with
    | Error _ -> None
    | Ok policy ->
        Some { add = [||]
               remove = expandPatterns (policy.deniedTools @ policy.deniedPermissions) |> Array.ofList }
