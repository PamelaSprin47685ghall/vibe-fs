namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Xunit
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session

/// Provider-prefix cache gates for Companion epoch freeze.
module CompanionCacheTests =
    let private bloggerModel =
        { providerID = "cheap-provider"
          modelID = "cheap-blogger"
          variant = Some "fast" }

    let private makeHost (nextText: string ref) (condenseText: string) =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let mutable output = [ "history" ]
        let childId = SessionId.create "blogger-cache"

        { new ISessionHostPort with
            member _.SubscribeTerminal(_, listener) =
                terminal <- Some listener

                { new IDisposable with
                    member _.Dispose() = terminal <- None }

            member _.SendPrompt(_, prompt, _) =
                let text = if prompt.Contains("Condense") then condenseText else nextText.Value
                output <- output @ [ text ]

                terminal
                |> Option.iter (fun l -> l childId (Completed(MessageId.create "blog")))

                Task.FromResult(Ok(MessageId.create "accepted"))

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task
            member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
            member _.GetSessionOutput(_) = output }

    let private headText (messages: obj list) =
        let head = List.head messages
        unbox<string> (unbox<obj array> head?parts).[0]?text

    let private msg sessionId id text =
        createObj
            [ "info", box (createObj [ "id", box id; "role", box "user"; "sessionID", box sessionId ])
              "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]

    /// P0: FrozenB stays byte-identical across blog success and Y self-rebase.
    [<Fact>]
    let ``FrozenB_stable_across_blog_and_self_rebase`` () =
        task {
            let nextText = ref "B1"
            let host = makeHost nextText "B-condensed"
            let sid = "freeze-primary"

            let companion =
                new CompanionHost(SessionId.create sid, host, ?bloggerModel = Some(Ok bloggerModel))

            // Establish projection baseline from real messages (not a fake step JSON).
            let baseMsgs = [ msg sid "u1" "old"; msg sid "u2" "mid" ]
            companion.TransformRaw baseMsgs |> ignore
            do! companion.WaitInFlightAsync()
            Assert.Equal(Some "B1", companion.Memory.LatestB)

            Assert.True(companion.EnablePrefixReplacement())
            Assert.True(companion.Memory.ActivePrefixEpoch.IsSome)
            Assert.Equal("B1", companion.Memory.ActivePrefixEpoch.Value.FrozenB)

            // Extended tail keeps the same prefix so watermark > 0 and b-head injects.
            let extended = baseMsgs @ [ msg sid "u3" "tail" ]
            let t1 = companion.TransformRaw extended
            do! companion.WaitInFlightAsync()
            Assert.Equal("companion-b-head", unbox<string> t1.Head?info?id)
            Assert.Equal("B1", headText t1)

            // Blogger accumulates LatestB; FrozenB and injected head stay B1.
            nextText.Value <- "B2"
            let extended2 = extended @ [ msg sid "u4" "more" ]
            let t2 = companion.TransformRaw extended2
            do! companion.WaitInFlightAsync()
            Assert.True(companion.Memory.LatestB.Value.Contains("B2"))
            Assert.Equal("B1", companion.Memory.ActivePrefixEpoch.Value.FrozenB)
            Assert.Equal("B1", headText t2)

            Assert.Equal(Submitted, companion.SelfRebase())
            do! companion.WaitInFlightAsync()
            Assert.Equal(Some "B-condensed", companion.Memory.LatestB)
            Assert.Equal("B1", companion.Memory.ActivePrefixEpoch.Value.FrozenB)

            let t3 = companion.TransformRaw (extended2 @ [ msg sid "u5" "later" ])
            Assert.Equal("B1", headText t3)
        }

    /// Dual-hook safety: second transform with b-head present is a no-op.
    [<Fact>]
    let ``Transform_idempotent_when_b_head_already_present`` () =
        task {
            let nextText = ref "blog paragraph"
            let host = makeHost nextText "x"
            let companions = Dictionary<string, CompanionHost>()
            let gate = obj ()
            let sessionRoles = Dictionary<string, string>()
            sessionRoles.["primary"] <- "manager"
            let sessionBudgets = Dictionary<string, int>()
            sessionBudgets.["primary"] <- 100
            let sid = "primary"

            let companion =
                new CompanionHost(SessionId.create sid, host, ?bloggerModel = Some(Ok bloggerModel))

            companions.[sid] <- companion

            let baseMsgs =
                [| msg sid "u1" "old"
                   msg sid "u2" "mid" |]

            let out1 = createObj [ "messages", box baseMsgs ]
            let inObj = createObj [ "sessionID", box sid; "agent", box "manager" ]

            // First pass: establish baseline + LatestB.
            CompanionTransform.handleCompanionTransform
                companions
                gate
                host
                None
                None
                sessionBudgets
                sessionRoles
                (Ok bloggerModel)
                inObj
                out1

            do! companion.WaitInFlightAsync()
            Assert.True(companion.EnablePrefixReplacement())

            // Second pass with longer tail activates inject of companion-b-head.
            let extended =
                Array.append baseMsgs [| msg sid "u3" "tail" |]

            let out2 = createObj [ "messages", box extended ]

            CompanionTransform.handleCompanionTransform
                companions
                gate
                host
                None
                None
                sessionBudgets
                sessionRoles
                (Ok bloggerModel)
                inObj
                out2

            do! companion.WaitInFlightAsync()
            let after1 = unbox<obj array> out2?messages
            Assert.Equal("companion-b-head", unbox<string> after1.[0]?info?id)

            // Third pass (dual hook): already has b-head → no-op, still one head.
            CompanionTransform.handleCompanionTransform
                companions
                gate
                host
                None
                None
                sessionBudgets
                sessionRoles
                (Ok bloggerModel)
                inObj
                out2

            let after2 = unbox<obj array> out2?messages
            Assert.Equal(after1.Length, after2.Length)

            let heads =
                after2
                |> Array.filter (fun m ->
                    not (isNull m)
                    && not (isNull m?info)
                    && not (isNull m?info?id)
                    && unbox<string> m?info?id = "companion-b-head")

            Assert.Equal(1, heads.Length)
        }
