module VibeFs.Kernel.Permission

open VibeFs.Kernel.AgentRole

/// A binary verdict on whether a named tool may run.
type ToolPermission = Allow | Deny

let permissionOfStr = function
    | "allow" -> Ok Allow
    | "deny" -> Ok Deny
    | other -> Error $"Invalid ToolPermission: \"{other}\""

let permissionToStr = function Allow -> "allow" | Deny -> "deny"

/// Canonical tool names — the closed vocabulary the policy engine reasons over.
let canonicalToolNames: string list =
    [ "read"; "write"; "edit"; "executor"; "glob"; "fuzzy_find"; "fuzzy_grep"
      "grep"; "editor"; "greper"; "reverie"; "submit_review"; "submit_review_result"
      "todowrite"; "webfetch"; "websearch"; "browser"; "task"; "patch"
      "stealth-browser-mcp_*" ]

/// A universal permission rule: a declarative statement of policy that the
/// engine evaluates to decide a tool's fate for a given agent.
type UniversalRule =
    | DenyAll of permissionName: string
    | DenyAllExcept of permissionName: string * excludedRoles: AgentRole list
    | AllowForRoles of permissionName: string * includedRoles: AgentRole list

/// Evaluate one rule.  Returns Some(permission, verdict) when the rule applies
/// to this agent, None when the rule is silent.
let evaluate (rule: UniversalRule) (agent: AgentRole) : (string * ToolPermission) option =
    match rule with
    | DenyAll name -> Some(name, Deny)
    | DenyAllExcept (name, excluded) ->
        if List.contains agent excluded then None else Some(name, Deny)
    | AllowForRoles (name, included) ->
        if List.contains agent included then Some(name, Allow) else None

/// Fold every rule into a permission map.  First-write-wins: the earliest rule
/// that speaks for a tool name owns it.
let computePermissions (agent: AgentRole) (rules: UniversalRule seq) : Map<string, ToolPermission> =
    rules
    |> Seq.choose (fun rule -> evaluate rule agent)
    |> Seq.fold
        (fun acc (name, verdict) -> if Map.containsKey name acc then acc else Map.add name verdict acc)
        Map.empty
