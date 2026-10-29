import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { submitOnEnter } from './formKeyboard'
import { validateImportFiles } from './importFileValidation'
import './ProductImportDialog.css'

type ImportError = { row: number; column: string; message: string }
type ImportResult = { success: boolean; addedCount: number; updatedCount: number; errors: ImportError[] }
type Props = {
  warehouses: { id: string; name: string }[]
  send: (path: string, init?: RequestInit) => Promise<Response>
  onClose: () => void
  onImported: (added: number, updated: number) => Promise<void>
}

const columnLabels: Record<string, string> = {
  sku: 'SKU', name: 'Məhsulun adı', category: 'Kateqoriya', price: 'Qiymət',
  quantity: 'Miqdar', unit: 'Ölçü vahidi', file: 'Fayl', warehouseId: 'Anbar', user: 'İstifadəçi',
}

function isImportResult(value: unknown): value is ImportResult {
  if (!value || typeof value !== 'object') return false
  const result = value as Partial<ImportResult>
  return typeof result.success === 'boolean' && typeof result.addedCount === 'number'
    && typeof result.updatedCount === 'number' && Array.isArray(result.errors)
    && result.errors.every((error) => !!error && typeof error.row === 'number'
      && typeof error.column === 'string' && typeof error.message === 'string')
}

function responseError(status: number) {
  if (status === 401) return 'Sessiyanın vaxtı bitib. Yenidən daxil olun.'
  if (status === 403) return 'Excel idxalı üçün məhsul idarəetmə və stoka giriş icazələri tələb olunur.'
  if (status === 404 || status === 405) return 'Excel idxalı endpoint-i işləyən backend-də mövcud deyil. Backend-i yenidən yığıb başladın, sonra bu əməliyyatı təkrarlayın.'
  if (status === 413) return 'Fayl serverin ölçü həddini keçir. Maksimum 5 MB fayl seçin.'
  return `Sorğu tamamlanmadı (${status}). Yenidən cəhd edin.`
}

function FileIcon() {
  return <svg aria-hidden="true" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9Z"/><path d="M14 3v6h6M8 13h8M8 17h5"/></svg>
}

export default function ProductImportDialog({ warehouses, send, onClose, onImported }: Props) {
  const [file, setFile] = useState<File | null>(null)
  const [warehouseId, setWarehouseId] = useState(warehouses.length === 1 ? warehouses[0].id : '')
  const [busy, setBusy] = useState(false)
  const [downloading, setDownloading] = useState(false)
  const [error, setError] = useState('')
  const [result, setResult] = useState<ImportResult | null>(null)
  const [uncertain, setUncertain] = useState(false)
  const [dragActive, setDragActive] = useState(false)
  const dragDepth = useRef(0)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const panelRef = useRef<HTMLElement>(null)
  const submitting = useRef(false)

  useEffect(() => {
    const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    panelRef.current?.focus()
    return () => { document.body.style.overflow = previousOverflow; previousFocus?.focus() }
  }, [])

  useEffect(() => {
    const handleKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { if (!busy) onClose(); return }
      if (event.key !== 'Tab') return
      const controls = panelRef.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), [tabindex="0"]')
      if (!controls?.length) { event.preventDefault(); return }
      const first = controls[0]
      const last = controls[controls.length - 1]
      if (event.shiftKey && (document.activeElement === first || document.activeElement === panelRef.current)) {
        event.preventDefault(); last.focus()
      } else if (!event.shiftKey && (document.activeElement === last || document.activeElement === panelRef.current)) {
        event.preventDefault(); first.focus()
      }
    }
    document.addEventListener('keydown', handleKey)
    return () => document.removeEventListener('keydown', handleKey)
  }, [busy, onClose])

  function selectFiles(files: File[]) {
    if (submitting.current) return
    setResult(null); setError(''); setUncertain(false); setFile(null)
    if (fileInputRef.current) fileInputRef.current.value = ''
    const validationError = validateImportFiles(files)
    if (validationError) { setError(validationError); return }
    setFile(files[0])
  }

  async function downloadTemplate() {
    setDownloading(true); setError('')
    try {
      const response = await send('products/import/template')
      if (!response.ok) throw new Error(responseError(response.status))
      const url = URL.createObjectURL(await response.blob())
      const link = document.createElement('a')
      link.href = url; link.download = 'mal_sablon.xlsx'
      document.body.append(link); link.click(); link.remove()
      window.setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Şablon yüklənmədi.') }
    finally { setDownloading(false) }
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (submitting.current || result?.success || uncertain) return
    if (!file || !warehouseId) { setError('Excel faylını və anbarı seçin.'); return }
    submitting.current = true
    setBusy(true); setError(''); setResult(null)
    const body = new FormData()
    body.append('file', file)
    try {
      const response = await send(`products/import?warehouseId=${encodeURIComponent(warehouseId)}`, { method: 'POST', body })
      const payload: unknown = await response.json().catch(() => null)
      if (isImportResult(payload)) {
        if (response.ok && payload.success) {
          setResult(payload)
          try { await onImported(payload.addedCount, payload.updatedCount) }
          catch { setError('İdxal tamamlandı, siyahı yenilənmədi. Səhifəni yeniləyin. Faylı təkrar idxal etməyin.') }
        } else if (!payload.success) setResult(payload)
        else throw new Error('İdxalın nəticəsi təsdiqlənmədi.')
      } else if (response.status >= 500 || response.ok) throw new Error('İdxalın nəticəsi təsdiqlənmədi.')
      else setError(responseError(response.status))
    } catch {
      setUncertain(true)
      setError('İdxalın nəticəsi alınmadı. Miqdarın təkrar əlavə olunmaması üçün yenidən göndərməzdən əvvəl məhsul siyahısını və stok jurnalını yoxlayın.')
    } finally { setBusy(false); submitting.current = false }
  }

  return createPortal(<div className="modal-backdrop import-backdrop" onDragOver={(event) => { if (Array.from(event.dataTransfer.types).includes('Files')) event.preventDefault() }} onDrop={(event) => { if (Array.from(event.dataTransfer.types).includes('Files')) event.preventDefault() }} onMouseDown={(event) => { if (event.target === event.currentTarget && !busy) onClose() }}>
    <section className="modal-card product-import-dialog" role="dialog" aria-modal="true" aria-labelledby="product-import-title" aria-describedby="product-import-description" tabIndex={-1} ref={panelRef}>
      <div className="modal-heading"><div><span className="modal-kicker">STOCKROOM · TOPLU ƏLAVƏ</span><h2 id="product-import-title">Excel ilə məhsul idxalı</h2><p id="product-import-description">Şablonu doldurun, anbarı seçin və məhsulları birlikdə əlavə edin.</p></div><button className="modal-close" type="button" aria-label="İdxal pəncərəsini bağla" disabled={busy} onClick={onClose}>×</button></div>
      <form onKeyDown={submitOnEnter} onSubmit={(event) => void submit(event)} aria-busy={busy}>
        <div className="modal-body import-body">
          <div className="import-template"><span className="import-template-icon"><FileIcon/></span><div><strong>Hazır şablonla başlayın</strong><p>6 sütun və Azərbaycan dilində nümunə məhsullar.</p></div><button className="secondary-button" type="button" disabled={downloading || busy} onClick={() => void downloadTemplate()}>{downloading ? 'Yüklənir…' : 'Şablonu yüklə'}</button></div>
          <label className={`import-file-zone ${file ? 'has-file' : ''} ${dragActive ? 'is-dragging' : ''}`} aria-disabled={busy}
            onDragEnter={(event) => { if (!Array.from(event.dataTransfer.types).includes('Files')) return; event.preventDefault(); if (!busy) { dragDepth.current++; setDragActive(true) } }}
            onDragOver={(event) => { event.preventDefault(); event.dataTransfer.dropEffect = busy ? 'none' : 'copy' }}
            onDragLeave={(event) => { event.preventDefault(); dragDepth.current = Math.max(0, dragDepth.current - 1); if (dragDepth.current === 0) setDragActive(false) }}
            onDrop={(event) => { event.preventDefault(); event.stopPropagation(); dragDepth.current = 0; setDragActive(false); if (!busy) selectFiles(Array.from(event.dataTransfer.files)) }}>
            <FileIcon/><strong>{dragActive ? 'Faylı buraya buraxın' : file ? file.name : 'Excel faylını buraya sürükləyin'}</strong><span>{file ? `${(file.size / 1024).toLocaleString('az-AZ', { maximumFractionDigits: 1 })} KB · Dəyişmək üçün klikləyin və ya yeni fayl sürükləyin` : 'və ya fayl seçmək üçün klikləyin · .xlsx · Maksimum 5 MB'}</span><input ref={fileInputRef} type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" aria-label="İdxal üçün Excel faylı seçin" disabled={busy} onChange={(event) => { const files = Array.from(event.target.files ?? []); if (files.length) selectFiles(files) }}/></label>
          <label className="field"><span>Stokun əlavə ediləcəyi anbar <b>*</b></span><select value={warehouseId} required disabled={busy || result?.success || warehouses.length === 0} onChange={(event) => { setWarehouseId(event.target.value); setResult(null); setError('') }}><option value="" disabled>Anbar seçin</option>{warehouses.map((warehouse) => <option value={warehouse.id} key={warehouse.id}>{warehouse.name}</option>)}</select>{warehouses.length === 0 && <small>İdxal üçün mövcud anbar olmalıdır. Anbar siyahısı yüklənməyibsə, pəncərəni bağlayıb siyahını yeniləyin.</small>}</label>
          <div className="import-rules"><strong>Faylı göndərməzdən əvvəl</strong><ul><li>Sütunlar: <code>sku, name, category, price, quantity, unit</code>.</li><li>Kateqoriyalar bazada mövcud olmalıdır. Şablondakı nümunələri öz məlumatlarınızla əvəz edin.</li><li>Mövcud SKU üçün ad və qiymət yenilənir, miqdar cari qalığa əlavə olunur.</li><li>Bir sətirdə xəta olsa, bütün idxal rədd edilir. Bu əməliyyat FIFO partiyası yaratmır.</li></ul></div>
          {error && <div className="import-alert" role="alert">{error}</div>}
          {busy && <div className="import-progress" role="status"><span className="button-spinner"/>Fayl yoxlanır və idxal edilir. Pəncərəni açıq saxlayın.</div>}
          {result && <div className={`import-result ${result.success ? 'is-success' : 'is-error'}`} role={result.success ? 'status' : 'alert'}>
            <strong>{result.success ? 'İdxal uğurla tamamlandı' : 'İdxal edilmədi · Heç bir dəyişiklik saxlanmadı'}</strong>
            {result.success ? <div className="import-counts"><span><b>{result.addedCount}</b> yeni məhsul</span><span><b>{result.updatedCount}</b> yenilənən məhsul</span></div> : <><p>{result.errors.length} xəta tapıldı. Faylı düzəldib yenidən seçin.</p><div className="import-errors-scroll" tabIndex={0} role="region" aria-label="İdxal xətaları"><table className="import-errors"><thead><tr><th>Sətir</th><th>Sütun</th><th>Xəta</th></tr></thead><tbody>{result.errors.map((item, index) => <tr key={`${item.row}-${item.column}-${index}`}><td>{item.row || 'Ümumi'}</td><td>{columnLabels[item.column] ?? item.column}</td><td>{item.message}</td></tr>)}</tbody></table></div></>}
          </div>}
        </div>
        <div className="modal-footer"><button className="secondary-button" type="button" disabled={busy} onClick={onClose}>{result?.success ? 'Bağla' : 'Ləğv et'}</button><button className="primary-button" type="submit" disabled={busy || !file || !warehouseId || !!result?.success || uncertain}>{busy ? 'İdxal edilir…' : result?.success ? 'Tamamlandı' : 'Məhsulları idxal et'}</button></div>
      </form>
    </section>
  </div>, document.body)
}
