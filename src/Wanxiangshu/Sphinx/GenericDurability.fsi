namespace Wanxiangshu.Sphinx

open Wanxiangshu.Persistence.EventStore

/// WHAT[EPI-019]: durable codec for generic sphinx_inquiry_* inquiries.
/// Accepted generic transitions become canonical envelopes on one stream per
/// iq_ id; boot materializes the rule-derived Current into a fresh Registry.
module GenericDurability =
    val streamFor: inquiryId: string -> string
    val envelopeId: inquiryId: string -> revision: int -> string

    val encodeStarted: entry: GecInquiry.GecInquiryEntry -> EventEnvelope

    val encodeSubmitted:
        entry: GecInquiry.GecInquiryEntry -> expectedRevision: int -> results: obj list -> EventEnvelope

    val encodeCancelled: entry: GecInquiry.GecInquiryEntry -> EventEnvelope

    val restore: current: GenericIntegrator.SphinxGenericCurrent -> Result<GecInquiry.Registry, string>
