namespace Wanxiangshu.Interaction.Dispatch

open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type PromptPhysicalOutcome =
    | Accepted of PhysicalUserMessageId
    | Rejected of string

/// It carries no business stage: callers register one callback before transport,
/// and the sole PhysicalAccepted writer completes it exactly once.
module PromptPhysicalAcceptance =

    [<Emit("(function(){let timer;return Promise.race([$0.then(function(v){return{ok:true,v:v};}),new Promise(function(r){timer=setTimeout(function(){r({ok:false});},$1);})]).finally(function(){clearTimeout(timer);});})()")>]
    let private raceTimeout (task: Task<'T>) (ms: int) : Task<obj> = jsNative

    let private gate = obj ()

    let private callbacks: Dictionary<string, PhysicalUserMessageId -> unit> =
        Dictionary<string, PhysicalUserMessageId -> unit>()

    let private waiters: Dictionary<string, ResizeArray<TaskCompletionSource<PromptPhysicalOutcome>>> =
        Dictionary<string, ResizeArray<TaskCompletionSource<PromptPhysicalOutcome>>>()

    let private trySetResult (tcs: TaskCompletionSource<'T>) (value: 'T) =
        try
            tcs.SetResult value
            true
        with _ ->
            false

    [<Literal>]
    let private DefaultAdmissionTimeoutMs = 10000

    let private parseAdmissionTimeout (value: string) : int =
        match System.Int32.TryParse value with
        | true, parsed -> parsed
        | false, _ -> DefaultAdmissionTimeoutMs

    let private admissionTimeoutFromEnvironment () : int =
        match System.Environment.GetEnvironmentVariable "WANXIANGSHU_ADMISSION_TIMEOUT_MS" with
        | null
        | "" -> DefaultAdmissionTimeoutMs
        | value -> parseAdmissionTimeout value

    let register (promptKey: PromptKey) (callback: PhysicalUserMessageId -> unit) =
        lock gate (fun () -> callbacks.[PromptKey.value promptKey] <- callback)

    let private takeWaiters (promptKey: PromptKey) =
        match waiters.TryGetValue(PromptKey.value promptKey) with
        | true, pending ->
            waiters.Remove(PromptKey.value promptKey) |> ignore
            pending.ToArray()
        | false, _ -> [||]

    let private removeWaiter
        (promptKey: PromptKey)
        (pending: ResizeArray<TaskCompletionSource<PromptPhysicalOutcome>>)
        (waiter: TaskCompletionSource<PromptPhysicalOutcome>)
        =
        let index =
            pending.FindIndex(fun current -> System.Object.ReferenceEquals(current, waiter))

        if index >= 0 then
            pending.RemoveAt index

        if index >= 0 && pending.Count = 0 then
            waiters.Remove(PromptKey.value promptKey) |> ignore

    let private releaseWaiter (promptKey: PromptKey) (waiter: TaskCompletionSource<PromptPhysicalOutcome>) =
        lock gate (fun () ->
            match waiters.TryGetValue(PromptKey.value promptKey) with
            | true, pending -> removeWaiter promptKey pending waiter
            | false, _ -> ())

    let private completeWaiters (pending: TaskCompletionSource<PromptPhysicalOutcome> array) outcome =
        for waiter in pending do
            trySetResult waiter outcome |> ignore

    let private confirmationOutcome (result: obj) =
        if unbox<bool> (result?ok) then
            Some(unbox<PromptPhysicalOutcome> (result?v))
        else
            None

    let cancel (promptKey: PromptKey) =
        let pending =
            lock gate (fun () ->
                callbacks.Remove(PromptKey.value promptKey) |> ignore
                takeWaiters promptKey)

        completeWaiters pending (PromptPhysicalOutcome.Rejected "Cancelled")

    let accepted (promptKey: PromptKey) (physicalUserMessageId: PhysicalUserMessageId) =
        let callback, pending =
            lock gate (fun () ->
                let cb =
                    match callbacks.TryGetValue(PromptKey.value promptKey) with
                    | true, pending ->
                        callbacks.Remove(PromptKey.value promptKey) |> ignore
                        Some pending
                    | false, _ -> None

                cb, takeWaiters promptKey)

        try
            callback |> Option.iter (fun notify -> notify physicalUserMessageId)
        finally
            completeWaiters pending (PromptPhysicalOutcome.Accepted physicalUserMessageId)

    let rejected (promptKey: PromptKey) (reason: string) =
        let pending =
            lock gate (fun () ->
                callbacks.Remove(PromptKey.value promptKey) |> ignore
                takeWaiters promptKey)

        completeWaiters pending (PromptPhysicalOutcome.Rejected reason)

    let awaitConfirmation (promptKey: PromptKey) (timeoutMs: int option) : Task<PromptPhysicalOutcome option> =
        let waiter =
            TaskCompletionSource<PromptPhysicalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

        lock gate (fun () ->
            let pending =
                match waiters.TryGetValue(PromptKey.value promptKey) with
                | true, existing -> existing
                | false, _ ->
                    let created = ResizeArray<TaskCompletionSource<PromptPhysicalOutcome>>()
                    waiters.[PromptKey.value promptKey] <- created
                    created

            pending.Add waiter)

        let ms =
            match timeoutMs with
            | Some m -> m
            | None -> admissionTimeoutFromEnvironment ()

        task {
            try
                let! (res: obj) = raceTimeout waiter.Task ms

                return confirmationOutcome res
            finally
                releaseWaiter promptKey waiter
        }
