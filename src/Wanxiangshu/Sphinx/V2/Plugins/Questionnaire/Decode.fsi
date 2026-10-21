namespace Wanxiangshu.Sphinx.V2.Plugins

type DecodeError = { Code: string; Message: string }

module Decode =
    val decodeMaxDiff: obj -> Result<MaxDiffObservation, DecodeError>
