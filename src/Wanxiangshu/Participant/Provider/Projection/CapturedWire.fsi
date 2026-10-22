namespace Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

/// Host-neutral capture view of a provider wire message (moved out of the OpenCode
/// adapter's `ProviderWireCapture`, CUT-34): the decoded wire part plus the stable Host
/// part addresses, which capture keeps and the ordinary wire projection drops. The OpenCode
/// codec builds these values from raw host JSON; kernel consumers (XTrace capture) only read them.
module CapturedWire =

    type CapturedWirePart =
        { WirePart: WirePart
          HostPartId: HostMessagePartId option
          HostToolPartId: HostToolPartId option }

    type CapturedWireMessage =
        { Role: string
          ProviderRun: ProviderRunIdentity option
          Parts: CapturedWirePart list }
