namespace Wanxiangshu.Next.Tests.OpenCode

open Xunit
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

module EventsTests =

    [<Fact>]
    let ``HostEventPort_records_assistant_output_and_exposes_watermark`` () =
        let eventPort = Events.HostEventPort()
        let sessionId = SessionId.create "event-output-session"

        eventPort.RecordSessionOutput sessionId "assistant answer"

        let observation = eventPort :> IEventObservationPort
        Assert.Equal([ "assistant answer" ], observation.GetSessionOutput sessionId)
        Assert.Equal(1, observation.GetSessionOutputWatermark sessionId)
        Assert.Empty(observation.GetSessionOutputSince(sessionId, 1))
        Assert.Equal([ "assistant answer" ], observation.GetSessionOutputSince(sessionId, 0))
