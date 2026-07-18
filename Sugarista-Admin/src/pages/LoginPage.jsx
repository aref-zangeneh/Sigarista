import { useAdmin } from '../context/AdminContext'

export function LoginPage() {
  const { loginForm, setLoginForm, handleLogin, saving, error } = useAdmin()

  return (
    <main className="login-shell">
      <section className="login-card">
        <p className="eyebrow">شوگریستا</p>
        <h1>ورود</h1>
        <form className="login-form" onSubmit={handleLogin}>
          <label>
            <span>نام کاربری</span>
            <input
              className="ltr-input"
              value={loginForm.username}
              onChange={(event) =>
                setLoginForm((currentForm) => ({ ...currentForm, username: event.target.value }))
              }
              autoComplete="username"
            />
          </label>
          <label>
            <span>رمز عبور</span>
            <input
              className="ltr-input"
              type="password"
              value={loginForm.password}
              onChange={(event) =>
                setLoginForm((currentForm) => ({ ...currentForm, password: event.target.value }))
              }
              autoComplete="current-password"
            />
          </label>
          <button type="submit" disabled={saving}>
            {saving ? 'در حال ورود...' : 'ورود'}
          </button>
        </form>
        {error ? <p className="feedback feedback-error">{error}</p> : null}
      </section>
    </main>
  )
}
