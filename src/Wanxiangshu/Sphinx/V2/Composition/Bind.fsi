namespace Wanxiangshu.Sphinx.V2.Composition

open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Persistence

[<RequireQualifiedAccess>]
module Bind =
    /// The v2 rule list, ready for `CanonicalIntegrator.createWithRules`.
    val rules: IntegrationRule list

    /// The `Current` key v2 publishes under.
    val currentKey: string

    /// Builds a local EventStore bound to the canonical spine with the v2 rule. There is
    /// no memory fallback: an inquiry whose facts cannot survive a restart is not one.
    val createDurableStore: commonDir: string -> writerId: string -> Result<IEventStore, string>

    /// Reads the published state for one inquiry.
    val tryInquiry: IEventStore -> InquiryId -> Result<InquiryState option, CurrentError>

    /// Reads the accepted origin-to-head envelope chain from one canonical Current snapshot.
    val tryTrace: IEventStore -> InquiryId -> Result<EventEnvelope list option, CurrentError>
