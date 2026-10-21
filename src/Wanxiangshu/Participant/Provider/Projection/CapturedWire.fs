namespace Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

/// Host-neutral capture view of a provider wire message (moved out of the OpenCode
/// adapter's `ProviderWireCapture`, CUT-34): the decoded wire part plus the stable Host
/// part addresses, which capture keeps and the ordinary wire projection drops. The OpenCode
/// codec builds these values from raw host JSON; kernel consumers (XTrace capture) only read them.
module CapturedWire =

    /// One decoded wire part plus the stable Host ToolPart address when present.
    type CapturedWirePart =
        { WirePart: WirePart
          HostPartId: HostMessagePartId option
          HostToolPartId: HostToolPartId option }

    /// A decoded wire message retaining the assistant provider-run identity.
    /// The normal wire projection deliberately omits source addresses; capture
    /// needs them to bind a tool call to its durable XTrace range.
    type CapturedWireMessage =
        { Role: string
          ProviderRun: ProviderRunIdentity option
          Parts: CapturedWirePart list }
