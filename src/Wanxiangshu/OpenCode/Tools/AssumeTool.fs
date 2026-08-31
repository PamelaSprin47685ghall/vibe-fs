namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources
open ToolHostCodec

/// A cognitive commitment point: the caller records the abstraction it has
/// already chosen, then proceeds until materially new evidence appears.
///
/// This tool deliberately has no persistence and grants no authority. Its only
/// consequence is provider-visible reinforcement of the caller's current
/// working assumption.
module AssumeTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/assume/description"

        [<Literal>]
        let ArgAssumption = "tool/assume/arg-assumption"

        [<Literal>]
        let Committed = "tool/assume/committed"

    let private languageOf (ctx: HostToolContext) =
        ProviderLanguageBinding.forSessionText ctx.SessionId

    let private execute (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            let assumption = args.Text "assumption"

            return
                ProviderProse.instructionLines (languageOf ctx) Path.Committed (Map [ "assumption", assumption ])
                |> LlmFacing.renderInstructions
        }

    let admission: ToolAdmission = fun _ r -> r <> Role.Blogger && r <> Role.Distiller

    let spec (factory: HostToolFactory) : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "assume"
          Description = ProviderProse.render language Path.Description Map.empty
          Arguments =
            [ "assumption",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgAssumption Map.empty) factory ]
          Admission = admission
          Execute = execute }
