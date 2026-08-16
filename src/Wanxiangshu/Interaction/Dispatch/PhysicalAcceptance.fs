namespace Wanxiangshu.Interaction.Dispatch

open System.Collections.Generic
open Wanxiangshu.Foundation.Identity

/// Process-local waiter for the physical acceptance of an already-claimed PromptKey.
/// It carries no business stage: callers register one callback before transport,
/// and the sole PhysicalAccepted writer completes it exactly once.
module PromptPhysicalAcceptance =

    let private gate = obj ()
    let private callbacks = Dictionary<string, PhysicalUserMessageId -> unit>()

    let register (promptKey: PromptKey) (callback: PhysicalUserMessageId -> unit) =
        lock gate (fun () -> callbacks.[PromptKey.value promptKey] <- callback)

    let cancel (promptKey: PromptKey) =
        lock gate (fun () -> callbacks.Remove(PromptKey.value promptKey) |> ignore)

    let accepted (promptKey: PromptKey) (physicalUserMessageId: PhysicalUserMessageId) =
        let callback =
            lock gate (fun () ->
                match callbacks.TryGetValue(PromptKey.value promptKey) with
                | true, pending ->
                    callbacks.Remove(PromptKey.value promptKey) |> ignore
                    Some pending
                | false, _ -> None)

        callback |> Option.iter (fun notify -> notify physicalUserMessageId)
