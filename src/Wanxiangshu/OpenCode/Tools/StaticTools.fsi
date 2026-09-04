namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module StaticTools =
    val toolNames: p: ToolPermission -> string list
    val toolName: p: ToolPermission -> string
    val jsToolName: role: Role -> string
    val knownToolNames: string list
    val requestToolMap: allowed: Set<ToolPermission> -> Map<string, bool>
    val permissionObj: role: Role -> obj
    val managerAgentConfig: prompt: string option -> obj
    val orchestratorAgentConfig: prompt: string option -> obj
    val coderAgentConfig: prompt: string option -> obj
    val bloggerAgentConfig: prompt: string -> obj
    val distillerAgentConfig: prompt: string -> obj
    val inquiryAgentConfig: prompt: string option -> obj
    val bookkeeperAgentConfig: prompt: string -> obj
    val browserAgentConfig: prompt: string option -> obj
    val inspectorAgentConfig: prompt: string option -> obj
    val devopsAgentConfig: prompt: string option -> obj
