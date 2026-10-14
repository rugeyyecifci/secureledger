import { useState } from 'react'
import { api } from './api'

// Giriş / Kayıt formu. Başarılı olunca token'ı üst bileşene (App) verir.
export default function Login({ onLogin }) {
  const [isRegister, setIsRegister] = useState(false)
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  async function handleSubmit(e) {
    e.preventDefault()   // Sayfanın yenilenmesini engelle
    setError('')

    try {
      if (isRegister) {
        await api('/api/auth/register', { method: 'POST', body: { fullName, email, password } })
      }

      // Kayıttan sonra da otomatik giriş yap
      const result = await api('/api/auth/login', { method: 'POST', body: { email, password } })
      onLogin(result.accessToken)
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <section className="card narrow">
      <h2>{isRegister ? 'Kayıt ol' : 'Giriş yap'}</h2>

      <form onSubmit={handleSubmit} className="stack">
        {isRegister && (
          <input placeholder="Ad soyad" value={fullName} onChange={(e) => setFullName(e.target.value)} />
        )}
        <input type="email" placeholder="E-posta" value={email} onChange={(e) => setEmail(e.target.value)} />
        <input type="password" placeholder="Şifre (en az 8 karakter)" value={password} onChange={(e) => setPassword(e.target.value)} />
        <button type="submit">{isRegister ? 'Kayıt ol' : 'Giriş yap'}</button>
      </form>

      {error && <p className="error">{error}</p>}

      <button className="link" onClick={() => { setIsRegister(!isRegister); setError('') }}>
        {isRegister ? 'Hesabınız var mı? Giriş yapın' : 'Hesabınız yok mu? Kayıt olun'}
      </button>
    </section>
  )
}
