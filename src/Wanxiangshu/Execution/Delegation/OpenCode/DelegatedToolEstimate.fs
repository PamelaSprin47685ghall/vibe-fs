namespace Wanxiangshu.Execution.Delegation.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module DelegatedToolEstimate =

    let ArgumentPath = "delegation/expected-tool-calls-argument"
    let InvalidPath = "delegation/expected-tool-calls-invalid"

    let decode (args: HostToolArguments) =
        args.OptionalNonNegativeInteger "expected_tool_calls"

    let schema language factory =
        ToolHostCodec.optionalNonNegativeIntegerSchemaDescribed
            (ProviderProse.render language ArgumentPath Map.empty)
            factory

    let invalid language =
        ProviderProse.render language InvalidPath Map.empty
