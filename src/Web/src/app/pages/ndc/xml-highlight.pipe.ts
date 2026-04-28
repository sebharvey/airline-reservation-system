import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Pipe({ name: 'xmlHighlight', standalone: true })
export class XmlHighlightPipe implements PipeTransform {
  constructor(private sanitizer: DomSanitizer) {}

  transform(xml: string): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(this.process(xml));
  }

  private esc(s: string): string {
    return s
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  private process(xml: string): string {
    const re = /(<\?xml[\s\S]*?\?>)|(<\/[\w:.]+>)|(<[\w:.]+[\s\S]*?\/>)|(<[\w:.]+[\s\S]*?>)|([^<]+)/g;
    let out = '';
    let m: RegExpExecArray | null;

    while ((m = re.exec(xml)) !== null) {
      const [, decl, close, self, open, text] = m;
      if (decl)       out += this.hlDecl(decl);
      else if (close) out += this.hlClose(close);
      else if (self)  out += this.hlTag(self, true);
      else if (open)  out += this.hlTag(open, false);
      else if (text)  out += this.esc(text);
    }

    return out;
  }

  private hlDecl(tag: string): string {
    const inner = tag.slice(5, -2); // strip <?xml and ?>
    return `<span class="xh-p">&lt;?</span><span class="xh-dn">xml</span>${this.hlAttrs(inner)}<span class="xh-p">?&gt;</span>`;
  }

  private hlClose(tag: string): string {
    const name = tag.slice(2, -1); // strip </ and >
    return `<span class="xh-p">&lt;/</span><span class="xh-t">${this.esc(name)}</span><span class="xh-p">&gt;</span>`;
  }

  private hlTag(tag: string, selfClose: boolean): string {
    const m = /^<([\w:.]+)([\s\S]*)>$/.exec(tag);
    if (!m) return this.esc(tag);
    const [, name, rest] = m;
    const attrs = selfClose ? rest.replace(/\s*\/\s*$/, '') : rest;
    const end = selfClose
      ? `<span class="xh-p"> /&gt;</span>`
      : `<span class="xh-p">&gt;</span>`;
    return `<span class="xh-p">&lt;</span><span class="xh-t">${this.esc(name)}</span>${this.hlAttrs(attrs)}${end}`;
  }

  private hlAttrs(s: string): string {
    if (!s.trim()) return this.esc(s);
    const re = /(\s+)([\w:.-]+)(=)("[^"]*")/g;
    let out = '';
    let last = 0;
    let m: RegExpExecArray | null;

    while ((m = re.exec(s)) !== null) {
      if (m.index > last) out += this.esc(s.slice(last, m.index));
      const [, ws, name, eq, val] = m;
      out += ws
        + `<span class="xh-an">${this.esc(name)}</span>`
        + `<span class="xh-p">${eq}</span>`
        + `<span class="xh-av">${this.esc(val)}</span>`;
      last = m.index + m[0].length;
    }

    if (last < s.length) out += this.esc(s.slice(last));
    return out;
  }
}
