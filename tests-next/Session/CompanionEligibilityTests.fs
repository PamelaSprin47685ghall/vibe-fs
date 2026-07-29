namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session

module CompanionEligibilityTests =
    [<Emit("(() => { const calls = []; const original = console.error; console.error = (...args) => calls.push(args.map(String).join(' ')); return { calls, restore: () => { console.error = original; } }; })()")>]
    let private captureConsoleError () : obj = jsNative

    [<Emit("$0.restore()")>]
    let private restoreConsoleError (capture: obj) : unit = jsNative

    [<Emit("$0.calls")>]
    let private capturedConsoleErrors (capture: obj) : string array = jsNative

    type private QuietHost() =
        interface ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) = Task.FromResult(Error "unexpected companion prompt")
            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task
            member _.CreateChildSession(_, _) = Task.FromResult(Error "unexpected companion child")
            member _.GetSessionOutput(_) = []

    let private message sessionId =
        createObj
            [ "info",
              box
                  (createObj
                      [ "id", box "u1"
                        "role", box "user"
                        "sessionID", box sessionId ])
              "parts", box [| createObj [ "type", box "text"; "text", box "pending" ] |] ]

    /// Missing Authority is expected for host-internal and pre-authority transforms.
    /// It must remain fail closed without writing a denial into the user's terminal.
    [<Fact>]
    let ``Missing authority stays fail closed without terminal pollution`` () =
        task {
            let sid = "missing-authority"
            let host = QuietHost() :> ISessionHostPort
            let companions = Dictionary<string, CompanionHost>()
            let sessionRoles = Dictionary<string, string>()
            let sessionBudgets = Dictionary<string, int>()
            let sessionOutputLimits = Dictionary<string, int>()
            let budgetStore = CompanionBudgetStore()
            let gate = obj ()
            sessionRoles.[sid] <- "manager"
            let inObj = createObj [ "sessionID", box sid; "agent", box "fast-manager" ]
            let outObj = createObj [ "messages", box [| message sid |] ]
            let capture = captureConsoleError ()

            try
                for _ in 1..3 do
                    CompanionTransform.handleCompanionTransform
                        companions
                        gate
                        host
                        None
                        sessionBudgets
                        sessionOutputLimits
                        budgetStore
                        sessionRoles
                        None
                        inObj
                        outObj
            finally
                restoreConsoleError capture

            Assert.Equal(0, companions.Count)
            Assert.Empty(capturedConsoleErrors capture)
        }
