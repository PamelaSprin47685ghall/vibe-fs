namespace Wanxiangshu.OpenCode

open System
open System.Text
open System.Threading.Tasks
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Process
open Wanxiangshu.Execution.Delegation.Fork

/// Tail truncation without Distiller runtime for spooled command output.
module Distillation =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let FragmentPrompt = "tool/distill/fragment-prompt"

        [<Literal>]
        let InputTruncated = "tool/distill/input-truncated"

        [<Literal>]
        let CondensationFailed = "tool/distill/condensation-failed"

        [<Literal>]
        let DistilledHeader = "tool/distill/distilled-header"

    type IDistillationRuntime = DistillationRuntime.IDistillationRuntime

    let asDistillationRuntime = DistillationRuntime.asDistillationRuntime
    let ofForkRuntime = DistillationRuntime.ofForkRuntime

    let distillFragmentPrompt (lang: ProviderLanguage) =
        ProviderProse.render lang Path.FragmentPrompt Map.empty

    [<Literal>]
    let AwaitAgentTimeoutMs = 600_000

    let awaitAgentWithPermit (_runtime: IDistillationRuntime) (_agentId: string) : Task<RunCompletion> =
        task {
            return raise (InvalidOperationException "Distiller runtime is deleted")
        }

    let distillSpool (_runtime: IDistillationRuntime) (spoolPath: string) (lang: ProviderLanguage) =
        task {
            let! tail = Spool.readLatestTail Spool.ChunkSizeBytes spoolPath

            if tail.Bytes.Length = 0 then
                return ""
            else
                let rawTail = Encoding.UTF8.GetString tail.Bytes
                if tail.Truncated then
                    return ProviderProse.render lang Path.InputTruncated (Map [ "account", rawTail ])
                else
                    return rawTail
        }
