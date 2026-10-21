namespace Wanxiangshu.Execution.Fission.OpenCode

open System.Threading.Tasks
open Wanxiangshu.OpenCode

/// Same-participant physical replacement. Fission never calls the Host session
/// fork endpoint: it creates fresh sibling sessions, starts them from the
/// owner's current session context (when there is one) + exact lane input, then
/// physically interrupts the old present without terminating the logical owner.
module FissionTool =

    /// The product policy of the tool — its prose vocabulary, its prose
    /// rendering and its office entitlement — lives in the product lane
    /// (`FissionToolPolicy` in `Execution/Fission/ToolPolicy.fs(i)`). This
    /// adapter owns only the wire encoding.

    type FissionReceipt =
        { GroupId: string
          LaneCount: int
          Lanes: (int * string) list }

    val admission: ToolAdmission
    val spec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec

    val executeReceipt:
        scope: ToolRuntimeScope ->
        context: HostToolContext ->
        args: HostToolArguments ->
            Task<Result<FissionReceipt, string>>
