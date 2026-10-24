namespace Wanxiangshu.Host.Domain

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

type EphemeralTurnRequest =
    {
        DelegatorSessionId: string
        Receipt: string
        PromptText: string
        RetryPromptText: string
    }

type SteerDeliveryRequest =
    {
        RecipientSessionId: string
        Receipt: string
        SteerText: string
        CorrelationKey: PromptKey
    }

type INegotiationTransport =
    abstract member CanNegotiate: delegatorSessionId: string -> bool
    abstract member AskEphemeral: request: EphemeralTurnRequest -> Task<Result<string, string>>
    abstract member DeliverAndObserve: request: SteerDeliveryRequest -> Task<Result<unit, string>>

type INegotiationTransportConsumer =
    abstract member AttachTransport: transport: INegotiationTransport -> unit
