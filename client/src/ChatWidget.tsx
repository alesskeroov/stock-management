import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import './ChatWidget.css'
import { submitOnEnter } from './formKeyboard'

type Message = { role: 'user' | 'assistant'; text: string }
const CHAT_API_URL = 'http://localhost:8000/api/chat'

export default function ChatWidget() {
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<Message[]>([
    { role: 'assistant', text: 'Salam! Stok və məhsullarla bağlı necə kömək edə bilərəm?' },
  ])
  const [value, setValue] = useState('')
  const [typing, setTyping] = useState(false)
  const [error, setError] = useState('')
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, typing, open])

  async function sendMessage(event: FormEvent) {
    event.preventDefault()
    const message = value.trim()
    if (!message || typing) return
    setValue('')
    setError('')
    setMessages((current) => [...current, { role: 'user', text: message }])
    setTyping(true)
    try {
      const response = await fetch(CHAT_API_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message }),
      })
      if (!response.ok) throw new Error(`Sorğu uğursuz oldu (${response.status}).`)
      const data = await response.json() as { reply?: unknown }
      if (typeof data.reply !== 'string' || !data.reply.trim()) {
        throw new Error('Chatbot cavab mətni qaytarmadı.')
      }
      setMessages((current) => [...current, { role: 'assistant', text: data.reply as string }])
    } catch (caught) {
      console.error('Chat sorğusu uğursuz oldu:', caught)
      setError('Mesaj göndərilmədi. Zəhmət olmasa, internet bağlantınızı yoxlayıb yenidən cəhd edin.')
    } finally {
      setTyping(false)
    }
  }

  return <div className="chat-widget">
    {open && <section className="chat-panel" aria-label="Chatbot">
      <header className="chat-header">
        <div className="chat-avatar">✦</div>
        <div><strong>Stockroom köməkçisi</strong><span><i /> Onlayn</span></div>
        <button className="chat-close" type="button" onClick={() => setOpen(false)} aria-label="Söhbəti bağla">×</button>
      </header>
      <div className="chat-messages" aria-live="polite">
        {messages.map((item, index) => <div key={index} className={`chat-message ${item.role}`}>{item.text}</div>)}
        {typing && <div className="chat-message assistant typing" aria-label="Yazır"><i/><i/><i/></div>}
        {error && <p className="chat-error">{error}</p>}
        <div ref={bottomRef} />
      </div>
      <form className="chat-form" onKeyDown={submitOnEnter} onSubmit={sendMessage}>
        <input enterKeyHint="send" value={value} onChange={(event) => setValue(event.target.value)} placeholder="Mesajınızı yazın..." aria-label="Mesajınız" />
        <button type="submit" disabled={!value.trim() || typing} aria-label="Göndər">➤</button>
      </form>
      <div className="chat-caption">Enter ilə göndərin · AI cavabları səhv ola bilər</div>
    </section>}
    <button className="chat-launcher" type="button" onClick={() => setOpen((current) => !current)} aria-label={open ? 'Söhbəti bağla' : 'Chat aç'}>
      {open ? '×' : <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20 11.5a7.5 7.5 0 0 1-7.5 7.5 8 8 0 0 1-3.5-.8L4 20l1.3-4A7.5 7.5 0 1 1 20 11.5Z"/><path d="M8 11.5h.01M12 11.5h.01M16 11.5h.01"/></svg>}
    </button>
  </div>
}
