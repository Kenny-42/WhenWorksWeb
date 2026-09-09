// Shared test-setup helper (mirrors the .NET side's Fixtures/ convention: shared test-only
// infrastructure that doesn't correspond to one source file lives here, not under a mirrored
// path). None of wwwroot/js/'s scripts use ES module syntax -- each is a plain IIFE assigning
// to a `window.WW*` global -- and no source file is changed to accommodate testing. This reads
// a script's source text and evaluates it against the current jsdom `window` (vitest's jsdom
// environment makes `window` the global object), the same way the real views load it via a
// plain <script> tag.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import vm from 'node:vm';

const JS_ROOT = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    '../../../WhenWorksWeb/wwwroot/js'
);

export function loadScript(filename) {
    const filePath = path.join(JS_ROOT, filename);
    const source = readFileSync(filePath, 'utf8');
    vm.runInThisContext(source, { filename: filePath });
}
