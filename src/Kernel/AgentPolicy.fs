module VibeFs.Kernel.AgentPolicy

open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.Permission

/// A tool map answers, for every canonical tool name, whether a role may use it.
type ToolMap = Map<string, ToolPermission>

let private searchRoles = [ Editor; Greper ]
let private fuzzyFindRoles = [ Editor; Greper; Orchestrator ]

/// The closed, role-specific tool inventories.  These are data, not behaviour.
let private enabledFor role : string list =
    match role with
    | Orchestrator ->
        [ "read"; "editor"; "greper"; "reverie"; "submit_review"; "webfetch"
          "websearch"; "executor"; "browser"; "glob"; "todowrite"; "fuzzy_find" ]
    | Editor ->
        [ "read"; "write"; "edit"; "glob"; "patch"; "fuzzy_find"; "fuzzy_grep" ]
    | Reviewer -> [ "read"; "submit_review_result" ]
    | Greper -> [ "read"; "executor"; "glob"; "fuzzy_find"; "fuzzy_grep" ]
    | Browser -> [ "read"; "stealth-browser-mcp_*" ]
    | Reverie -> [ "read" ]

/// Build the full tool map by marking enabled tools Allow and the rest Deny.
let toolMapFor (role: AgentRole) : ToolMap =
    let enabled = enabledFor role |> Set.ofList
    canonicalToolNames
    |> List.map (fun name -> name, (if Set.contains name enabled then Allow else Deny))
    |> Map.ofList

/// Universal rules apply to every role and are evaluated first.
let universalRules: UniversalRule list =
    [ DenyAll "bash"
      DenyAllExcept ("stealth-browser-mcp_*", [ Browser ])
      DenyAllExcept ("submit_review_result", [ Reviewer ])
      DenyAllExcept ("glob", searchRoles)
      AllowForRoles ("fuzzy_find", fuzzyFindRoles)
      DenyAllExcept ("fuzzy_find", fuzzyFindRoles)
      AllowForRoles ("fuzzy_grep", searchRoles)
      DenyAllExcept ("fuzzy_grep", searchRoles)
      DenyAll "grep"
      DenyAllExcept ("question", [ Orchestrator ])
      DenyAllExcept ("todowrite", [ Orchestrator ]) ]

let defaultPermissions (role: AgentRole) : Map<string, ToolPermission> =
    computePermissions role universalRules

/// The fully-resolved policy a host needs to enforce a role's boundaries.
type EffectivePolicy =
    { role: AgentRole
      tools: ToolMap
      permissions: Map<string, ToolPermission>
      allowedTools: string list
      deniedTools: string list
      deniedPermissions: string list }

let effectivePolicy (role: AgentRole) : EffectivePolicy =
    let tools = toolMapFor role
    let permissions = defaultPermissions role
    let allowed, denied =
        canonicalToolNames
        |> List.choose (fun name -> Map.tryFind name tools |> Option.map (fun p -> name, p))
        |> List.partition (fun (_, p) -> p = Allow)
    { role = role
      tools = tools
      permissions = permissions
      allowedTools = List.map fst allowed
      deniedTools = List.map fst denied
      deniedPermissions =
        permissions |> Map.toList |> List.choose (fun (n, p) -> if p = Deny then Some n else None) }

let effectivePolicyOfStr (value: string) : Result<EffectivePolicy, string> =
    ofString value |> Result.map effectivePolicy
