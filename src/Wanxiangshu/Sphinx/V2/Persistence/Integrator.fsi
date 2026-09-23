namespace Wanxiangshu.Sphinx.V2.Persistence

open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
module Integrator =
    /// The `Current` key v2 inquiries are published under.
    val currentKey: string

    /// Rebuilds one typed event body from its wire bytes.
    val bodyOf: Codec.EventBodyWire -> InquiryEventBody

    /// The v2 integration rule. Folds accepted envelopes through the single reducer.
    val rule: IntegrationRule

    /// Reads one published inquiry state.
    val tryState: obj -> InquiryId -> InquiryState option
