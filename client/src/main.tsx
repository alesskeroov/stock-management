import { Component, StrictMode } from 'react'
import type { ErrorInfo, ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import ChatWidget from './ChatWidget.tsx'

type ErrorBoundaryState = { hasError: boolean }

class AppErrorBoundary extends Component<{ children: ReactNode }, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false }

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Stockroom UI render failed.', error, info.componentStack)
  }

  render() {
    if (this.state.hasError) {
      return <main role="alert" style={{ minHeight: '100vh', display: 'grid', placeContent: 'center', padding: 24, color: '#24322b', background: '#f5f7f5', fontFamily: 'sans-serif' }}>
        <section style={{ maxWidth: 480, padding: 28, border: '1px solid #e2e9e4', borderRadius: 12, background: '#fff' }}>
          <h1 style={{ marginTop: 0 }}>Səhifə açıla bilmədi</h1>
          <p>İnterfeysin göstərilməsi zamanı xəta baş verdi. Səhifəni yeniləyin; problem davam edərsə, brauzer konsolundakı xəta məlumatını texniki dəstəyə göndərin.</p>
          <button type="button" onClick={() => window.location.reload()}>Səhifəni yenilə</button>
        </section>
      </main>
    }

    return this.props.children
  }
}

const rootElement = document.getElementById('root')
if (!rootElement) throw new Error('Required #root element was not found in index.html.')

createRoot(rootElement).render(
  <StrictMode>
    <AppErrorBoundary>
      <App />
      <ChatWidget />
    </AppErrorBoundary>
  </StrictMode>,
)
