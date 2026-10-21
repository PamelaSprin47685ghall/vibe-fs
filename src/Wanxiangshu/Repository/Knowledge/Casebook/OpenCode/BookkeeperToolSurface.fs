namespace Wanxiangshu.Repository.Knowledge.Casebook.OpenCode

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Host.Contract
open Wanxiangshu.Repository.Programming.Js.OpenCode

/// JS-native adapter boundary for the Bookkeeper's OpenCode tool binding
/// (`JsBookkeeperTool`). Split out of `CasebookBookkeeperSurface` (CUT-40) so the
/// runtime/staging surface stays host-neutral; only these two entries touch
/// `HostToolArguments`/`HostToolContext`/`ToolHostCodec`.
module CasebookBookkeeperToolSurface =

    let runProgram (sessionId: string) (program: string) : Task<string> =
        let args =
            Wanxiangshu.OpenCode.HostToolArguments(createObj [ "program" ==> program ])

        let context: Wanxiangshu.OpenCode.HostToolContext =
            { SessionId = sessionId
              Agent = None
              ToolCallId = None
              ProviderRunId = None
              PromptText = None
              AttachAbort = fun _ -> fun () -> () }

        task {
            let! result = JsBookkeeperTool.execute args context
            return ToolResultBound.bound result
        }

    let contract (toolModule: obj) : obj =
        let spec =
            JsBookkeeperTool.spec (Wanxiangshu.OpenCode.ToolHostCodec.factory toolModule)

        box
            {| name = spec.Name
               description = spec.Description
               argumentNames = spec.Arguments |> List.map fst |> List.toArray |}
