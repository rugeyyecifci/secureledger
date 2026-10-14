// API'ye istek gönderen tek yardımcı fonksiyon.
// Giriş anahtarı (token) varsa her isteğe ekler; hata olursa anlaşılır bir mesaj fırlatır.
export async function api(path, { method = 'GET', body, token } = {}) {
  const response = await fetch(path, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token && { Authorization: `Bearer ${token}` }),
    },
    body: body ? JSON.stringify(body) : undefined,
  })

  const text = await response.text()
  const data = text ? JSON.parse(text) : null

  if (!response.ok) {
    throw new Error(errorMessage(response.status, data))
  }

  return data
}

function errorMessage(status, data) {
  if (typeof data === 'string') return data   // API'nin kendi mesajı, ör. "Ad soyad zorunlu."
  if (status === 401) return 'E-posta veya şifre hatalı ya da oturum süresi doldu.'
  if (status === 429) return 'Çok fazla deneme yaptınız. 1 dakika bekleyin.'
  return `Bir hata oluştu (${status}).`
}
