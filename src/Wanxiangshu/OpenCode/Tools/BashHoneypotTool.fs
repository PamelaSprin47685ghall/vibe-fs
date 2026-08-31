namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open ToolHostCodec

/// Coder-visible bash honeypot: no parameters, no shell, only a hard denial.
/// Host's real `bash` stays denied for every managed role (AGENT-007); this tool
/// exists so a Coder that still reaches for a shell gets an explicit scolding
/// instead of a successful execution path.
module BashHoneypotTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/bash-honeypot/description"

        [<Literal>]
        let Denial = "tool/bash-honeypot/denial"

    let private languageOf (ctx: HostToolContext) =
        ProviderLanguageBinding.forSessionText ctx.SessionId

    let private execute (_args: HostToolArguments) (ctx: HostToolContext) =
        task {
            return tomlObjectWithInstructions (ProviderProse.instructionLines (languageOf ctx) Path.Denial Map.empty) []
        }

    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.BashHoneypot)

    let spec: ToolSpec =
        { Name = "bash-honeypot"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = []
          Admission = admission
          Execute = execute }
