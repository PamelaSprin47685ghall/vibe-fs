namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

/// A cognitive commitment point.
///
/// Assume deliberately owns no memory, todo state or compression state. The
/// caller has already finished the abstraction; this tool only makes that
/// commitment explicit before execution continues.
module AssumeTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/assume/description"

        [<Literal>]
        let ArgAssumption = "tool/assume/arg-assumption"

        [<Literal>]
        let Committed = "tool/assume/committed"

    let roleAllowed (role: Role) : bool =
        StaticTools.cognitiveUtilityRoleAllowed role

    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ role -> roleAllowed role)

    let private execute (args: HostToolArguments) (ctx: HostToolContext) =
        task {
            args.Text "assumption" |> ignore

            return ProviderProse.render (ProviderLanguageBinding.forSessionText ctx.SessionId) Path.Committed Map.empty
        }

    let spec (factory: HostToolFactory) : ToolSpec =
        let language = ProviderLanguageBinding.readGlobalPreference ()

        { Name = "assume"
          Description = ProviderProse.render language Path.Description Map.empty
          Arguments =
            [ "assumption",
              ToolHostCodec.stringSchemaDescribed (ProviderProse.render language Path.ArgAssumption Map.empty) factory ]
          Admission = admission
          Execute = execute }
