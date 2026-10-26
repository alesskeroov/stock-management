import { Fragment, useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
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
  reorderLevel: number
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
type ProductPageResponse = { items: ProductResponse[]; currentPage: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }
function normalizeProductDates(product: ProductResponse): Product {
  const createdAt = product.createdAt ?? product.CreatedAt ?? product.entryDate ?? product.EntryDate ?? ''
  const updatedAt = product.updatedAt ?? product.UpdatedAt ?? product.lastUpdatedDate ?? product.LastUpdatedDate ?? createdAt
  return { ...product, createdAt, updatedAt }
}

type Category = { id: string; name: string; description: string; parentCategoryId: string | null }
type CategoryPage = { items: Category[]; currentPage: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }
type ProductForm = Omit<Product, 'id' | 'quantity' | 'createdAt' | 'updatedAt' | 'reorderLevel'>
type WarehouseOption = { id: string; name: string }
type BatchInventoryItem = { batchNumber: string; entryDate: string; expiryDate: string | null; remainingQuantity: number; purchasePrice: number }
type InventoryScanResult = { code: string; codeType: string; productId: string; productName: string; sku: string; barcode: string; batchNumber: string | null; warehouseName: string | null; remainingQuantity: number | null; entryDate: string | null; expiryDate: string | null }
type ReceiveStockForm = { warehouseId: string; quantity: string; purchasePrice: string; salePrice: string; entryDate: string; expiryDate: string }
type CategoryForm = Omit<Category, 'id'>
type EmployeeForm = { userName: string; email: string; firstName: string; lastName: string; password: string; role: 'Admin' | 'WarehouseKeeper' | 'Manager' | 'ProcurementStaff' }
type AuthUser = { id: string; userName: string; email: string; firstName: string; lastName: string; roles: string[] }
type AuthSession = { accessToken: string; refreshToken: string; accessTokenExpiresAtUtc: string; user: AuthUser }
type StockNotification = { id: string; productId: string; productName: string; remainingQuantity: number; message: string; type: 'LOW_STOCK' | 'OUT_OF_STOCK'; isRead: boolean; createdAt: string }
type View = 'products' | 'categories' | 'purchase-orders' | 'goods-receipts' | 'suppliers' | 'procurement-reports' | 'stock-logs' | 'documents'
type Dialog = { kind: 'product'; item?: Product } | { kind: 'stock'; item: Product } | { kind: 'category'; item?: Category } | { kind: 'employee' } | { kind: 'qr' } | null
type DeleteTarget = { kind: View; id: string; name: string }

const emptyProduct: ProductForm = {
  name: '', sku: '', barcode: '', description: '', categoryId: '',
  unitPrice: 0, unitOfMeasure: 'Piece', isActive: true,
}
const emptyCategory: CategoryForm = { name: '', description: '', parentCategoryId: null }
const currency = new Intl.NumberFormat('az-AZ', { style: 'currency', currency: 'AZN', maximumFractionDigits: 2 })
const sessionKey = 'stockroom.auth'
const PRODUCT_PAGE_SIZE = 10
function formatDateTime(value: string | null | undefined, includeTime = true) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('az-AZ', includeTime
    ? { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }
    : { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date)
}
type ProductSort = 'price-desc' | 'price-asc' | 'quantity-asc' | 'name-asc'
type Permission = 'products.read' | 'products.manage' | 'inventory.count' | 'employees.manage' | 'stock.in' | 'stock.out' | 'stock.read' | 'po.view' | 'po.create' | 'po.approve' | 'po.approve.override' | 'po.cancel' | 'grn.create' | 'grn.reverse' | 'supplier.manage'
const rolePermissions: Record<AuthUser['roles'][number], Permission[]> = {
  Admin: ['products.read', 'products.manage', 'inventory.count', 'employees.manage', 'stock.in', 'stock.out', 'stock.read', 'po.view', 'po.create', 'po.approve', 'po.approve.override', 'po.cancel', 'grn.create', 'grn.reverse', 'supplier.manage'],
  Manager: ['products.read', 'stock.read', 'po.view', 'po.create', 'po.approve', 'po.approve.override', 'po.cancel', 'grn.create', 'grn.reverse', 'supplier.manage'],
  WarehouseKeeper: ['products.read', 'stock.in', 'stock.out', 'po.view', 'grn.create'],
  ProcurementStaff: ['po.view', 'po.create', 'supplier.manage'],
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
    check: <><path d="m5 12 4 4L19 6"/><circle cx="12" cy="12" r="9"/></>,
    bell: <><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></>,
  }
  return <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">{paths[name] ?? paths.box}</svg>
}

function App() {
  const [auth, setAuth] = useState<AuthSession | null>(() => readSession())
  const [notifications, setNotifications] = useState<StockNotification[]>([])
  const [unreadNotificationCount, setUnreadNotificationCount] = useState(0)
  const [notificationsOpen, setNotificationsOpen] = useState(false)
  const notificationWrapRef = useRef<HTMLDivElement>(null)
  const [authError, setAuthError] = useState('')
  const [authBusy, setAuthBusy] = useState(false)
  const [view, setView] = useState<View>('products')
  const [products, setProducts] = useState<Product[]>([])
  const [productPage, setProductPage] = useState<ProductPageResponse>({ items: [], currentPage: 1, pageSize: PRODUCT_PAGE_SIZE, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false })
  const [page, setPage] = useState(() => Math.max(1, Number(new URLSearchParams(window.location.search).get('page')) || 1))
  const [categories, setCategories] = useState<Category[]>([])
  const [categoryPageData, setCategoryPageData] = useState<CategoryPage>({ items: [], currentPage: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false })
  const [categoryPage, setCategoryPage] = useState(1)
  const [categoryLoading, setCategoryLoading] = useState(false)
  const [categoryRevision, setCategoryRevision] = useState(0)
  const [query, setQuery] = useState('')
  const [productSort, setProductSort] = useState<ProductSort>('price-desc')
  const [dialog, setDialog] = useState<Dialog>(null)
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [noticeExiting, setNoticeExiting] = useState(false)
  useEffect(() => {
    if (!notice) return
    setNoticeExiting(false)
    let exitTimeout = 0
    const timeout = window.setTimeout(() => {
      setNoticeExiting(true)
      exitTimeout = window.setTimeout(() => setNotice(''), 240)
    }, 4000)
    return () => { window.clearTimeout(timeout); window.clearTimeout(exitTimeout) }
  }, [notice])
  const [scannerOpen, setScannerOpen] = useState(false)
  const [receiptQr, setReceiptQr] = useState<{ batchNumber: string; svg: string } | null>(null)
  const [connected, setConnected] = useState(false)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [busyId, setBusyId] = useState('')
  const [profileOpen, setProfileOpen] = useState(false)
  const [warehouses, setWarehouses] = useState<WarehouseOption[]>([])

  useEffect(() => {
    if (!notificationsOpen) return
    const closeOnOutsidePointer = (event: PointerEvent) => {
      if (!notificationWrapRef.current?.contains(event.target as Node)) setNotificationsOpen(false)
    }
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setNotificationsOpen(false)
    }
    document.addEventListener('pointerdown', closeOnOutsidePointer)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePointer)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [notificationsOpen])

  async function refreshNotificationCount() {
    try {
      setUnreadNotificationCount(await request<number>('notifications/unread-count'))
    } catch { /* Notification badge can retry the next time it is opened. */ }
  }

  function announce(message: string) {
    setNotice(message)
    void refreshNotificationCount()
  }

  async function openNotifications() {
    const opening = !notificationsOpen
    setNotificationsOpen(opening)
    if (!opening) return
    try {
      const items = await request<StockNotification[]>('notifications')
      setNotifications(items)
      setUnreadNotificationCount(items.filter((item) => !item.isRead).length)
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Bildirişlər yüklənmədi.') }
  }

  async function markNotificationRead(id: string) {
    try {
      await request(`notifications/${id}/read`, { method: 'PUT' })
      setNotifications((items) => items.map((item) => item.id === id ? { ...item, isRead: true } : item))
      await refreshNotificationCount()
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Bildiriş oxunmadı.') }
  }

  async function markAllNotificationsRead() {
    try {
      await request('notifications/read-all', { method: 'PUT' })
      setNotifications((items) => items.map((item) => ({ ...item, isRead: true })))
      setUnreadNotificationCount(0)
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Bildirişlər oxunmadı.') }
  }

  function goToNotificationProduct(item: StockNotification) {
    if (!item.isRead) void markNotificationRead(item.id)
    setNotificationsOpen(false)
    setView('products')
    setQuery(item.productName)
    setPage(1)
  }

  useEffect(() => {
    if (auth) void refreshNotificationCount()
    else { setNotifications([]); setUnreadNotificationCount(0); setNotificationsOpen(false) }
  }, [auth])

  useEffect(() => {
    if (auth && notice) void refreshNotificationCount()
  }, [auth, notice])

  const loadData = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [productRows, categoryRows] = await Promise.all([
        request<ProductPageResponse>(`products?page=${page}&size=${PRODUCT_PAGE_SIZE}`), request<Category[]>('categories/options'),
      ])
      setProductPage(productRows)
      setPage(productRows.currentPage)
      setProducts(productRows.items.map(normalizeProductDates))
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
  }, [page])

  function changePage(nextPage: number) {
    const params = new URLSearchParams(window.location.search)
    params.set('page', String(nextPage))
    params.set('size', String(PRODUCT_PAGE_SIZE))
    window.history.replaceState(null, '', `${window.location.pathname}?${params.toString()}`)
    setPage(nextPage)
  }

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

  useEffect(() => {
    if (view !== 'categories') return
    const params = new URLSearchParams({ page: String(categoryPage), size: String(PRODUCT_PAGE_SIZE) })
    if (query.trim()) params.set('q', query.trim())
    setCategoryLoading(true)
    void request<CategoryPage>(`categories?${params}`)
      .then((result) => setCategoryPageData(result))
      .catch((caught) => setError(caught instanceof Error ? caught.message : 'Kateqoriyalar yüklənmədi.'))
      .finally(() => setCategoryLoading(false))
  }, [view, categoryPage, query, categoryRevision])

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
  const canViewPurchaseOrders = hasPermission(auth.user, 'po.view')
  const canManageSuppliers = hasPermission(auth.user, 'supplier.manage')
  const canManageStock = hasPermission(auth.user, 'stock.in')
  const canViewDocuments = hasPermission(auth.user, 'products.read') && (hasPermission(auth.user, 'stock.read') || hasPermission(auth.user, 'stock.in') || hasPermission(auth.user, 'stock.out'))
  const canCreateDocuments = hasPermission(auth.user, 'stock.in') || hasPermission(auth.user, 'stock.out')
  const canAdjustStock = auth.user.roles.includes('Admin') && hasPermission(auth.user, 'inventory.count')
  const canManageEmployees = hasPermission(auth.user, 'employees.manage')

  async function saveProduct(form: ProductForm, id?: string, initialStock?: { warehouseId: string; quantity: number; purchasePrice: number }, details?: { reorderLevel: number; quantity?: number }) {
    if (!canManageProducts) return
    setSaving(true); setError('')
    try {
      const productForm = {
        name: form.name, sku: form.sku, barcode: form.barcode, description: form.description,
        categoryId: form.categoryId, unitPrice: form.unitPrice, unitOfMeasure: form.unitOfMeasure, isActive: form.isActive,
        reorderLevel: details?.reorderLevel ?? 10,
      }
      const savedProduct = await request<Product>(id ? `products/${id}` : 'products', { method: id ? 'PUT' : 'POST', body: JSON.stringify(productForm) })
      if (id && details?.quantity !== undefined) {
        await request(`products/${id}/stock`, { method: 'PUT', body: JSON.stringify({ quantity: details.quantity }) })
      }
      if (!id && initialStock && initialStock.quantity > 0) {
        try {
          await request('inventory/receipts', {
            method: 'POST', body: JSON.stringify({
              productId: savedProduct.id, warehouseId: initialStock.warehouseId, quantity: initialStock.quantity,
              purchasePrice: initialStock.purchasePrice, salePrice: form.unitPrice, entryDate: new Date().toISOString(), expiryDate: null,
            }),
          })
        } catch (caught) {
          setDialog(null)
          await loadData()
          setError(`Məhsul yaradıldı, amma ilkin stok daxil edilmədi: ${caught instanceof Error ? caught.message : 'Xəta baş verdi.'}`)
          return
        }
      }
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
      announce(`${receipt.batchNumber} partiyası yaradıldı (${receipt.quantity} ${product.unitOfMeasure}).`)
      await loadData()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Stok yenilənmədi.')
    } finally {
      setSaving(false)
    }
  }

  async function adjustStock(product: Product, quantity: number) {
    if (!canAdjustStock) return
    setBusyId(product.id); setError('')
    try {
      const response = await request<ProductResponse>(`products/${product.id}/stock`, { method: 'PUT', body: JSON.stringify({ quantity }) })
      const saved = normalizeProductDates(response)
      setProducts((current) => current.map((item) => item.id === product.id ? { ...item, ...saved } : item))
      setProductPage((current) => ({ ...current, items: current.items.map((item) => item.id === product.id ? { ...item, ...saved } : item) }))
      announce(`${product.name} məhsulunun stok qalığı yeniləndi.`)
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Stok qalığı dəyişdirilmədi.') }
    finally { setBusyId('') }
  }

  async function updateProductInline(product: Product, patch: Partial<ProductForm>) {
    if (!canManageProducts) return
    setBusyId(product.id); setError('')
    try {
      const response = await request<ProductResponse>(`products/${product.id}`, { method: 'PUT', body: JSON.stringify({
        name: product.name, sku: product.sku, barcode: product.barcode, description: product.description,
        categoryId: product.categoryId, unitPrice: product.unitPrice, unitOfMeasure: product.unitOfMeasure,
        isActive: product.isActive, reorderLevel: product.reorderLevel, ...patch,
      }) })
      const saved = normalizeProductDates(response)
      setProducts((current) => current.map((item) => item.id === product.id ? { ...item, ...saved } : item))
      setProductPage((current) => ({ ...current, items: current.items.map((item) => item.id === product.id ? { ...item, ...saved } : item) }))
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Məhsul yenilənmədi.') }
    finally { setBusyId('') }
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
      setDialog(null); await loadData(); setCategoryRevision((revision) => revision + 1)
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
    try { await request(`${target.kind}/${target.id}`, { method: 'DELETE' }); setDeleteTarget(null); await loadData(); if (target.kind === 'categories') setCategoryRevision((revision) => revision + 1) }
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
        <button className={`nav-item ${view === 'categories' ? 'selected' : ''}`} type="button" onClick={() => { setView('categories'); setCategoryPage(1); setQuery('') }}><Icon name="tag"/><span>Kateqoriyalar</span><span className="nav-count">{categories.length}</span></button>
        {canViewPurchaseOrders && <button className={`nav-item ${view === 'purchase-orders' ? 'selected' : ''}`} type="button" onClick={() => { setView('purchase-orders'); setQuery('') }}><Icon name="package"/><span>Satınalma sifarişləri</span></button>}
        {canViewPurchaseOrders && <button className={`nav-item ${view === 'goods-receipts' ? 'selected' : ''}`} type="button" onClick={() => { setView('goods-receipts'); setQuery('') }}><Icon name="layers"/><span>Mal qəbulları (GRN)</span></button>}
        {canManageSuppliers && <button className={`nav-item ${view === 'suppliers' ? 'selected' : ''}`} type="button" onClick={() => { setView('suppliers'); setQuery('') }}><Icon name="tag"/><span>Təchizatçılar</span></button>}
        {canViewPurchaseOrders && <button className={`nav-item ${view === 'procurement-reports' ? 'selected' : ''}`} type="button" onClick={() => { setView('procurement-reports'); setQuery('') }}><Icon name="layers"/><span>Satınalma hesabatları</span></button>}
        {canViewDocuments && <button className={`nav-item ${view === 'documents' ? 'selected' : ''}`} type="button" onClick={() => { setView('documents'); setQuery('') }}><Icon name="layers"/><span>Stok sənədləri</span></button>}
        {auth.user.roles.includes('Admin') && <button className={`nav-item ${view === 'stock-logs' ? 'selected' : ''}`} type="button" onClick={() => { setView('stock-logs'); setQuery('') }}><Icon name="layers"/><span>Loqlar</span></button>}
      </nav>
      <div className="sidebar-bottom">
        <div className="help-card"><div className="help-icon"><Icon name="layers"/></div><strong>Stok nəzarətdə</strong><p>Məhsul və kateqoriyalarınızı bir yerdən idarə edin.</p></div>
        <div className="profile"><div className="avatar">{auth.user.firstName.slice(0, 1)}{auth.user.lastName.slice(0, 1)}</div><div className="profile-copy"><strong>{auth.user.firstName} {auth.user.lastName}</strong><span>{auth.user.roles.join(', ')}</span></div><button className="logout-button" type="button" onClick={() => void logout()}>Çıxış</button></div>
      </div>
    </aside>

    <main className="main-area">
      <header className="topbar">
        <div className="breadcrumbs"><span>İş sahəsi</span><Icon name="chevron" size={14}/><strong>{view === 'products' ? 'Məhsullar' : view === 'categories' ? 'Kateqoriyalar' : view === 'purchase-orders' ? 'Satınalma sifarişləri' : view === 'goods-receipts' ? 'Mal qəbulları' : view === 'suppliers' ? 'Təchizatçılar' : view === 'stock-logs' ? 'Loqlar' : view === 'documents' ? 'Stok sənədləri' : 'Satınalma hesabatları'}</strong></div>
        <div className="topbar-right"><a className="docs-link" href="http://localhost:5135/swagger" target="_blank" rel="noreferrer">API sənədləri <Icon name="arrow" size={14}/></a>{canManageEmployees && <button className="staff-button" type="button" onClick={() => setDialog({ kind: 'employee' })}>İşçi əlavə et</button>}<span className={`api-status ${connected ? 'online' : 'offline'}`}><i/>{connected ? 'API aktivdir' : 'API bağlantısı yoxdur'}</span><div className="notification-wrap" ref={notificationWrapRef}><button className="notification-trigger" type="button" aria-label={`Bildirişlər, oxunmamış: ${unreadNotificationCount}`} aria-expanded={notificationsOpen} onClick={() => void openNotifications()}><Icon name="bell" size={19}/>{unreadNotificationCount > 0 && <span className="notification-count">{unreadNotificationCount > 99 ? '99+' : unreadNotificationCount}</span>}</button>{notificationsOpen && <section className="notification-popover" aria-label="Stok bildirişləri"><div className="notification-heading"><strong>Stok bildirişləri</strong><button type="button" disabled={!unreadNotificationCount} onClick={() => void markAllNotificationsRead()}>Hamısını oxundu et</button></div><div className="notification-list">{notifications.length === 0 ? <p className="notification-empty">Bildiriş yoxdur.</p> : notifications.map((item) => <article className={`notification-item ${item.type === 'OUT_OF_STOCK' ? 'out-of-stock' : 'low-stock'} ${item.isRead ? 'is-read' : ''}`} key={item.id}><button className="notification-content" type="button" onClick={() => goToNotificationProduct(item)}><span className="notification-item-title">{item.productName}<b>{item.type === 'OUT_OF_STOCK' ? 'Stok bitib' : 'Az stok'}</b></span><span>Qalıq: {item.remainingQuantity.toLocaleString('az-AZ')}</span><time>{formatDateTime(item.createdAt)}</time></button>{!item.isRead && <button className="notification-read" type="button" onClick={() => void markNotificationRead(item.id)}>Oxundu</button>}</article>)}</div></section>}</div><div className="profile-trigger-wrap"><button className="top-avatar" type="button" aria-label="İstifadəçi profilini göstər" aria-expanded={profileOpen} onClick={() => setProfileOpen((open) => !open)}>{auth.user.firstName.slice(0, 1).toLocaleUpperCase('az')}{auth.user.lastName.slice(0, 1).toLocaleUpperCase('az')}</button>{profileOpen && <div className="profile-popover" role="status"><strong>{auth.user.firstName} {auth.user.lastName}</strong><span>{auth.user.email}</span><small>{auth.user.roles.join(', ')}</small><button type="button" onClick={() => { setProfileOpen(false); void logout() }}>Çıxış</button></div>}</div></div>
      </header>

      <div className="content-wrap">
        <section className="welcome-row">
          <div><div className="eyebrow">STOCK MANAGEMENT <span>•</span> İDARƏETMƏ PANELİ</div><h1>İşlər qaydasındadır.</h1><p className="welcome-subtitle">Anbar məlumatlarınızı izləyin və gündəlik əməliyyatları idarə edin.</p></div>
          <div className="date-chip"><span className="date-dot"/> Canlı məlumat <span className="date-divider"/>{new Intl.DateTimeFormat('az-AZ', { day: 'numeric', month: 'long', year: 'numeric' }).format(new Date())}</div>
        </section>
        {error && <div className="error-banner" role="alert"><span>{error}</span><button type="button" onClick={() => void loadData()}><Icon name="refresh" size={15}/> Yenidən yoxla</button></div>}
        {notice && <div className={`success-banner ${noticeExiting ? 'is-dismissing' : ''}`} role="status">{notice}{receiptQr && <button type="button" onClick={() => setDialog({ kind: 'qr' })}>QR kodu göstər</button>}<button type="button" onClick={() => { setNotice(''); setReceiptQr(null) }} aria-label="Bildirişi bağla"><Icon name="close" size={14}/></button></div>}

        <section className="stat-grid" aria-label="Qısa göstəricilər">
          <StatCard label="Ümumi məhsul" value={products.length.toLocaleString('az-AZ')} note="Kataloqda qeydiyyatda" icon="box" tone="blue" loading={loading}/>
          <StatCard label="Aktiv məhsul" value={activeCount.toLocaleString('az-AZ')} note={products.length ? `${Math.round(activeCount / products.length * 100)}% aktivdir` : 'Hələ məhsul əlavə edilməyib'} icon="arrow" tone="green" loading={loading}/>
          <StatCard label="Kateqoriya" value={categories.length.toLocaleString('az-AZ')} note="Məhsul qrupları" icon="layers" tone="violet" loading={loading}/>
          <StatCard label="Orta satış qiyməti" value={currency.format(averagePrice)} note="Kataloq üzrə orta göstərici" icon="tag" tone="amber" loading={loading}/>
        </section>

        {view === 'documents' ? <StockDocumentsPanel products={products} canCreate={canCreateDocuments} canReceive={hasPermission(auth.user, 'stock.in')} canIssue={hasPermission(auth.user, 'stock.out')} onError={setError} onNotice={announce}/> : view === 'stock-logs' ? <StockLogsPanel onError={setError}/> : view === 'purchase-orders' ? <PurchaseOrdersPanel user={auth.user} onError={setError} onNotice={announce} onOpenReceipts={() => setView('goods-receipts')}/> : view === 'goods-receipts' ? <GoodsReceiptsPanel user={auth.user} onError={setError} onNotice={announce}/> : view === 'suppliers' ? <SuppliersPanel user={auth.user} onError={setError} onNotice={announce}/> : view === 'procurement-reports' ? <ProcurementReportsPanel onError={setError}/> : <section className="data-panel">
        <div className="panel-heading"><div><h2>İnventar kataloqu</h2><p>Məhsulları və kateqoriyaları idarə edin.</p></div><div className="panel-actions"><button className="refresh-button" type="button" onClick={() => void loadData()} aria-label="Məlumatı yenilə"><Icon name="refresh" size={17}/></button>{canManageProducts && (view === 'products' || view === 'categories') && <button className="primary-button" type="button" onClick={openCreate}><Icon name="plus" size={17}/>{view === 'products' ? 'Məhsul əlavə et' : 'Kateqoriya əlavə et'}</button>}</div></div>
          <div className="toolbar">
            <div className="tabs" role="tablist" aria-label="İnventar bölməsi"><button type="button" role="tab" aria-selected={view === 'products'} className={view === 'products' ? 'active' : ''} onClick={() => { setView('products'); setQuery('') }}>Məhsullar <span>{products.length}</span></button><button type="button" role="tab" aria-selected={view === 'categories'} className={view === 'categories' ? 'active' : ''} onClick={() => { setView('categories'); setCategoryPage(1); setQuery('') }}>Kateqoriyalar <span>{categories.length}</span></button></div>
            <div className="toolbar-controls"><button className="secondary-button" type="button" onClick={() => setScannerOpen(true)}>QR / barkod skan et</button><div className="search-box"><Icon name="search" size={17}/><input aria-label={view === 'products' ? 'Məhsul, SKU və ya barkod axtar' : 'Kateqoriya axtar'} value={query} onChange={(event) => { setQuery(event.target.value); if (view === 'categories') setCategoryPage(1) }} placeholder={view === 'products' ? 'Məhsul, SKU və ya barkod axtar...' : 'Kateqoriya axtar...'}/>{query && <button className="clear-search" type="button" aria-label="Axtarışı təmizlə" title="Axtarışı təmizlə" onClick={() => { setQuery(''); if (view === 'categories') setCategoryPage(1) }}><Icon name="close" size={14}/></button>}<kbd>⌘ K</kbd></div>{view === 'products' && <label className="sort-control"><span>Sıralama</span><select value={productSort} onChange={(event) => setProductSort(event.target.value as ProductSort)}><option value="price-desc">Qiymət: bahadan ucuza</option><option value="price-asc">Qiymət: ucuzdan bahaya</option><option value="quantity-asc">Stok: azdan çoxa</option><option value="name-asc">Ad: A-dan Z-yə</option></select></label>}</div>
          </div>
          {view === 'products' ? <ProductTable products={filteredProducts} categories={categories} loading={loading} canManageProduct={canManageProducts} canManageStock={canManageStock} canAdjustStock={canAdjustStock} onUpdateProduct={updateProductInline} onEdit={(item) => setDialog({ kind: 'product', item })} onStock={(item) => setDialog({ kind: 'stock', item })} onAdjustStock={adjustStock} onError={setError} onDelete={(id) => requestDelete('products', id)} busyId={busyId}/> : <CategoryTable categories={categoryPageData.items} allCategories={categories} products={products} loading={loading || categoryLoading} canManage={canManageProducts} onEdit={(item) => setDialog({ kind: 'category', item })} onDelete={(id) => requestDelete('categories', id)} busyId={busyId}/>}
          {view === 'products' && productPage.totalPages > 1 && <div className="pagination"><span>{productPage.totalCount} məhsuldan {productPage.totalCount ? (productPage.currentPage - 1) * productPage.pageSize + 1 : 0}-{Math.min(productPage.currentPage * productPage.pageSize, productPage.totalCount)} göstərilir</span><div><button type="button" disabled={!productPage.hasPrevious} onClick={() => changePage(productPage.currentPage - 1)}>Əvvəlki</button>{Array.from({ length: productPage.totalPages }, (_, index) => index + 1).map(number => <button type="button" key={number} className={number === productPage.currentPage ? 'current' : ''} aria-current={number === productPage.currentPage ? 'page' : undefined} onClick={() => changePage(number)}>{number}</button>)}<button type="button" disabled={!productPage.hasNext} onClick={() => changePage(productPage.currentPage + 1)}>Növbəti</button></div></div>}
          {view === 'categories' && categoryPageData.totalPages > 1 && <div className="pagination"><span>{categoryPageData.totalCount} kateqoriyadan {(categoryPageData.currentPage - 1) * categoryPageData.pageSize + 1}-{Math.min(categoryPageData.currentPage * categoryPageData.pageSize, categoryPageData.totalCount)} göstərilir</span><div><button type="button" disabled={!categoryPageData.hasPrevious} onClick={() => setCategoryPage((current) => current - 1)}>Əvvəlki</button>{Array.from({ length: categoryPageData.totalPages }, (_, index) => index + 1).map((number) => <button type="button" key={number} className={number === categoryPage ? 'current' : ''} aria-current={number === categoryPage ? 'page' : undefined} onClick={() => setCategoryPage(number)}>{number}</button>)}<button type="button" disabled={!categoryPageData.hasNext} onClick={() => setCategoryPage((current) => current + 1)}>Növbəti</button></div></div>}
          <div className="panel-footer"><span>{view === 'products' ? `${filteredProducts.length} məhsul göstərilir` : `${filteredCategories.length} kateqoriya göstərilir`}</span><span>Stockroom <b>•</b> İnventar idarəetməsi</span></div>
        </section>}
        <footer className="page-footer"><span>© {new Date().getFullYear()} Stockroom</span><span>Sadə. Dəqiq. Nəzarətdə.</span></footer>
      </div>
    </main>

    {dialog?.kind === 'product' && <ProductDialog item={dialog.item} categories={categories} warehouses={warehouses} saving={saving} canManageProduct={canManageProducts} canManageStock={canManageStock} canAdjustStock={canAdjustStock} onClose={() => setDialog(null)} onSave={(form, initialStock, details) => void saveProduct(form, dialog.item?.id, initialStock, details)}/>}
    {dialog?.kind === 'stock' && <StockDialog item={dialog.item} warehouses={warehouses} saving={saving} onClose={() => setDialog(null)} onSave={(form) => void receiveStock(dialog.item, form)}/>}
    {dialog?.kind === 'category' && <CategoryDialog item={dialog.item} categories={categories} saving={saving} onClose={() => setDialog(null)} onSave={(form) => void saveCategory(form, dialog.item?.id)}/>}
    {dialog?.kind === 'employee' && <EmployeeDialog saving={saving} onClose={() => setDialog(null)} onSave={(form) => void addEmployee(form)}/>}
    {scannerOpen && <BarcodeScannerDialog onClose={() => setScannerOpen(false)} onScan={(code) => handleScannedCode(code)}/>}
    {dialog?.kind === 'qr' && receiptQr && <QrDisplayDialog batchNumber={receiptQr.batchNumber} svg={receiptQr.svg} onClose={() => setDialog(null)}/>}
    {deleteTarget && <ConfirmDeleteDialog target={deleteTarget} busy={busyId === deleteTarget.id} onCancel={() => setDeleteTarget(null)} onConfirm={() => void confirmDelete()}/>}
  </div>
}

type PurchaseOrderItem = { id: string; productId: string; productName: string; sku: string; orderedQty: number; receivedQty: number; remainingQty: number; unitPrice: number; lineTotal: number }
type PurchaseOrder = { id: string; poNumber: string; supplierId: string; supplierName: string; supplierPhone: string; supplierEmail: string; supplierAddress: string; supplierTaxId: string; warehouseId: string; warehouseName: string; status: string; orderDate: string; expectedDate: string | null; currency: string; totalAmount: number; note: string | null; createdBy: string; items: PurchaseOrderItem[]; goodsReceipts: { id: string; grnNumber: string; receivedDate: string; acceptedQty: number; rejectedQty: number }[] }
type PurchaseOrderOption = { id: string; name: string; sku?: string | null }
type PurchaseOrderOptions = { suppliers: PurchaseOrderOption[]; warehouses: PurchaseOrderOption[]; products: PurchaseOrderOption[] }
type PurchaseOrderPage = { items: PurchaseOrder[]; currentPage: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }
type PurchaseOrderLineDraft = { productId: string; orderedQty: number; unitPrice: number }

type Supplier = { id: string; name: string; phone: string; email: string; address: string; taxId: string; contactPerson: string; isActive: boolean }
type SupplierPage = { items: Supplier[]; currentPage: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }

function SuppliersPanel({ user, onError, onNotice }: { user: AuthUser; onError: (value: string) => void; onNotice: (value: string) => void }) {
  const [pageData, setPageData] = useState<SupplierPage>({ items: [], currentPage: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false })
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [editing, setEditing] = useState<Supplier | null | false>(false)
  const [draft, setDraft] = useState({ name: '', phone: '', email: '', address: '', taxId: '', contactPerson: '' })
  const [saving, setSaving] = useState(false)
  const [loading, setLoading] = useState(false)
  const [busyId, setBusyId] = useState('')
  const canManage = hasPermission(user, 'supplier.manage')
  const load = useCallback(async (requestedPage = 1) => {
    setLoading(true)
    const params = new URLSearchParams({ page: String(requestedPage), size: '10' })
    if (search.trim()) params.set('q', search.trim())
    if (status) params.set('isActive', status)
    try {
      const result = await request<SupplierPage>(`suppliers?${params}`)
      setPageData(result); setPage(result.currentPage)
    } finally { setLoading(false) }
  }, [search, status])
  useEffect(() => { void load(1).catch((error) => onError(error instanceof Error ? error.message : 'Təchizatçı siyahısı yüklənmədi.')) }, [load, onError])
  function beginEdit(supplier?: Supplier) {
    setEditing(supplier ?? null)
    setDraft(supplier ? { name: supplier.name, phone: supplier.phone, email: supplier.email, address: supplier.address, taxId: supplier.taxId, contactPerson: supplier.contactPerson } :
      { name: '', phone: '', email: '', address: '', taxId: '', contactPerson: '' })
  }
  async function save(event: FormEvent) {
    event.preventDefault()
    if (!draft.name.trim() || !draft.taxId.trim()) { onError('Təchizatçı adı və VÖEN məcburidir.'); return }
    setSaving(true); onError('')
    try {
      await request(editing ? `suppliers/${editing.id}` : 'suppliers', { method: editing ? 'PUT' : 'POST', body: JSON.stringify(draft) })
      setEditing(false); await load(1); onNotice(editing ? 'Təchizatçı yeniləndi.' : 'Təchizatçı yaradıldı.')
    } catch (error) { onError(error instanceof Error ? error.message : 'Təchizatçı saxlanmadı.') }
    finally { setSaving(false) }
  }
  async function setActive(supplier: Supplier) {
    setBusyId(supplier.id); onError('')
    try {
      await request(`suppliers/${supplier.id}/${supplier.isActive ? 'deactivate' : 'activate'}`, { method: 'POST' })
      await load(page)
      onNotice(`${supplier.name} ${supplier.isActive ? 'deaktiv edildi' : 'aktiv edildi'}.`)
    }
    catch (error) { onError(error instanceof Error ? error.message : 'Təchizatçı statusu dəyişmədi.') }
    finally { setBusyId('') }
  }
  return <section className="data-panel">
    <div className="panel-heading"><div className="module-heading"><span className="module-icon supplier-icon"><Icon name="layers" size={19}/></span><div><h2>Təchizatçılar</h2><p>Təchizatçı məlumatları və aktivlik statusu.</p></div></div><div className="panel-actions"><button className={`refresh-button ${loading ? 'is-refreshing' : ''}`} type="button" disabled={loading} onClick={() => void load(page)} aria-label="Siyahını yenilə" title="Siyahını yenilə"><Icon name="refresh" size={16}/><span>{loading ? 'Yenilənir' : 'Yenilə'}</span></button>{canManage && <button className="primary-button" type="button" onClick={() => beginEdit()}><Icon name="plus" size={17}/>Yeni təchizatçı</button>}</div></div>
    <div className="toolbar"><div className="toolbar-controls"><div className="search-box"><Icon name="search" size={17}/><input aria-label="Təchizatçı axtar" placeholder="Ad, telefon, email və ya VÖEN" value={search} onChange={(event) => setSearch(event.target.value)}/></div><label className="sort-control"><span>Status</span><select value={status} onChange={(event) => setStatus(event.target.value)}><option value="">Hamısı</option><option value="true">Aktiv</option><option value="false">Deaktiv</option></select></label></div></div>
    {editing !== false && <form className="po-editor supplier-editor" onSubmit={(event) => void save(event)}><div className="editor-heading"><span className="editor-icon"><Icon name={editing ? 'edit' : 'plus'} size={17}/></span><div><h3>{editing ? 'Təchizatçını redaktə et' : 'Yeni təchizatçı'}</h3><p>Əsas məlumatları daxil edin və yadda saxlayın.</p></div></div><div className="po-editor-fields"><label className="field"><span>Ad *</span><input maxLength={200} required value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })}/></label><label className="field"><span>Telefon</span><input maxLength={32} value={draft.phone} onChange={(e) => setDraft({ ...draft, phone: e.target.value })}/></label><label className="field"><span>Email</span><input type="email" maxLength={320} value={draft.email} onChange={(e) => setDraft({ ...draft, email: e.target.value })}/></label><label className="field"><span>VÖEN *</span><input maxLength={32} required value={draft.taxId} onChange={(e) => setDraft({ ...draft, taxId: e.target.value })}/></label><label className="field"><span>Əlaqələndirici</span><input maxLength={150} value={draft.contactPerson} onChange={(e) => setDraft({ ...draft, contactPerson: e.target.value })}/></label><label className="field"><span>Ünvan</span><input maxLength={500} value={draft.address} onChange={(e) => setDraft({ ...draft, address: e.target.value })}/></label></div><div className="po-editor-actions"><button className="primary-button" disabled={saving}>{saving ? <><span className="button-spinner"/>Saxlanır…</> : <><Icon name="check" size={15}/>Yadda saxla</>}</button><button className="secondary-button" type="button" disabled={saving} onClick={() => setEditing(false)}>Bağla</button></div></form>}
    <div className="module-table-wrap">{loading && <div className="module-loading" role="status"><span className="button-spinner"/>Təchizatçılar yüklənir</div>}<div className="table-scroll"><table className="data-table"><thead><tr><th>Ad</th><th>Telefon</th><th>Email</th><th>VÖEN</th><th>Status</th><th>Əməliyyat</th></tr></thead><tbody>{pageData.items.length === 0 ? <EmptyRow colSpan={6} title={loading ? 'Yüklənir…' : 'Təchizatçı tapılmadı'} detail={loading ? 'Məlumat bazası ilə əlaqə qurulur.' : 'Axtarış və filtrləri yoxlayın.'}/>: pageData.items.map((supplier) => <tr key={supplier.id}><td><div className="supplier-name"><span className="supplier-avatar">{supplier.name.slice(0, 1).toLocaleUpperCase('az')}</span><strong>{supplier.name}</strong></div></td><td>{supplier.phone || '—'}</td><td>{supplier.email || '—'}</td><td><span className="mono-value">{supplier.taxId}</span></td><td><span className={`status-badge ${supplier.isActive ? 'status-active' : 'status-inactive'}`}><i/>{supplier.isActive ? 'Aktiv' : 'Deaktiv'}</span></td><td><div className="table-actions">{canManage && <><button className="table-action edit-action" type="button" disabled={!!busyId} onClick={() => beginEdit(supplier)}><Icon name="edit" size={14}/><span>Redaktə</span></button><button className={`table-action ${supplier.isActive ? 'deactivate-action' : 'activate-action'}`} type="button" disabled={!!busyId} onClick={() => void setActive(supplier)}>{busyId === supplier.id ? <span className="button-spinner"/> : <Icon name={supplier.isActive ? 'close' : 'check'} size={14}/>}<span>{supplier.isActive ? 'Deaktiv et' : 'Aktiv et'}</span></button></>}</div></td></tr>)}</tbody></table></div></div>
    {pageData.totalPages > 1 && <div className="pagination"><span>{pageData.totalCount} təchizatçıdan {(pageData.currentPage - 1) * pageData.pageSize + 1}-{Math.min(pageData.currentPage * pageData.pageSize, pageData.totalCount)} göstərilir</span><div><button type="button" disabled={!pageData.hasPrevious} onClick={() => void load(page - 1)}>Əvvəlki</button>{Array.from({ length: pageData.totalPages }, (_, index) => index + 1).map((number) => <button type="button" key={number} className={number === page ? 'current' : ''} onClick={() => void load(number)}>{number}</button>)}<button type="button" disabled={!pageData.hasNext} onClick={() => void load(page + 1)}>Növbəti</button></div></div>}
    <div className="panel-footer"><span>{pageData.totalCount} təchizatçı</span><span>PO ilə əlaqəli təchizatçı silinmir, deaktiv edilir.</span></div>
  </section>
}

function PurchaseOrdersPanel({ user, onError, onNotice, onOpenReceipts }: { user: AuthUser; onError: (value: string) => void; onNotice: (value: string) => void; onOpenReceipts: () => void }) {
  const [result, setResult] = useState<PurchaseOrderPage>({ items: [], currentPage: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false })
  const [options, setOptions] = useState<PurchaseOrderOptions>({ suppliers: [], warehouses: [], products: [] })
  const [statusFilter, setStatusFilter] = useState('')
  const [supplierFilter, setSupplierFilter] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [page, setPage] = useState(1)
  const [expandedId, setExpandedId] = useState('')
  const [editing, setEditing] = useState<PurchaseOrder | null | false>(false)
  const [supplierId, setSupplierId] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [orderDate, setOrderDate] = useState(new Date().toISOString().slice(0, 10))
  const [expectedDate, setExpectedDate] = useState('')
  const [currencyCode, setCurrencyCode] = useState('AZN')
  const [note, setNote] = useState('')
  const [lines, setLines] = useState<PurchaseOrderLineDraft[]>([])
  const [lineProduct, setLineProduct] = useState('')
  const [lineQty, setLineQty] = useState('1')
  const [linePrice, setLinePrice] = useState('0')
  const [saving, setSaving] = useState(false)
  const [busyId, setBusyId] = useState('')
  const [loading, setLoading] = useState(false)
  const [receivingOrder, setReceivingOrder] = useState<PurchaseOrder | null>(null)
  const [receiptDraft, setReceiptDraft] = useState<Record<string, { receivedQty: number; rejectedQty: number; unitCost: number; batchNo: string; expiryDate: string }>>({})
  const [receiptDate, setReceiptDate] = useState(new Date().toISOString().slice(0, 10))
  const [receiptNote, setReceiptNote] = useState('')
  const [receiptKey, setReceiptKey] = useState('')
  const canCreate = hasPermission(user, 'po.create')
  const canApprove = hasPermission(user, 'po.approve')
  const canCancel = hasPermission(user, 'po.cancel')
  const canReceive = hasPermission(user, 'grn.create')

  const load = useCallback(async (requestedPage = 1) => {
    setLoading(true)
    const params = new URLSearchParams({ page: String(requestedPage), size: '10' })
    if (statusFilter) params.set('status', statusFilter)
    if (supplierFilter) params.set('supplierId', supplierFilter)
    if (fromDate) params.set('fromDate', fromDate)
    if (toDate) params.set('toDate', toDate)
    try {
      const data = await request<PurchaseOrderPage>(`purchase-orders?${params}`)
      setResult(data)
      setPage(data.currentPage)
    } finally { setLoading(false) }
  }, [statusFilter, supplierFilter, fromDate, toDate])

  useEffect(() => {
    void load(1).catch((error) => onError(error instanceof Error ? error.message : 'PO siyahısı yüklənmədi.'))
    void request<PurchaseOrderOption[]>('purchase-orders/options/suppliers').then((suppliers) => setOptions((current) => ({ ...current, suppliers })))
      .catch((error) => onError(error instanceof Error ? error.message : 'Təchizatçı siyahısı yüklənmədi.'))
    if (canCreate) void request<PurchaseOrderOptions>('purchase-orders/options')
      .then((loaded) => setOptions(loaded)).catch((error) => onError(error instanceof Error ? error.message : 'Seçim siyahıları yüklənmədi.'))
  }, [load, canCreate, onError])

  function beginCreate() {
    setEditing(null); setSupplierId(options.suppliers[0]?.id ?? ''); setWarehouseId(options.warehouses[0]?.id ?? '')
    setOrderDate(new Date().toISOString().slice(0, 10)); setExpectedDate(''); setCurrencyCode('AZN'); setNote(''); setLines([])
    setLineProduct(options.products[0]?.id ?? '')
  }

  function beginEdit(order: PurchaseOrder) {
    setEditing(order); setSupplierId(order.supplierId); setWarehouseId(order.warehouseId); setOrderDate(order.orderDate)
    setExpectedDate(order.expectedDate ?? ''); setCurrencyCode(order.currency); setNote(order.note ?? '')
    setLines(order.items.map((item) => ({ productId: item.productId, orderedQty: item.orderedQty, unitPrice: item.unitPrice })))
    setLineProduct(options.products[0]?.id ?? '')
  }

  function addLine() {
    const qty = Number(lineQty); const price = Number(linePrice)
    if (!lineProduct || !Number.isFinite(qty) || qty <= 0 || !Number.isFinite(price) || price < 0 || lines.some((line) => line.productId === lineProduct)) {
      onError('Məhsul təkrarlanmamalı, miqdar 0-dan böyük, qiymət isə 0 və ya böyük olmalıdır.')
      return
    }
    setLines((current) => [...current, { productId: lineProduct, orderedQty: qty, unitPrice: price }])
    setLineProduct(options.products.find((x) => x.id !== lineProduct && !lines.some((line) => line.productId === x.id))?.id ?? '')
    onError('')
  }

  async function saveDraft(event: FormEvent) {
    event.preventDefault()
    if (!supplierId || !warehouseId || lines.length === 0) { onError('Təchizatçı, anbar və ən azı bir məhsul sətri seçin.'); return }
    setSaving(true); onError('')
    const body = { supplierId, warehouseId, orderDate, expectedDate: expectedDate || null, currency: currencyCode, note,
      items: lines.map((line) => ({ ...line, lineTotal: line.orderedQty * line.unitPrice })),
      totalAmount: lines.reduce((sum, line) => sum + line.orderedQty * line.unitPrice, 0) }
    try {
      await request(editing ? `purchase-orders/${editing.id}` : 'purchase-orders', { method: editing ? 'PUT' : 'POST', body: JSON.stringify(body) })
      setEditing(false); await load(1); onNotice(editing ? 'Draft PO yeniləndi.' : 'Draft PO yaradıldı.');
    } catch (error) { onError(error instanceof Error ? error.message : 'PO yadda saxlanmadı.') }
    finally { setSaving(false) }
  }

  async function action(order: PurchaseOrder, operation: 'submit' | 'approve' | 'cancel') {
    const reason = operation === 'cancel' ? window.prompt('Ləğv səbəbini daxil edin:') : null
    if (operation === 'cancel' && !reason?.trim()) return
    setBusyId(order.id); onError('')
    try {
      await request(`purchase-orders/${order.id}/${operation}`, { method: 'POST', ...(operation === 'cancel' ? { body: JSON.stringify({ reason }) } : {}) })
      await load(); onNotice(`${order.poNumber} statusu yeniləndi.`)
    } catch (error) { onError(error instanceof Error ? error.message : 'Status dəyişdirilmədi.') }
    finally { setBusyId('') }
  }

  function beginReceipt(order: PurchaseOrder) {
    setReceivingOrder(order)
    setReceiptDate(new Date().toISOString().slice(0, 10))
    setReceiptNote('')
    setReceiptKey(crypto.randomUUID())
    setReceiptDraft(Object.fromEntries(order.items.map((item) => [item.id, {
      receivedQty: Math.max(0, item.remainingQty), rejectedQty: 0, unitCost: item.unitPrice, batchNo: '', expiryDate: '',
    }])))
  }

  async function createReceipt(event: FormEvent) {
    event.preventDefault()
    if (!receivingOrder) return
    const items = receivingOrder.items.map((item) => ({ purchaseOrderItemId: item.id, ...receiptDraft[item.id],
      batchNo: receiptDraft[item.id]?.batchNo.trim() || null, expiryDate: receiptDraft[item.id]?.expiryDate || null }))
      .filter((item) => item.receivedQty + item.rejectedQty > 0)
    setSaving(true); onError('')
    try {
      await request('goods-receipts', { method: 'POST', headers: { 'Idempotency-Key': receiptKey }, body: JSON.stringify({
        purchaseOrderId: receivingOrder.id, receivedDate: receiptDate, note: receiptNote, idempotencyKey: receiptKey, items,
      }) })
      const poNumber = receivingOrder.poNumber
      setReceivingOrder(null)
      await load(page)
      onNotice(`Goods receipt yaradıldı (${poNumber}).`)
    } catch (error) { onError(error instanceof Error ? error.message : 'GRN yaradılmadı.') }
    finally { setSaving(false) }
  }

  return <section className="data-panel">
    <div className="panel-heading"><div className="module-heading"><span className="module-icon po-icon"><Icon name="box" size={19}/></span><div><h2>Satınalma sifarişləri</h2><p>Sifarişləri, təsdiqləri və qəbulları bir yerdən idarə edin.</p></div></div><div className="panel-actions"><button className={`refresh-button ${loading ? 'is-refreshing' : ''}`} type="button" disabled={loading} aria-label="Siyahını yenilə" title="Siyahını yenilə" onClick={() => void load(page)}><Icon name="refresh" size={16}/><span>{loading ? 'Yenilənir' : 'Yenilə'}</span></button>{canCreate && <button className="primary-button" type="button" onClick={beginCreate}><Icon name="plus" size={17}/>Yeni PO</button>}</div></div>
    <div className="toolbar"><div className="toolbar-controls">
      <label className="sort-control"><span>Status</span><select value={statusFilter} onChange={(e) => { setPage(1); setStatusFilter(e.target.value) }}><option value="">Hamısı</option>{['draft','pending_approval','approved','partially_received','received','cancelled'].map((s) => <option key={s} value={s}>{s}</option>)}</select></label>
      <label className="sort-control"><span>Təchizatçı</span><select value={supplierFilter} onChange={(e) => { setPage(1); setSupplierFilter(e.target.value) }}><option value="">Hamısı</option>{options.suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
      <label className="sort-control"><span>Tarixdən</span><input type="date" value={fromDate} onChange={(e) => { setPage(1); setFromDate(e.target.value) }}/></label>
      <label className="sort-control"><span>Tarixədək</span><input type="date" value={toDate} onChange={(e) => { setPage(1); setToDate(e.target.value) }}/></label>
    </div></div>
    {editing !== false && <form className="po-editor" onSubmit={(event) => void saveDraft(event)}>
      <h3>{editing ? `${editing.poNumber} draft redaktəsi` : 'Yeni draft PO'}</h3>
      <div className="po-editor-fields"><label className="field"><span>Təchizatçı</span><select value={supplierId} onChange={(e) => setSupplierId(e.target.value)} required>{options.suppliers.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label className="field"><span>Anbar</span><select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>{options.warehouses.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label className="field"><span>Sifariş tarixi</span><input type="date" value={orderDate} onChange={(e) => setOrderDate(e.target.value)} required/></label>
        <label className="field"><span>Gözlənilən tarix</span><input type="date" value={expectedDate} onChange={(e) => setExpectedDate(e.target.value)}/></label>
        <label className="field"><span>Valyuta</span><input maxLength={3} value={currencyCode} onChange={(e) => setCurrencyCode(e.target.value.toUpperCase())}/></label>
        <label className="field"><span>Qeyd</span><input maxLength={2000} value={note} onChange={(e) => setNote(e.target.value)}/></label></div>
      <div className="po-line-adder"><select aria-label="Məhsul" value={lineProduct} onChange={(e) => setLineProduct(e.target.value)}><option value="">Məhsul seçin</option>{options.products.filter((x) => !lines.some((line) => line.productId === x.id)).map((x) => <option key={x.id} value={x.id}>{x.name} ({x.sku})</option>)}</select><input aria-label="Sifariş miqdarı" type="number" min="0.001" step="0.001" placeholder="Miqdar" value={lineQty} onChange={(e) => setLineQty(e.target.value)}/><input aria-label="Vahid qiyməti" type="number" min="0" step="0.01" placeholder="Vahid qiyməti" value={linePrice} onChange={(e) => setLinePrice(e.target.value)}/><button type="button" className="secondary-button" onClick={addLine}>Sətir əlavə et</button></div>
      {lines.length > 0 && <div className="po-lines">{lines.map((line) => <div key={line.productId}><span>{options.products.find((x) => x.id === line.productId)?.name}</span><span>{line.orderedQty} × {currency.format(line.unitPrice)} = {currency.format(line.orderedQty * line.unitPrice)}</span><button type="button" onClick={() => setLines((current) => current.filter((x) => x.productId !== line.productId))}>Sil</button></div>)}<strong>Cəmi: {currency.format(lines.reduce((sum, line) => sum + line.orderedQty * line.unitPrice, 0))}</strong></div>}
      <div className="po-editor-actions"><button className="primary-button" disabled={saving}>{saving ? 'Saxlanır…' : 'Draft saxla'}</button><button className="secondary-button" type="button" onClick={() => setEditing(false)}>Bağla</button></div>
    </form>}
    {receivingOrder && <form className="po-editor" onSubmit={(event) => void createReceipt(event)}>
      <h3>{receivingOrder.poNumber} üçün mal qəbulu</h3><p>Qalıq miqdarlar əvvəlcədən doldurulub. Qüsurlu miqdar stoka daxil edilmir.</p>
      <div className="po-lines">{receivingOrder.items.map((item) => <div className="grn-entry" key={item.id}><strong>{item.productName} ({item.sku}) · qalıq {item.remainingQty}</strong><label>Qəbul<input type="number" min="0" step="0.001" value={receiptDraft[item.id]?.receivedQty ?? 0} onChange={(e) => setReceiptDraft((current) => ({ ...current, [item.id]: { ...current[item.id], receivedQty: Number(e.target.value) } }))}/></label><label>Qüsurlu<input type="number" min="0" step="0.001" value={receiptDraft[item.id]?.rejectedQty ?? 0} onChange={(e) => setReceiptDraft((current) => ({ ...current, [item.id]: { ...current[item.id], rejectedQty: Number(e.target.value) } }))}/></label><label>Maya dəyəri<input type="number" min="0" step="0.01" value={receiptDraft[item.id]?.unitCost ?? 0} onChange={(e) => setReceiptDraft((current) => ({ ...current, [item.id]: { ...current[item.id], unitCost: Number(e.target.value) } }))}/></label><label>Partiya<input maxLength={64} value={receiptDraft[item.id]?.batchNo ?? ''} onChange={(e) => setReceiptDraft((current) => ({ ...current, [item.id]: { ...current[item.id], batchNo: e.target.value } }))}/></label><label>Son istifadə<input type="date" value={receiptDraft[item.id]?.expiryDate ?? ''} onChange={(e) => setReceiptDraft((current) => ({ ...current, [item.id]: { ...current[item.id], expiryDate: e.target.value } }))}/></label></div>)}</div>
      <div className="po-editor-fields"><label className="field"><span>Qəbul tarixi</span><input type="date" value={receiptDate} onChange={(e) => setReceiptDate(e.target.value)} required/></label><label className="field"><span>Qeyd</span><input value={receiptNote} onChange={(e) => setReceiptNote(e.target.value)}/></label></div>
      <div className="po-editor-actions"><button className="primary-button" disabled={saving}>{saving ? 'Qəbul edilir…' : 'GRN yarat'}</button><button className="secondary-button" type="button" onClick={() => setReceivingOrder(null)}>Bağla</button></div>
    </form>}
    <div className="module-table-wrap">{loading && <div className="module-loading" role="status"><span className="button-spinner"/>Sifarişlər yenilənir</div>}<div className="table-scroll"><table className="data-table"><thead><tr><th>PO nömrəsi</th><th>Təchizatçı</th><th>Anbar</th><th>Sifariş tarixi</th><th>Gözlənilən tarix</th><th>Status</th><th>Cəmi</th><th>Əməliyyat</th></tr></thead><tbody>
      {result.items.length === 0 ? <EmptyRow colSpan={8} title="PO tapılmadı" detail="Filtrləri dəyişin və ya yeni PO yaradın."/> : result.items.map((order) => <Fragment key={order.id}><tr><td><strong>{order.poNumber}</strong></td><td>{order.supplierName}</td><td>{order.warehouseName}</td><td>{order.orderDate}</td><td>{order.expectedDate ?? '—'}</td><td><span className={`status-badge status-${order.status}`}>{order.status}</span></td><td>{currency.format(order.totalAmount)}</td><td className="po-actions"><button type="button" onClick={() => setExpandedId(expandedId === order.id ? '' : order.id)}>Detallar</button>{canCreate && order.status === 'draft' && <><button type="button" onClick={() => beginEdit(order)}>Redaktə</button><button type="button" disabled={busyId === order.id} onClick={() => void action(order, 'submit')}>Təsdiqə göndər</button></>}{canApprove && order.status === 'pending_approval' && <button type="button" disabled={busyId === order.id} onClick={() => void action(order, 'approve')}>Təsdiqlə</button>}{canReceive && ['approved','partially_received'].includes(order.status) && <button type="button" onClick={() => beginReceipt(order)}>GRN qəbul</button>}{canCancel && ['draft','pending_approval','approved'].includes(order.status) && <button type="button" disabled={busyId === order.id} onClick={() => void action(order, 'cancel')}>Ləğv et</button>}</td></tr>{expandedId === order.id && <tr><td colSpan={8}><div className="po-detail-lines"><div className="detail-actions"><strong>PO detalı</strong><button className="secondary-button" type="button" onClick={() => window.print()}>Çap et</button></div>{order.items.map((line) => <div key={line.id}><strong>{line.productName} ({line.sku})</strong><span>Sifariş: {line.orderedQty}</span><span>Qəbul: {line.receivedQty}</span><span>Qalıq: {line.remainingQty}</span><span>{currency.format(line.lineTotal)}</span></div>)}<strong>Bağlı GRN-lər:</strong>{order.goodsReceipts.map((grn) => <button type="button" key={grn.id} onClick={onOpenReceipts}>{grn.grnNumber} · {grn.receivedDate} · qəbul {grn.acceptedQty} · qüsurlu {grn.rejectedQty}</button>)}<PurchaseOrderPrintView order={order}/></div></td></tr>}</Fragment>)}
    </tbody></table></div></div>
    {result.totalPages > 1 && <div className="pagination"><span>{result.totalCount} PO-dan {(result.currentPage - 1) * result.pageSize + 1}-{Math.min(result.currentPage * result.pageSize, result.totalCount)} göstərilir</span><div><button type="button" disabled={!result.hasPrevious} onClick={() => void load(page - 1)}>Əvvəlki</button>{Array.from({ length: result.totalPages }, (_, i) => i + 1).map((n) => <button type="button" key={n} className={n === page ? 'current' : ''} onClick={() => void load(n)}>{n}</button>)}<button type="button" disabled={!result.hasNext} onClick={() => void load(page + 1)}>Növbəti</button></div></div>}
    <div className="panel-footer"><span>{result.totalCount} satınalma sifarişi</span><span>PO yaratmaq və dəyişmək stoka toxunmur.</span></div>
  </section>
}

function PurchaseOrderPrintView({ order }: { order: PurchaseOrder }) {
  return <div className="printable-document"><header><h1>Satınalma sifarişi</h1><strong>{order.poNumber}</strong></header><div className="print-meta"><p><b>Təchizatçı:</b> {order.supplierName}</p><p><b>Telefon:</b> {order.supplierPhone || '—'}</p><p><b>Email:</b> {order.supplierEmail || '—'}</p><p><b>VÖEN:</b> {order.supplierTaxId || '—'}</p><p><b>Ünvan:</b> {order.supplierAddress || '—'}</p><p><b>Təyinat anbarı:</b> {order.warehouseName}</p><p><b>Sifariş tarixi:</b> {order.orderDate}</p><p><b>Gözlənilən tarix:</b> {order.expectedDate ?? '—'}</p><p><b>Status:</b> {order.status}</p></div><table><thead><tr><th>Məhsul</th><th>SKU</th><th>Sifariş</th><th>Qəbul</th><th>Qalıq</th><th>Vahid qiymət</th><th>Cəmi</th></tr></thead><tbody>{order.items.map((item) => <tr key={item.id}><td>{item.productName}</td><td>{item.sku}</td><td>{item.orderedQty}</td><td>{item.receivedQty}</td><td>{item.remainingQty}</td><td>{currency.format(item.unitPrice)}</td><td>{currency.format(item.lineTotal)}</td></tr>)}</tbody></table><p className="print-total"><b>Ümumi məbləğ:</b> {currency.format(order.totalAmount)}</p>{order.note && <p><b>Qeyd:</b> {order.note}</p>}</div>
}

type GoodsReceiptRow = { id: string; grnNumber: string; purchaseOrderId: string; poNumber: string; warehouseName: string; receivedDate: string; receivedBy: string; receivedByName: string; note: string | null; reversedAt: string | null; reverseReason: string | null; items: { id: string; productName: string; receivedQty: number; rejectedQty: number; unitCost: number; batchNo: string | null; expiryDate: string | null }[] }
type GoodsReceiptPageResponse = { items: GoodsReceiptRow[]; currentPage: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }

function GoodsReceiptsPanel({ user, onError, onNotice }: { user: AuthUser; onError: (value: string) => void; onNotice: (value: string) => void }) {
  const [data, setData] = useState<GoodsReceiptPageResponse>({ items: [], currentPage: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false })
  const [page, setPage] = useState(1)
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [expandedId, setExpandedId] = useState('')
  const [reverseTarget, setReverseTarget] = useState<GoodsReceiptRow | null>(null)
  const [reverseReason, setReverseReason] = useState('')
  const [reverseBusy, setReverseBusy] = useState(false)
  const [loading, setLoading] = useState(false)
  const canReverse = hasPermission(user, 'grn.reverse')
  const load = useCallback(async (requestedPage = 1) => {
    setLoading(true)
    const params = new URLSearchParams({ page: String(requestedPage), size: '10' })
    if (fromDate) params.set('fromDate', fromDate)
    if (toDate) params.set('toDate', toDate)
    try {
      const result = await request<GoodsReceiptPageResponse>(`goods-receipts?${params}`)
      setData(result); setPage(result.currentPage)
    } finally { setLoading(false) }
  }, [fromDate, toDate])
  const reverse = async (event: FormEvent) => {
    event.preventDefault()
    if (!reverseTarget || !reverseReason.trim()) return
    setReverseBusy(true)
    try {
      await request(`goods-receipts/${reverseTarget.id}/reverse`, { method: 'POST', body: JSON.stringify({ reason: reverseReason.trim() }) })
      setReverseTarget(null); setReverseReason('')
      await load(page)
      onNotice('Mal qəbulu geri qaytarıldı.')
    } catch (error) { onError(error instanceof Error ? error.message : 'GRN geri qaytarılmadı.') }
    finally { setReverseBusy(false) }
  }
  useEffect(() => { void load(1).catch((error) => onError(error instanceof Error ? error.message : 'GRN siyahısı yüklənmədi.')) }, [load, onError])

  return <section className="data-panel">
    <div className="panel-heading"><div className="module-heading"><span className="module-icon grn-icon"><Icon name="layers" size={19}/></span><div><h2>Mal qəbulları (GRN)</h2><p>Qəbul sənədlərini, məhsul sətirlərini və qaytarmaları izləyin.</p></div></div><div className="panel-actions"><button className={`refresh-button ${loading ? 'is-refreshing' : ''}`} type="button" disabled={loading} onClick={() => void load(page)} aria-label="GRN siyahısını yenilə" title="Siyahını yenilə"><Icon name="refresh" size={16}/><span>{loading ? 'Yenilənir' : 'Yenilə'}</span></button></div></div>
    <div className="toolbar"><div className="toolbar-controls"><label className="sort-control"><span>Tarixdən</span><input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)}/></label><label className="sort-control"><span>Tarixədək</span><input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)}/></label></div></div>
    <div className="module-table-wrap">{loading && <div className="module-loading" role="status"><span className="button-spinner"/>Qəbullar yenilənir</div>}<div className="table-scroll"><table className="data-table"><thead><tr><th>GRN nömrəsi</th><th>PO</th><th>Anbar</th><th>Qəbul tarixi</th><th>Qəbul edən</th><th>Məhsul sətri</th><th>Detallar</th></tr></thead><tbody>
      {data.items.length === 0 ? <EmptyRow colSpan={7} title="GRN tapılmadı" detail="Təsdiqlənmiş PO-dan qəbul yaradın."/> : data.items.map((receipt) => <Fragment key={receipt.id}><tr><td><strong>{receipt.grnNumber}</strong>{receipt.reversedAt && <span className="category-pill">Geri qaytarılıb</span>}</td><td>{receipt.poNumber}</td><td>{receipt.warehouseName}</td><td>{receipt.receivedDate}</td><td>{receipt.receivedByName}</td><td>{receipt.items.length}</td><td><button className="secondary-button" type="button" onClick={() => setExpandedId(expandedId === receipt.id ? '' : receipt.id)}>Detalları göstər</button></td></tr>{expandedId === receipt.id && <tr><td colSpan={7}><div className="po-detail-lines"><div className="detail-actions"><strong>{receipt.grnNumber} · {receipt.poNumber}</strong><button className="secondary-button" type="button" onClick={() => window.print()}>Çap et</button></div>{receipt.items.map((line) => <div key={line.id}><strong>{line.productName}</strong><span>Qəbul: {line.receivedQty}</span><span>Qüsurlu: {line.rejectedQty}</span><span>Maya: {currency.format(line.unitCost)}</span><span>Partiya: {line.batchNo ?? '—'}</span><span>Son istifadə: {line.expiryDate ?? '—'}</span></div>)}{receipt.note && <p>{receipt.note}</p>}{receipt.reverseReason && <p>Geri qaytarma səbəbi: {receipt.reverseReason}</p>}{canReverse && !receipt.reversedAt && receipt.items.some((line) => line.receivedQty > 0) && <button className="secondary-button" type="button" onClick={() => { setReverseTarget(receipt); setReverseReason('') }}>Qəbulu geri qaytar</button>}<GoodsReceiptPrintView receipt={receipt}/></div></td></tr>}</Fragment>)}
    </tbody></table></div></div>
    {data.totalPages > 1 && <div className="pagination"><span>{data.totalCount} GRN-dən {(data.currentPage - 1) * data.pageSize + 1}-{Math.min(data.currentPage * data.pageSize, data.totalCount)} göstərilir</span><div><button type="button" disabled={!data.hasPrevious} onClick={() => void load(page - 1)}>Əvvəlki</button>{Array.from({ length: data.totalPages }, (_, i) => i + 1).map((n) => <button type="button" key={n} className={n === page ? 'current' : ''} onClick={() => void load(n)}>{n}</button>)}<button type="button" disabled={!data.hasNext} onClick={() => void load(page + 1)}>Növbəti</button></div></div>}
    <div className="panel-footer"><span>{data.totalCount} GRN</span><span>Qəbul sənədləri dəyişdirilə və silinə bilməz.</span></div>
    {reverseTarget && <div className="modal-backdrop" role="presentation"><form className="modal-card" onSubmit={(event) => void reverse(event)}><div className="modal-heading"><div><span className="modal-kicker">GRN REVERSAL</span><h2>{reverseTarget.grnNumber} qəbulunu geri qaytar</h2><p>Stokda kifayət qədər miqdar yoxdursa server əməliyyatı rədd edəcək.</p></div><button className="modal-close" type="button" onClick={() => setReverseTarget(null)} aria-label="Bağla">×</button></div><div className="modal-body"><label className="field"><span>Səbəb *</span><textarea required minLength={2} maxLength={1000} rows={4} value={reverseReason} onChange={(event) => setReverseReason(event.target.value)} placeholder="Geri qaytarma səbəbini yazın"/></label></div><div className="modal-footer"><button className="secondary-button" type="button" onClick={() => setReverseTarget(null)}>Ləğv et</button><button className="primary-button" type="submit" disabled={reverseBusy || !reverseReason.trim()}>{reverseBusy ? 'Geri qaytarılır…' : 'Geri qaytar'}</button></div></form></div>}
  </section>
}

function GoodsReceiptPrintView({ receipt }: { receipt: GoodsReceiptRow }) {
  return <div className="printable-document"><header><h1>Mal qəbulu</h1><strong>{receipt.grnNumber}</strong></header><div className="print-meta"><p><b>Satınalma sifarişi:</b> {receipt.poNumber}</p><p><b>Anbar:</b> {receipt.warehouseName}</p><p><b>Qəbul tarixi:</b> {receipt.receivedDate}</p><p><b>Qəbul edən:</b> {receipt.receivedByName}</p><p><b>Status:</b> {receipt.reversedAt ? 'Geri qaytarılıb' : 'Qəbul edilib'}</p></div><table><thead><tr><th>Məhsul</th><th>Qəbul</th><th>Qüsurlu</th><th>Partiya</th><th>Son istifadə</th><th>Vahid maya</th></tr></thead><tbody>{receipt.items.map((item) => <tr key={item.id}><td>{item.productName}</td><td>{item.receivedQty}</td><td>{item.rejectedQty}</td><td>{item.batchNo ?? '—'}</td><td>{item.expiryDate ?? '—'}</td><td>{currency.format(item.unitCost)}</td></tr>)}</tbody></table>{receipt.note && <p><b>Qeyd:</b> {receipt.note}</p>}{receipt.reverseReason && <p><b>Geri qaytarma səbəbi:</b> {receipt.reverseReason}</p>}</div>
}

type ProcurementReportRow = Record<string, string | number | null>
type ProcurementReportPage = { items: ProcurementReportRow[]; currentPage: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }
type StockDocumentItem = { id: string; productId: string; productName: string; sku: string; qty: number; price: number }
type StockDocument = { id: string; number: string; type: 'in' | 'out'; date: string; partner: string | null; status: string; createdAt: string; items: StockDocumentItem[] }
type StockDocumentPage = { items: StockDocument[]; currentPage: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }

function StockDocumentsPanel({ products, canCreate, canReceive, canIssue, onError, onNotice }: {
  products: Product[]; canCreate: boolean; canReceive: boolean; canIssue: boolean
  onError: (value: string) => void; onNotice: (value: string) => void
}) {
  const emptyPage: StockDocumentPage = { items: [], currentPage: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false }
  const [data, setData] = useState<StockDocumentPage>(emptyPage)
  const [page, setPage] = useState(1)
  const [typeFilter, setTypeFilter] = useState('')
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [creating, setCreating] = useState(false)
  const [type, setType] = useState<'in' | 'out'>(canReceive ? 'in' : 'out')
  const [date, setDate] = useState(() => new Date().toLocaleDateString('sv-SE'))
  const [partner, setPartner] = useState('')
  const [lines, setLines] = useState<Array<{ productId: string; qty: string; price: string }>>(() => products[0] ? [{ productId: products[0].id, qty: '1', price: String(products[0].unitPrice) }] : [])

  const load = useCallback(async (requestedPage = 1) => {
    setLoading(true)
    const params = new URLSearchParams({ page: String(requestedPage), size: '10' })
    if (typeFilter) params.set('type', typeFilter)
    try {
      const result = await request<StockDocumentPage>(`documents?${params}`)
      setData(result); setPage(result.currentPage)
    } finally { setLoading(false) }
  }, [typeFilter])
  useEffect(() => { void load(1).catch((caught) => onError(caught instanceof Error ? caught.message : 'Sənədlər yüklənmədi.')) }, [load, onError])

  function updateLine(index: number, patch: Partial<(typeof lines)[number]>) {
    setLines((current) => current.map((line, lineIndex) => lineIndex === index ? { ...line, ...patch } : line))
  }

  async function createDocument(event: FormEvent) {
    event.preventDefault()
    if (!lines.length) { onError('Sənədə ən azı bir məhsul sətri əlavə edin.'); return }
    setSaving(true)
    try {
      const created = await request<StockDocument>('documents', { method: 'POST', body: JSON.stringify({
        type, date, partner: partner.trim() || null,
        items: lines.map((line) => ({ productId: line.productId, qty: Number(line.qty), price: Number(line.price) })),
      }) })
      setCreating(false); setPartner(''); setDate(new Date().toLocaleDateString('sv-SE'))
      setLines(products[0] ? [{ productId: products[0].id, qty: '1', price: String(products[0].unitPrice) }] : [])
      onNotice(`${created.number} nömrəli sənəd qaralama kimi yaradıldı.`)
      await load(1)
    } catch (caught) { onError(caught instanceof Error ? caught.message : 'Sənəd yaradılmadı.') }
    finally { setSaving(false) }
  }

  return <section className="data-panel"><div className="panel-heading"><div className="module-heading"><span className="module-icon report-icon"><Icon name="layers" size={19}/></span><div><h2>Stok sənədləri</h2><p>Daxilolma və xaricolma sənədlərinin siyahısı.</p></div></div><div className="panel-actions"><button className="refresh-button" type="button" disabled={loading} onClick={() => void load(page)} aria-label="Sənədləri yenilə"><Icon name="refresh" size={16}/></button>{canCreate && <button className="primary-button" type="button" onClick={() => setCreating(true)}><Icon name="plus" size={16}/>Sənəd yarat</button>}</div></div>
    <div className="toolbar"><div className="toolbar-controls"><label className="sort-control"><span>Sənəd növü</span><select value={typeFilter} onChange={(event) => setTypeFilter(event.target.value)}><option value="">Hamısı</option><option value="in">Daxilolma</option><option value="out">Xaricolma</option></select></label><span className="document-hint">Yeni sənədlər qaralama kimi saxlanılır; stok qalığı dəyişmir.</span></div></div>
    <div className="module-table-wrap">{loading && <div className="module-loading" role="status"><span className="button-spinner"/>Sənədlər yüklənir</div>}<div className="table-scroll"><table className="data-table"><thead><tr><th>Nömrə</th><th>Növ</th><th>Tarix</th><th>Tərəfdaş</th><th>Məhsullar</th><th>Status</th><th>Yaradılma vaxtı</th></tr></thead><tbody>{!data.items.length ? <EmptyRow colSpan={7} title={loading ? 'Yüklənir…' : 'Sənəd tapılmadı'} detail={loading ? 'Sənədlər yüklənir.' : 'Sənəd yaratmaq üçün yuxarıdakı düymədən istifadə edin.'}/> : data.items.map((item) => <tr key={item.id}><td><strong>{item.number}</strong></td><td><span className={`stock-log-type stock-log-${item.type === 'in' ? 'in' : 'out'}`}>{item.type === 'in' ? 'Daxilolma' : 'Xaricolma'}</span></td><td>{formatDateTime(item.date, false)}</td><td>{item.partner || '—'}</td><td>{item.items.map((line) => `${line.productName} × ${line.qty.toLocaleString('az-AZ')}`).join(', ') || '—'}</td><td><span className="status-pill">Qaralama</span></td><td>{formatDateTime(item.createdAt)}</td></tr>)}</tbody></table></div></div>
    {data.totalPages > 1 && <div className="pagination"><span>{data.totalCount} sənəddən {(data.currentPage - 1) * data.pageSize + 1}-{Math.min(data.currentPage * data.pageSize, data.totalCount)} göstərilir</span><div><button type="button" disabled={!data.hasPrevious} onClick={() => void load(page - 1)}>Əvvəlki</button>{Array.from({ length: data.totalPages }, (_, index) => index + 1).map((number) => <button type="button" key={number} className={number === page ? 'current' : ''} onClick={() => void load(number)}>{number}</button>)}<button type="button" disabled={!data.hasNext} onClick={() => void load(page + 1)}>Növbəti</button></div></div>}
    <div className="panel-footer"><span>{data.totalCount} sənəd</span><span>Sənədlər təsdiqlənənədək anbar qalığına təsir etmir.</span></div>
    {creating && <div className="modal-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget && !saving) setCreating(false) }}>
      <form className="modal-card document-modal" onSubmit={(event) => void createDocument(event)}>
        <div className="modal-heading"><div><span className="modal-eyebrow">STOK SƏNƏDİ</span><h2>Yeni sənəd</h2><p>Sənəd qaralama statusunda saxlanacaq.</p></div><button className="icon-button" type="button" aria-label="Bağla" onClick={() => setCreating(false)}><Icon name="close" size={17}/></button></div>
        <div className="modal-body"><div className="form-grid">
          <label className="field"><span>Sənəd növü</span><select value={type} onChange={(event) => setType(event.target.value as 'in' | 'out')} required>{canReceive && <option value="in">Daxilolma</option>}{canIssue && <option value="out">Xaricolma</option>}</select></label>
          <label className="field"><span>Tarix</span><input type="date" value={date} onChange={(event) => setDate(event.target.value)} required/></label>
          <label className="field full"><span>Tərəfdaş</span><input value={partner} onChange={(event) => setPartner(event.target.value)} placeholder="Tərəfdaşın adı (istəyə görə)"/></label>
          <div className="field full"><span>Məhsul sətirləri</span><div className="document-lines">
            {lines.map((line, index) => <div className="document-line" key={index}>
              <select aria-label="Məhsul" value={line.productId} onChange={(event) => { const product = products.find((item) => item.id === event.target.value); updateLine(index, { productId: event.target.value, ...(product ? { price: String(product.unitPrice) } : {}) }) }} required><option value="" disabled>Məhsul seçin</option>{products.map((product) => <option key={product.id} value={product.id}>{product.name} ({product.sku})</option>)}</select>
              <input aria-label="Miqdar" type="number" min="0.01" step="any" value={line.qty} onChange={(event) => updateLine(index, { qty: event.target.value })} required placeholder="Miqdar"/>
              <input aria-label="Qiymət" type="number" min="0" step="any" value={line.price} onChange={(event) => updateLine(index, { price: event.target.value })} required placeholder="Qiymət"/>
              <button className="secondary-button" type="button" disabled={lines.length === 1} aria-label="Sətri sil" onClick={() => setLines((current) => current.filter((_, lineIndex) => lineIndex !== index))}><Icon name="trash" size={14}/></button>
            </div>)}
            {products.length === 0 && <p className="document-hint">Sənəd yaratmaq üçün əvvəlcə məhsul əlavə edin.</p>}
            <button className="secondary-button" type="button" disabled={!products.length} onClick={() => setLines((current) => [...current, { productId: products[0].id, qty: '1', price: String(products[0].unitPrice) }])}><Icon name="plus" size={14}/>Sətir əlavə et</button>
          </div></div>
        </div></div>
        <div className="modal-footer"><button className="secondary-button" type="button" disabled={saving} onClick={() => setCreating(false)}>Ləğv et</button><button className="primary-button" type="submit" disabled={saving || !products.length}>{saving ? 'Saxlanılır…' : 'Qaralama yarat'}</button></div>
      </form>
    </div>}
  </section>
}

type StockLog = { id: string; productId: string | null; productName: string; type: 'IN' | 'OUT' | 'ADJUST'; quantityChange: number; quantityBefore: number; quantityAfter: number; reason: string | null; createdBy: string | null; createdAt: string }
type StockLogPage = { items: StockLog[]; currentPage: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }

function StockLogsPanel({ onError }: { onError: (value: string) => void }) {
  const [data, setData] = useState<StockLogPage>({ items: [], currentPage: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false })
  const [page, setPage] = useState(1)
  const [product, setProduct] = useState('')
  const [type, setType] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [loading, setLoading] = useState(false)
  const load = useCallback(async (requestedPage = 1) => {
    setLoading(true)
    const params = new URLSearchParams({ page: String(requestedPage), size: '10' })
    if (product.trim()) params.set('product', product.trim())
    if (type) params.set('type', type)
    if (fromDate) params.set('fromDate', fromDate)
    if (toDate) params.set('toDate', toDate)
    try {
      const result = await request<StockLogPage>(`stock-logs?${params}`)
      setData(result); setPage(result.currentPage)
    } finally { setLoading(false) }
  }, [product, type, fromDate, toDate])
  useEffect(() => { void load(1).catch((caught) => onError(caught instanceof Error ? caught.message : 'Stok loqları yüklənmədi.')) }, [load, onError])

  return <section className="data-panel"><div className="panel-heading"><div className="module-heading"><span className="module-icon report-icon"><Icon name="layers" size={19}/></span><div><h2>Stok hərəkətləri</h2><p>Stok giriş, çıxış və düzəlişlərinin dəyişdirilməz tarixçəsi.</p></div></div><div className="panel-actions"><button className="refresh-button" type="button" disabled={loading} onClick={() => void load(page)} aria-label="Loqları yenilə"><Icon name="refresh" size={16}/></button></div></div>
    <div className="toolbar"><div className="toolbar-controls"><label className="sort-control"><span>Məhsul adı və ya ID</span><input value={product} onChange={(event) => setProduct(event.target.value)} placeholder="Məhsul axtar"/></label><label className="sort-control"><span>Növ</span><select value={type} onChange={(event) => setType(event.target.value)}><option value="">Hamısı</option><option value="IN">Giriş</option><option value="OUT">Çıxış</option><option value="ADJUST">Düzəliş</option></select></label><label className="sort-control"><span>Tarixdən</span><input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)}/></label><label className="sort-control"><span>Tarixədək</span><input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)}/></label></div></div>
    <div className="module-table-wrap">{loading && <div className="module-loading" role="status"><span className="button-spinner"/>Loqlar yüklənir</div>}<div className="table-scroll"><table className="data-table"><thead><tr><th>Tarix / saat</th><th>Məhsul</th><th>Növ</th><th>Dəyişiklik</th><th>Əvvəl / sonra</th><th>Qeyd</th><th>İstifadəçi</th></tr></thead><tbody>{data.items.length === 0 ? <EmptyRow colSpan={7} title={loading ? 'Yüklənir…' : 'Stok loqu tapılmadı'} detail={loading ? 'Stok hərəkətləri yüklənir.' : 'Filtrləri yoxlayın və ya stok əməliyyatı yaradın.'}/> : data.items.map((log) => <tr key={log.id}><td>{formatDateTime(log.createdAt)}</td><td><strong>{log.productName}</strong></td><td><span className={`stock-log-type stock-log-${log.type.toLocaleLowerCase('en-US')}`}>{log.type === 'IN' ? 'Giriş' : log.type === 'OUT' ? 'Çıxış' : 'Düzəliş'}</span></td><td className="stock-log-change">{log.quantityChange > 0 ? '+' : ''}{log.quantityChange.toLocaleString('az-AZ')}</td><td>{log.quantityBefore.toLocaleString('az-AZ')} / {log.quantityAfter.toLocaleString('az-AZ')}</td><td>{log.reason || '—'}</td><td>{log.createdBy || 'System'}</td></tr>)}</tbody></table></div></div>
    {data.totalPages > 1 && <div className="pagination"><span>{data.totalCount} qeyddən {(data.currentPage - 1) * data.pageSize + 1}-{Math.min(data.currentPage * data.pageSize, data.totalCount)} göstərilir</span><div><button type="button" disabled={!data.hasPrevious} onClick={() => void load(page - 1)}>Əvvəlki</button>{Array.from({ length: data.totalPages }, (_, index) => index + 1).map((number) => <button type="button" key={number} className={number === page ? 'current' : ''} onClick={() => void load(number)}>{number}</button>)}<button type="button" disabled={!data.hasNext} onClick={() => void load(page + 1)}>Növbəti</button></div></div>}
    <div className="panel-footer"><span>{data.totalCount} stok qeydi</span><span>Yalnız Admin görə bilər</span></div>
  </section>
}

const reportRoutes = { open: 'open-orders', overdue: 'overdue-orders', comparison: 'receipt-comparison', suppliers: 'supplier-receipt-amounts' } as const
const reportTitles = { open: 'Açıq PO-lar', overdue: 'Gecikən PO-lar', comparison: 'Sifariş və qəbul müqayisəsi', suppliers: 'Təchizatçı xərcləri' } as const
const reportLabels: Record<string, string> = { poNumber: 'PO nömrəsi', supplierName: 'Təchizatçı', warehouseName: 'Anbar', status: 'Status', orderDate: 'Sifariş tarixi', expectedDate: 'Gözlənilən tarix', daysOverdue: 'Gecikmə (gün)', totalAmount: 'Ümumi məbləğ', productName: 'Məhsul', orderedQty: 'Sifariş', receivedQty: 'Qəbul', rejectedQty: 'Rədd', currency: 'Valyuta', receivedAmount: 'Qəbul məbləği', receiptCount: 'GRN sayı' }

function ProcurementReportsPanel({ onError }: { onError: (value: string) => void }) {
  const [report, setReport] = useState<keyof typeof reportRoutes>('open')
  const [data, setData] = useState<ProcurementReportPage>({ items: [], currentPage: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false })
  const [page, setPage] = useState(1)
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [purchaseOrderId, setPurchaseOrderId] = useState('')
  const [loading, setLoading] = useState(false)
  const load = useCallback(async (requestedPage = 1) => {
    setLoading(true)
    const params = new URLSearchParams({ page: String(requestedPage), size: '10' })
    if (report === 'suppliers' && fromDate) params.set('fromDate', fromDate)
    if (report === 'suppliers' && toDate) params.set('toDate', toDate)
    if (report === 'comparison' && purchaseOrderId) params.set('purchaseOrderId', purchaseOrderId)
    try {
      const result = await request<ProcurementReportPage>(`procurement-reports/${reportRoutes[report]}?${params}`)
      setData(result); setPage(result.currentPage)
    } finally { setLoading(false) }
  }, [report, fromDate, toDate, purchaseOrderId])
  useEffect(() => { void load(1).catch((error) => onError(error instanceof Error ? error.message : 'Hesabat yüklənmədi.')) }, [load, onError])
  const columns = data.items.length ? Object.keys(data.items[0]).filter((key) => !['id', 'purchaseOrderId', 'supplierId', 'productId'].includes(key)) : []
  return <section className="data-panel"><div className="panel-heading"><div className="module-heading"><span className="module-icon report-icon"><Icon name="grid" size={19}/></span><div><h2>Satınalma hesabatları</h2><p>PO və mal qəbulu göstəricilərini yoxlayın və filtrləyin.</p></div></div><div className="panel-actions"><button className={`refresh-button ${loading ? 'is-refreshing' : ''}`} type="button" disabled={loading} aria-label="Hesabatı yenilə" title="Hesabatı yenilə" onClick={() => void load(page)}><Icon name="refresh" size={16}/><span>{loading ? 'Yenilənir' : 'Yenilə'}</span></button></div></div>
    <div className="toolbar"><div className="toolbar-controls"><label className="sort-control"><span>Hesabat</span><select value={report} onChange={(event) => setReport(event.target.value as keyof typeof reportRoutes)}>{Object.entries(reportTitles).map(([key, title]) => <option key={key} value={key}>{title}</option>)}</select></label>{report === 'suppliers' && <><label className="sort-control"><span>Tarixdən</span><input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)}/></label><label className="sort-control"><span>Tarixədək</span><input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)}/></label></>}{report === 'comparison' && <label className="sort-control"><span>PO ID (istəyə görə)</span><input value={purchaseOrderId} onChange={(event) => setPurchaseOrderId(event.target.value)} placeholder="PO GUID"/></label>}</div></div>
    <div className="module-table-wrap">{loading && <div className="module-loading" role="status"><span className="button-spinner"/>Hesabat yenilənir</div>}<div className="table-scroll"><table className="data-table"><thead><tr>{columns.map((column) => <th key={column}>{reportLabels[column] ?? column}</th>)}</tr></thead><tbody>{!data.items.length ? <EmptyRow colSpan={Math.max(columns.length, 1)} title={loading ? 'Yüklənir…' : 'Hesabatda məlumat yoxdur'} detail={loading ? 'Hesabat məlumatları hazırlanır.' : 'Tarix və status filtrlərini yoxlayın.'}/> : data.items.map((row, index) => <tr key={String(row.id ?? row.purchaseOrderId ?? row.supplierId ?? index)}>{columns.map((column) => <td key={column}>{column === 'totalAmount' || column === 'receivedAmount' ? currency.format(Number(row[column] ?? 0)) : row[column] === null ? '—' : String(row[column])}</td>)}</tr>)}</tbody></table></div></div>
    {data.totalPages > 1 && <div className="pagination"><span>{data.totalCount} sətirdən {(data.currentPage - 1) * data.pageSize + 1}-{Math.min(data.currentPage * data.pageSize, data.totalCount)} göstərilir</span><div><button type="button" disabled={!data.hasPrevious} onClick={() => void load(page - 1)}>Əvvəlki</button>{Array.from({ length: data.totalPages }, (_, index) => index + 1).map((number) => <button type="button" key={number} className={number === page ? 'current' : ''} onClick={() => void load(number)}>{number}</button>)}<button type="button" disabled={!data.hasNext} onClick={() => void load(page + 1)}>Növbəti</button></div></div>}
    <div className="panel-footer"><span>{data.totalCount} hesabat sətri</span><span>Gecikən sifarişlərdə gün sayı göstərilir.</span></div>
  </section>
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

function ProductTable({ products, categories, loading, canManageProduct, canManageStock, canAdjustStock, onUpdateProduct, onEdit, onStock, onAdjustStock, onError, onDelete, busyId }: { products: Product[]; categories: Category[]; loading: boolean; canManageProduct: boolean; canManageStock: boolean; canAdjustStock: boolean; onUpdateProduct: (item: Product, patch: Partial<ProductForm>) => Promise<void>; onEdit: (item: Product) => void; onStock: (item: Product) => void; onAdjustStock: (item: Product, quantity: number) => Promise<void>; onError: (message: string) => void; onDelete: (id: string) => void; busyId: string }) {
  const [datePopoverId, setDatePopoverId] = useState<string | null>(null)
  const [editingCell, setEditingCell] = useState<string | null>(null)
  const [draftValue, setDraftValue] = useState('')
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

  const editable = (product: Product, field: keyof ProductForm, value: string | number, display: ReactNode, options?: { type?: string; stock?: boolean; choices?: { value: string; label: string }[] }) => {
    const key = `${product.id}:${field}`
    const allowed = options?.stock ? canAdjustStock : canManageProduct
    if (editingCell === key && allowed) {
      const commit = () => {
        const raw = draftValue.trim()
        setEditingCell(null)
        if ((!raw && field !== 'barcode') || raw === String(value)) return
        const parsed: string | number = options?.type === 'number' ? Number(raw) : raw
        if (typeof parsed === 'number' && (!Number.isFinite(parsed) || parsed < 0 || (options?.stock && !Number.isInteger(parsed)))) { onError(options?.stock ? 'Stok miqdarı sıfır və ya daha böyük tam ədəd olmalıdır.' : 'Dəyər sıfır və ya daha böyük olmalıdır.'); return }
        void (options?.stock ? onAdjustStock(product, Number(parsed)) : onUpdateProduct(product, { [field]: parsed }))
      }
      if (options?.choices) return <select autoFocus className="inline-edit-input" aria-label={`${field} redaktəsi`} value={draftValue} onChange={(event) => { const next = event.target.value; setDraftValue(next); setEditingCell(null); void onUpdateProduct(product, { [field]: field === 'isActive' ? next === 'true' : next } as Partial<ProductForm>) }} onBlur={() => setEditingCell(null)} onKeyDown={(event) => { if (event.key === 'Escape') setEditingCell(null) }}><option value="" disabled>Seçin</option>{options.choices.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select>
      return <input autoFocus className="inline-edit-input" aria-label={`${field} redaktəsi`} type={options?.type ?? 'text'} min={options?.type === 'number' ? 0 : undefined} step={options?.stock ? 1 : options?.type === 'number' ? 'any' : undefined} value={draftValue} onChange={(event) => setDraftValue(event.target.value)} onBlur={commit} onKeyDown={(event) => { if (event.key === 'Enter') event.currentTarget.blur(); if (event.key === 'Escape') setEditingCell(null) }}/>
    }
    if (!allowed) return display
    return <button type="button" className="inline-edit-value" title="Redaktə etmək üçün klikləyin" onClick={() => { setDraftValue(String(value)); setEditingCell(key) }}>{display}</button>
  }

  return <div className="table-scroll"><table><thead><tr><th>SKU / BARCODE</th><th>KATEQORİYA</th><th>QİYMƏT</th><th>STOK</th><th>ÖLÇÜ</th><th>STATUS</th><th>TARİX</th><th><span className="sr-only">Əməliyyatlar</span></th></tr></thead><tbody>
    {loading ? <EmptyRow colSpan={8} title="Məlumat yüklənir..."/> : products.length === 0 ? <EmptyRow colSpan={8} title="Məhsul tapılmadı" detail="Axtarışınızı dəyişin və ya yeni məhsul əlavə edin."/> : products.map((product, index) => <tr key={product.id} className="table-row" style={{ animationDelay: `${index * 25}ms` }}>
      <td><div className="code-cell">{editable(product, 'sku', product.sku, <strong>{product.sku}</strong>)}{editable(product, 'barcode', product.barcode, <span>{product.barcode || 'Barkod yoxdur'}</span>)}{editable(product, 'name', product.name, <span>{product.name}</span>)}</div></td>
      <td>{editable(product, 'categoryId', product.categoryId, <span className="category-pill">{categories.find((category) => category.id === product.categoryId)?.name ?? 'Kateqoriya yoxdur'}</span>, { choices: categories.map((category) => ({ value: category.id, label: category.name })) })}</td>
      <td>{editable(product, 'unitPrice', product.unitPrice, <strong className="price-cell">{currency.format(product.unitPrice)}</strong>, { type: 'number' })}</td><td>{editable(product, 'quantity' as keyof ProductForm, product.quantity ?? 0, <span className={`stock-pill ${product.quantity === undefined ? 'stock-unknown' : product.quantity < 5 ? 'stock-low' : 'stock-ok'}`}>{product.quantity === undefined ? '—' : product.quantity.toLocaleString('az-AZ')}{product.quantity !== undefined && product.quantity < 5 && <strong>Stok bitir</strong>}</span>, { type: 'number', stock: true })}</td><td>{editable(product, 'unitOfMeasure', product.unitOfMeasure, <span className="unit-cell">/ {product.unitOfMeasure}</span>)}</td>
      <td>{editable(product, 'isActive', String(product.isActive), <span className={`status-pill ${product.isActive ? 'enabled' : 'disabled'}`}><i/>{product.isActive ? 'Aktiv' : 'Passiv'}</span>, { choices: [{ value: 'true', label: 'Aktiv' }, { value: 'false', label: 'Passiv' }] })}</td>
      <td><div className="date-popover-wrap"><button ref={datePopoverId === product.id ? dateButtonRef : null} type="button" className="date-link" aria-expanded={datePopoverId === product.id} onClick={() => setDatePopoverId((current) => current === product.id ? null : product.id)}>{formatDateTime(product.createdAt, false)}</button>{datePopoverId === product.id && createPortal(<div ref={datePopoverRef} className="date-popover" style={datePopoverPosition} role="dialog" aria-label="Məhsulun tarixçəsi"><div className="date-popover-row"><span>Giriş tarixi</span><strong>{formatDateTime(product.createdAt)}</strong></div><div className="date-popover-row"><span>Yenilənmə tarixi</span><strong>{formatDateTime(product.updatedAt)}</strong></div></div>, document.body)}</div></td>
      <td><RowActions showEdit={canManageProduct} showStock={canManageStock} showDelete={canManageProduct} onEdit={() => onEdit(product)} onStock={() => onStock(product)} onDelete={() => onDelete(product.id)} busy={busyId === product.id}/></td>
    </tr>)}
  </tbody></table></div>
}

function CategoryTable({ categories, allCategories, products, loading, canManage, onEdit, onDelete, busyId }: { categories: Category[]; allCategories: Category[]; products: Product[]; loading: boolean; canManage: boolean; onEdit: (item: Category) => void; onDelete: (id: string) => void; busyId: string }) {
  return <div className="table-scroll"><table><thead><tr><th>KATEQORİYA</th><th>TƏSVİR</th><th>ÜST KATEQORİYA</th><th>MƏHSUL SAYI</th><th><span className="sr-only">Əməliyyatlar</span></th></tr></thead><tbody>
    {loading ? <EmptyRow colSpan={5} title="Məlumat yüklənir..."/> : categories.length === 0 ? <EmptyRow colSpan={5} title="Kateqoriya tapılmadı" detail="Məhsul əlavə etməzdən əvvəl kateqoriya yaradın."/> : categories.map((category, index) => <tr key={category.id} className="table-row" style={{ animationDelay: `${index * 25}ms` }}>
      <td><div className="product-cell"><div className="category-thumb"><Icon name="tag" size={19}/></div><div><strong>{category.name}</strong><span>{category.id.slice(0, 8).toUpperCase()}</span></div></div></td>
      <td><span className="description-cell">{category.description || 'Təsvir əlavə edilməyib'}</span></td>
      <td><span className="category-pill">{allCategories.find((item) => item.id === category.parentCategoryId)?.name ?? '—'}</span></td>
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
  return <div className="row-actions">{showEdit && <button type="button" aria-label="Redaktə et" title="Redaktə et" onClick={onEdit}><Icon name="edit" size={16}/></button>}{showStock && onStock && <button type="button" aria-label="Anbara mal daxil et və partiyalara bax" title="Anbara mal daxil et" onClick={onStock}><Icon name="package" size={16}/></button>}{showDelete && <button type="button" aria-label="Məhsulu sil" title="Məhsulu sil" disabled={busy} onClick={onDelete}><Icon name="trash" size={16}/></button>}</div>
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

function ProductDialog({ item, categories, warehouses, saving, canManageProduct, canManageStock, canAdjustStock, onClose, onSave }: { item?: Product; categories: Category[]; warehouses: WarehouseOption[]; saving: boolean; canManageProduct: boolean; canManageStock: boolean; canAdjustStock: boolean; onClose: () => void; onSave: (form: ProductForm, initialStock?: { warehouseId: string; quantity: number; purchasePrice: number }, details?: { reorderLevel: number; quantity?: number }) => void }) {
  const [form, setForm] = useState<ProductForm>(item ? {
    name: item.name, sku: item.sku, barcode: item.barcode, description: item.description,
    categoryId: item.categoryId, unitPrice: item.unitPrice, unitOfMeasure: item.unitOfMeasure, isActive: item.isActive,
  } : { ...emptyProduct, categoryId: categories[0]?.id ?? '' })
  const [initialStock, setInitialStock] = useState({ warehouseId: warehouses[0]?.id ?? '', quantity: '0', purchasePrice: '0' })
  const [reorderLevel, setReorderLevel] = useState(String(item?.reorderLevel ?? 10))
  const [stockQuantity, setStockQuantity] = useState(String(item?.quantity ?? 0))
  const set = <K extends keyof ProductForm>(key: K, value: ProductForm[K]) => setForm((current) => ({ ...current, [key]: value }))
  return <DialogFrame title={item ? 'Məhsulu redaktə et' : 'Yeni məhsul'} subtitle="Məhsul məlumatlarını daxil edin." onClose={onClose} saving={saving} canSubmit={canManageProduct} submitLabel="Yadda saxla" onSubmit={(event) => {
    event.preventDefault()
    const quantity = Number(initialStock.quantity)
    const purchasePrice = Number(initialStock.purchasePrice)
    if (!item && canManageStock && quantity > 0 && (!Number.isInteger(quantity) || !initialStock.warehouseId || !Number.isFinite(purchasePrice) || purchasePrice < 0)) return
    const parsedReorderLevel = Number(reorderLevel)
    const parsedStockQuantity = Number(stockQuantity)
    if (!Number.isFinite(parsedReorderLevel) || parsedReorderLevel < 0) return
    if (item && canAdjustStock && (!Number.isInteger(parsedStockQuantity) || parsedStockQuantity < 0)) return
    onSave(form, !item && canManageStock && quantity > 0 ? { warehouseId: initialStock.warehouseId, quantity, purchasePrice } : undefined, {
      reorderLevel: parsedReorderLevel,
      quantity: item && canAdjustStock ? parsedStockQuantity : undefined,
    })
  }}>
    <div className="form-grid"><label className="field full"><span>Məhsulun adı <b>*</b></span><input required disabled={!canManageProduct} maxLength={200} autoFocus value={form.name} onChange={(event) => set('name', event.target.value)} placeholder="Məs. Simsiz klaviatura"/></label>
      <label className="field"><span>SKU <b>*</b></span><input required disabled={!canManageProduct} maxLength={64} value={form.sku} onChange={(event) => set('sku', event.target.value)} placeholder="KB-001"/></label>
      <label className="field"><span>Barkod</span><input disabled={!canManageProduct} maxLength={64} value={form.barcode} onChange={(event) => set('barcode', event.target.value)} placeholder="Barkod nömrəsi"/></label>
      <label className="field"><span>Kateqoriya <b>*</b></span><select required disabled={!canManageProduct} value={form.categoryId} onChange={(event) => set('categoryId', event.target.value)}><option value="" disabled>Kateqoriya seçin</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select>{categories.length === 0 && <small>Əvvəlcə kateqoriya yaradın.</small>}</label>
      <div className="field-row"><label className="field"><span>Satış qiyməti <b>*</b></span><div className="input-suffix"><input required disabled={!canManageProduct} type="number" min="0" step="0.01" value={form.unitPrice} onChange={(event) => set('unitPrice', Number(event.target.value))}/><span>AZN</span></div></label><label className="field"><span>Ölçü vahidi <b>*</b></span><select disabled={!canManageProduct} value={form.unitOfMeasure} onChange={(event) => set('unitOfMeasure', event.target.value)}><option value="Piece">Ədəd</option><option value="Kg">Kiloqram</option><option value="Liter">Litr</option><option value="Package">Paket</option><option value="Box">Qutu</option></select></label></div>
      <label className="field full"><span>Təsvir</span><textarea disabled={!canManageProduct} rows={3} maxLength={1000} value={form.description} onChange={(event) => set('description', event.target.value)} placeholder="Məhsul haqqında qısa məlumat"/></label>
      <label className="field"><span>Az stok həddi</span><div className="input-suffix"><input type="number" min="0" step="1" value={reorderLevel} onChange={(event) => setReorderLevel(event.target.value)}/><span>{form.unitOfMeasure}</span></div><small>Bu hədd və aşağısı bildiriş yaradır.</small></label>
      {item && canAdjustStock && <label className="field"><span>Ümumi stok qalığı</span><div className="input-suffix"><input type="number" min="0" step="1" value={stockQuantity} onChange={(event) => setStockQuantity(event.target.value)}/><span>{form.unitOfMeasure}</span></div></label>}
      {!item && canManageStock && <div className="field full initial-stock-fields"><strong>İlkin stok</strong><div className="field-row"><label className="field"><span>Miqdar</span><div className="input-suffix"><input type="number" min="0" step="1" value={initialStock.quantity} onChange={(event) => setInitialStock((current) => ({ ...current, quantity: event.target.value }))}/><span>{form.unitOfMeasure}</span></div></label><label className="field"><span>Anbar{Number(initialStock.quantity) > 0 && <b> *</b>}</span><select required={Number(initialStock.quantity) > 0} value={initialStock.warehouseId} onChange={(event) => setInitialStock((current) => ({ ...current, warehouseId: event.target.value }))}><option value="">Anbar seçin</option>{warehouses.map((warehouse) => <option key={warehouse.id} value={warehouse.id}>{warehouse.name}</option>)}</select></label></div><label className="field"><span>Alış qiyməti{Number(initialStock.quantity) > 0 && <b> *</b>}</span><div className="input-suffix"><input required={Number(initialStock.quantity) > 0} type="number" min="0" step="0.01" value={initialStock.purchasePrice} onChange={(event) => setInitialStock((current) => ({ ...current, purchasePrice: event.target.value }))}/><span>AZN</span></div></label>{warehouses.length === 0 && <small>İlkin stok daxil etmək üçün əvvəlcə anbar yaradın.</small>}</div>}
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
      <label className="field full"><span>Rol <b>*</b></span><select value={form.role} onChange={(event) => set('role', event.target.value as EmployeeForm['role'])}><option value="Admin">Admin</option><option value="WarehouseKeeper">Anbardar (WarehouseKeeper)</option><option value="Manager">Menecer (Manager)</option><option value="ProcurementStaff">Satınalma işçisi (ProcurementStaff)</option></select></label>
    </div>
  </DialogFrame>
}

export default App
