
import { newGuid } from "../fable_modules/fable-library-js.5.6.0/Guid.js";
import { add } from "../fable_modules/fable-library-js.5.6.0/Map.js";
import { Auto_generateBoxedEncoder_437914C6, toString, int64 } from "../fable_modules/Thoth.Json.10.5.1/Encode.fs.js";
import { Auto_generateBoxedDecoder_Z6670B51, fromString, int64 as int64_1 } from "../fable_modules/Thoth.Json.10.5.1/Decode.fs.js";
import { empty } from "../fable_modules/Thoth.Json.10.5.1/Extra.fs.js";
import { ExtraCoders } from "../fable_modules/Thoth.Json.10.5.1/Types.fs.js";
import { Fact_$reflection } from "../Kernel/Fact.js";
import { uncurry2 } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";

const extra = new ExtraCoders((() => {
    let copyOfStruct = newGuid();
    return copyOfStruct;
})(), add("System.Int64", [int64, int64_1], empty.Coders));

export function serializeFact(fact) {
    return toString(0, Auto_generateBoxedEncoder_437914C6(Fact_$reflection(), undefined, extra, undefined)(fact));
}

export function deserializeFact(json) {
    const matchValue = fromString(uncurry2(Auto_generateBoxedDecoder_Z6670B51(Fact_$reflection(), undefined, extra)), json);
    if (matchValue.tag === 1) {
        return new FSharpResult$2(1, [matchValue.fields[0]]);
    }
    else {
        return new FSharpResult$2(0, [matchValue.fields[0]]);
    }
}

