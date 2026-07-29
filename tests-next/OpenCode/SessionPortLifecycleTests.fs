namespace Wanxiangshu.Next.Tests.OpenCode

open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.OpenCode

module SessionPortLifecycleTests =

    [<Fact>]
    let ``Parent cancellation aborts all child sessions and cleans up`` () =
        task {
            let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
            let sessionPort = InjectedSessionPort(None, eventPort) :> ISessionHostPort
            let parentId = SessionId.create "parent-sess"
            let mutable childTerminalCount = 0

            let childOptions: OpenCodeChildOptions =
                { Title = Some "Child 1"
                  Agent = None
                  Directory = None }

            let! childIdRes = sessionPort.CreateChildSession(parentId, childOptions)

            let childId =
                match childIdRes with
                | Ok cId -> cId
                | Error err -> failwith err

            use _subChild =
                sessionPort.SubscribeTerminal(
                    childId,
                    fun _ outcome ->
                        match outcome with
                        | Aborted _ -> childTerminalCount <- childTerminalCount + 1
                        | _ -> ()
                )

            let! _ = sessionPort.AbortSession(parentId)

            Assert.Equal(1, childTerminalCount)
            let childOut = sessionPort.GetSessionOutput(childId)
            Assert.True(childOut |> List.exists (fun line -> line.Contains("Aborted")))
        }

    [<Fact>]
    let ``Nested children belong directly to the family root`` () =
        task {
            let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
            let sessionPort = InjectedSessionPort(None, eventPort) :> ISessionHostPort
            let rootId = SessionId.create "family-root"

            let childOptions: OpenCodeChildOptions =
                { Title = None
                  Agent = None
                  Directory = None }

            let! childResult = sessionPort.CreateChildSession(rootId, childOptions)

            let childId =
                match childResult with
                | Ok value -> value
                | Error error -> failwith error

            let! grandchildResult = sessionPort.CreateChildSession(childId, childOptions)

            let grandchildId =
                match grandchildResult with
                | Ok value -> value
                | Error error -> failwith error

            let mutable childAborted = false
            let mutable grandchildAborted = false

            use _childSubscription =
                sessionPort.SubscribeTerminal(
                    childId,
                    fun _ outcome ->
                        match outcome with
                        | Aborted _ -> childAborted <- true
                        | _ -> ()
                )

            use _grandchildSubscription =
                sessionPort.SubscribeTerminal(
                    grandchildId,
                    fun _ outcome ->
                        match outcome with
                        | Aborted _ -> grandchildAborted <- true
                        | _ -> ()
                )

            let! _ = sessionPort.AbortSession(rootId)

            Assert.True(childAborted)
            Assert.True(grandchildAborted)
        }

    [<Fact>]
    let ``Restored descendants keep the family root`` () =
        task {
            let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
            let rootId = SessionId.create "restored-root"
            let restoredChildId = SessionId.create "restored-child"

            let familyParent sessionId =
                if sessionId = restoredChildId then Some rootId else None

            let sessionPort =
                InjectedSessionPort(None, eventPort, familyParent = familyParent) :> ISessionHostPort

            let childOptions: OpenCodeChildOptions =
                { Title = None
                  Agent = None
                  Directory = None }

            let! created = sessionPort.CreateChildSession(restoredChildId, childOptions)

            let descendantId =
                match created with
                | Ok value -> value
                | Error error -> failwith error

            let mutable descendantAborted = false

            use _subscription =
                sessionPort.SubscribeTerminal(
                    descendantId,
                    fun _ outcome ->
                        match outcome with
                        | Aborted _ -> descendantAborted <- true
                        | _ -> ()
                )

            let! _ = sessionPort.AbortSession(rootId)
            Assert.True(descendantAborted)
        }
