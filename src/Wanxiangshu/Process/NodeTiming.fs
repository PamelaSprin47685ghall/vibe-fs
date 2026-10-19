namespace Wanxiangshu.Process

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

module NodeTiming =

    // 实现唯一来源：wanxiangli/time-node。此模块只做类型桥——
    // 主仓 ITimerPort / IDeadlineHandle / IClockPort 契约不变，调用方零改动。
    [<Import("NodeTime", "wanxiangli/time-node")>]
    let private library: obj = jsNative

    let private call (name: string) (args: obj) : obj =
        emitJsExpr (library, name, args) "$0[$1].apply(null, $2)"

    let timerTask (milliseconds: int) : Task<unit> =
        unbox<Task<unit>> (call "timerTask" (box [| box milliseconds |]))

    let raceExit (exitTask: Task) (milliseconds: int) : Task<bool> =
        // Semicolons: a comma here builds a one-element tuple array, which
        // reaches Time.raceExit as a single array argument.
        unbox<Task<bool>> (call "raceExit" (box [| box exitTask; box milliseconds |]))

    let nodeTimerPort () : ITimerPort =
        let port = call "nodeTimerPort" (box [||])

        { new ITimerPort with
            member _.Delay(milliseconds: int) =
                let handle = emitJsExpr (port, box milliseconds) "$0.Delay($1)"
                let delayTask = unbox<Task> handle?Delay

                { new IDeadlineHandle with
                    member _.Delay = unbox<Task<unit>> delayTask

                    member _.Cancel() =
                        emitJsExpr handle "if (typeof $0.Cancel === 'function') $0.Cancel()" |> ignore }

            member _.Dispose() =
                emitJsExpr port "$0.Dispose()" |> ignore }

    let nodeClockPort () : IClockPort =
        let port = call "nodeClockPort" (box [||])

        { new IClockPort with
            member _.UtcNow() =
                // 库 JS 面返回 JS Date（Fable DateTimeOffset 表示），原样承接。
                unbox<DateTimeOffset> (emitJsExpr port "$0.UtcNow()") }
