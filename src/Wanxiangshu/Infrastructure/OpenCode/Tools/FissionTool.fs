namespace Wanxiangshu.OpenCode

open System
open ToolHostCodec

/// Fission MVP.
///
/// The provider contract is frozen now (`prompts: string`, one non-empty line
/// per present, at least two). The multi-lane runtime is deliberately deferred:
/// a valid request fails closed with the canonical capacity consequence and no
/// physical lane is created. This keeps the advertised Manager surface honest
/// without pretending the deferred Fission engine already exists.
module FissionTool =

    let private TooFew = "# Fission needs at least two independent charges."

    let private Capacity =
        "# The world cannot hold all of these presents at once. No fission occurred."

    let private execute (args: HostToolArguments) (_context: HostToolContext) =
        task {
            let prompts = args.Text "prompts"

            let charges =
                if String.IsNullOrWhiteSpace prompts then
                    []
                else
                    prompts.Split('\n')
                    |> Array.map (fun value -> value.Trim())
                    |> Array.filter (String.IsNullOrWhiteSpace >> not)
                    |> Array.toList

            if List.length charges < 2 then
                return tomlObjectWithInstructions [ TooFew ] []
            else
                // MVP boundary: no partial allocation and no synthetic lane
                // identities. Until the lane engine exists, every admissible
                // request is a truthful all-or-none capacity refusal.
                return tomlObjectWithInstructions [ Capacity ] []
        }

    let spec (factory: HostToolFactory) : ToolSpec =
        { Name = "fission"
          Description =
            "Temporarily divide one Manager life into independent presents. Pass one charge per line in prompts. MVP currently fails closed without allocating lanes."
          Arguments = [ "prompts", ToolHostCodec.stringSchema factory ]
          Execute = execute }
