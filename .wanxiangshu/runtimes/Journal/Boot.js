
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { EnvelopeModule_compareSortKey, EnvelopeModule_deserialize, Envelope_$reflection } from "./Envelope.js";
import { record_type, string_type, class_type, int64_type, list_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { LocalSeqModule_value, RuntimeIdModule_value, RuntimeIdModule_create, RuntimeId_$reflection } from "../Kernel/Identity.js";
import { join, basename } from "node:path";
import { split, printf, toText, substring } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { op_Addition, equals as equals_1, toInt32_unchecked, compare as compare_1, min, fromFloat64, toInt64_unchecked } from "../fable_modules/fable-library-js.5.6.0/BigInt.js";
import { closeSync, readSync, openSync, readdirSync, statSync, existsSync } from "node:fs";
import { tryFind, ofArray, empty } from "../fable_modules/fable-library-js.5.6.0/Map.js";
import { equals, defaultOf, compare } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { item, map } from "../fable_modules/fable-library-js.5.6.0/Array.js";
import { append, ofArray as ofArray_1, tail, head, map as map_1, reduce, isEmpty, filter, cons, reverse, singleton, empty as empty_1 } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { get_UTF8 } from "../fable_modules/fable-library-js.5.6.0/Encoding.js";
import { defaultArg } from "../fable_modules/fable-library-js.5.6.0/Option.js";

export class BootSnapshot extends Record {
    constructor(Envelopes, Frontier, Diagnostics) {
        super();
        this.Envelopes = Envelopes;
        this.Frontier = Frontier;
        this.Diagnostics = Diagnostics;
    }
}

export function BootSnapshot_$reflection() {
    return record_type("Wanxiangshu.Next.Journal.BootSnapshot", [], BootSnapshot, () => [["Envelopes", list_type(Envelope_$reflection())], ["Frontier", class_type("Microsoft.FSharp.Collections.FSharpMap`2", [RuntimeId_$reflection(), int64_type])], ["Diagnostics", list_type(string_type)]]);
}

function Boot_getRuntimeIdFromFilename(filePath) {
    const name = basename(filePath);
    const idx = name.lastIndexOf(".") | 0;
    return RuntimeIdModule_create((idx > 0) ? substring(name, 0, idx) : name);
}

function Boot_getStatSize(stat) {
    return toInt64_unchecked(fromFloat64(stat.size));
}

export function Boot_captureFrontiers(directory) {
    let array;
    if (!existsSync(directory)) {
        return empty({
            Compare: (x, y) => (compare(x, y) | 0),
        });
    }
    else {
        return ofArray(map((name) => {
            const filePath_1 = join(directory, name);
            return [Boot_getRuntimeIdFromFilename(filePath_1), Boot_getStatSize(statSync(filePath_1))];
        }, (array = readdirSync(directory), array.filter((filePath) => filePath.endsWith(".ndjson")))), {
            Compare: (x_1, y_1) => (compare(x_1, y_1) | 0),
        });
    }
}

function Boot_readPrefixEnvelopes(filePath, frontierBytes) {
    let arg_9, arg_10;
    if (!existsSync(filePath)) {
        return [empty_1(), empty_1()];
    }
    else {
        const readLen = min(frontierBytes, Boot_getStatSize(statSync(filePath)));
        if (compare_1(readLen, 0n) <= 0) {
            return [empty_1(), empty_1()];
        }
        else {
            const fd = openSync(filePath, "r") | 0;
            let res = [empty_1(), empty_1()];
            try {
                const buffer = new Uint8Array(~~toInt32_unchecked(readLen));
                const bytesRead = readSync(fd, buffer, 0, ~~toInt32_unchecked(readLen), defaultOf()) | 0;
                let effectiveBytes;
                if (bytesRead <= 0) {
                    effectiveBytes = (new Uint8Array([]));
                }
                else {
                    let lastNewline = -1;
                    let i = bytesRead - 1;
                    while ((i >= 0) && (lastNewline === -1)) {
                        if (item(i, buffer) === 10) {
                            lastNewline = (i | 0);
                        }
                        i = ((i - 1) | 0);
                    }
                    effectiveBytes = ((lastNewline === -1) ? (new Uint8Array([])) : buffer.slice(0, lastNewline + 1));
                }
                const lines = split(get_UTF8().getString(effectiveBytes), ["\r\n", "\n"], undefined, 1);
                const expectedRuntimeId = Boot_getRuntimeIdFromFilename(filePath);
                const collect = (idx_mut, expectedSeq_mut, acc_mut) => {
                    collect:
                    while (true) {
                        const idx = idx_mut, expectedSeq = expectedSeq_mut, acc = acc_mut;
                        if (idx >= lines.length) {
                            return [reverse(acc), empty_1()];
                        }
                        else {
                            const matchValue = EnvelopeModule_deserialize(item(idx, lines));
                            if (matchValue.tag === 1) {
                                let diag_2;
                                const arg_7 = basename(filePath);
                                diag_2 = toText(printf("Failed to parse line %d in %s: %s"))(idx)(arg_7)(matchValue.fields[0]);
                                return [reverse(acc), singleton(diag_2)];
                            }
                            else {
                                const env = matchValue.fields[0];
                                if (!equals(env.RuntimeId, expectedRuntimeId)) {
                                    let diag;
                                    const arg = basename(filePath);
                                    const arg_1 = RuntimeIdModule_value(expectedRuntimeId);
                                    const arg_2 = RuntimeIdModule_value(env.RuntimeId);
                                    diag = toText(printf("RuntimeId mismatch in %s: expected %s, got %s"))(arg)(arg_1)(arg_2);
                                    return [reverse(acc), singleton(diag)];
                                }
                                else {
                                    const seqVal = LocalSeqModule_value(env.LocalSeq);
                                    if (!equals_1(seqVal, expectedSeq)) {
                                        let diag_1;
                                        const arg_3 = basename(filePath);
                                        diag_1 = toText(printf("LocalSeq anomaly in %s: expected %d, got %d"))(arg_3)(expectedSeq)(seqVal);
                                        return [reverse(acc), singleton(diag_1)];
                                    }
                                    else {
                                        idx_mut = (idx + 1);
                                        expectedSeq_mut = toInt64_unchecked(op_Addition(expectedSeq, 1n));
                                        acc_mut = cons(env, acc);
                                        continue collect;
                                    }
                                }
                            }
                        }
                        break;
                    }
                };
                res = collect(0, 1n, empty_1());
            }
            catch (ex) {
                res = [empty_1(), singleton((arg_9 = basename(filePath), (arg_10 = ex.message, toText(printf("IO error reading %s: %s"))(arg_9)(arg_10))))];
            }
            try {
                closeSync(fd);
            }
            catch (matchValue_1) {
            }
            return res;
        }
    }
}

export function Boot_kWayMerge(streams) {
    const merge = (queues_mut, acc_mut) => {
        merge:
        while (true) {
            const queues = queues_mut, acc = acc_mut;
            const active = filter((arg) => !isEmpty(arg), queues);
            if (isEmpty(active)) {
                return reverse(acc);
            }
            else {
                const minHeadEnv = reduce((acc_1, env) => {
                    if (EnvelopeModule_compareSortKey(env, acc_1) < 0) {
                        return env;
                    }
                    else {
                        return acc_1;
                    }
                }, map_1(head, active));
                const pickAndRemove = (headsList) => {
                    let matchResult, hd_1, rest_1, tl_1, q, rest_2;
                    if (!isEmpty(headsList)) {
                        if (!isEmpty(head(headsList))) {
                            if (EnvelopeModule_compareSortKey(head(head(headsList)), minHeadEnv) === 0) {
                                matchResult = 1;
                                hd_1 = head(head(headsList));
                                rest_1 = tail(headsList);
                                tl_1 = tail(head(headsList));
                            }
                            else {
                                matchResult = 2;
                                q = head(headsList);
                                rest_2 = tail(headsList);
                            }
                        }
                        else {
                            matchResult = 2;
                            q = head(headsList);
                            rest_2 = tail(headsList);
                        }
                    }
                    else {
                        matchResult = 0;
                    }
                    switch (matchResult) {
                        case 0:
                            return [empty_1(), empty_1()];
                        case 1:
                            return [cons(tl_1, rest_1), singleton(hd_1)];
                        default: {
                            const patternInput = pickAndRemove(rest_2);
                            return [cons(q, patternInput[0]), patternInput[1]];
                        }
                    }
                };
                const patternInput_1 = pickAndRemove(active);
                queues_mut = patternInput_1[0];
                acc_mut = cons(head(patternInput_1[1]), acc);
                continue merge;
            }
            break;
        }
    };
    return merge(streams, empty_1());
}

export function Boot_boot(directory) {
    let array;
    const frontier = Boot_captureFrontiers(directory);
    if (!existsSync(directory)) {
        return new BootSnapshot(empty_1(), empty({
            Compare: (x, y) => (compare(x, y) | 0),
        }), empty_1());
    }
    else {
        let allDiags = empty_1();
        return new BootSnapshot(Boot_kWayMerge(ofArray_1(map((filename) => {
            const filePath = join(directory, filename);
            const patternInput = Boot_readPrefixEnvelopes(filePath, defaultArg(tryFind(Boot_getRuntimeIdFromFilename(filePath), frontier), 0n));
            allDiags = append(allDiags, patternInput[1]);
            return patternInput[0];
        }, (array = readdirSync(directory), array.filter((f) => f.endsWith(".ndjson")))))), frontier, allDiags);
    }
}

