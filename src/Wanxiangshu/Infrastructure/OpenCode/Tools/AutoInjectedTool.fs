namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open ToolHostCodec

/// HOST-013 executable entity for the wire name auto-injected.
/// Empty arguments; execute always returns OK. Not a business capability.
/// PairProgrammingThoughtTransform renders completed historical pairs;
/// this module owns the Tool.Def so a live model call cannot miss an entity.
module AutoInjectedTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/auto-injected/description"

    let spec: ToolSpec =
        { Name = "auto-injected"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = []
          Execute = fun _ _ -> task { return "OK" } }
