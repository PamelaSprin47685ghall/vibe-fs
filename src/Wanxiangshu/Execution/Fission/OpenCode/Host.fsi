namespace Wanxiangshu.Execution.Fission.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Admission

[<RequireQualifiedAccess>]
module FissionHostRequestProjection =

    val projectExternalManaged:
        hasPhysicalParent: (SessionId -> bool) -> intent: ChatAdmissionIntent.Decision -> output: obj -> unit

    val projectPendingManaged:
        hasPhysicalParent: (SessionId -> bool) -> intent: ChatAdmissionIntent.Decision -> output: obj -> unit
