import { useEffect, useState } from 'react'
import { api } from './api'
import Login from './Login'
import Customers from './Customers'
import Transactions from './Transactions'

// Ana bileşen: Giriş yapılmadıysa giriş formunu, yapıldıysa müşteri ve işlem bölümlerini gösterir.
export default function App() {
  // Giriş anahtarı sadece bellekte tutulur: sayfa yenilenince tekrar giriş gerekir.
  // (Tarayıcı deposuna yazmak daha rahat olurdu ama anahtarın çalınma riski artardı.)
  const [token, setToken] = useState(null)
  const [customers, setCustomers] = useState([])

  async function loadCustomers() {
    try {
      setCustomers(await api('/api/customers', { token }))
    } catch {
      setToken(null)   // Anahtar geçersiz veya süresi dolmuşsa girişe dön
    }
  }

  // Giriş yapılınca müşterileri getir
  // eslint-disable-next-line react-hooks/exhaustive-deps -- bilerek: sadece bu anda yüklensin
  useEffect(() => { if (token) loadCustomers() }, [token])

  return (
    <main>
      <header>
        <h1>SecureLedger</h1>
        {token && <button className="link" onClick={() => setToken(null)}>Çıkış yap</button>}
      </header>

      {!token ? (
        <Login onLogin={setToken} />
      ) : (
        <>
          <Customers token={token} customers={customers} onChanged={loadCustomers} />
          <Transactions token={token} customers={customers} />
        </>
      )}
    </main>
  )
}
