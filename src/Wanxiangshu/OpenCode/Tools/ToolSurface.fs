namespace Wanxiangshu.OpenCode

/// JS-native static contracts for capability-owned tools. Dynamic Host schemas
/// remain exercised through the real plugin; this surface exposes only the
/// owner-defined identity and catalog facts needed by semantic unit laws.
module ToolSurface =

    let bashHoneypotContract () : obj =
        box
            {| name = BashHoneypotTool.spec.Name
               description = BashHoneypotTool.spec.Description
               argumentNames = [||] |}

    let chronicleContract () : obj =
        box
            {| name = "chronicle"
               argumentNames = [| "entry"; "tip" |]
               tipCount = ChronicleTool.tipFieldNames () |> List.length |}
