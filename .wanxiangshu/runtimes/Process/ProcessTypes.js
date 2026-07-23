
import { Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { int32_type, record_type, class_type, option_type, union_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";

export class ProcessError extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["SpawnFailed", "ProcessCancelled", "Timeout", "ExecutionFailed"];
    }
}

export function ProcessError_$reflection() {
    return union_type("Wanxiangshu.Next.Process.ProcessError", [], ProcessError, () => [[["reason", string_type]], [["reason", string_type]], [["reason", string_type]], [["reason", string_type]]]);
}

export class ProcessContext extends Record {
    constructor(WorkingDirectory, DefaultTimeout) {
        super();
        this.WorkingDirectory = WorkingDirectory;
        this.DefaultTimeout = DefaultTimeout;
    }
}

export function ProcessContext_$reflection() {
    return record_type("Wanxiangshu.Next.Process.ProcessContext", [], ProcessContext, () => [["WorkingDirectory", option_type(string_type)], ["DefaultTimeout", option_type(class_type("System.TimeSpan"))]]);
}

export class PtyOptions extends Record {
    constructor(Cols, Rows) {
        super();
        this.Cols = (Cols | 0);
        this.Rows = (Rows | 0);
    }
}

export function PtyOptions_$reflection() {
    return record_type("Wanxiangshu.Next.Process.PtyOptions", [], PtyOptions, () => [["Cols", int32_type], ["Rows", int32_type]]);
}

