/*import { useEffect, useRef } from 'react'
import katex from 'katex'
import 'katex/dist/katex.min.css'

const SYMBOLS = ['√', '∑', '∫', 'π', 'θ', 'α', 'β', 'λ', 'μ', 'σ', 'Δ', '±', '≤', '≥', '≠', '≈', '∞', '→', '⇒', '∈', '∀', '∃', 'x²', 'x³', '½']

type Props = { id: string; html: string; onChange: (html: string, text: string) => void; disabled?: boolean }

export default function RichEditor({ id, html, onChange, disabled }: Props) {
  const ref = useRef<HTMLDivElement>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  // set content only when a different question is shown (keeps the cursor while typing)
  useEffect(() => { if (ref.current) ref.current.innerHTML = html || '' }, [id])

  const emit = () => { const el = ref.current!; onChange(el.innerHTML, el.innerText.trim()) }
  const exec = (cmd: string, val?: string) => { ref.current!.focus(); document.execCommand(cmd, false, val); emit() }

  const table = () => {
    const r = prompt('Table size (rows x columns)', '3x3'); if (!r) return
    const [rows, cols] = r.toLowerCase().split('x').map(n => Math.max(1, Math.min(10, parseInt(n) || 2)))
    let h = '<table>'
    for (let i = 0; i < rows; i++) h += '<tr>' + '<td>&nbsp;</td>'.repeat(cols) + '</tr>'
    exec('insertHTML', h + '</table><br>')
  }
  const formula = () => {
    const tex = prompt('Formula in LaTeX, e.g.  \\int_0^1 x^2\\,dx   or   T(n)=2T(n/2)+n'); if (!tex) return
    const h = katex.renderToString(tex, { throwOnError: false })
    exec('insertHTML', `<span class="formula" contenteditable="false">${h}</span>&nbsp;`)
  }
  const insertImageFile = (f: File) => {
    const rd = new FileReader(); rd.onload = () => exec('insertImage', rd.result as string); rd.readAsDataURL(f)
  }
  const onFile = (e: any) => { const f = e.target.files?.[0]; if (f) insertImageFile(f); e.target.value = '' }
  const onPaste = (e: React.ClipboardEvent) => {
    const imgs = Array.from(e.clipboardData.files || []).filter(f => f.type.startsWith('image/'))
    if (imgs.length) { e.preventDefault(); imgs.forEach(insertImageFile) } else setTimeout(emit, 0)
  }

  return (
    <div>
      {!disabled && (
        <div className="tb noprint">
          <button type="button" className="g" onClick={() => exec('bold')}><b>B</b></button>
          <button type="button" className="g" onClick={() => exec('italic')}><i>I</i></button>
          <button type="button" className="g" onClick={() => exec('subscript')}>x₂</button>
          <button type="button" className="g" onClick={() => exec('superscript')}>x²</button>
          <button type="button" className="g" onClick={table}>▦ Table</button>
          <button type="button" className="g" onClick={() => fileRef.current?.click()}>🖼 Image</button>
          <input type="file" accept="image/*" hidden ref={fileRef} onChange={onFile} />
          <button type="button" className="g" onClick={formula}>ƒx Formula</button>
          {SYMBOLS.map(s => <button type="button" key={s} className="g" onClick={() => exec('insertText', s)}>{s}</button>)}
        </div>
      )}
      <div ref={ref} className="ed" contentEditable={!disabled} suppressContentEditableWarning
           onInput={emit} onPaste={onPaste} />
    </div>
  )
}*/
import { useEffect, useRef, useState } from 'react'
import katex from 'katex'
import 'katex/dist/katex.min.css'

const SYMBOLS = ['√', '∑', '∫', 'π', 'θ', 'α', 'β', 'λ', 'μ', 'σ', 'Δ', '±', '≤', '≥', '≠', '≈', '∞', '→', '⇒', '∈', '∀', '∃', '½']

// Generic building blocks for any subject: [button label, LaTeX inserted at the cursor]
const SNIPPETS: [string, string][] = [
  ['a/b', '\\frac{a}{b}'], ['xᵢ', '_{i}'], ['xⁿ', '^{n}'], ['√x', '\\sqrt{x}'], ['ⁿ√x', '\\sqrt[n]{x}'],
  ['Σ', '\\sum_{i=1}^{n} '], ['∏', '\\prod_{i=1}^{n} '], ['∫', '\\int_{a}^{b} '], ['lim', '\\lim_{x \\to \\infty} '],
  ['log', '\\log '], ['ln', '\\ln '], ['sin', '\\sin '], ['×', '\\times '], ['÷', '\\div '], ['·', '\\cdot '],
  ['≤', '\\le '], ['≥', '\\ge '], ['≠', '\\ne '], ['≈', '\\approx '], ['±', '\\pm '], ['∞', '\\infty '],
  ['→', '\\rightarrow '], ['⇒', '\\Rightarrow '], ['∈', '\\in '], ['α', '\\alpha '], ['β', '\\beta '],
  ['γ', '\\gamma '], ['θ', '\\theta '], ['λ', '\\lambda '], ['μ', '\\mu '], ['σ', '\\sigma '], ['π', '\\pi '],
  ['Δ', '\\Delta '], ['Ω', '\\Omega '], ['vector', '\\vec{v}'], ['bar', '\\overline{x}'], ['text', '\\text{word}'],
  ['matrix', '\\begin{bmatrix} a & b \\\\ c & d \\end{bmatrix}'], ['cases', 'f(x) = \\begin{cases} 1 & x > 0 \\\\ 0 & x \\le 0 \\end{cases}'],
]

type Props = { id: string; html: string; onChange: (html: string, text: string) => void; disabled?: boolean }

export default function RichEditor({ id, html, onChange, disabled }: Props) {
  const ref = useRef<HTMLDivElement>(null)
  const fileRef = useRef<HTMLInputElement>(null)
  const texRef = useRef<HTMLTextAreaElement>(null)
  const savedRange = useRef<Range | null>(null)
  const [fx, setFx] = useState(false)
  const [tex, setTex] = useState('')

  // set content only when a different question is shown (keeps the cursor while typing)
  useEffect(() => { if (ref.current) ref.current.innerHTML = html || '' }, [id])

  const emit = () => { const el = ref.current!; onChange(el.innerHTML, el.innerText.trim()) }

  const saveRange = () => {
    const sel = window.getSelection()
    if (sel && sel.rangeCount && ref.current?.contains(sel.anchorNode)) savedRange.current = sel.getRangeAt(0).cloneRange()
  }
  const restoreRange = () => {
    ref.current!.focus()
    const sel = window.getSelection()
    if (savedRange.current && sel) { sel.removeAllRanges(); sel.addRange(savedRange.current) }
  }
  const exec = (cmd: string, val?: string) => { restoreRange(); document.execCommand(cmd, false, val); saveRange(); emit() }

  const table = () => {
    const r = prompt('Table size (rows x columns)', '3x3'); if (!r) return
    const [rows, cols] = r.toLowerCase().split('x').map(n => Math.max(1, Math.min(10, parseInt(n) || 2)))
    let h = '<table>'
    for (let i = 0; i < rows; i++) h += '<tr>' + '<td>&nbsp;</td>'.repeat(cols) + '</tr>'
    exec('insertHTML', h + '</table><br>')
  }

  // ----- formula builder with live preview -----
  const openFx = () => { saveRange(); setTex(''); setFx(true); setTimeout(() => texRef.current?.focus(), 0) }
  const addSnippet = (snip: string) => {
    const ta = texRef.current!
    const a = ta.selectionStart ?? tex.length, b = ta.selectionEnd ?? tex.length
    const next = tex.slice(0, a) + snip + tex.slice(b)
    setTex(next)
    setTimeout(() => { ta.focus(); ta.selectionStart = ta.selectionEnd = a + snip.length }, 0)
  }
  const insertFormula = () => {
    if (!tex.trim()) { setFx(false); return }
    const h = katex.renderToString(tex, { throwOnError: false })
    setFx(false)
    exec('insertHTML', `<span class="formula" contenteditable="false" data-tex="${tex.replace(/"/g, '&quot;')}">${h}</span>&nbsp;`)
  }
  let preview = ''
  try { preview = tex.trim() ? katex.renderToString(tex, { throwOnError: true, displayMode: true }) : '' }
  catch (e: any) { preview = `<span style="color:#c62828;font-size:12px">${(e.message || 'Formula error').replace(/</g, '&lt;')}</span>` }

  const insertImageFile = (f: File) => {
    const rd = new FileReader(); rd.onload = () => exec('insertImage', rd.result as string); rd.readAsDataURL(f)
  }
  const onFile = (e: any) => { const f = e.target.files?.[0]; if (f) insertImageFile(f); e.target.value = '' }
  const onPaste = (e: React.ClipboardEvent) => {
    const imgs = Array.from(e.clipboardData.files || []).filter(f => f.type.startsWith('image/'))
    if (imgs.length) { e.preventDefault(); saveRange(); imgs.forEach(insertImageFile) } else setTimeout(emit, 0)
  }

  return (
    <div>
      {!disabled && (
        <div className="tb noprint">
          <button type="button" className="g" onMouseDown={e => e.preventDefault()} onClick={() => exec('bold')}><b>B</b></button>
          <button type="button" className="g" onMouseDown={e => e.preventDefault()} onClick={() => exec('italic')}><i>I</i></button>
          <button type="button" className="g" onMouseDown={e => e.preventDefault()} onClick={() => exec('subscript')}>x₂</button>
          <button type="button" className="g" onMouseDown={e => e.preventDefault()} onClick={() => exec('superscript')}>x²</button>
          <button type="button" className="g" onMouseDown={e => e.preventDefault()} onClick={table}>▦ Table</button>
          <button type="button" className="g" onMouseDown={e => { e.preventDefault(); saveRange() }} onClick={() => fileRef.current?.click()}>🖼 Image</button>
          <input type="file" accept="image/*" hidden ref={fileRef} onChange={onFile} />
          <button type="button" className="or" onMouseDown={e => { e.preventDefault(); saveRange() }} onClick={openFx}>ƒx Formula</button>
          {SYMBOLS.map(s => <button type="button" key={s} className="g" onMouseDown={e => e.preventDefault()} onClick={() => exec('insertText', s)}>{s}</button>)}
        </div>
      )}

      {fx && (
        <div className="card noprint" style={{ border: '1px solid var(--ac)', margin: '6px 0' }}>
          <b>Formula builder</b> <span className="mu">– click the buttons or type LaTeX; the preview updates as you type.</span>
          <div className="tb" style={{ marginTop: 6 }}>
            {SNIPPETS.map(([l, sn]) => <button type="button" key={l} className="g" onClick={() => addSnippet(sn)}>{l}</button>)}
          </div>
          <textarea ref={texRef} rows={2} value={tex} onChange={e => setTex(e.target.value)}
                    placeholder="e.g.  W_{avg} = \frac{\sum W_i}{n}" style={{ fontFamily: 'monospace' }}
                    onKeyDown={e => { if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) insertFormula(); if (e.key === 'Escape') setFx(false) }} />
          <div style={{ minHeight: 40, padding: '6px 0' }} dangerouslySetInnerHTML={{ __html: preview || '<span class="mu">Preview appears here</span>' }} />
          <div className="flex">
            <button type="button" onClick={insertFormula}>Insert formula</button>
            <button type="button" className="g" onClick={() => setFx(false)}>Cancel</button>
            <span className="mu">Ctrl+Enter to insert · Esc to cancel</span>
          </div>
        </div>
      )}

      <div ref={ref} className="ed" contentEditable={!disabled} suppressContentEditableWarning
           onInput={emit} onPaste={onPaste} onKeyUp={saveRange} onMouseUp={saveRange} onBlur={saveRange} />
    </div>
  )
}
