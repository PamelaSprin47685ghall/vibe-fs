namespace Wanxiangshu.Process

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

type VirtualTimerPort =
    { Port: ITimerPort
      Advance: int -> unit
      NowMs: unit -> int }

type VirtualClockPort =
    { Port: IClockPort
      AdvanceMs: int -> unit
      Set: DateTimeOffset -> unit }

/// 实现唯一来源：wanxiangli/time 的虚拟时基（CUT-21，验证专用）。
/// 此模块把库 JS 面包装回主仓 ITimerPort / IClockPort 契约，签名不变。
module VirtualTiming =

    [<Import("Time", "wanxiangli/time")>]
    let private library: obj = jsNative

    let private timerPortOf (port: obj) : ITimerPort =
        { new ITimerPort with
            member _.Delay(milliseconds: int) =
                let handle = emitJsExpr (port, box milliseconds) "$0.Delay($1)"
                let delayTask = unbox<Task> handle?Delay

                { new IDeadlineHandle with
                    member _.Delay = unbox<Task<unit>> delayTask

                    member _.Cancel() = emitJsExpr handle "if (typeof $0.Cancel === 'function') $0.Cancel()" |> ignore }

            member _.Dispose() = emitJsExpr port "$0.Dispose()" |> ignore }

    let createVirtualTimerPort () : VirtualTimerPort =
        let instance = library?virtualTimerPort()

        { Port = timerPortOf (unbox<obj> instance?port)
          Advance = fun ms -> instance?advance(ms) |> ignore<obj>
          NowMs = fun () -> unbox<int> (instance?nowMs()) }

    let createVirtualClockPort () : VirtualClockPort =
        let instance = library?virtualClockPort()
        let rawPort = unbox<obj> instance?port

        { Port =
            { new IClockPort with
                member _.UtcNow() = unbox<DateTimeOffset> (emitJsExpr rawPort "$0.UtcNow()") }
          AdvanceMs = fun ms -> instance?advanceMs(ms)
          Set = fun value -> instance?set(value.ToString("O")) }
