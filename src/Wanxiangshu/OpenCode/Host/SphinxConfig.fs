namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Ablation

module SphinxConfig =

    let private register (config: obj) =
        if isNull config?command then
            config?command <- createObj []

        config?command?sphinx <-
            createObj
                [ "description" ==> "Investigate a question with Sphinx"
                  "subtask" ==> false
                  "template" ==> "[sphinx: handled by the native command hook]\n$ARGUMENTS" ]

    let private apply (config: obj) =
        emitJsStatement config "if ($0.mcp) delete $0.mcp.sphinx"

        if AblationSettings.allowsToolSchema "sphinx" then
            register config
        else
            emitJsStatement config "if ($0.command) delete $0.command.sphinx"

    let configure (config: obj) : unit =
        if not (isNull config) then
            apply config
