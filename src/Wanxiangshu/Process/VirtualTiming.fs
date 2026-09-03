namespace Wanxiangshu.Process

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation

type VirtualTimerPort =
    { Port: ITimerPort
      Advance: int -> unit
      NowMs: unit -> int }

type VirtualClockPort =
    { Port: IClockPort
      AdvanceMs: int -> unit
      Set: DateTimeOffset -> unit }

module VirtualTiming =
    let createVirtualTimerPort () : VirtualTimerPort =
        // DSL-MUTABLE: resource — virtual clock cursor
        let mutable nowMs = 0
        // DSL-MUTABLE: cancellation — port disposed latch
        let mutable disposed = false
        // DSL-MUTABLE: resource — monotonic handle id counter
        let mutable nextId = 0
        // DSL-MUTABLE: resource — timer entry registry
        let entries = ResizeArray<int * int * TaskCompletionSource<unit> * bool ref>()

        let removeId (id: int) =
            let index = entries |> Seq.tryFindIndex (fun (entryId, _, _, _) -> entryId = id)

            match index with
            | Some value -> entries.RemoveAt(value)
            | None -> ()

        let port =
            { new ITimerPort with
                member _.Delay(milliseconds: int) =
                    let completion = TaskCompletionSource<unit>()
                    // DSL-MUTABLE: cancellation — deadline handle cancel flag
                    let cancelled = ref false
                    let id = nextId
                    nextId <- nextId + 1
                    let fireAt = nowMs + max 0 milliseconds
                    entries.Add((id, fireAt, completion, cancelled))

                    { new IDeadlineHandle with
                        member _.Delay = completion.Task

                        member _.Cancel() =
                            if not cancelled.Value then
                                cancelled.Value <- true
                                removeId id }

                member _.Dispose() =
                    disposed <- true
                    entries.Clear() }

        let fireOne (id: int) (completion: TaskCompletionSource<unit>) (cancelled: bool ref) =
            if not cancelled.Value then
                cancelled.Value <- true
                removeId id
                AsyncSupport.trySetResult completion () |> ignore

        let fireDue due =
            for id, _, completion, cancelled in due do
                fireOne id completion cancelled

        let advance (milliseconds: int) =
            if disposed then
                ()
            else
                nowMs <- nowMs + max 0 milliseconds

                let due =
                    entries
                    |> Seq.filter (fun (_, fireAt, _, cancelled) -> not cancelled.Value && fireAt <= nowMs)
                    |> Seq.toList

                fireDue due

        { Port = port
          Advance = advance
          NowMs = fun () -> nowMs }

    let createVirtualClockPort () : VirtualClockPort =
        // DSL-MUTABLE: resource — virtual wall-clock cursor
        let mutable now = DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)

        let port =
            { new IClockPort with
                member _.UtcNow() = now }

        { Port = port
          AdvanceMs = fun ms -> now <- now.AddMilliseconds(float (max 0 ms))
          Set = fun value -> now <- value }
