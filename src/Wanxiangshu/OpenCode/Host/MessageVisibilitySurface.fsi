namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module MessageVisibilitySurface =
    val create: timerPort: obj -> obj
    val notify: handle: obj -> sessionId: string -> unit
    val awaitChange: handle: obj -> sessionId: string -> budgetMilliseconds: int -> Task<unit>
    val pendingCount: handle: obj -> sessionId: string -> int
