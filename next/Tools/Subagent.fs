namespace Wanxiangshu.Next.Tools

open System
open System.Threading.Tasks
open Thoth.Json
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

module SubagentTool =

    let subagentTool (name: string) (role: string) (script: ChildScript) : Tool =
        { Name = name
          Description = sprintf "Spawn subagent %s for role %s" name role
          SchemaJson = """{"type":"object","properties":{"prompt":{"type":"string"}},"required":["prompt"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    if not (SubagentRoles.isAllowed role) then
                        return
                            { Result = sprintf "Permission denied: Role '%s' is not an authorized subagent role. Allowed roles: coder, inspector, browser, meditator, reviewer." role
                              Truncated = false }
                    else
                        let promptText =
                            try
                                let decoder = Decode.field "prompt" Decode.string
                                match Decode.fromString decoder input.Payload with
                                | Ok p -> p
                                | Error _ -> input.Payload
                            with _ ->
                                input.Payload

                        let req: ChildRequest =
                            { Prompt = promptText
                              TargetAgent = Some role
                              ChildId = Some (ChildId.create (sprintf "subagent_%s_%s" role (Guid.NewGuid().ToString("N")))) }

                        let flow = ChildFlows.runChild script req
                        let! res = Flow.run script ctx.Cancellation flow

                        match res with
                        | Ok(CompletedChild out) ->
                            return
                                { Result = sprintf "Subagent %s completed: %s" name out
                                  Truncated = false }
                        | Ok(FailedChild err) ->
                            return
                                { Result = sprintf "Subagent %s failed: %s" name err
                                  Truncated = false }
                        | Error err ->
                            return
                                { Result = sprintf "Subagent %s flow error: %A" name err
                                  Truncated = false }
                } }

    let subagentParallelTool (maxConcurrency: int) (createScript: unit -> ChildScript) (roles: string list) : Tool =
        { Name = "subagent_parallel"
          Description = "Run multiple subagent requests in parallel with ordered output."
          SchemaJson = """{"type":"object","properties":{"prompts":{"type":"array","items":{"type":"string"}}},"required":["prompts"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let prompts =
                        try
                            let decoder = Decode.field "prompts" (Decode.list Decode.string)
                            match Decode.fromString decoder input.Payload with
                            | Ok list -> list
                            | Error _ -> []
                        with _ -> []

                    let unauthorized = roles |> List.filter (not << SubagentRoles.isAllowed)
                    if not (List.isEmpty unauthorized) then
                        return
                            { Result = sprintf "Permission denied for roles: %A. Allowed roles: coder, inspector, browser, meditator, reviewer." unauthorized
                              Truncated = false }
                    else
                        let requests =
                            prompts
                            |> List.mapi (fun idx p ->
                                let r = if idx < roles.Length then Some roles.[idx] else None
                                { Prompt = p
                                  TargetAgent = r
                                  ChildId = Some (ChildId.create (sprintf "parallel_%d_%s" idx (Guid.NewGuid().ToString("N")))) })

                        let dummyScript = createScript ()
                        let flow = ChildFlows.runParallel maxConcurrency createScript requests
                        let! res = Flow.run dummyScript ctx.Cancellation flow

                        match res with
                        | Ok results ->
                            let formatted =
                                results
                                |> List.mapi (fun i r ->
                                    match r with
                                    | CompletedChild out -> sprintf "[%d] Completed: %s" i out
                                    | FailedChild err -> sprintf "[%d] Failed: %s" i err)
                                |> String.concat "\n"

                            return { Result = formatted; Truncated = false }
                        | Error err ->
                            return { Result = sprintf "Parallel subagent flow error: %A" err; Truncated = false }
                } }
