import { useState } from 'react'
import { api } from './api'

// Müşteri listesi ve "müşteri ekle" formu.
export default function Customers({ token, customers, onChanged }) {
  const [form, setForm] = useState({ fullName: '', email: '', phone: '', address: '' })
  const [error, setError] = useState('')

  // Formdaki bir kutucuk değişince sadece o alanı güncelle
  const update = (field) => (e) => setForm({ ...form, [field]: e.target.value })

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')

    try {
      await api('/api/customers', {
        method: 'POST',
        token,
        // Boş bırakılan alanları null gönder
        body: {
          fullName: form.fullName,
          email: form.email || null,
          phone: form.phone || null,
          address: form.address || null,
        },
      })
      setForm({ fullName: '', email: '', phone: '', address: '' })
      onChanged()   // Listeyi yenile
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <section className="card">
      <h2>Müşteriler</h2>
      <p className="hint">🔒 Telefon ve adres veritabanında AES-256-GCM ile şifreli saklanır.</p>

      <form onSubmit={handleSubmit} className="row">
        <input placeholder="Ad soyad *" value={form.fullName} onChange={update('fullName')} />
        <input placeholder="E-posta" value={form.email} onChange={update('email')} />
        <input placeholder="Telefon" value={form.phone} onChange={update('phone')} />
        <input placeholder="Adres" value={form.address} onChange={update('address')} />
        <button type="submit">Ekle</button>
      </form>
      {error && <p className="error">{error}</p>}

      <table>
        <thead>
          <tr><th>#</th><th>Ad soyad</th><th>E-posta</th><th>Telefon</th><th>Adres</th></tr>
        </thead>
        <tbody>
          {customers.map((c) => (
            <tr key={c.id}>
              <td>{c.id}</td>
              <td>{c.fullName}</td>
              <td>{c.email}</td>
              <td>{c.phone}</td>
              <td>{c.address}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
