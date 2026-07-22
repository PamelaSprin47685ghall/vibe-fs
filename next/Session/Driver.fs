namespace Wanxiangshu.Next.Session

open System.Collections.Concurrent
open System.Threading
open Wanxiangshu.Next.Kernel.Identity

type DriverSlot =
    | Idle
    | Running of cancellationSource: CancellationTokenSource

type SessionDriversKey =
    { RuntimeId: RuntimeId
      SessionId: SessionId }

type SessionDrivers() =
    let drivers = new ConcurrentDictionary<SessionDriversKey, DriverSlot>()
    let localEpochs = new ConcurrentDictionary<SessionDriversKey, LocalEpoch>()

    member _.GetLocalEpoch(key: SessionDriversKey) : LocalEpoch = localEpochs.GetOrAdd(key, 0L)

    member _.BumpLocalEpochOnHuman(key: SessionDriversKey) : LocalEpoch =
        localEpochs.AddOrUpdate(key, 1L, (fun _ current -> current + 1L))

    member _.Activate(key: SessionDriversKey, cts: CancellationTokenSource) : bool =
        if drivers.TryAdd(key, Running cts) then
            true
        else
            let rec loop () =
                match drivers.TryGetValue(key) with
                | true, Idle ->
                    if drivers.TryUpdate(key, Running cts, Idle) then
                        true
                    else
                        loop ()
                | true, Running _ -> false
                | false, _ -> if drivers.TryAdd(key, Running cts) then true else loop ()

            loop ()

    member _.Cancel(key: SessionDriversKey) : unit =
        let rec loop () =
            match drivers.TryGetValue(key) with
            | true, Running cts ->
                try
                    cts.Cancel()
                with _ ->
                    ()

                if not (drivers.TryUpdate(key, Idle, Running cts)) then
                    loop ()
            | true, Idle -> ()
            | false, _ -> ()

        loop ()

    member _.Deactivate(key: SessionDriversKey) : unit = drivers.TryRemove(key) |> ignore
