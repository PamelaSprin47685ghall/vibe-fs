// Provider language is ADOPTED EAGERLY by ProviderLanguageBinding at import time; the
// strict mock fixtures are English-only. `014.test.mjs` MUST import this file FIRST
// (before scenario-driver / lane / journal / production surfaces) so any transitive
// read of `WANXIANGSHU_PROVIDER_LANGUAGE` inside the harness sees 'en', not whatever
// the user's shell (e.g. `zh_CN`) had already carried in.
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en';
