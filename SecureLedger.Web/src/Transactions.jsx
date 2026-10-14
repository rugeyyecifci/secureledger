import { useEffect, useState } from 'react'
import { api } from './api'

// İşlem listesi, "işlem ekle" formu ve "zinciri kontrol et" butonu.
export default function Transactions({ token, customers }) {
  const [transactions, setTransactions] = useState([])
  const [form, setForm] = useState({ customerId: '', amount: '', type: 'PAYMENT', currency: 'TRY', description: '' })
  const [error, setError] = useState('')
  const [verify, setVerify] = useState(null)   // Zincir kontrolünün sonucu

  const update = (field) => (e) => setForm({ ...form, [field]: e.target.value })

  async function load() {
    try {
      setTransactions(await api('/api/transactions', { token }))
    } catch (err) {
      setError(err.message)
    }
  }

  // Bölüm ilk açıldığında işlemleri getir
  // eslint-disable-next-line react-hooks/exhaustive-deps -- bilerek: sadece bu anda yüklensin
  useEffect(() => { load() }, [])

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')
    setVerify(null)

    try {
      await api('/api/transactions', {
        method: 'POST',
        token,
        body: {
          customerId: Number(form.customerId),
          amount: Number(form.amount),
          type: form.type,
          currency: form.currency,
          description: form.description || null,
        },
      })
      setForm({ ...form, amount: '', description: '' })
      load()
    } catch (err) {
      setError(err.message)
    }
  }

  async function handleVerify() {
    setError('')
    try {
      setVerify(await api('/api/audit/verify', { token }))
    } catch (err) {
      setError(err.message)
    }
  }

  // Müşteri numarasından adını bul (listede göstermek için)
  const customerName = (id) => customers.find((c) => c.id === id)?.fullName ?? `#${id}`

  return (
    <section className="card">
      <h2>İşlemler</h2>
      <p className="hint">🔗 Her işlem HMAC-SHA256 hash zincirine mühürlenir.</p>

      <form onSubmit={handleSubmit} className="row">
        <select value={form.customerId} onChange={update('customerId')}>
          <option value="">Müşteri seçin *</option>
          {customers.map((c) => (
            <option key={c.id} value={c.id}>{c.fullName}</option>
          ))}
        </select>
        <input type="number" step="0.01" placeholder="Tutar *" value={form.amount} onChange={update('amount')} />
        <select value={form.type} onChange={update('type')}>
          <option value="PAYMENT">Ödeme</option>
          <option value="REFUND">İade</option>
        </select>
        <input placeholder="Para birimi" value={form.currency} onChange={update('currency')} />
        <input placeholder="Açıklama" value={form.description} onChange={update('description')} />
        <button type="submit">Ekle</button>
      </form>
      {error && <p className="error">{error}</p>}

      <div className="verify">
        <button onClick={handleVerify}>Zinciri kontrol et</button>
        {verify && (verify.valid
          ? <span className="ok">✅ Zincir sağlam ({verify.checkedPages} kayıt kontrol edildi)</span>
          : <span className="error">❌ {verify.reason} (kayıt #{verify.brokenAtAuditId})</span>
        )}
      </div>

      <table>
        <thead>
          <tr><th>#</th><th>Müşteri</th><th>Tutar</th><th>Tür</th><th>Açıklama</th><th>Tarih</th></tr>
        </thead>
        <tbody>
          {transactions.map((t) => (
            <tr key={t.id}>
              <td>{t.id}</td>
              <td>{customerName(t.customerId)}</td>
              <td className="amount">{t.amount.toFixed(2)} {t.currency}</td>
              <td>{t.type === 'PAYMENT' ? 'Ödeme' : 'İade'}</td>
              <td>{t.description}</td>
              <td>{new Date(t.createdAt).toLocaleString('tr-TR')}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
