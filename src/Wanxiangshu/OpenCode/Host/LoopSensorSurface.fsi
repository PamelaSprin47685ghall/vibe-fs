namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module LoopSensorSurface =
    val create: options: obj -> obj
    val observe: sensor: obj -> raw: obj -> unit
    val consumeAbortCause: sensor: obj -> session: string -> run: string -> Task<obj>
    val activeTask: sensor: obj -> session: string -> run: string -> obj
    val dropSession: sensor: obj -> session: string -> unit
    val resetDetector: sensor: obj -> session: string -> unit
    val textDelta: session: string -> text: string -> messageId: obj -> obj
