namespace Wanxiangshu.Execution.Delegation.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module DelegatedToolEstimate =
    val ArgumentPath: string
    val InvalidPath: string

    val decode: args: HostToolArguments -> Result<int option, unit>
    val schema: language: ProviderLanguage -> factory: HostToolFactory -> HostSchema
    val invalid: language: ProviderLanguage -> string
