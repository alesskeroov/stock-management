import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Html5Qrcode, Html5QrcodeSupportedFormats } from 'html5-qrcode'
import './App.css'

type Product = {
  id: string
  name: string
  sku: string
  barcode: string
  description: string
  categoryId: string
  unitPrice: number
  unitOfMeasure: string
  isActive: boolean
  createdAt: string
  updatedAt: string
  quantity?: number
}
type ProductResponse = Omit<Product, 'createdAt' | 'updatedAt'> & Partial<Pick<Product, 'createdAt' | 'updatedAt'>> & {
  CreatedAt?: string
  UpdatedAt?: string
  entryDate?: string
  EntryDate?: string
  lastUpdatedDate?: string
  LastUpdatedDate?: string
}
function normalizeProductDates(product: ProductResponse): Product {
  const createdAt = product.createdAt ?? product.CreatedAt ?? product.entryDate ?? product.EntryDate ?? ''
  const updatedAt = product.updatedAt ?? product.UpdatedAt ?? product.lastUpdatedDate ?? product.LastUpdatedDate ?? createdAt
  return { ...product, createdAt, updatedAt }
}

type Category = { id: string; name: string; description: string; parentCategoryId: string | null }
type ProductForm = Omit<Product, 'id' | 'quantity' | 'createdAt' | 'updatedAt'>
type WarehouseOption = { id: string; name: string }
type BatchInventoryItem = { batchNumber: string; entryDate: string; expiryDate: string | null; remainingQuantity: number; purchasePrice: number }
type InventoryScanResult = { code: string; codeType: string; productId: string; productName: string; sku: string; barcode: string; batchNumber: string | null; warehouseName: string | null; remainingQuantity: number | null; entryDate: string | null; expiryDate: string | null }
type ReceiveStockForm = { warehouseId: string; quantity: string; purchasePrice: string; salePrice: string; entryDate: string; expiryDate: string }
type CategoryForm = Omit<Category, 'id'>
type EmployeeForm = { userName: string; email: string; firstName: string; lastName: string; password: string; role: 'Admin' | 'WarehouseKeeper' | 'Manager' }
type AuthUser = { id: string; userName: string; email: string; firstName: string; lastName: string; roles: string[] }
type AuthSession = { accessToken: string; refreshToken: string; accessTokenExpiresAtUtc: string; user: AuthUser }
type View = 'products' | 'categories'
type Dialog = { kind: 'product'; item?: Product } | { kind: 'stock'; item: Product } | { kind: 'category'; item?: Category } | { kind: 'employee' } | { kind: 'qr' } | null
type DeleteTarget = { kind: View; id: string; name: string }

const emptyProduct: ProductForm = {
  name: '', sku: '', barcode: '', description: '', categoryId: '',
  unitPrice: 0, unitOfMeasure: 'Piece', isActive: true,
}
const emptyCategory: CategoryForm = { name: '', description: '', parentCategoryId: null }
const currency = new Intl.NumberFormat('az-AZ', { style: 'currency', currency: 'AZN', maximumFractionDigits: 2 })
const sessionKey = 'stockroom.auth'
function formatDateTime(value: string | null | undefined, includeTime = true) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('az-AZ', includeTime
    ? { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }
    : { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date)
}
type ProductSort = 'price-desc' | 'price-asc' | 'quantity-asc' | 'name-asc'
type Permission = 'products.read' | 'products.manage' | 'inventory.count' | 'employees.manage' | 'stock.in'
const rolePermissions: Record<AuthUser['roles'][number], Permission[]> = {
  Admin: ['products.read', 'products.manage', 'inventory.count', 'employees.manage', 'stock.in'],
  Manager: ['products.read'],
  WarehouseKeeper: ['products.read', 'stock.in'],
}
let refreshInFlight: Promise<AuthSession | null> | null = null

function hasPermission(user: AuthUser, permission: Permission) {
  return user.roles.some((role) => rolePermissions[role as keyof typeof rolePermissions]?.includes(permission))
}

function readSession(): AuthSession | null {
  try {
    const stored: unknown = JSON.parse(sessionStorage.getItem(sessionKey) ?? 'null')
    if (!stored || typeof stored !== 'object') return null

    const candidate = stored as Partial<AuthSession>
    const user = candidate.user as Partial<AuthUser> | undefined
    const valid = typeof candidate.accessToken === 'string'
      && typeof candidate.refreshToken === 'string'
      && typeof candidate.accessTokenExpiresAtUtc === 'string'
      && !!user
      && typeof user.id === 'string'
      && typeof user.userName === 'string'
      && typeof user.email === 'string'
      && typeof user.firstName === 'string'
      && typeof user.lastName === 'string'
      && Array.isArray(user.roles)
      && user.roles.every((role) => typeof role === 'string')

    if (valid) return candidate as AuthSession
    sessionStorage.removeItem(sessionKey)
    return null
  } catch {
    try { sessionStorage.removeItem(sessionKey) } catch { /* Storage can be unavailable in restricted browser contexts. */ }
    return null
  }
}

function saveSession(session: AuthSession | null) {
  if (session) sessionStorage.setItem(sessionKey, JSON.stringify(session))
  else sessionStorage.removeItem(sessionKey)
}

async function refreshSession(): Promise<AuthSession | null> {
  const current = readSession()
  if (!current?.refreshToken) return null
  const baseUrl = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')
  const response = await fetch(`${baseUrl}/api/auth/refresh-token`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: current.refreshToken }),
  })
  if (!response.ok) return null
  const session = await response.json() as AuthSession
  saveSession(session)
  return session
}

function rotateSession(): Promise<AuthSession | null> {
  if (!refreshInFlight) refreshInFlight = refreshSession().finally(() => { refreshInFlight = null })
  return refreshInFlight
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const baseUrl = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')
  const send = (token?: string) => fetch(`${baseUrl}/api/${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...init?.headers },
  })
  let session = readSession()
  let response = await send(session?.accessToken)
  if (response.status === 401 && session?.refreshToken && !path.startsWith('auth/')) {
    try { session = await rotateSession() }
    catch { session = null }
    if (session) response = await send(session.accessToken)
    else {
      saveSession(null)
      window.dispatchEvent(new Event('stockroom:auth-expired'))
    }
  }
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { error?: string; title?: string; errors?: Record<string, string[]> } | null
    const validationErrors = payload?.errors ? Object.values(payload.errors).flat().join(' ') : ''
    throw new Error(payload?.error ?? validationErrors ?? payload?.title ?? `Sorğu alınmadı (${response.status}).`)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

function Icon({ name, size = 18 }: { name: string; size?: number }) {
  const paths: Record<string, ReactNode> = {
    grid: <><rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/></>,
    box: <><path d="m12 3 9 4.5v9L12 21l-9-4.5v-9L12 3Z"/><path d="m3.5 7.5 8.5 4 8.5-4M12 12v9M7.5 5.2l9 4.5"/></>,
    tag: <><path d="M20.6 13.4 13.4 20.6a2 2 0 0 1-2.8 0L3.4 13.4a2 2 0 0 1-.6-1.4V5a2 2 0 0 1 2-2h7a2 2 0 0 1 1.4.6l7.4 7a2 2 0 0 1 0 2.8Z"/><circle cx="7.5" cy="7.5" r="1"/></>,
    search: <><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></>,
    plus: <><path d="M12 5v14M5 12h14"/></>,
    edit: <><path d="m15 5 4 4M4 20l4-.8L19 8a2.1 2.1 0 0 0-3-3L5 16l-1 4Z"/></>,
    trash: <><path d="M4 7h16M10 11v6M14 11v6M5 7l1 14h12l1-14M9 7V4h6v3"/></>,
    close: <><path d="m18 6-12 12M6 6l12 12"/></>,
    chevron: <path d="m9 18 6-6-6-6"/>,
    package: <><path d="m12 3 8 4.5v9L12 21l-8-4.5v-9L12 3Z"/><path d="m4.5 7.5 7.5 4 7.5-4M12 12v9"/></>,
    layers: <><path d="m12 3 9 5-9 5-9-5 9-5Z"/><path d="m3 12 9 5 9-5M3 16l9 5 9-5"/></>,
    arrow: <><path d="M7 17 17 7M7 7h10v10"/></>,
    refresh: <><path d="M20 7v5h-5M4 17v-5h5"/><path d="M5.6 9A7 7 0 0 1 17.5 6L20 12M4 12l2.5 6A7 7 0 0 0 18.4 15"/></>,
  }
  return <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">{paths[name] ?? paths.box}</svg>
}

function App() {
  const [auth, setAuth] = useState<AuthSession | null>(() => readSession())
  const [authError, setAuthError] = useState('')
  const [authBusy, setAuthBusy] = useState(false)
  const [view, setView] = useState<View>('products')
  const [products, setProducts] = useState<Product[]>([])
  const [categories, setCategories] = useState<Category[]>([])
  const [query, setQuery] = useState('')
  const [productSort, setProductSort] = useState<ProductSort>('price-desc')
  const [dialog, setDialog] = useState<Dialog>(null)
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [scannerOpen, setScannerOpen] = useState(false)
  const [receiptQr, setReceiptQr] = useState<{ batchNumber: string; svg: string } | null>(null)
  const [connected, setConnected] = useState(false)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [busyId, setBusyId] = useState('')
  const [profileOpen, setProfileOpen] = useState(false)
  const [warehouses, setWarehouses] = useState<WarehouseOption[]>([])

  const loadData = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [productRows, categoryRows] = await Promise.all([
        request<ProductResponse[]>('products'), request<Category[]>('categories'),
      ])
      setProducts(productRows.map(normalizeProductDates))
      setCategories(categoryRows)
      const currentSession = readSession()
      if (currentSession && hasPermission(currentSession.user, 'stock.in')) {
        const warehouseRows = await request<WarehouseOption[]>('inventory/warehouses')
        setWarehouses(warehouseRows)
      }
      setConnected(true)
    } catch (caught) {
      setConnected(false)
      setError(caught instanceof Error ? caught.message : 'Məlumat yüklənmədi.')
    } finally { setLoading(false) }
  }, [])

  useEffect(() => {
    const expire = () => { setAuth(null); setAuthError('Sessiyanın vaxtı bitib. Yenidən daxil olun.') }
    window.addEventListener('stockroom:auth-expired', expire)
    return () => window.removeEventListener('stockroom:auth-expired', expire)
  }, [])

  useEffect(() => {
    async function loadInitialData() {
      if (auth) await loadData()
      else { setProducts([]); setCategories([]); setConnected(false); setLoading(false) }
    }

    void loadInitialData()
  }, [auth, loadData])

  async function login(identifier: string, password: string) {
    setAuthBusy(true); setAuthError('')
    try {
      const session = await request<AuthSession>('auth/login', { method: 'POST', body: JSON.stringify({ identifier, password }) })
      saveSession(session)
      setAuth(session)
    } catch (caught) { setAuthError(caught instanceof Error ? caught.message : 'Giriş alınmadı.') }
    finally { setAuthBusy(false) }
  }

  async function logout() {
    const current = readSession()
    try {
      if (current?.refreshToken) await request('auth/logout', { method: 'POST', body: JSON.stringify({ refreshToken: current.refreshToken }) })
    } catch { /* Session is cleared locally even when the API is unavailable. */ }
    saveSession(null); setAuth(null); setAuthError('')
  }

  const filteredProducts = useMemo(() => {
    const value = query.trim().toLocaleLowerCase('az')
    return products.filter((product) => {
      const category = categories.find((item) => item.id === product.categoryId)?.name ?? ''
      return [product.name, product.sku, product.barcode, category].some((field) => field.toLocaleLowerCase('az').includes(value))
    }).sort((a, b) => {
      if (productSort === 'price-desc') return b.unitPrice - a.unitPrice
      if (productSort === 'price-asc') return a.unitPrice - b.unitPrice
      if (productSort === 'quantity-asc') return (a.quantity ?? 0) - (b.quantity ?? 0)
      return a.name.localeCompare(b.name, 'az')
    })
  }, [products, categories, query, productSort])

  const filteredCategories = useMemo(() => {
    const value = query.trim().toLocaleLowerCase('az')
    return categories.filter((category) => [category.name, category.description].some((field) => field.toLocaleLowerCase('az').includes(value)))
  }, [categories, query])

  const activeCount = products.filter((product) => product.isActive).length
  const averagePrice = products.length ? products.reduce((sum, item) => sum + item.unitPrice, 0) / products.length : 0

  if (!auth) return <LoginScreen error={authError} busy={authBusy} onLogin={(identifier, password) => void login(identifier, password)}/>

  const canManageProducts = hasPermission(auth.user, 'products.manage')
  const canManageStock = hasPermission(auth.user, 'stock.in')
  const canManageEmployees = hasPermission(auth.user, 'employees.manage')

  async function saveProduct(form: ProductForm, id?: string) {
    if (!canManageProducts) return
    setSaving(true); setError('')
    try {
      const productForm = {
        name: form.name, sku: form.sku, barcode: form.barcode, description: form.description,
        categoryId: form.categoryId, unitPrice: form.unitPrice, unitOfMeasure: form.unitOfMeasure, isActive: form.isActive,
      }
      if (!id || canManageProducts)
        await request<Product>(id ? `products/${id}` : 'products', { method: id ? 'PUT' : 'POST', body: JSON.stringify(productForm) })
      setDialog(null); await loadData()
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Məhsul yadda saxlanmadı.') }
    finally { setSaving(false) }
  }

  async function receiveStock(product: Product, form: ReceiveStockForm) {
    if (!canManageStock) return
    const quantity = Number(form.quantity)
    const purchasePrice = Number(form.purchasePrice)
    const salePrice = Number(form.salePrice)
    if (!Number.isInteger(quantity) || quantity < 1 || !Number.isFinite(purchasePrice) || purchasePrice < 0 || !Number.isFinite(salePrice) || salePrice < 0) {
      setError('Miqdar ən azı 1 olan tam ədəd olmalı, qiymətlər isə 0 və ya daha böyük olmalıdır.')
      return
    }

    setSaving(true); setError('')
    try {
      const receipt = await request<{ batchNumber: string; quantity: number; entryDate: string; qrCodeSvg: string }>('inventory/receipts', {
        method: 'POST', body: JSON.stringify({
          productId: product.id, warehouseId: form.warehouseId, quantity, purchasePrice, salePrice,
          entryDate: new Date(form.entryDate).toISOString(), expiryDate: form.expiryDate ? new Date(form.expiryDate).toISOString() : null,
        }),
      })
      setDialog(null)
      setReceiptQr({ batchNumber: receipt.batchNumber, svg: receipt.qrCodeSvg })
      setNotice(`${receipt.batchNumber} partiyası yaradıldı (${receipt.quantity} ${product.unitOfMeasure}).`)
      await loadData()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Stok yenilənmədi.')
    } finally {
      setSaving(false)
    }
  }

  async function handleScannedCode(code: string) {
    setReceiptQr(null)
    try {
      const result = await request<InventoryScanResult>(`inventory/scan/${encodeURIComponent(code)}`)
      setView('products')
      setQuery(result.sku)
      setNotice(result.codeType === 'batch'
        ? `${result.batchNumber} partiyası: ${result.productName} · ${result.remainingQuantity ?? 0} qalıq`
        : `${result.productName} məhsulu tapıldı.`)
      setError('')
      setScannerOpen(false)
    } catch (caught) {
      const localProduct = products.find((product) =>
        product.barcode.trim().toLocaleLowerCase('az') === code.trim().toLocaleLowerCase('az') ||
        product.sku.trim().toLocaleLowerCase('az') === code.trim().toLocaleLowerCase('az'))
      if (localProduct) {
        setView('products')
        setQuery(localProduct.sku)
        setNotice(`${localProduct.name} məhsulu tapıldı.`)
        setScannerOpen(false)
        return
      }
      throw caught
    }
  }

  async function saveCategory(form: CategoryForm, id?: string) {
    if (!canManageProducts) return
    setSaving(true); setError('')
    try {
      await request<Category>(id ? `categories/${id}` : 'categories', { method: id ? 'PUT' : 'POST', body: JSON.stringify(form) })
      setDialog(null); await loadData()
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Kateqoriya yadda saxlanmadı.') }
    finally { setSaving(false) }
  }

  async function addEmployee(form: EmployeeForm) {
    if (!canManageEmployees) return
    setReceiptQr(null)
    setSaving(true); setError(''); setNotice('')
    try {
      await request('auth/register', { method: 'POST', body: JSON.stringify(form) })
      setDialog(null); setNotice(`${form.firstName} ${form.lastName} istifadəçisi əlavə edildi.`)
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'İşçi əlavə edilmədi.') }
    finally { setSaving(false) }
  }

  function requestDelete(kind: View, id: string) {
    if (!canManageProducts) return
    const item = kind === 'products' ? products.find((product) => product.id === id) : categories.find((category) => category.id === id)
    if (item) setDeleteTarget({ kind, id, name: item.name })
  }

  async function confirmDelete() {
    if (!canManageProducts || !deleteTarget) return
    const target = deleteTarget
    setBusyId(target.id); setError('')
    try { await request(`${target.kind}/${target.id}`, { method: 'DELETE' }); setDeleteTarget(null); await loadData() }
    catch (caught) { setError(caught instanceof Error ? caught.message : 'Silinmə əməliyyatı alınmadı.') }
    finally { setBusyId('') }
  }

  const openCreate = () => setDialog(view === 'products' ? { kind: 'product' } : { kind: 'category' })

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand"><span className="brand-mark"><Icon name="box" size={20}/></span><span>stock<span className="brand-light">room</span></span></div>
      <div className="workspace-label">İŞ SAHƏSİ</div>
      <nav className="side-nav" aria-label="Əsas menyu">
        <button className={`nav-item ${view === 'products' ? 'selected' : ''}`} type="button" onClick={() => { setView('products'); setQuery('') }}><Icon name="box"/><span>Məhsullar</span><span className="nav-count">{products.length}</span></button>
        <button className={`nav-item ${view === 'categories' ? 'selected' : ''}`} type="button" onClick={() => { setView('categories'); setQuery('') }}><Icon name="tag"/><span>Kateqoriyalar</span><span className="nav-count">{categories.length}</span></button>
      </nav>
      <div className="sidebar-bottom">
        <div className="help-card"><div className="help-icon"><Icon name="layers"/></div><strong>Stok nəzarətdə</strong><p>Məhsul və kateqoriyalarınızı bir yerdən idarə edin.</p></div>
        <div className="profile"><div className="avatar">{auth.user.firstName.slice(0, 1)}{auth.user.lastName.slice(0, 1)}</div><div className="profile-copy"><strong>{auth.user.firstName} {auth.user.lastName}</strong><span>{auth.user.roles.join(', ')}</span></div><button className="logout-button" type="button" onClick={() => void logout()}>Çıxış</button></div>
      </div>
    </aside>

    <main className="main-area">
      <header className="topbar">
        <div className="breadcrumbs"><span>İş sahəsi</span><Icon name="chevron" size={14}/><strong>{view === 'products' ? 'Məhsullar' : 'Kateqoriyalar'}</strong></div>
        <div className="topbar-right"><a className="docs-link" href="http://localhost:5135/swagger" target="_blank" rel="noreferrer">API sənədləri <Icon name="arrow" size={14}/></a>{canManageEmployees && <button className="staff-button" type="button" onClick={() => setDialog({ kind: 'employee' })}>İşçi əlavə et</button>}<span className={`api-status ${connected ? 'online' : 'offline'}`}><i/>{connected ? 'API aktivdir' : 'API bağlantısı yoxdur'}</span><div className="profile-trigger-wrap"><button className="top-avatar" type="button" aria-label="İstifadəçi profilini göstər" aria-expanded={profileOpen} onClick={() => setProfileOpen((open) => !open)}>{auth.user.firstName.slice(0, 1).toLocaleUpperCase('az')}{auth.user.lastName.slice(0, 1).toLocaleUpperCase('az')}</button>{profileOpen && <div className="profile-popover" role="status"><strong>{auth.user.firstName} {auth.user.lastName}</strong><span>{auth.user.email}</span><small>{auth.user.roles.join(', ')}</small><button type="button" onClick={() => { setProfileOpen(false); void logout() }}>Çıxış</button></div>}</div></div>
      </header>

      <div className="content-wrap">
        <section className="welcome-row">
          <div><div className="eyebrow">STOCK MANAGEMENT <span>•</span> İDARƏETMƏ PANELİ</div><h1>İşlər qaydasındadır.</h1><p className="welcome-subtitle">Anbar məlumatlarınızı izləyin və gündəlik əməliyyatları idarə edin.</p></div>
          <div className="date-chip"><span className="date-dot"/> Canlı məlumat <span className="date-divider"/>{new Intl.DateTimeFormat('az-AZ', { day: 'numeric', month: 'long', year: 'numeric' }).format(new Date())}</div>
        </section>
        {error && <div className="error-banner" role="alert"><span>{error}</span><button type="button" onClick={() => void loadData()}><Icon name="refresh" size={15}/> Yenidən yoxla</button></div>}
        {notice && <div className="success-banner" role="status">{notice}{receiptQr && <button type="button" onClick={() => setDialog({ kind: 'qr' })}>QR kodu göstər</button>}<button type="button" onClick={() => { setNotice(''); setReceiptQr(null) }} aria-label="Bildirişi bağla"><Icon name="close" size={14}/></button></div>}

        <section className="stat-grid" aria-label="Qısa göstəricilər">
          <StatCard label="Ümumi məhsul" value={products.length.toLocaleString('az-AZ')} note="Kataloqda qeydiyyatda" icon="box" tone="blue" loading={loading}/>
          <StatCard label="Aktiv məhsul" value={activeCount.toLocaleString('az-AZ')} note={products.length ? `${Math.round(activeCount / products.length * 100)}% aktivdir` : 'Hələ məhsul əlavə edilməyib'} icon="arrow" tone="green" loading={loading}/>
          <StatCard label="Kateqoriya" value={categories.length.toLocaleString('az-AZ')} note="Məhsul qrupları" icon="layers" tone="violet" loading={loading}/>
          <StatCard label="Orta satış qiyməti" value={currency.format(averagePrice)} note="Kataloq üzrə orta göstərici" icon="tag" tone="amber" loading={loading}/>
        </section>

        <section className="data-panel">
          <div className="panel-heading"><div><h2>İnventar kataloqu</h2><p>Məhsulları və kateqoriyaları idarə edin.</p></div><div className="panel-actions"><button className="refresh-button" type="button" onClick={() => void loadData()} aria-label="Məlumatı yenilə"><Icon name="refresh" size={17}/></button>{canManageProducts && <button className="primary-button" type="button" onClick={openCreate}><Icon name="plus" size={17}/>{view === 'products' ? 'Məhsul əlavə et' : 'Kateqoriya əlavə et'}</button>}</div></div>
          <div className="toolbar">
            <div className="tabs" role="tablist" aria-label="İnventar bölməsi"><button type="button" role="tab" aria-selected={view === 'products'} className={view === 'products' ? 'active' : ''} onClick={() => { setView('products'); setQuery('') }}>Məhsullar <span>{products.length}</span></button><button type="button" role="tab" aria-selected={view === 'categories'} className={view === 'categories' ? 'active' : ''} onClick={() => { setView('categories'); setQuery('') }}>Kateqoriyalar <span>{categories.length}</span></button></div>
            <div className="toolbar-controls"><button className="secondary-button" type="button" onClick={() => setScannerOpen(true)}>QR / barkod skan et</button><div className="search-box"><Icon name="search" size={17}/><input aria-label={view === 'products' ? 'Məhsul, SKU və ya barkod axtar' : 'Kateqoriya axtar'} value={query} onChange={(event) => setQuery(event.target.value)} placeholder={view === 'products' ? 'Məhsul, SKU və ya barkod axtar...' : 'Kateqoriya axtar...'}/>{query && <button className="clear-search" type="button" aria-label="Axtarışı təmizlə" title="Axtarışı təmizlə" onClick={() => setQuery('')}><Icon name="close" size={14}/></button>}<kbd>⌘ K</kbd></div>{view === 'products' && <label className="sort-control"><span>Sıralama</span><select value={productSort} onChange={(event) => setProductSort(event.target.value as ProductSort)}><option value="price-desc">Qiymət: bahadan ucuza</option><option value="price-asc">Qiymət: ucuzdan bahaya</option><option value="quantity-asc">Stok: azdan çoxa</option><option value="name-asc">Ad: A-dan Z-yə</option></select></label>}</div>
          </div>
          {view === 'products' ? <ProductTable products={filteredProducts} categories={categories} loading={loading} canManageProduct={canManageProducts} canManageStock={canManageStock} onEdit={(item) => setDialog({ kind: 'product', item })} onStock={(item) => setDialog({ kind: 'stock', item })} onDelete={(id) => requestDelete('products', id)} busyId={busyId}/> : <CategoryTable categories={filteredCategories} products={products} loading={loading} canManage={canManageProducts} onEdit={(item) => setDialog({ kind: 'category', item })} onDelete={(id) => requestDelete('categories', id)} busyId={busyId}/>}
          <div className="panel-footer"><span>{view === 'products' ? `${filteredProducts.length} məhsul göstərilir` : `${filteredCategories.length} kateqoriya göstərilir`}</span><span>Stockroom <b>•</b> İnventar idarəetməsi</span></div>
        </section>
        <footer className="page-footer"><span>© {new Date().getFullYear()} Stockroom</span><span>Sadə. Dəqiq. Nəzarətdə.</span></footer>
      </div>
    </main>

    {dialog?.kind === 'product' && <ProductDialog item={dialog.item} categories={categories} saving={saving} canManageProduct={canManageProducts} onClose={() => setDialog(null)} onSave={(form) => void saveProduct(form, dialog.item?.id)}/>}
    {dialog?.kind === 'stock' && <StockDialog item={dialog.item} warehouses={warehouses} saving={saving} onClose={() => setDialog(null)} onSave={(form) => void receiveStock(dialog.item, form)}/>}
    {dialog?.kind === 'category' && <CategoryDialog item={dialog.item} categories={categories} saving={saving} onClose={() => setDialog(null)} onSave={(form) => void saveCategory(form, dialog.item?.id)}/>}
    {dialog?.kind === 'employee' && <EmployeeDialog saving={saving} onClose={() => setDialog(null)} onSave={(form) => void addEmployee(form)}/>}
    {scannerOpen && <BarcodeScannerDialog onClose={() => setScannerOpen(false)} onScan={(code) => handleScannedCode(code)}/>}
    {dialog?.kind === 'qr' && receiptQr && <QrDisplayDialog batchNumber={receiptQr.batchNumber} svg={receiptQr.svg} onClose={() => setDialog(null)}/>}
    {deleteTarget && <ConfirmDeleteDialog target={deleteTarget} busy={busyId === deleteTarget.id} onCancel={() => setDeleteTarget(null)} onConfirm={() => void confirmDelete()}/>}
  </div>
}

function LoginScreen({ error, busy, onLogin }: { error: string; busy: boolean; onLogin: (identifier: string, password: string) => void }) {
  const [identifier, setIdentifier] = useState('')
  const [password, setPassword] = useState('')
  const submit = (event: FormEvent) => {
    event.preventDefault()
    onLogin(identifier.trim(), password)
  }

  return <main className="auth-screen">
    <form className="auth-card" onSubmit={submit}>
      <div className="auth-brand"><span className="brand-mark"><Icon name="box" size={20}/></span><span>stock<span className="brand-light">room</span></span></div>
      <span className="auth-eyebrow">ANBAR İDARƏETMƏSİ</span>
      <h1>Hesabınıza daxil olun</h1>
      <p>İdarəetmə panelinə davam etmək üçün giriş məlumatlarınızı yazın.</p>
      {error && <div className="error-banner auth-error" role="alert">{error}</div>}
      <label className="auth-field"><span>E-poçt və ya istifadəçi adı</span><input autoComplete="username" value={identifier} onChange={(event) => setIdentifier(event.target.value)} required placeholder="admin@company.az"/></label>
      <label className="auth-field"><span>Şifrə</span><input type="password" autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} required placeholder="Şifrənizi daxil edin"/></label>
      <button className="primary-button auth-submit" type="submit" disabled={busy}>{busy ? 'Yoxlanılır…' : 'Daxil ol'}<Icon name="arrow" size={17}/></button>
      <span className="auth-note">Giriş məlumatları administrator tərəfindən verilir.</span>
    </form>
  </main>
}

function StatCard({ label, value, note, icon, tone, loading }: { label: string; value: string; note: string; icon: string; tone: string; loading: boolean }) {
  return <article className="stat-card"><div className={`stat-icon ${tone}`}><Icon name={icon}/></div><span className="stat-label">{label}</span><strong className={`stat-value ${loading ? 'skeleton-text' : ''}`}>{loading ? '—' : value}</strong><span className="stat-note">{note}</span></article>
}

function ProductTable({ products, categories, loading, canManageProduct, canManageStock, onEdit, onStock, onDelete, busyId }: { products: Product[]; categories: Category[]; loading: boolean; canManageProduct: boolean; canManageStock: boolean; onEdit: (item: Product) => void; onStock: (item: Product) => void; onDelete: (id: string) => void; busyId: string }) {
  const [datePopoverId, setDatePopoverId] = useState<string | null>(null)
  const dateButtonRef = useRef<HTMLButtonElement>(null)
  const datePopoverRef = useRef<HTMLDivElement>(null)
  const [datePopoverPosition, setDatePopoverPosition] = useState({ top: 0, left: 0 })

  useEffect(() => {
    if (!datePopoverId) return

    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target
      if (!(target instanceof Node)) return
      if (dateButtonRef.current?.contains(target) || datePopoverRef.current?.contains(target)) return
      setDatePopoverId(null)
    }
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setDatePopoverId(null)
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [datePopoverId])

  useLayoutEffect(() => {
    if (!datePopoverId) return

    const updatePosition = () => {
      const button = dateButtonRef.current
      const popover = datePopoverRef.current
      if (!button || !popover) return

      const anchor = button.getBoundingClientRect()
      const margin = 8
      const gap = 7
      const below = anchor.bottom + gap
      const top = below + popover.offsetHeight <= window.innerHeight - margin
        ? below
        : Math.max(margin, anchor.top - popover.offsetHeight - gap)
      const left = Math.max(margin, Math.min(anchor.right - popover.offsetWidth, window.innerWidth - popover.offsetWidth - margin))

      setDatePopoverPosition({ top, left })
    }

    updatePosition()
    window.addEventListener('resize', updatePosition)
    window.addEventListener('scroll', updatePosition, true)
    return () => {
      window.removeEventListener('resize', updatePosition)
      window.removeEventListener('scroll', updatePosition, true)
    }
  }, [datePopoverId])

  return <div className="table-scroll"><table><thead><tr><th>SKU / BARCODE</th><th>KATEQORİYA</th><th>QİYMƏT</th><th>STOK</th><th>ÖLÇÜ</th><th>STATUS</th><th>TARİX</th><th><span className="sr-only">Əməliyyatlar</span></th></tr></thead><tbody>
    {loading ? <EmptyRow colSpan={8} title="Məlumat yüklənir..."/> : products.length === 0 ? <EmptyRow colSpan={8} title="Məhsul tapılmadı" detail="Axtarışınızı dəyişin və ya yeni məhsul əlavə edin."/> : products.map((product, index) => <tr key={product.id} className="table-row" style={{ animationDelay: `${index * 25}ms` }}>
      <td><div className="code-cell"><strong>{product.sku}</strong><span>{product.barcode || 'Barkod yoxdur'}</span><span>{product.name}</span></div></td>
      <td><span className="category-pill">{categories.find((category) => category.id === product.categoryId)?.name ?? 'Kateqoriya yoxdur'}</span></td>
      <td><strong className="price-cell">{currency.format(product.unitPrice)}</strong></td><td><span className={`stock-pill ${product.quantity === undefined ? 'stock-unknown' : product.quantity < 5 ? 'stock-low' : 'stock-ok'}`}>{product.quantity === undefined ? '—' : product.quantity.toLocaleString('az-AZ')}{product.quantity !== undefined && product.quantity < 5 && <strong>Stok bitir</strong>}</span></td><td><span className="unit-cell">/ {product.unitOfMeasure}</span></td>
      <td><span className={`status-pill ${product.isActive ? 'enabled' : 'disabled'}`}><i/>{product.isActive ? 'Aktiv' : 'Passiv'}</span></td>
      <td><div className="date-popover-wrap"><button ref={datePopoverId === product.id ? dateButtonRef : null} type="button" className="date-link" aria-expanded={datePopoverId === product.id} onClick={() => setDatePopoverId((current) => current === product.id ? null : product.id)}>{formatDateTime(product.createdAt, false)}</button>{datePopoverId === product.id && createPortal(<div ref={datePopoverRef} className="date-popover" style={datePopoverPosition} role="dialog" aria-label="Məhsulun tarixçəsi"><div className="date-popover-row"><span>Giriş tarixi</span><strong>{formatDateTime(product.createdAt)}</strong></div><div className="date-popover-row"><span>Yenilənmə tarixi</span><strong>{formatDateTime(product.updatedAt)}</strong></div></div>, document.body)}</div></td>
      <td><RowActions showEdit={canManageProduct} showStock={canManageStock} showDelete={canManageProduct} onEdit={() => onEdit(product)} onStock={() => onStock(product)} onDelete={() => onDelete(product.id)} busy={busyId === product.id}/></td>
    </tr>)}
  </tbody></table></div>
}

function CategoryTable({ categories, products, loading, canManage, onEdit, onDelete, busyId }: { categories: Category[]; products: Product[]; loading: boolean; canManage: boolean; onEdit: (item: Category) => void; onDelete: (id: string) => void; busyId: string }) {
  return <div className="table-scroll"><table><thead><tr><th>KATEQORİYA</th><th>TƏSVİR</th><th>ÜST KATEQORİYA</th><th>MƏHSUL SAYI</th><th><span className="sr-only">Əməliyyatlar</span></th></tr></thead><tbody>
    {loading ? <EmptyRow colSpan={5} title="Məlumat yüklənir..."/> : categories.length === 0 ? <EmptyRow colSpan={5} title="Kateqoriya tapılmadı" detail="Məhsul əlavə etməzdən əvvəl kateqoriya yaradın."/> : categories.map((category, index) => <tr key={category.id} className="table-row" style={{ animationDelay: `${index * 25}ms` }}>
      <td><div className="product-cell"><div className="category-thumb"><Icon name="tag" size={19}/></div><div><strong>{category.name}</strong><span>{category.id.slice(0, 8).toUpperCase()}</span></div></div></td>
      <td><span className="description-cell">{category.description || 'Təsvir əlavə edilməyib'}</span></td>
      <td><span className="category-pill">{categories.find((item) => item.id === category.parentCategoryId)?.name ?? '—'}</span></td>
      <td><span className="count-pill">{products.filter((product) => product.categoryId === category.id).length}</span></td>
      <td><RowActions showEdit={canManage} showDelete={canManage} onEdit={() => onEdit(category)} onDelete={() => onDelete(category.id)} busy={busyId === category.id}/></td>
    </tr>)}
  </tbody></table></div>
}

function EmptyRow({ colSpan, title, detail }: { colSpan: number; title: string; detail?: string }) {
  return <tr><td colSpan={colSpan}><div className="empty-state"><div className="empty-icon"><Icon name="box" size={22}/></div><strong>{title}</strong>{detail && <span>{detail}</span>}</div></td></tr>
}

function RowActions({ showEdit, showStock = false, showDelete, onEdit, onStock, onDelete, busy }: { showEdit: boolean; showStock?: boolean; showDelete: boolean; onEdit: () => void; onStock?: () => void; onDelete: () => void; busy: boolean }) {
  if (!showEdit && !showStock && !showDelete) return null
  return <div className="row-actions">{showEdit && <button type="button" aria-label="Məhsulu redaktə et" title="Məhsulu redaktə et" onClick={onEdit}><Icon name="edit" size={16}/></button>}{showStock && onStock && <button type="button" aria-label="Anbara mal daxil et və partiyalara bax" title="Anbara mal daxil et" onClick={onStock}><Icon name="package" size={16}/></button>}{showDelete && <button type="button" aria-label="Məhsulu sil" title="Məhsulu sil" disabled={busy} onClick={onDelete}><Icon name="trash" size={16}/></button>}</div>
}

function DialogFrame({ title, subtitle, onClose, children, onSubmit, saving, canSubmit = true, submitLabel = 'Yadda saxla' }: { title: string; subtitle: string; onClose: () => void; children: ReactNode; onSubmit: (event: FormEvent<HTMLFormElement>) => void; saving: boolean; canSubmit?: boolean; submitLabel?: string }) {
  return <div className="modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}><section className="modal-card" role="dialog" aria-modal="true" aria-labelledby="dialog-title"><div className="modal-heading"><div><span className="modal-kicker">STOCKROOM KATALOQ</span><h2 id="dialog-title">{title}</h2><p>{subtitle}</p></div><button className="modal-close" type="button" aria-label="Bağla" onClick={onClose}><Icon name="close"/></button></div><form onSubmit={onSubmit}><div className="modal-body">{children}</div><div className="modal-footer"><button className="secondary-button" type="button" onClick={onClose}>Ləğv et</button>{canSubmit && <button className="primary-button" type="submit" disabled={saving}>{saving ? 'Yadda saxlanır…' : submitLabel}</button>}</div></form></section></div>
}

function StockDialog({ item, warehouses, saving, onClose, onSave }: { item: Product; warehouses: WarehouseOption[]; saving: boolean; onClose: () => void; onSave: (form: ReceiveStockForm) => void }) {
  const localNow = () => { const now = new Date(); now.setMinutes(now.getMinutes() - now.getTimezoneOffset()); return now.toISOString().slice(0, 16) }
  const [form, setForm] = useState<ReceiveStockForm>({ warehouseId: warehouses[0]?.id ?? '', quantity: '', purchasePrice: '', salePrice: String(item.unitPrice), entryDate: localNow(), expiryDate: '' })
  const [batches, setBatches] = useState<BatchInventoryItem[]>([])
  const [batchError, setBatchError] = useState('')
  useEffect(() => { void request<BatchInventoryItem[]>(`inventory/products/${item.id}/batches`).then(setBatches).catch((caught: unknown) => setBatchError(caught instanceof Error ? caught.message : 'Partiyalar yüklənmədi.')) }, [item.id])
  const set = <K extends keyof ReceiveStockForm>(key: K, value: ReceiveStockForm[K]) => setForm((current) => ({ ...current, [key]: value }))
  return <DialogFrame title="Anbara mal qəbulu" subtitle={`${item.name} · cari ümumi stok: ${(item.quantity ?? 0).toLocaleString('az-AZ')} ${item.unitOfMeasure}`} onClose={onClose} saving={saving} canSubmit={warehouses.length > 0} submitLabel="Partiyanı qəbul et" onSubmit={(event) => { event.preventDefault(); onSave(form) }}>
    <div className="form-grid">
      <label className="field full"><span>Anbar <b>*</b></span><select required value={form.warehouseId} onChange={(event) => set('warehouseId', event.target.value)}><option value="" disabled>Anbar seçin</option>{warehouses.map((warehouse) => <option key={warehouse.id} value={warehouse.id}>{warehouse.name}</option>)}</select>{warehouses.length === 0 && <small>Qəbul üçün əvvəlcə anbar yaradılmalıdır.</small>}</label>
      <label className="field"><span>İlkin miqdar <b>*</b></span><div className="input-suffix"><input required type="number" min="1" step="1" value={form.quantity} onChange={(event) => set('quantity', event.target.value)}/><span>{item.unitOfMeasure}</span></div></label>
      <div className="field-row full"><label className="field"><span>Alış qiyməti <b>*</b></span><div className="input-suffix"><input required type="number" min="0" step="0.01" value={form.purchasePrice} onChange={(event) => set('purchasePrice', event.target.value)}/><span>AZN</span></div></label><label className="field"><span>Satış qiyməti <b>*</b></span><div className="input-suffix"><input required type="number" min="0" step="0.01" value={form.salePrice} onChange={(event) => set('salePrice', event.target.value)}/><span>AZN</span></div></label></div>
      <label className="field"><span>Giriş tarixi <b>*</b></span><input required type="datetime-local" value={form.entryDate} onChange={(event) => set('entryDate', event.target.value)}/></label>
      <label className="field"><span>Son istifadə tarixi</span><input type="datetime-local" value={form.expiryDate} onChange={(event) => set('expiryDate', event.target.value)}/></label>
      <div className="batch-code-note full">Partiya kodu avtomatik yaradılacaq və qəbuldan sonra burada göstəriləcək.</div>
      <div className="batch-section full"><div className="batch-section-heading"><strong>Aktiv partiyalar</strong><span>FIFO: ən köhnə giriş əvvəl</span></div>{batchError ? <div className="batch-empty">{batchError}</div> : batches.length === 0 ? <div className="batch-empty">Aktiv partiya yoxdur.</div> : <div className="batch-table-wrap"><table className="batch-table"><thead><tr><th>Partiya kodu</th><th>Giriş tarixi</th><th>Son istifadə</th><th>Qalıq</th><th>Alış qiyməti</th></tr></thead><tbody>{batches.map((batch) => <tr key={batch.batchNumber}><td><strong>{batch.batchNumber}</strong></td><td>{formatDateTime(batch.entryDate, false)}</td><td>{formatDateTime(batch.expiryDate, false)}</td><td>{batch.remainingQuantity.toLocaleString('az-AZ')} {item.unitOfMeasure}</td><td>{currency.format(batch.purchasePrice)}</td></tr>)}</tbody></table></div>}</div>
    </div>
  </DialogFrame>
}

function BarcodeScannerDialog({ onClose, onScan }: { onClose: () => void; onScan: (code: string) => Promise<void> }) {
  const [manualCode, setManualCode] = useState('')
  const [scanError, setScanError] = useState('')
  const [cameraReady, setCameraReady] = useState(false)
  const onScanRef = useRef(onScan)
  const scanPending = useRef(false)
  onScanRef.current = onScan

  useEffect(() => {
    const scanner = new Html5Qrcode('barcode-reader', {
      verbose: false,
      formatsToSupport: [Html5QrcodeSupportedFormats.QR_CODE, Html5QrcodeSupportedFormats.CODE_128,
        Html5QrcodeSupportedFormats.EAN_13, Html5QrcodeSupportedFormats.EAN_8,
        Html5QrcodeSupportedFormats.UPC_A, Html5QrcodeSupportedFormats.UPC_E],
    })
    let active = true
    const handleScan = async (code: string) => {
      if (scanPending.current) return
      scanPending.current = true
      setScanError('')
      try { await onScanRef.current(code.trim()) }
      catch (caught) { setScanError(caught instanceof Error ? caught.message : 'Kod üzrə məlumat tapılmadı.') }
      finally { scanPending.current = false }
    }
    void scanner.start({ facingMode: 'environment' }, {
      fps: 10,
      qrbox: { width: 240, height: 240 },
    }, (decodedText) => {
      if (!active) return
      void handleScan(decodedText)
    }, () => undefined).then(() => {
      if (active) setCameraReady(true)
    }).catch((caught: unknown) => {
      if (active) setScanError(caught instanceof Error ? caught.message : 'Kameraya qoşulmaq mümkün olmadı.')
    })
    return () => {
      active = false
      if (scanner.isScanning) void scanner.stop().then(() => scanner.clear()).catch(() => undefined)
    }
  }, [])

  return <DialogFrame title="QR / barkod skanı" subtitle="Kodu kamera ilə oxudun və ya skanerdən daxil edin." onClose={onClose} saving={false} canSubmit={false} onSubmit={(event) => { event.preventDefault(); if (manualCode.trim()) void onScan(manualCode.trim()).catch((caught: unknown) => setScanError(caught instanceof Error ? caught.message : 'Kod üzrə məlumat tapılmadı.')) }}>
    <div className="scanner-panel"><div id="barcode-reader" className="barcode-reader"/><p>{cameraReady ? 'Kameranı koda yönəldin.' : scanError || 'Kamera başladılır…'}</p>{scanError && <small>Kameraya icazə verin və ya aşağıdakı sahəyə kodu daxil edin.</small>}</div>
    <label className="field full"><span>Skan edilmiş kod</span><input autoFocus value={manualCode} onChange={(event) => setManualCode(event.target.value)} placeholder="USB skaner və ya əl ilə kod"/></label>
    {scanError && <div className="batch-empty">{scanError}</div>}
    {manualCode.trim() && <button className="primary-button" type="submit">Kodu axtar</button>}
  </DialogFrame>
}

function QrDisplayDialog({ batchNumber, svg, onClose }: { batchNumber: string; svg: string; onClose: () => void }) {
  return <DialogFrame title="Partiya QR kodu" subtitle={`${batchNumber} · Skan edərək partiya məlumatını açın.`} onClose={onClose} saving={false} canSubmit={false} onSubmit={(event) => event.preventDefault()}>
    <div className="qr-display"><img src={`data:image/svg+xml,${encodeURIComponent(svg)}`} alt={`${batchNumber} QR kodu`}/><strong>{batchNumber}</strong></div>
  </DialogFrame>
}

function ConfirmDeleteDialog({ target, busy, onCancel, onConfirm }: {
  target: DeleteTarget; busy: boolean; onCancel: () => void; onConfirm: () => void
}) {
  const isProduct = target.kind === 'products'
  return <div className="modal-backdrop" onMouseDown={(event) => { if (!busy && event.target === event.currentTarget) onCancel() }}>
    <section className="modal-card confirm-delete-card" role="alertdialog" aria-modal="true" aria-labelledby="delete-dialog-title" aria-describedby="delete-dialog-description">
      <div className="modal-heading"><div><span className="modal-kicker">SİLMƏ TƏSDİQİ</span><h2 id="delete-dialog-title">Bu {isProduct ? 'məhsulu' : 'kateqoriyanı'} silmək istədiyinizə əminsinizmi?</h2><p>{target.name}</p></div><button className="modal-close" type="button" aria-label="Bağla" disabled={busy} onClick={onCancel}><Icon name="close"/></button></div>
      <div className="modal-body"><p id="delete-dialog-description">{isProduct ? 'Məhsul və onun partiyaları aktiv siyahılardan gizlədiləcək. Əməliyyat tarixçəsi saxlanacaq.' : 'Bu kateqoriya silinəcək. Məhsullarla əlaqəlidirsə, əməliyyat rədd edilə bilər.'}</p></div>
      <div className="modal-footer"><button className="secondary-button" type="button" disabled={busy} onClick={onCancel}>Ləğv et</button><button className="danger-button" type="button" disabled={busy} onClick={onConfirm}>{busy ? 'Silinir…' : 'Bəli, sil'}</button></div>
    </section>
  </div>
}

function ProductDialog({ item, categories, saving, canManageProduct, onClose, onSave }: { item?: Product; categories: Category[]; saving: boolean; canManageProduct: boolean; onClose: () => void; onSave: (form: ProductForm) => void }) {
  const [form, setForm] = useState<ProductForm>(item ? {
    name: item.name, sku: item.sku, barcode: item.barcode, description: item.description,
    categoryId: item.categoryId, unitPrice: item.unitPrice, unitOfMeasure: item.unitOfMeasure, isActive: item.isActive,
  } : { ...emptyProduct, categoryId: categories[0]?.id ?? '' })
  const set = <K extends keyof ProductForm>(key: K, value: ProductForm[K]) => setForm((current) => ({ ...current, [key]: value }))
  return <DialogFrame title={item ? 'Məhsulu redaktə et' : 'Yeni məhsul'} subtitle="Məhsul məlumatlarını daxil edin." onClose={onClose} saving={saving} canSubmit={canManageProduct} submitLabel="Yadda saxla" onSubmit={(event) => { event.preventDefault(); onSave(form) }}>
    <div className="form-grid"><label className="field full"><span>Məhsulun adı <b>*</b></span><input required disabled={!canManageProduct} maxLength={200} autoFocus value={form.name} onChange={(event) => set('name', event.target.value)} placeholder="Məs. Simsiz klaviatura"/></label>
      <label className="field"><span>SKU <b>*</b></span><input required disabled={!canManageProduct} maxLength={64} value={form.sku} onChange={(event) => set('sku', event.target.value)} placeholder="KB-001"/></label>
      <label className="field"><span>Barkod</span><input disabled={!canManageProduct} maxLength={64} value={form.barcode} onChange={(event) => set('barcode', event.target.value)} placeholder="Barkod nömrəsi"/></label>
      <label className="field"><span>Kateqoriya <b>*</b></span><select required disabled={!canManageProduct} value={form.categoryId} onChange={(event) => set('categoryId', event.target.value)}><option value="" disabled>Kateqoriya seçin</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select>{categories.length === 0 && <small>Əvvəlcə kateqoriya yaradın.</small>}</label>
      <div className="field-row"><label className="field"><span>Satış qiyməti <b>*</b></span><div className="input-suffix"><input required disabled={!canManageProduct} type="number" min="0" step="0.01" value={form.unitPrice} onChange={(event) => set('unitPrice', Number(event.target.value))}/><span>AZN</span></div></label><label className="field"><span>Ölçü vahidi <b>*</b></span><select disabled={!canManageProduct} value={form.unitOfMeasure} onChange={(event) => set('unitOfMeasure', event.target.value)}><option value="Piece">Ədəd</option><option value="Kg">Kiloqram</option><option value="Liter">Litr</option><option value="Package">Paket</option><option value="Box">Qutu</option></select></label></div>
      <label className="field full"><span>Təsvir</span><textarea disabled={!canManageProduct} rows={3} maxLength={1000} value={form.description} onChange={(event) => set('description', event.target.value)} placeholder="Məhsul haqqında qısa məlumat"/></label>
      <label className="toggle-field full"><span><strong>Satışda aktivdir</strong><small>Passiv məhsul kataloqda qalır, ancaq satışda görünmür.</small></span><input type="checkbox" disabled={!canManageProduct} checked={form.isActive} onChange={(event) => set('isActive', event.target.checked)}/></label>
    </div>
  </DialogFrame>
}

function CategoryDialog({ item, categories, saving, onClose, onSave }: { item?: Category; categories: Category[]; saving: boolean; onClose: () => void; onSave: (form: CategoryForm) => void }) {
  const [form, setForm] = useState<CategoryForm>(item ? { name: item.name, description: item.description, parentCategoryId: item.parentCategoryId } : { ...emptyCategory })
  return <DialogFrame title={item ? 'Kateqoriyanı redaktə et' : 'Yeni kateqoriya'} subtitle="Məhsullar üçün kateqoriya məlumatlarını daxil edin." onClose={onClose} saving={saving} onSubmit={(event) => { event.preventDefault(); onSave(form) }}>
    <div className="form-grid"><label className="field full"><span>Kateqoriya adı <b>*</b></span><input required maxLength={150} autoFocus value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} placeholder="Məs. Elektronika"/></label>
      <label className="field full"><span>Üst kateqoriya</span><select value={form.parentCategoryId ?? ''} onChange={(event) => setForm({ ...form, parentCategoryId: event.target.value || null })}><option value="">Əsas kateqoriya</option>{categories.filter((category) => category.id !== item?.id).map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>
      <label className="field full"><span>Təsvir</span><textarea rows={4} maxLength={1000} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} placeholder="Kateqoriya haqqında qısa məlumat"/></label>
    </div>
  </DialogFrame>
}

function EmployeeDialog({ saving, onClose, onSave }: { saving: boolean; onClose: () => void; onSave: (form: EmployeeForm) => void }) {
  const [form, setForm] = useState<EmployeeForm>({ userName: '', email: '', firstName: '', lastName: '', password: '', role: 'Manager' })
  const set = <K extends keyof EmployeeForm>(key: K, value: EmployeeForm[K]) => setForm((current) => ({ ...current, [key]: value }))
  return <DialogFrame title="Yeni işçi" subtitle="İşçi hesabı və rolunu yaradın." onClose={onClose} saving={saving} onSubmit={(event) => { event.preventDefault(); onSave(form) }}>
    <div className="form-grid">
      <label className="field"><span>Ad <b>*</b></span><input required maxLength={100} autoFocus value={form.firstName} onChange={(event) => set('firstName', event.target.value)}/></label>
      <label className="field"><span>Soyad <b>*</b></span><input required maxLength={100} value={form.lastName} onChange={(event) => set('lastName', event.target.value)}/></label>
      <label className="field"><span>İstifadəçi adı <b>*</b></span><input required maxLength={64} value={form.userName} onChange={(event) => set('userName', event.target.value)}/></label>
      <label className="field"><span>E-poçt <b>*</b></span><input required type="email" maxLength={256} value={form.email} onChange={(event) => set('email', event.target.value)}/></label>
      <label className="field full"><span>Şifrə <b>*</b></span><input required type="password" minLength={12} autoComplete="new-password" value={form.password} onChange={(event) => set('password', event.target.value)} placeholder="12+ simvol, böyük/kiçik hərf, rəqəm və işarə"/></label>
      <label className="field full"><span>Rol <b>*</b></span><select value={form.role} onChange={(event) => set('role', event.target.value as EmployeeForm['role'])}><option value="Admin">Admin</option><option value="WarehouseKeeper">Anbardar (WarehouseKeeper)</option><option value="Manager">Menecer (Manager)</option></select></label>
    </div>
  </DialogFrame>
}

export default App
