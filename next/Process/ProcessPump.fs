namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

type JsTcs<'T>() =
    let mutable completed = false
    let mutable resolveFn: ('T -> unit) option = None

    let p =
        Fable.Core.JS.Constructors.Promise.Create(fun res _ -> resolveFn <- Some res)

    member _.Task: Task<'T> = unbox p
    member _.IsCompleted = completed

    member _.SetResult(res: 'T) =
        completed <- true

        match resolveFn with
        | Some f -> f res
        | None -> ()

    member _.TrySetResult(res: 'T) =
        if completed then
            false
        else
            completed <- true

            match resolveFn with
            | Some f ->
                f res
                true
            | None -> false

module ProcessPump =

    let pumpStream (stream: obj) (cancellation: CancellationToken) (maxChars: int) : Task<string * bool> =
        task {
            let tcs = JsTcs<string * bool>()
            let mutable text = ""
            let mutable truncated = false

            if isNull stream then
                return ("", false)
            else
                let onData =
                    fun (chunk: obj) ->
                        let s = unbox<string> (chunk?toString ("utf-8"))

                        if text.Length + s.Length > maxChars then
                            let allowed = Math.Max(0, maxChars - text.Length)

                            if allowed > 0 then
                                text <- text + s.Substring(0, allowed)

                            truncated <- true
                        else
                            text <- text + s

                let onEnd = fun () -> tcs.TrySetResult((text, truncated)) |> ignore

                let onError = fun (_: obj) -> tcs.TrySetResult((text, truncated)) |> ignore

                use reg = cancellation.Register(fun () -> tcs.TrySetResult((text, truncated)) |> ignore)

                stream?on ("data", onData) |> ignore
                stream?on ("end", onEnd) |> ignore
                stream?on ("error", onError) |> ignore

                return! tcs.Task
        }
