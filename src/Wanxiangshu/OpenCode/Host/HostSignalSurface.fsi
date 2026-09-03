namespace Wanxiangshu.OpenCode

module HostSignalSurface =
    val unwrapEnvelope: raw: obj -> obj
    val envelopeEventType: raw: obj -> string
    val envelopeSessionId: raw: obj -> string
    val envelopeMessageSessionId: raw: obj -> string
    val tryDecode: raw: obj -> obj
    val tryDecodePhysicalExecutionEnd: raw: obj -> obj
    val tryDecodeExactProviderStart: raw: obj -> obj
    val tryDecodeExactProviderTerminal: raw: obj -> obj
    val tryDecodeProviderStepEnd: raw: obj -> obj
    val tryAdapt: owned: string array -> raw: obj -> obj
