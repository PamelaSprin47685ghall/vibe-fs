namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Mission.Review

module NodeFs =
    val readFileSync: path: string * encoding: string -> string
    val writeFileSync: path: string * data: string * encoding: string -> unit
    val existsSync: path: string -> bool
    val statSync: path: string -> obj
    val readdirSync: path: string -> obj
    val renameSync: source: string * destination: string -> unit
    val rmSync: path: string * options: obj -> unit

module StaticTools =
    val toolNames: p: ToolPermission -> string list
    val toolName: p: ToolPermission -> string
    val jsToolName: role: Role -> string
    val knownToolNames: string list
    val requestToolMap: allowed: Set<ToolPermission> -> Map<string, bool>
    val permissionObj: role: Role -> obj
    val reviewerVerdictOfString: value: string -> Result<ReviewGuardVerdict, string>
    val reviewerVerdictSchemaJson: string
    val managerAgentConfig: prompt: string option -> obj
    val orchestratorAgentConfig: prompt: string option -> obj
    val coderAgentConfig: prompt: string option -> obj
    val reviewerAgentConfig: prompt: string option -> obj
    val bloggerAgentConfig: prompt: string -> obj
    val distillerAgentConfig: prompt: string -> obj
    val inquiryAgentConfig: prompt: string option -> obj
    val bookkeeperAgentConfig: prompt: string -> obj
    val browserAgentConfig: prompt: string option -> obj
    val inspectorAgentConfig: prompt: string option -> obj
    val devopsAgentConfig: prompt: string option -> obj
