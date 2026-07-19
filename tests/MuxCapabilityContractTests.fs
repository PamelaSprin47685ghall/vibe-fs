module Wanxiangshu.Tests.MuxCapabilityContractTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostCapability

let private objectKeys (o: obj) : string array =
    Fable.Core.JS.Constructors.Object.keys (o) |> unbox

let private contains (arr: string array) (item: string) : bool = arr |> Array.exists (fun x -> x = item)

let run () : unit =
    check
        "HostCapability.muxDefault excludes SubsessionDispatch"
        (not (Set.contains HostCapability.SubsessionDispatch muxDefault))

    check
        "HostCapability.muxDefault excludes SubsessionAbort"
        (not (Set.contains HostCapability.SubsessionAbort muxDefault))

    check
        "HostCapability.muxDefault excludes SubsessionReconcile"
        (not (Set.contains HostCapability.SubsessionReconcile muxDefault))

    check "HostCapability.allFull contains SubsessionDispatch" (Set.contains HostCapability.SubsessionDispatch allFull)

    check "HostCapability.allFull contains SubsessionAbort" (Set.contains HostCapability.SubsessionAbort allFull)

    check
        "HostCapability.allFull contains SubsessionReconcile"
        (Set.contains HostCapability.SubsessionReconcile allFull)

    let deps = createObj []
    let registration = Wanxiangshu.Hosts.Mux.PluginRegistration.createRegistration deps
    let keys = objectKeys registration

    check "Mux registration exposes 'capabilities' key" (contains keys "capabilities")

    let caps: string array = registration?capabilities |> unbox

    check "Mux capabilities does not contain 'subsessionDispatch'" (not (contains caps "subsessionDispatch"))

    check "Mux capabilities does not contain 'subsessionAbort'" (not (contains caps "subsessionAbort"))

    check "Mux capabilities does not contain 'subsessionReconcile'" (not (contains caps "subsessionReconcile"))

    check "Mux capabilities contains 'toolCatalog'" (contains caps "toolCatalog")

    check "Mux capabilities contains 'reviewStore'" (contains caps "reviewStore")

    check "HostCapability.allFull contains all 15 capabilities" (Set.count allFull = 15)

    check "HostCapability.muxDefault contains 12 capabilities" (Set.count muxDefault = 12)
