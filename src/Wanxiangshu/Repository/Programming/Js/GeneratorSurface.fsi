namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

/// JS-native generator boundary. Role and permission labels are vocabulary at
/// this edge; ToolPermission and JsSurface stay inside the owner.
[<RequireQualifiedAccess>]
module JsGeneratorSurface =
    val internal typedFor: role: string -> labels: string array -> language: string -> JsSurface option
    val internal typedRole: role: string -> language: string -> JsSurface option
    val generate: role: string -> permissionLabels: string array -> language: string -> obj
    val generateRole: role: string -> language: string -> obj
    val isGeneratedToolName: role: string -> permissionLabels: string array -> toolName: string -> bool
    val memberBinding: role: string -> permissionLabels: string array -> memberName: string -> obj
    val permissionLabels: role: string -> string array
