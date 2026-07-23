
import { Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { RuntimeIdModule_value, LocalSeqModule_value, TurnId_$reflection, EventId_$reflection, LocalSeq_$reflection, RuntimeId_$reflection, ProcessId_$reflection, SquadId_$reflection, ChildId_$reflection, SessionId_$reflection } from "../Kernel/Identity.js";
import { record_type, option_type, class_type, union_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Fact_$reflection } from "../Kernel/Fact.js";
import { newGuid } from "../fable_modules/fable-library-js.5.6.0/Guid.js";
import { add } from "../fable_modules/fable-library-js.5.6.0/Map.js";
import { Auto_generateBoxedEncoder_437914C6, toString, int64 } from "../fable_modules/Thoth.Json.10.5.1/Encode.fs.js";
import { Auto_generateBoxedDecoder_Z6670B51, fromString, int64 as int64_1 } from "../fable_modules/Thoth.Json.10.5.1/Decode.fs.js";
import { empty } from "../fable_modules/Thoth.Json.10.5.1/Extra.fs.js";
import { ExtraCoders } from "../fable_modules/Thoth.Json.10.5.1/Types.fs.js";
import { uncurry2, equals } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { compare } from "../fable_modules/fable-library-js.5.6.0/BigInt.js";
import { compareTo } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";
import { compare as compare_1 } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";

export class StreamId extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Workspace", "Session", "Child", "Squad", "Process"];
    }
}

export function StreamId_$reflection() {
    return union_type("Wanxiangshu.Next.Journal.StreamId", [], StreamId, () => [[], [["Item", SessionId_$reflection()]], [["Item", ChildId_$reflection()]], [["Item", SquadId_$reflection()]], [["Item", ProcessId_$reflection()]]]);
}

export class Envelope extends Record {
    constructor(RuntimeId, LocalSeq, ObservedAt, EventId, Stream, TurnId, Fact) {
        super();
        this.RuntimeId = RuntimeId;
        this.LocalSeq = LocalSeq;
        this.ObservedAt = ObservedAt;
        this.EventId = EventId;
        this.Stream = Stream;
        this.TurnId = TurnId;
        this.Fact = Fact;
    }
}

export function Envelope_$reflection() {
    return record_type("Wanxiangshu.Next.Journal.Envelope", [], Envelope, () => [["RuntimeId", RuntimeId_$reflection()], ["LocalSeq", LocalSeq_$reflection()], ["ObservedAt", class_type("System.DateTimeOffset")], ["EventId", EventId_$reflection()], ["Stream", StreamId_$reflection()], ["TurnId", option_type(TurnId_$reflection())], ["Fact", Fact_$reflection()]]);
}

const EnvelopeModule_extra = new ExtraCoders((() => {
    let copyOfStruct = newGuid();
    return copyOfStruct;
})(), add("System.Int64", [int64, int64_1], empty.Coders));

export function EnvelopeModule_compareSortKey(a, b) {
    if (equals(a.RuntimeId, b.RuntimeId)) {
        return compare(LocalSeqModule_value(a.LocalSeq), LocalSeqModule_value(b.LocalSeq)) | 0;
    }
    else {
        const cmpObs = compareTo(a.ObservedAt, b.ObservedAt) | 0;
        if (cmpObs !== 0) {
            return cmpObs | 0;
        }
        else {
            return compare_1(RuntimeIdModule_value(a.RuntimeId), RuntimeIdModule_value(b.RuntimeId), 4) | 0;
        }
    }
}

export function EnvelopeModule_serialize(env) {
    return toString(0, Auto_generateBoxedEncoder_437914C6(Envelope_$reflection(), undefined, EnvelopeModule_extra, undefined)(env));
}

export function EnvelopeModule_deserialize(json) {
    const matchValue = fromString(uncurry2(Auto_generateBoxedDecoder_Z6670B51(Envelope_$reflection(), undefined, EnvelopeModule_extra)), json);
    if (matchValue.tag === 1) {
        return new FSharpResult$2(1, [matchValue.fields[0]]);
    }
    else {
        return new FSharpResult$2(0, [matchValue.fields[0]]);
    }
}

