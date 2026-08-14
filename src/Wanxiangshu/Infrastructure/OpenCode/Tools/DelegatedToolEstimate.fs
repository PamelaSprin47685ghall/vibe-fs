namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity

[<RequireQualifiedAccess>]
module DelegatedToolEstimate =

    let ArgumentPath = "delegation/expected-tool-calls-argument"
    let InvalidPath = "delegation/expected-tool-calls-invalid"

    let decode (args: HostToolArguments) = args.OptionalNonNegativeInteger "expected_tool_calls"

    let schema language factory =
        ToolHostCodec.optionalNonNegativeIntegerSchemaDescribed
            (ProviderProse.render language ArgumentPath Map.empty)
            factory

    let invalid language = ProviderProse.render language InvalidPath Map.empty

    let replaceIfSpecified journal sessionId expectedToolCalls : Task<unit> =
        task {
            match journal, expectedToolCalls with
            | Some durable, Some expected ->
                do! DelegatedToolEstimateLedger.replace durable sessionId expected
            | _ -> ()
        }
