namespace Wanxiangshu.Next.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Next.Tools

module ManagerConfig =

    let configureManager (config: obj) : unit =
        if not (isNull config) then
            let agents =
                if isNull config?agent then
                    let created: obj = createEmpty
                    config?agent <- created
                    created
                else
                    config?agent

            let managerConfig = StaticTools.managerAgentConfig ()
            agents?manager <- managerConfig
            agents?build <- managerConfig
            agents?plan <- managerConfig
            agents?orchestrator <- StaticTools.orchestratorAgentConfig ()
            agents?coder <- StaticTools.coderAgentConfig ()
            let toollessConfig = StaticTools.toollessAgentConfig ()
            agents?blogger <- toollessConfig
            agents?executor <- toollessConfig
            agents?inspector <- StaticTools.inspectorAgentConfig ()
            agents?browser <- StaticTools.browserAgentConfig ()
            agents?meditator <- StaticTools.meditatorAgentConfig ()
            agents?reviewer <- StaticTools.reviewerAgentConfig ()
