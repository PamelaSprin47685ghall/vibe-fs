namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading
open Wanxiangshu.Next.Kernel.Identity

type DriverSlot =
    | Idle
    | Running of cancellationSource: CancellationTokenSource

type SessionDriversKey =
    { RuntimeId: RuntimeId
      SessionId: SessionId }

type SessionDrivers() =
    let drivers = Dictionary<SessionDriversKey, DriverSlot>()
    let localEpochs = Dictionary<SessionDriversKey, LocalEpoch>()
    let lockObj = obj ()

    member _.GetLocalEpoch(key: SessionDriversKey) : LocalEpoch =
        lock lockObj (fun () ->
            match localEpochs.TryGetValue(key) with
            | true, v -> v
            | false, _ ->
                localEpochs.[key] <- 0L
                0L)

    member _.BumpLocalEpochOnHuman(key: SessionDriversKey) : LocalEpoch =
        lock lockObj (fun () ->
            let current =
                match localEpochs.TryGetValue(key) with
                | true, v -> v
                | false, _ -> 0L

            let next = current + 1L
            localEpochs.[key] <- next
            next)

    member _.Activate(key: SessionDriversKey, cts: CancellationTokenSource) : bool =
        lock lockObj (fun () ->
            match drivers.TryGetValue(key) with
            | true, Running _ -> false
            | true, Idle
            | false, _ ->
                drivers.[key] <- Running cts
                true)

    member _.Cancel(key: SessionDriversKey) : unit =
        let ctsOpt =
            lock lockObj (fun () ->
                match drivers.TryGetValue(key) with
                | true, Running cts ->
                    drivers.[key] <- Idle
                    Some cts
                | _ -> None)

        match ctsOpt with
        | Some cts ->
            try
                cts.Cancel()
            with _ ->
                ()

            try
                cts.Dispose()
            with _ ->
                ()
        | None -> ()

    member _.Deactivate(key: SessionDriversKey) : unit =
        let ctsOpt =
            lock lockObj (fun () ->
                match drivers.TryGetValue(key) with
                | true, Running cts ->
                    drivers.Remove(key) |> ignore
                    Some cts
                | true, Idle ->
                    drivers.Remove(key) |> ignore
                    None
                | false, _ -> None)

        match ctsOpt with
        | Some cts ->
            try
                cts.Cancel()
            with _ ->
                ()

            try
                cts.Dispose()
            with _ ->
                ()
        | None -> ()

type SessionDriver(gateway: obj, sessionId: SessionId, inbox: ISessionInbox) =
    let cts = new CancellationTokenSource()

    member _.SessionId = sessionId
    member _.Inbox = inbox
    member _.CancellationToken = cts.Token

    member _.Cancel() =
        try
            cts.Cancel()
        with _ ->
            ()

    interface IDisposable with
        member _.Dispose() =
            try
                cts.Cancel()
            with _ ->
                ()

            try
                cts.Dispose()
            with _ ->
                ()
