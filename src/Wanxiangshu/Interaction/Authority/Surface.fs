namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// JS-native prompt identity boundary. Canonical role labels are the only
/// input; the typed SystemPromptId stays private to PromptAuthority.
module Surface =

    let systemPromptIdForRole (roleLabel: string) : string =
        if isNull roleLabel then
            ""
        else
            match Roles.tryParseRole roleLabel with
            | Some role -> PromptAuthority.systemPromptIdFor role |> SystemPromptId.value
            | None -> ""
