namespace Wanxiangshu.Sphinx.V2.Plugins

type ProbeManifest =
    {
        Id: string
        Title: string
        /// The schema its response is decoded against.
        ResponseSchemaId: string
        ResponseSchemaHash: string
        /// Extra response fields beyond the shared probe envelope.
        ResponseFields: string list
        /// Tool capabilities the probe may request. A request is not a grant.
        ToolNeeds: string list
        /// Declared default write permission. Probes are read-only by default.
        DefaultWriteAccess: bool
        /// Maximum response bytes.
        ResponseByteLimit: int
        /// How findings become graph deltas.
        DeltaShape: string list
    }

type ProbeError = { Code: string; Message: string }

module Catalog =
    /// The shared envelope every probe response carries.
    val sharedResponseFields: string list

    /// `applicability` is one of three honest answers, and "not-applicable" is a
    /// successful result rather than a retry signal.
    val applicabilityValues: string list

    /// The 16 probes. The count is not a product target.
    val all: ProbeManifest list

    /// The dynamic wrapper for a question the LLM invents outside the library.
    val openQuestion: ProbeManifest

    val tryFind: string -> Result<ProbeManifest, ProbeError>
    val validate: ProbeManifest -> Result<unit, ProbeError>
