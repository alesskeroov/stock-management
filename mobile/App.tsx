import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  ActivityIndicator, Alert, FlatList, KeyboardAvoidingView, Modal, Platform, Pressable,
  SafeAreaView, ScrollView, StatusBar, StyleSheet, Text, TextInput, View,
} from 'react-native'
import * as SecureStore from 'expo-secure-store'

const API_URL = (process.env.EXPO_PUBLIC_API_URL ?? 'http://10.0.2.2:5136').replace(/\/$/, '')
const PAGE_SIZE = 10
const TOKEN_KEY = 'stockroom.auth'
const C = { forest: '#153c30', green: '#287653', lime: '#c5f09a', bg: '#f5f7f5', ink: '#24342b', muted: '#829089', line: '#e7ede9', white: '#ffffff', soft: '#eaf6ef', red: '#b85646' }

type User = { id: string; userName: string; email: string; firstName: string; lastName: string; roles: string[] }
type Session = { accessToken: string; refreshToken: string; accessTokenExpiresAtUtc: string; user: User }
type Product = { id: string; name: string; sku: string; barcode: string; description: string; categoryId: string; unitPrice: number; unitOfMeasure: string; isActive: boolean; quantity: number }
type Category = { id: string; name: string; description: string; parentCategoryId: string | null }
type ProductPage = { items: Product[]; currentPage: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }
type Screen = 'products' | 'categories'

async function readSession(): Promise<Session | null> {
  try { const value = await SecureStore.getItemAsync(TOKEN_KEY); return value ? JSON.parse(value) as Session : null }
  catch { return null }
}

async function saveSession(session: Session | null) {
  if (session) await SecureStore.setItemAsync(TOKEN_KEY, JSON.stringify(session))
  else await SecureStore.deleteItemAsync(TOKEN_KEY)
}

function errorMessage(payload: unknown, fallback: string) {
  if (payload && typeof payload === 'object') {
    const body = payload as { error?: string; title?: string; errors?: Record<string, string[]> }
    if (body.error) return body.error
    if (body.title) return body.title
    if (body.errors) return Object.values(body.errors).flat().join(' ')
  }
  return fallback
}

export default function App() {
  const [session, setSession] = useState<Session | null>(null)
  const [ready, setReady] = useState(false)
  const [screen, setScreen] = useState<Screen>('products')
  const [products, setProducts] = useState<Product[]>([])
  const [categories, setCategories] = useState<Category[]>([])
  const [page, setPage] = useState<ProductPage>({ items: [], currentPage: 1, pageSize: PAGE_SIZE, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false })
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [loginBusy, setLoginBusy] = useState(false)
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<Product | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => { void readSession().then(value => { setSession(value); setReady(true) }) }, [])

  const api = useCallback(async <T,>(path: string, init: RequestInit = {}, auth = session): Promise<T> => {
    const send = (token?: string) => fetch(`${API_URL}/api/${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...init.headers },
    })
    let response = await send(auth?.accessToken)
    if (response.status === 401 && auth?.refreshToken && !path.startsWith('auth/')) {
      const refreshedResponse = await fetch(`${API_URL}/api/auth/refresh-token`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: auth.refreshToken }),
      }).catch(() => null)
      if (refreshedResponse?.ok) {
        const refreshed = await refreshedResponse.json() as Session
        await saveSession(refreshed); setSession(refreshed); response = await send(refreshed.accessToken)
      } else { await saveSession(null); setSession(null) }
    }
    if (!response.ok) {
      const payload: unknown = await response.json().catch(() => null)
      throw new Error(errorMessage(payload, `Sorğu alınmadı (${response.status}).`))
    }
    if (response.status === 204) return undefined as T
    return response.json() as Promise<T>
  }, [session])

  const loadData = useCallback(async (targetPage = 1) => {
    if (!session) return
    setLoading(true); setError('')
    try {
      const [productsPage, categoryRows] = await Promise.all([
        api<ProductPage>(`products?page=${targetPage}&size=${PAGE_SIZE}`), api<Category[]>('categories'),
      ])
      setPage(productsPage); setProducts(productsPage.items); setCategories(categoryRows)
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Məlumat yüklənmədi.') }
    finally { setLoading(false) }
  }, [api, session])

  useEffect(() => { if (session) void loadData(page.currentPage) }, [session?.accessToken])

  const filteredProducts = useMemo(() => {
    const value = query.trim().toLocaleLowerCase('az')
    if (!value) return products
    return products.filter(item => {
      const category = categories.find(entry => entry.id === item.categoryId)?.name ?? ''
      return [item.name, item.sku, item.barcode, category].some(field => field.toLocaleLowerCase('az').includes(value))
    })
  }, [products, categories, query])

  async function login(identifier: string, password: string) {
    setLoginBusy(true); setError('')
    try {
      const next = await api<Session>('auth/login', { method: 'POST', body: JSON.stringify({ identifier, password }) }, null)
      await saveSession(next); setSession(next)
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Giriş alınmadı.') }
    finally { setLoginBusy(false) }
  }

  async function logout() {
    if (session) await api<void>('auth/logout', { method: 'POST', body: JSON.stringify({ refreshToken: session.refreshToken }) }).catch(() => undefined)
    await saveSession(null); setSession(null); setProducts([]); setError('')
  }

  async function saveProduct(values: Omit<Product, 'id' | 'quantity'>) {
    setSaving(true); setError('')
    try {
      await api(editing ? `products/${editing.id}` : 'products', {
        method: editing ? 'PUT' : 'POST', body: JSON.stringify(values),
      })
      setFormOpen(false); setEditing(null); setNotice(editing ? 'Məhsul yeniləndi.' : 'Məhsul yaradıldı.')
      await loadData(page.currentPage)
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Məhsul yadda saxlanmadı.') }
    finally { setSaving(false) }
  }

  async function deleteProduct(item: Product) {
    Alert.alert('Məhsulu silmək', `“${item.name}” məhsulunu silmək istəyirsiniz?`, [
      { text: 'Ləğv et', style: 'cancel' },
      { text: 'Sil', style: 'destructive', onPress: () => { void (async () => {
        setError('')
        try {
          await api<void>(`products/${item.id}`, { method: 'DELETE' }); setNotice('Məhsul silindi.')
          await loadData(page.currentPage)
        } catch (caught) { setError(caught instanceof Error ? caught.message : 'Məhsul silinmədi.') }
      })() } },
    ])
  }

  if (!ready) return <Splash />
  if (!session) return <LoginScreen busy={loginBusy} error={error} onLogin={login} />

  const canManage = session.user.roles.includes('Admin')
  return <SafeAreaView style={styles.safe}>
    <StatusBar barStyle="light-content" backgroundColor={C.forest} />
    <View style={styles.topbar}>
      <View style={styles.brandMark}><Text style={styles.brandGlyph}>▦</Text></View>
      <View style={styles.brandCopy}><Text style={styles.brand}>Stockroom</Text><Text style={styles.brandCaption}>İNVENTAR İDARƏETMƏSİ</Text></View>
      <Pressable style={styles.avatar} onPress={() => void logout()} accessibilityLabel="Hesabdan çıxış et"><Text style={styles.avatarText}>{session.user.firstName.slice(0, 1)}{session.user.lastName.slice(0, 1)}</Text></Pressable>
    </View>
    <View style={styles.heading}><Text style={styles.eyebrow}>İŞ SAHƏSİ  /  {screen === 'products' ? 'MƏHSULLAR' : 'KATEQORİYALAR'}</Text><Text style={styles.title}>İnventar kataloqu</Text><Text style={styles.subtitle}>Məhsul və kateqoriyaları idarə edin.</Text></View>
    <View style={styles.stats}><Stat label="Ümumi məhsul" value={String(page.totalCount)} /><Stat label="Səhifə" value={`${page.currentPage} / ${Math.max(page.totalPages, 1)}`} /></View>
    <View style={styles.tabs}>
      <Tab label="Məhsullar" count={page.totalCount} active={screen === 'products'} onPress={() => setScreen('products')} />
      <Tab label="Kateqoriyalar" count={categories.length} active={screen === 'categories'} onPress={() => setScreen('categories')} />
    </View>
    {screen === 'products' && <View style={styles.toolbar}>
      <TextInput style={styles.search} value={query} onChangeText={setQuery} placeholder="Məhsul, SKU və ya barkod axtar..." placeholderTextColor="#a5afa9" />
      {canManage && <Pressable style={styles.addButton} onPress={() => { setEditing(null); setFormOpen(true) }}><Text style={styles.addText}>＋ Məhsul əlavə et</Text></Pressable>}
    </View>}
    {!!error && <Notice message={error} onClose={() => setError('')} danger />}
    {!!notice && <Notice message={notice} onClose={() => setNotice('')} />}
    {loading ? <View style={styles.center}><ActivityIndicator color={C.green} /><Text style={styles.muted}>Məlumat yüklənir...</Text></View> : screen === 'products' ? <FlatList
      data={filteredProducts} keyExtractor={item => item.id} contentContainerStyle={styles.listContent}
      ListEmptyComponent={<Empty title={query ? 'Məhsul tapılmadı' : 'Məhsul yoxdur'} detail={query ? 'Axtarış sözünü dəyişin.' : 'Kataloqa yeni məhsul əlavə edin.'} />}
      renderItem={({ item }) => <ProductCard item={item} category={categories.find(entry => entry.id === item.categoryId)?.name ?? 'Kateqoriya'} canManage={canManage}
        onEdit={() => { setEditing(item); setFormOpen(true) }} onDelete={() => void deleteProduct(item)} />}
      ListFooterComponent={page.totalPages > 1 ? <Pagination page={page} onChange={next => void loadData(next)} /> : null}
    /> : <FlatList data={categories} keyExtractor={item => item.id} contentContainerStyle={styles.listContent}
      ListEmptyComponent={<Empty title="Kateqoriya yoxdur" detail="Kateqoriyalar backend-də yaradılır." />}
      renderItem={({ item }) => <View style={styles.categoryCard}><View style={styles.categoryIcon}><Text style={styles.categoryGlyph}>◇</Text></View><View style={styles.cardMain}><Text style={styles.cardTitle}>{item.name}</Text><Text style={styles.cardMeta}>{item.description || 'Təsvir əlavə edilməyib'}</Text></View></View>} />}
    <View style={styles.bottomBar}><Text style={styles.bottomText}>Stockroom  <Text style={{ color: C.lime }}>•</Text>  İnventar</Text><Text style={styles.bottomUser}>{session.user.userName}</Text></View>
    <ProductFormModal visible={formOpen} item={editing} categories={categories} busy={saving} onClose={() => { setFormOpen(false); setEditing(null) }} onSave={saveProduct} />
  </SafeAreaView>
}

function Splash() { return <View style={[styles.safe, styles.center]}><ActivityIndicator color={C.green} /></View> }

function LoginScreen({ busy, error, onLogin }: { busy: boolean; error: string; onLogin: (identifier: string, password: string) => void }) {
  const [identifier, setIdentifier] = useState(''); const [password, setPassword] = useState('')
  return <SafeAreaView style={styles.loginSafe}><StatusBar barStyle="light-content" backgroundColor={C.forest} /><KeyboardAvoidingView style={styles.loginWrap} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
    <View style={styles.loginLogo}><Text style={styles.brandGlyph}>▦</Text></View><Text style={styles.loginBrand}>Stockroom</Text><Text style={styles.loginKicker}>STOK İDARƏETMƏSİ</Text>
    <View style={styles.loginCard}><Text style={styles.loginTitle}>Xoş gəlmisiniz</Text><Text style={styles.loginHint}>Davam etmək üçün hesabınıza daxil olun.</Text>
      <Text style={styles.label}>İstifadəçi adı və ya email</Text><TextInput style={styles.input} value={identifier} onChangeText={setIdentifier} autoCapitalize="none" autoCorrect={false} placeholder="admin@company.az" placeholderTextColor="#a5afa9" />
      <Text style={styles.label}>Şifrə</Text><TextInput style={styles.input} value={password} onChangeText={setPassword} secureTextEntry placeholder="Şifrənizi daxil edin" placeholderTextColor="#a5afa9" onSubmitEditing={() => onLogin(identifier, password)} />
      {!!error && <Text style={styles.loginError}>{error}</Text>}
      <Pressable style={[styles.loginButton, (!identifier || !password || busy) && styles.disabled]} disabled={!identifier || !password || busy} onPress={() => onLogin(identifier, password)}>{busy ? <ActivityIndicator color="white" /> : <Text style={styles.loginButtonText}>Daxil ol  →</Text>}</Pressable>
    </View><Text style={styles.loginFooter}>Stockroom  •  Təhlükəsiz inventar idarəetməsi</Text>
  </KeyboardAvoidingView></SafeAreaView>
}

function Stat({ label, value }: { label: string; value: string }) { return <View style={styles.stat}><Text style={styles.statLabel}>{label}</Text><Text style={styles.statValue}>{value}</Text></View> }
function Tab({ label, count, active, onPress }: { label: string; count: number; active: boolean; onPress: () => void }) { return <Pressable onPress={onPress} style={[styles.tab, active && styles.tabActive]}><Text style={[styles.tabText, active && styles.tabTextActive]}>{label}</Text><Text style={[styles.tabCount, active && styles.tabCountActive]}>{count}</Text></Pressable> }
function Notice({ message, danger, onClose }: { message: string; danger?: boolean; onClose: () => void }) { return <Pressable onPress={onClose} style={[styles.notice, danger && styles.noticeDanger]}><Text style={[styles.noticeText, danger && styles.noticeDangerText]}>{message}  ×</Text></Pressable> }
function Empty({ title, detail }: { title: string; detail: string }) { return <View style={styles.empty}><View style={styles.emptyIcon}><Text style={styles.categoryGlyph}>▦</Text></View><Text style={styles.emptyTitle}>{title}</Text><Text style={styles.emptyDetail}>{detail}</Text></View> }

function ProductCard({ item, category, canManage, onEdit, onDelete }: { item: Product; category: string; canManage: boolean; onEdit: () => void; onDelete: () => void }) {
  return <View style={styles.productCard}><View style={styles.productTop}><View style={styles.productIcon}><Text style={styles.productGlyph}>▤</Text></View><View style={styles.cardMain}><Text style={styles.cardTitle} numberOfLines={1}>{item.name}</Text><Text style={styles.cardMeta}>{item.sku}  ·  {category}</Text></View><View style={[styles.status, item.isActive ? styles.statusOn : styles.statusOff]}><Text style={[styles.statusText, item.isActive ? styles.statusTextOn : styles.statusTextOff]}>{item.isActive ? 'Aktiv' : 'Passiv'}</Text></View></View>
    <View style={styles.productDetails}><View><Text style={styles.detailLabel}>SATIŞ QİYMƏTİ</Text><Text style={styles.price}>{item.unitPrice.toLocaleString('az-AZ', { minimumFractionDigits: 2 })} ₼</Text></View><View><Text style={styles.detailLabel}>STOK</Text><Text style={[styles.stock, item.quantity <= 0 && { color: C.red }]}>{item.quantity} {item.unitOfMeasure}</Text></View><View><Text style={styles.detailLabel}>BARKOD</Text><Text style={styles.barcode}>{item.barcode || '—'}</Text></View></View>
    {canManage && <View style={styles.actions}><Pressable style={styles.actionButton} onPress={onEdit}><Text style={styles.actionText}>Redaktə et</Text></Pressable><Pressable style={[styles.actionButton, styles.deleteAction]} onPress={onDelete}><Text style={[styles.actionText, { color: C.red }]}>Sil</Text></Pressable></View>}
  </View>
}

function Pagination({ page, onChange }: { page: ProductPage; onChange: (page: number) => void }) {
  const from = (page.currentPage - 1) * page.pageSize + 1; const to = Math.min(page.currentPage * page.pageSize, page.totalCount)
  const numbers = page.totalPages <= 5
    ? Array.from({ length: page.totalPages }, (_, index) => index + 1)
    : Array.from({ length: 3 }, (_, index) => Math.max(1, Math.min(page.totalPages - 2, page.currentPage - 1)) + index)
  return <View style={styles.pagination}><Text style={styles.pageRange}>{page.totalCount} məhsuldan {from}-{to} göstərilir</Text><View style={styles.pageButtons}><Pressable disabled={!page.hasPrevious} style={[styles.pageButton, !page.hasPrevious && styles.pageDisabled]} onPress={() => onChange(page.currentPage - 1)}><Text style={styles.pageButtonText}>‹</Text></Pressable>{numbers.map(number => <Pressable key={number} style={[styles.pageButton, number === page.currentPage && styles.pageCurrent]} onPress={() => onChange(number)}><Text style={[styles.pageButtonText, number === page.currentPage && styles.pageCurrentText]}>{number}</Text></Pressable>)}<Pressable disabled={!page.hasNext} style={[styles.pageButton, !page.hasNext && styles.pageDisabled]} onPress={() => onChange(page.currentPage + 1)}><Text style={styles.pageButtonText}>›</Text></Pressable></View></View>
}

function ProductFormModal({ visible, item, categories, busy, onClose, onSave }: { visible: boolean; item: Product | null; categories: Category[]; busy: boolean; onClose: () => void; onSave: (values: Omit<Product, 'id' | 'quantity'>) => void }) {
  const [name, setName] = useState(''); const [sku, setSku] = useState(''); const [barcode, setBarcode] = useState(''); const [description, setDescription] = useState(''); const [categoryId, setCategoryId] = useState(''); const [price, setPrice] = useState(''); const [unit, setUnit] = useState('Piece'); const [active, setActive] = useState(true)
  useEffect(() => {
    setName(item?.name ?? ''); setSku(item?.sku ?? ''); setBarcode(item?.barcode ?? ''); setDescription(item?.description ?? '');
    setCategoryId(item?.categoryId ?? categories[0]?.id ?? ''); setPrice(item ? String(item.unitPrice) : ''); setUnit(item?.unitOfMeasure ?? 'Piece'); setActive(item?.isActive ?? true)
  }, [item, visible, categories])
  const input = (label: string, value: string, change: (value: string) => void, placeholder: string, options: { keyboard?: 'default' | 'numeric'; multiline?: boolean } = {}) => <View style={styles.formField}><Text style={styles.label}>{label}</Text><TextInput style={[styles.input, options.multiline && styles.multiline]} value={value} onChangeText={change} placeholder={placeholder} placeholderTextColor="#a5afa9" keyboardType={options.keyboard ?? 'default'} multiline={options.multiline} /></View>
  return <Modal visible={visible} animationType="slide" onRequestClose={onClose}><SafeAreaView style={styles.modalSafe}><View style={styles.modalHead}><View><Text style={styles.eyebrow}>{item ? 'MƏHSULU DÜZƏLT' : 'YENİ MƏHSUL'}</Text><Text style={styles.modalTitle}>{item ? 'Məhsul məlumatları' : 'Kataloqa əlavə et'}</Text></View><Pressable style={styles.closeButton} onPress={onClose}><Text style={styles.closeText}>×</Text></Pressable></View>
    <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.formBody}>
      {input('Məhsulun adı *', name, setName, 'Məs. Simsiz klaviatura')}{input('SKU *', sku, setSku, 'KB-001')}{input('Barkod', barcode, setBarcode, 'Barkod nömrəsi')}
      <View style={styles.formField}><Text style={styles.label}>Kateqoriya *</Text><ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.choices}>{categories.map(category => <Pressable key={category.id} onPress={() => setCategoryId(category.id)} style={[styles.choice, categoryId === category.id && styles.choiceActive]}><Text style={[styles.choiceText, categoryId === category.id && styles.choiceTextActive]}>{category.name}</Text></Pressable>)}</ScrollView></View>
      {input('Satış qiyməti (AZN) *', price, setPrice, '0.00', { keyboard: 'numeric' })}
      <View style={styles.formField}><Text style={styles.label}>Ölçü vahidi</Text><View style={styles.choices}>{['Piece', 'Kg', 'Liter', 'Package', 'Box'].map(value => <Pressable key={value} onPress={() => setUnit(value)} style={[styles.choice, unit === value && styles.choiceActive]}><Text style={[styles.choiceText, unit === value && styles.choiceTextActive]}>{({ Piece: 'Ədəd', Kg: 'Kq', Liter: 'Litr', Package: 'Paket', Box: 'Qutu' } as Record<string, string>)[value]}</Text></Pressable>)}</View></View>
      {input('Təsvir', description, setDescription, 'Məhsul haqqında qısa məlumat', { multiline: true })}
      <Pressable style={styles.activeToggle} onPress={() => setActive(value => !value)}><View><Text style={styles.activeTitle}>Satışda aktivdir</Text><Text style={styles.cardMeta}>Passiv məhsul kataloqda saxlanılır.</Text></View><View style={[styles.switch, active && styles.switchOn]}><View style={[styles.switchKnob, active && styles.switchKnobOn]} /></View></Pressable>
    </ScrollView><View style={styles.formFooter}><Pressable style={styles.cancelButton} onPress={onClose}><Text style={styles.cancelText}>Ləğv et</Text></Pressable><Pressable disabled={busy || !name.trim() || !sku.trim() || !categoryId} style={[styles.saveButton, (busy || !name.trim() || !sku.trim() || !categoryId) && styles.disabled]} onPress={() => onSave({ name: name.trim(), sku: sku.trim(), barcode, description, categoryId, unitPrice: Number(price || 0), unitOfMeasure: unit, isActive: active })}>{busy ? <ActivityIndicator color="white" /> : <Text style={styles.saveText}>Yadda saxla</Text>}</Pressable></View>
  </SafeAreaView></Modal>
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: C.bg }, loginSafe: { flex: 1, backgroundColor: C.forest },
  topbar: { minHeight: 70, backgroundColor: C.forest, flexDirection: 'row', alignItems: 'center', paddingHorizontal: 19, gap: 11 }, brandMark: { width: 36, height: 36, borderRadius: 11, backgroundColor: C.lime, alignItems: 'center', justifyContent: 'center' }, brandGlyph: { color: C.forest, fontSize: 22, fontWeight: '800' }, brandCopy: { flex: 1 }, brand: { color: '#f7fbf8', fontWeight: '800', fontSize: 18, letterSpacing: -.5 }, brandCaption: { color: '#91ad9f', fontSize: 8, letterSpacing: 1.2, marginTop: 2 }, avatar: { width: 35, height: 35, borderRadius: 11, backgroundColor: C.lime, alignItems: 'center', justifyContent: 'center' }, avatarText: { color: C.forest, fontSize: 11, fontWeight: '800' },
  heading: { paddingHorizontal: 20, paddingTop: 22, paddingBottom: 15 }, eyebrow: { color: '#8a9991', fontSize: 9, fontWeight: '800', letterSpacing: 1.1 }, title: { color: '#17251e', fontSize: 24, fontWeight: '800', letterSpacing: -.6, marginTop: 6 }, subtitle: { color: '#87938d', fontSize: 12, marginTop: 4 }, stats: { flexDirection: 'row', gap: 11, paddingHorizontal: 18, paddingBottom: 16 }, stat: { flex: 1, minHeight: 66, paddingHorizontal: 13, paddingVertical: 10, backgroundColor: C.white, borderRadius: 9, borderWidth: 1, borderColor: C.line }, statLabel: { color: C.muted, fontSize: 10 }, statValue: { color: C.ink, fontSize: 19, fontWeight: '800', marginTop: 5 },
  tabs: { flexDirection: 'row', gap: 20, paddingHorizontal: 20, backgroundColor: C.white, borderTopWidth: 1, borderBottomWidth: 1, borderColor: C.line }, tab: { minHeight: 44, flexDirection: 'row', alignItems: 'center', gap: 7, borderBottomWidth: 2, borderBottomColor: 'transparent' }, tabActive: { borderBottomColor: C.green }, tabText: { color: '#89958e', fontSize: 11 }, tabTextActive: { color: C.green, fontWeight: '800' }, tabCount: { color: '#819088', backgroundColor: '#eef2ef', overflow: 'hidden', borderRadius: 10, paddingHorizontal: 6, paddingVertical: 2, fontSize: 9 }, tabCountActive: { color: C.green, backgroundColor: C.soft },
  toolbar: { padding: 13, backgroundColor: '#fcfdfc', borderBottomWidth: 1, borderColor: C.line, gap: 9 }, search: { height: 40, borderColor: C.line, borderWidth: 1, borderRadius: 7, backgroundColor: C.white, paddingHorizontal: 11, color: C.ink, fontSize: 12 }, addButton: { height: 39, borderRadius: 7, backgroundColor: C.green, alignItems: 'center', justifyContent: 'center' }, addText: { color: C.white, fontSize: 12, fontWeight: '700' },
  listContent: { padding: 13, paddingBottom: 20, flexGrow: 1 }, productCard: { backgroundColor: C.white, borderWidth: 1, borderColor: C.line, borderRadius: 10, padding: 13, marginBottom: 10 }, productTop: { flexDirection: 'row', alignItems: 'center', gap: 10 }, productIcon: { width: 38, height: 38, backgroundColor: '#eef6fc', borderRadius: 9, alignItems: 'center', justifyContent: 'center' }, productGlyph: { color: '#447cad', fontSize: 21 }, cardMain: { flex: 1 }, cardTitle: { color: '#33433a', fontSize: 13, fontWeight: '700' }, cardMeta: { color: '#9aa59f', fontSize: 10, marginTop: 4 }, status: { paddingHorizontal: 8, paddingVertical: 4, borderRadius: 14 }, statusOn: { backgroundColor: C.soft }, statusOff: { backgroundColor: '#f0f2f1' }, statusText: { fontSize: 9, fontWeight: '700' }, statusTextOn: { color: C.green }, statusTextOff: { color: C.muted }, productDetails: { flexDirection: 'row', justifyContent: 'space-between', paddingTop: 13, marginTop: 12, borderTopWidth: 1, borderColor: '#f0f3f1' }, detailLabel: { color: '#9ba69f', fontSize: 8, fontWeight: '700', letterSpacing: .6 }, price: { color: '#34463c', fontSize: 13, fontWeight: '800', marginTop: 5 }, stock: { color: C.green, fontSize: 12, fontWeight: '700', marginTop: 5 }, barcode: { color: '#66756d', fontSize: 11, marginTop: 5 }, actions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 8, marginTop: 11, paddingTop: 9, borderTopWidth: 1, borderColor: '#f0f3f1' }, actionButton: { paddingHorizontal: 11, paddingVertical: 7, borderWidth: 1, borderColor: C.line, borderRadius: 6 }, actionText: { color: C.green, fontSize: 10, fontWeight: '700' }, deleteAction: { borderColor: '#f2e3df' },
  categoryCard: { minHeight: 68, backgroundColor: C.white, borderWidth: 1, borderColor: C.line, borderRadius: 9, padding: 12, marginBottom: 9, flexDirection: 'row', alignItems: 'center', gap: 11 }, categoryIcon: { width: 36, height: 36, backgroundColor: '#f2eff9', borderRadius: 9, alignItems: 'center', justifyContent: 'center' }, categoryGlyph: { color: '#8064a5', fontSize: 21 }, center: { flex: 1, justifyContent: 'center', alignItems: 'center', gap: 10 }, muted: { color: C.muted, fontSize: 11 }, empty: { minHeight: 200, justifyContent: 'center', alignItems: 'center', gap: 8, padding: 25 }, emptyIcon: { width: 45, height: 45, backgroundColor: C.soft, borderRadius: 13, alignItems: 'center', justifyContent: 'center', marginBottom: 2 }, emptyTitle: { color: '#56665d', fontSize: 13, fontWeight: '700' }, emptyDetail: { color: '#99a49e', fontSize: 11, textAlign: 'center' },
  pagination: { minHeight: 50, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: 3, paddingHorizontal: 3, gap: 4 }, pageRange: { flexShrink: 1, color: '#718078', fontSize: 9 }, pageButtons: { flexDirection: 'row', alignItems: 'center', gap: 4 }, pageButton: { width: 29, height: 31, backgroundColor: C.white, borderWidth: 1, borderColor: C.line, borderRadius: 6, alignItems: 'center', justifyContent: 'center' }, pageButtonText: { color: C.green, fontSize: 14, fontWeight: '700' }, pageCurrent: { backgroundColor: C.green, borderColor: C.green }, pageCurrentText: { color: C.white }, pageDisabled: { opacity: .4 }, pageLabel: { color: C.ink, fontSize: 10, fontWeight: '700' }, bottomBar: { minHeight: 36, backgroundColor: C.white, borderTopWidth: 1, borderColor: C.line, paddingHorizontal: 16, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }, bottomText: { color: '#9ba69f', fontSize: 9 }, bottomUser: { color: '#87948c', fontSize: 9 }, notice: { marginHorizontal: 13, marginTop: 9, padding: 10, borderRadius: 7, backgroundColor: C.soft }, noticeText: { color: C.green, fontSize: 11 }, noticeDanger: { backgroundColor: '#fff6f3' }, noticeDangerText: { color: C.red },
  loginWrap: { flex: 1, justifyContent: 'center', padding: 25 }, loginLogo: { width: 53, height: 53, borderRadius: 16, backgroundColor: C.lime, alignItems: 'center', justifyContent: 'center', alignSelf: 'center' }, loginBrand: { color: C.white, fontSize: 27, fontWeight: '800', textAlign: 'center', marginTop: 12 }, loginKicker: { color: '#9ab3a7', fontSize: 9, letterSpacing: 2, textAlign: 'center', marginTop: 4, marginBottom: 27 }, loginCard: { backgroundColor: C.white, borderRadius: 14, padding: 20, borderWidth: 1, borderColor: '#e8efea' }, loginTitle: { color: C.ink, fontSize: 19, fontWeight: '800' }, loginHint: { color: C.muted, fontSize: 11, marginTop: 5, marginBottom: 19 }, label: { color: '#64746b', fontSize: 10, fontWeight: '700', marginBottom: 7 }, input: { minHeight: 42, borderWidth: 1, borderColor: '#e2e9e4', borderRadius: 7, backgroundColor: '#fff', color: C.ink, paddingHorizontal: 11, fontSize: 12 }, loginError: { color: C.red, fontSize: 10, lineHeight: 15, marginTop: 11 }, loginButton: { height: 44, backgroundColor: C.green, borderRadius: 7, alignItems: 'center', justifyContent: 'center', marginTop: 17 }, loginButtonText: { color: C.white, fontSize: 12, fontWeight: '700' }, disabled: { opacity: .5 }, loginFooter: { color: '#91ad9f', fontSize: 9, textAlign: 'center', marginTop: 22 },
  modalSafe: { flex: 1, backgroundColor: C.bg }, modalHead: { minHeight: 70, paddingHorizontal: 18, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', backgroundColor: C.white, borderBottomWidth: 1, borderColor: C.line }, modalTitle: { color: C.ink, fontSize: 17, fontWeight: '800', marginTop: 4 }, closeButton: { width: 32, height: 32, borderWidth: 1, borderColor: C.line, borderRadius: 7, alignItems: 'center', justifyContent: 'center' }, closeText: { color: C.muted, fontSize: 22, lineHeight: 25 }, formBody: { padding: 17, gap: 14 }, formField: { gap: 0 }, multiline: { minHeight: 75, paddingTop: 10, textAlignVertical: 'top' }, choices: { flexDirection: 'row', flexWrap: 'wrap', gap: 7 }, choice: { paddingHorizontal: 10, paddingVertical: 8, borderWidth: 1, borderColor: C.line, borderRadius: 6, backgroundColor: C.white }, choiceActive: { borderColor: '#9acbac', backgroundColor: C.soft }, choiceText: { color: C.muted, fontSize: 10 }, choiceTextActive: { color: C.green, fontWeight: '700' }, activeToggle: { minHeight: 56, paddingVertical: 8, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', borderTopWidth: 1, borderColor: C.line }, activeTitle: { color: C.ink, fontSize: 11, fontWeight: '700', marginBottom: 3 }, switch: { width: 39, height: 23, borderRadius: 12, backgroundColor: '#dce3de', padding: 3, justifyContent: 'center' }, switchOn: { backgroundColor: C.green }, switchKnob: { width: 17, height: 17, borderRadius: 9, backgroundColor: C.white }, switchKnobOn: { alignSelf: 'flex-end' }, formFooter: { padding: 13, flexDirection: 'row', gap: 9, borderTopWidth: 1, borderColor: C.line, backgroundColor: C.white }, cancelButton: { flex: 1, height: 42, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: C.line, borderRadius: 7 }, cancelText: { color: '#66756d', fontSize: 11, fontWeight: '700' }, saveButton: { flex: 1.3, height: 42, alignItems: 'center', justifyContent: 'center', borderRadius: 7, backgroundColor: C.green }, saveText: { color: C.white, fontSize: 11, fontWeight: '700' },
})
