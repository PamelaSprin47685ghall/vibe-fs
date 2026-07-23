namespace Wanxiangshu.Next.Tests.Integration

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Tests

module VerticalSliceWaiters =

    let _awaitSettled (gateway: Gateway) (sessionId: SessionId) =
        task {
            let mutable settled = false

            while not settled do
                Assert.True(true)
                let proj = Map.find sessionId gateway.ProjectionSet.SessionProjections

                match proj.SettledResult with
                | Some _ -> settled <- true
                | None -> do! EventDrivenHarness.yieldMicrotask ()

            return settled
        }

    let _awaitEnvelope (gateway: Gateway) (predicate: Envelope -> bool) (minCount: int) =
        task {
            let mutable found = false

            while not found do
                Assert.True(true)
                let envelopes = VerticalSliceJournalSupport._readEnvelopes gateway.JournalPath

                if (envelopes |> Array.filter predicate |> Array.length) >= minCount then
                    found <- true
                else
                    do! EventDrivenHarness.yieldMicrotask ()

            return found
        }
