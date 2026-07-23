
import { Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { class_type, union_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Operators_Lock } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { unwrap } from "../fable_modules/fable-library-js.5.6.0/Option.js";
import { JournalWriter__get_FilePath, JournalWriter_create, JournalWriter__get_LocalSeq, JournalWriter__Append } from "../Journal/Writer.js";
import { Fold_empty, Fold_apply, RuntimeSnapshot, Fold_foldEnvelope } from "../Journal/Fold.js";
import { RuntimeIdModule_create, EventIdModule_create } from "../Kernel/Identity.js";
import { newGuid, toString } from "../fable_modules/fable-library-js.5.6.0/Guid.js";
import { CommitResult$1, JournalFailure } from "../Kernel/Outcome.js";
import { createCancellationToken, cancel } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { concat, printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { mkdirSync, existsSync } from "node:fs";
import { join } from "node:path";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { pid } from "node:process";
import { Boot_boot } from "../Journal/Boot.js";
import { utcNow } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";

export class GatewayError extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["StorageFailed", "BootFailed"];
    }
}

export function GatewayError_$reflection() {
    return union_type("Wanxiangshu.Next.OpenCode.GatewayError", [], GatewayError, () => [[["reason", string_type]], [["reason", string_type]]]);
}

class GatewayModule_GatewayImpl {
    constructor(runtimeId, bootSnapshot, initialProjectionSet, initialRuntimeSnapshot, journalPath, journalWriter, cts) {
        this.runtimeId = runtimeId;
        this.bootSnapshot = bootSnapshot;
        this.journalPath = journalPath;
        this.journalWriter = journalWriter;
        this.cts = cts;
        this.lockObj = {};
        this.currentProjectionSet = initialProjectionSet;
        this.currentRuntimeSnapshot = initialRuntimeSnapshot;
    }
    get RuntimeId() {
        const _ = this;
        return _.runtimeId;
    }
    get BootSnapshot() {
        const _ = this;
        return _.bootSnapshot;
    }
    get ProjectionSet() {
        const _ = this;
        return Operators_Lock(_.lockObj, () => _.currentProjectionSet);
    }
    get RuntimeSnapshot() {
        const _ = this;
        return Operators_Lock(_.lockObj, () => _.currentRuntimeSnapshot);
    }
    get JournalPath() {
        const _ = this;
        return _.journalPath;
    }
    get JournalWriter() {
        const _ = this;
        return unwrap(_.journalWriter);
    }
    Append(stream, turnId, fact) {
        const _ = this;
        const matchValue = _.journalWriter;
        if (matchValue != null) {
            const writer = matchValue;
            const commitRes = JournalWriter__Append(writer, stream, turnId, fact);
            if (commitRes.tag === 1) {
                return commitRes;
            }
            else {
                Operators_Lock(_.lockObj, () => {
                    const updatedProj = Fold_foldEnvelope(_.currentProjectionSet, commitRes.fields[0]);
                    let updatedSnapshot;
                    const inputRecord = _.currentRuntimeSnapshot;
                    updatedSnapshot = (new RuntimeSnapshot(inputRecord.Frontier, updatedProj, inputRecord.OwnRuntimeId, JournalWriter__get_LocalSeq(writer)));
                    _.currentProjectionSet = updatedProj;
                    _.currentRuntimeSnapshot = updatedSnapshot;
                });
                return commitRes;
            }
        }
        else {
            return new CommitResult$1(1, [EventIdModule_create(toString(newGuid(), "N")), new JournalFailure(0, ["JournalWriter not initialized"])]);
        }
    }
    "System.IAsyncDisposable.DisposeAsync"() {
        const _ = this;
        cancel(_.cts);
        const matchValue = _.journalWriter;
        if (matchValue == null) {
            return Promise.resolve();
        }
        else {
            const w = matchValue;
            return w["System.IAsyncDisposable.DisposeAsync"]();
        }
    }
}

function GatewayModule_GatewayImpl_$reflection() {
    return class_type("Wanxiangshu.Next.OpenCode.GatewayModule.GatewayImpl", undefined, GatewayModule_GatewayImpl);
}

function GatewayModule_GatewayImpl_$ctor_Z776095D3(runtimeId, bootSnapshot, initialProjectionSet, initialRuntimeSnapshot, journalPath, journalWriter, cts) {
    return new GatewayModule_GatewayImpl(runtimeId, bootSnapshot, initialProjectionSet, initialRuntimeSnapshot, journalPath, journalWriter, cts);
}

function GatewayModule_createWriterWithRetry(runtimesDir, processId, startedAt, maxAttempts) {
    const loop = (attemptsLeft_mut) => {
        loop:
        while (true) {
            const attemptsLeft = attemptsLeft_mut;
            if (attemptsLeft <= 0) {
                return new FSharpResult$2(1, [new GatewayError(0, [toText(printf("Failed to create JournalWriter after %d attempts due to RuntimeId collision"))(maxAttempts)])]);
            }
            else {
                const runtimeIdStr = toString(newGuid(), "N");
                const runtimeId = RuntimeIdModule_create(runtimeIdStr);
                if (existsSync(join(runtimesDir, runtimeIdStr + ".ndjson"))) {
                    attemptsLeft_mut = (attemptsLeft - 1);
                    continue loop;
                }
                else {
                    try {
                        const patternInput = JournalWriter_create(runtimesDir, runtimeId, processId, startedAt);
                        return new FSharpResult$2(0, [[runtimeId, patternInput[0], patternInput[1]]]);
                    }
                    catch (ex) {
                        return new FSharpResult$2(1, [new GatewayError(0, [ex.message])]);
                    }
                }
            }
            break;
        }
    };
    return loop(maxAttempts);
}

export function GatewayModule_start(baseDir, cancellationToken) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => {
        let value;
        const runtimesDir = join(baseDir, join(".wanxiangshu-next", "runtimes"));
        return builder$0040.Combine(!existsSync(runtimesDir) ? (((value = mkdirSync(runtimesDir, {
            recursive: true,
        }), void undefined), builder$0040.Zero())) : builder$0040.Zero(), builder$0040.Delay(() => {
            const processId = pid;
            const bootSnapshot = Boot_boot(runtimesDir);
            const projectionSet = Fold_apply(Fold_empty, bootSnapshot.Envelopes);
            const matchValue = GatewayModule_createWriterWithRetry(runtimesDir, processId, utcNow(), 10);
            if (matchValue.tag === 0) {
                const runtimeId = matchValue.fields[0][0];
                const journalWriter = matchValue.fields[0][1];
                const finalProjectionSet = Fold_foldEnvelope(projectionSet, matchValue.fields[0][2]);
                const runtimeSnapshot = new RuntimeSnapshot(bootSnapshot.Frontier, finalProjectionSet, runtimeId, JournalWriter__get_LocalSeq(journalWriter));
                const cts = createCancellationToken();
                const gatewayInstance = GatewayModule_GatewayImpl_$ctor_Z776095D3(runtimeId, bootSnapshot, finalProjectionSet, runtimeSnapshot, JournalWriter__get_FilePath(journalWriter), journalWriter, cts);
                return builder$0040.Return(new FSharpResult$2(0, [gatewayInstance]));
            }
            else {
                return builder$0040.Return(new FSharpResult$2(1, [matchValue.fields[0]]));
            }
        }));
    }), (_arg) => builder$0040.Return(new FSharpResult$2(1, [new GatewayError(1, [concat("[", _arg.message, "]")])])))));
}

