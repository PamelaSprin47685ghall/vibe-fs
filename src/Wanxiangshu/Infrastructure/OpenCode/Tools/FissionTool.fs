namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel.Identity
open ToolHostCodec

/// Fission MVP.
///
/// The provider contract is frozen now (`prompts: string`, one non-empty line
/// per present, at least two). The multi-lane runtime is deliberately deferred:
/// a valid request fails closed with the canonical capacity consequence and no
/// physical lane is created. This keeps the advertised Manager surface honest
/// without pretending the deferred Fission engine already exists.
module FissionTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/fission/description"

        [<Literal>]
        let TooFew = "tool/fission/too-few"

        [<Literal>]
        let Capacity = "tool/fission/capacity"

    let private languageOf (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private execute (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let lang = languageOf ctx
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
                return tomlObjectWithInstructions (ProviderProse.instructionLines lang Path.TooFew Map.empty) []
            else
                // MVP boundary: no partial allocation and no synthetic lane
                // identities. Until the lane engine exists, every admissible
                // request is a truthful all-or-none capacity refusal.
                return tomlObjectWithInstructions (ProviderProse.instructionLines lang Path.Capacity Map.empty) []
        }

    let spec (factory: HostToolFactory) : ToolSpec =
        { Name = "fission"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = [ "prompts", ToolHostCodec.stringSchema factory ]
          Execute = execute }
