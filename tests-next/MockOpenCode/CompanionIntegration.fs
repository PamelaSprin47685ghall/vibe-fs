namespace Wanxiangshu.Next.Tests.MockOpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.EventDrivenHarness

module CompanionIntegration =

    let private blogModel =
        Ok
            { providerID = "test"
              modelID = "blogger"
              variant = Some "fast" }
        : Result<OpencodeModel, string>

    let private trueThat condition message =
        if not condition then
            failwith message

    /// CompanionHost through InjectedSessionPort + MockOpenCodePort:
    /// blogger child session created and prompt sent through mock port.
    /// Covers the IsessionHostPort -> IOpenCodePort pipeline for
    /// CreateChildSession and SendPrompt.
    let ``Companion creates blogger through mock port`` () =
        task {
            let state, eventPort, sessionPort = MockOpenCode.createHost ()

            // Auto-complete prompts sent to blogger child via event port
            state.SendHandler <-
                Some(fun sId text opts ->
                    task {
                        let mid = MessageId.create ("c-" + Guid.NewGuid().ToString("N").Substring(0, 6))

                        let fakeResult: AgentRunResult =
                            { SessionId = sId
                              RootUserMessageId = mid
                              AssistantMessageId = mid
                              Role = "blogger"
                              Directory = ""
                              FinalText = "done"
                              Parts = [||] }

                        eventPort.NotifyTerminal sId (TerminalOutcome.Completed fakeResult) |> ignore
                        return Delivered mid
                    })

            let sid = SessionId.create "comp-primary"
            let companion = new CompanionHost(sid, sessionPort, ?bloggerModel = Some blogModel)

            // SubmitProjection — the first call (no baseline) returns Submitted
            // but does not trigger the blogger. The baseline is set implicitly.
            // This test verifies that session creation goes through MockOpenCodePort.
            let outcome = companion.SubmitProjection("{\"step\":1}")
            do! companion.WaitInFlightAsync()
            do! drainMicrotasks 32

            // Blogger child session was created through MockOpenCodePort.CreateChildSession
            trueThat (state.Created.Length > 0) "Companion must create a blogger child session via MockOpenCodePort"
            trueThat (state.Sent.Length > 0) "Companion must send prompt via MockOpenCodePort"
        }
